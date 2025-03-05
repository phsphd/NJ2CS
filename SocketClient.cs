using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.WebSockets;
using System.Threading;
using System.Reflection;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Xml.Linq; // For XDocument and XElement
using System.Text.RegularExpressions;
using NinjaTrader.Data;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using System.Windows.Threading;
using SchwabApiCS;
using WinForms = System.Windows.Forms;
using NinjaTrader.NinjaScript; 
using NinjaTrader.NinjaScript.AddOns;
public class SocketClient
{
    //private ClientWebSocket webSocket;
	private readonly ClientWebSocket _webSocket;
	//private ClientWebSocket webSocketClient= new ClientWebSocket();
	private ClientWebSocket webSocketClient ;
    private string apiKey;
    private string accountNumber;
    private string sessionToken;
	private readonly Uri _socketUri;
	private readonly CancellationTokenSource _cts;
    public event Action<string> OnMessageReceived; // Event to send messages to the base class UI
    private static readonly Dictionary<string, ClientWebSocket> TokenConnections = new Dictionary<string, ClientWebSocket>();
    private static readonly Dictionary<string, CancellationTokenSource> TokenCancellationSources = new Dictionary<string, CancellationTokenSource>();
    private static readonly SemaphoreSlim ConnectionSemaphore = new SemaphoreSlim(1, 1);		private readonly object positionLock = new object();
	private DateTime nextReconnectAttempt = DateTime.UtcNow;	
	private System.Timers.Timer  connectionCheckTimer=new System.Timers.Timer(10000);
	private bool isBanned = false;
	private DateTime banEndTime;
	private string currentToken;
	private int reconnectDelaySeconds = 300;  // Delay of 10 seconds before reconnection
	private int tokenBanDurationMinutes = 15;
	private int reconnectAttempts = 0;
	private const int maxReconnectAttempts = 3;
    private CancellationTokenSource webSocketCts;
 	private bool isReconnecting = false;
	private bool isTokenInvalid = false;    
    private string websocketUrl = "wss://excellgen.com/websk/?token=freetrial";
    private readonly string webserver = "excellgen.com";
    private string token = "freetrial", _token;
    private string previousToken = "freetrial";
    private string passcode = "freetrial";
    private string defaultAccountName = "Sim101";
    private string alltickers = "NVDA,SPY,QQQ,AAPL,MSFT,TSLA,AMZN,META,GOOGL";
	public SocketClient(string apiKey, string accountNumber, string sessionToken)
	{
	    this.apiKey = apiKey;
	    this.accountNumber = accountNumber;
	    this.sessionToken = sessionToken;
	    _webSocket = new ClientWebSocket();   
	    _cts = new CancellationTokenSource(); 
	}
    public SocketClient(string socketUrl)
    {
       _webSocket = new ClientWebSocket();
		webSocketClient = new ClientWebSocket();
        _socketUri = new Uri(socketUrl);
		websocketUrl =socketUrl;
        _cts = new CancellationTokenSource();
		webSocketCts = new CancellationTokenSource();
    }
	public void SetToken(string newToken)
	{
	    if (!string.IsNullOrEmpty(newToken))
	    {
	        token = newToken; // ✅ Update token
	        websocketUrl = $"wss://{webserver}/websk?token={token}"; // ✅ Update URL
	        NJ2CS.Instance.UpdateStatus($"🔗 Token updated: {token}");
	    }
	    else
	    {
	        NJ2CS.Instance.UpdateStatus("⚠️ Invalid token. Token not updated.");
	    }
	}
   	public void StartServices()
    {
    	//string localIP = GetLocalIPAddress();
             //StartWebhookServer($"http://localhost:{DefaultPort}/");
		websocketUrl = $"wss://{webserver}/websk?token={token}";
		//StartWebhookServer($"http://{localIP}/webhook/");
        StartWebSocket();
    }
	public void StopServices()
    {
        //StopWebhookServer();
        StopWebSocket();
    }
	public void StartWebSocket()
    {
        try
        {
            StopWebSocket(); // Ensure any existing connection is cleaned up
            
            if (TokenConnections.TryGetValue(token, out var existingClient))
            {
                if (existingClient.State == WebSocketState.Open)
                {
                    NJ2CS.Instance.UpdateStatus($"Connection for token {token} already exists and is open.");
                    return;
                }
                // If connection exists but not open, remove it so we can create a new one
                TokenConnections.Remove(token);
            }
            
            webSocketClient = new ClientWebSocket();
            webSocketCts = new CancellationTokenSource();
            
            TokenConnections[token] = webSocketClient;
            TokenCancellationSources[token] = webSocketCts;

            Task.Run(() => ConnectWebSocket(webSocketCts.Token));
            
            NJ2CS.Instance.UpdateStatus($"Attempting connection to server with token: {token}");
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error starting Server: {ex.Message}");
        }
    }
	public void StopWebSocket()
    {
        try
        {
            if (webSocketClient != null)
            {
                if (TokenConnections.ContainsKey(token) && TokenConnections[token] == webSocketClient)
                {
                    TokenConnections.Remove(token);
                }
                webSocketClient.Abort();
                webSocketClient.Dispose();
            }
            if (webSocketCts != null)
            {
                if (TokenCancellationSources.ContainsKey(token) && TokenCancellationSources[token] == webSocketCts)
                {
                    TokenCancellationSources.Remove(token);
                }
                webSocketCts.Cancel();
                webSocketCts.Dispose();
            }
            webSocketClient = null;
            webSocketCts = null;

            NJ2CS.Instance.UpdateStatus("Server connection stopped and cleaned up.");
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error stopping Server connection: {ex.Message}");
        }
    }
	public async Task ListenForWebSocketMessages1(CancellationToken cancellationToken)
	{
	    var buffer = new byte[4096];
	    NJ2CS.Instance.UpdateStatus($"Listening for TV signal, buffer size: {buffer.Length}");
	    NJ2CS.Instance.UpdateStatus($"Looking for token: {_token}");
	
	    try
	    {
	        if (!TokenConnections.TryGetValue(token, out var client))
	        {
	            NJ2CS.Instance.UpdateStatus($"Client not found in dictionary! Token: {token}");
	            return;
	        }
	
	        if (client.State != WebSocketState.Open)
	        {
	            NJ2CS.Instance.UpdateStatus($"Client not open: {client.State}");
	            return;
	        }
	
	        while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
	        {
	            NJ2CS.Instance.UpdateStatus("Waiting for server messages...");
	
	            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
	            NJ2CS.Instance.UpdateStatus($"Server message received: {result.Count} bytes");
	
	            if (result.MessageType == WebSocketMessageType.Close)
	            {
	                NJ2CS.Instance.UpdateStatus("Server connection closed by the server. Attempting to reconnect...");
	                await RestartWebSocketAsync();
	                break;
	            }
	
	            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
	            NJ2CS.Instance.UpdateStatus($"Received message: {message}");
	
	            var signal = NJ2CS.Instance.ParseSignal(message);
	            if (signal != null)
	            {
	                NJ2CS.Instance.HandleSignal(signal);
	            }
	            else
	            {
	                NJ2CS.Instance.UpdateStatus($"Failed to parse signal: {message}");
	            }
	        }
	        NJ2CS.Instance.UpdateStatus("Exiting message listener...");
	    }
	    catch (Exception ex)
	    {
	        NJ2CS.Instance.UpdateStatus($"Error while receiving server messages: {ex.Message}. Attempting to reconnect...");
	        await RestartWebSocketAsync();
	    }
	}	
	public async Task ListenForWebSocketMessages(CancellationToken cancellationToken)
	{
	    var buffer = new byte[4096];
	    NJ2CS.Instance.UpdateStatus($"Listening for TV signal, buffer size: {buffer.Length}");
	    NJ2CS.Instance.UpdateStatus($"Looking for token: {_token}");
	    
	    try
	    {
	        if (!TokenConnections.TryGetValue(_token, out var client))
	        {
	            NJ2CS.Instance.UpdateStatus($"Client not found in dictionary! Token: {_token}");
	            return;
	        }
	
	        if (client.State != WebSocketState.Open)
	        {
	            NJ2CS.Instance.UpdateStatus($"Client not open: {client.State}");
	            return;
	        }
	        
	        while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
	        {
	            NJ2CS.Instance.UpdateStatus("Waiting for server messages...");
	            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
	            NJ2CS.Instance.UpdateStatus($"Server message received: {result.Count} bytes");
	            
	            if (result.MessageType == WebSocketMessageType.Close)
	            {
	                NJ2CS.Instance.UpdateStatus("Server connection closed by the server. Attempting to reconnect...");
	                await RestartWebSocketAsync();
	                break;
	            }
	            
	            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
	            NJ2CS.Instance.UpdateStatus($"Received message: {message}");
	            
	            var signal = NJ2CS.Instance.ParseSignal(message);
	            if (signal != null)
	            {
	                NJ2CS.Instance.HandleSignal(signal);
	            }
	            else
	            {
	                NJ2CS.Instance.UpdateStatus($"Failed to parse signal: {message}");
	            }
	        }
	        NJ2CS.Instance.UpdateStatus("Exiting message listener...");
	    }
	    catch (Exception ex)
	    {
	        NJ2CS.Instance.UpdateStatus($"Error while receiving server messages: {ex.Message}. Attempting to reconnect...");
	        await RestartWebSocketAsync();
	    }
	}
	
	private void ManualStartListening()
	{
	    if (IsWebSocketConnected())
	    {
	        NJ2CS.Instance.UpdateStatus("Manually starting   listener...");
	        _ = Task.Run(() => ListenForWebSocketMessages(webSocketCts.Token), webSocketCts.Token);
	    }
	    else
	    {
	        NJ2CS.Instance.UpdateStatus("Cannot start listener. Server is not connected.");
	    }
	}
	private async Task SendPingMessages()
	{
	    while (IsWebSocketConnected())
	    {
	        try
	        {
	            await webSocketClient.SendAsync(
	                new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")),
	                WebSocketMessageType.Text,
	                true,
	                CancellationToken.None);
	            NJ2CS.Instance.UpdateStatus("Ping message sent");
	        }
	        catch (Exception ex)
	        {
	            NJ2CS.Instance.UpdateStatus($"Ping error: {ex.Message}");
	        }
	        await Task.Delay(30000); // Send a ping every 30 seconds
	    }
	}
	public bool IsReconnecting => isReconnecting;
    public void SetReconnecting(bool status)
    {
        isReconnecting = status;
    }		
	public void StartConnectionCheckTimer()
	{
		try
		{
		    if (connectionCheckTimer == null)
		    {
		        connectionCheckTimer = new System.Timers.Timer(10000); // Check every 10 seconds
		        connectionCheckTimer.Elapsed += (sender, e) =>
		        {
		            try
		            {
		                CheckWebSocketConnection(); // Perform the connection check
		            }
		            catch (Exception ex)
		            {
		                NJ2CS.Instance.UpdateStatus($"Error during connection check: {ex.Message}");
		            }
		        };
		        connectionCheckTimer.AutoReset = true;
		        connectionCheckTimer.Start();
		        NJ2CS.Instance.UpdateStatus("Server connection checker started.");
		    }
		}
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error StartConnectionCheckTime: {ex.Message}");
        }
	}	
	public void StopConnectionCheckTimer()
	{
		try
		{
		    if (connectionCheckTimer != null)
		    {
		        connectionCheckTimer.Stop();
		        connectionCheckTimer.Dispose();
		        connectionCheckTimer = null;
		        NJ2CS.Instance.UpdateStatus("Server connection checker stopped.");
		    }
		}
        catch (Exception ex)
        {

            NJ2CS.Instance.UpdateStatus($"Error StopConnectionCheckTimer: {ex.Message}");
        } 
	}
    private void CheckWebSocketConnection()
    {
		try
		{
            // Use the token-specific connection
            if (!TokenConnections.TryGetValue(token, out var client) || client.State != WebSocketState.Open)
            {
                if (!isReconnecting) // Prevent duplicate reconnections
                {
                    isReconnecting = true; // Set the reconnecting flag
                    NJ2CS.Instance.UpdateStatus("Server disconnected. Attempting to reconnect...");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await RestartWebSocketAsync();
                        }
                        catch (Exception ex)
                        {
                            NJ2CS.Instance.UpdateStatus($"Error during server reconnection: {ex.Message}");
                        }
                        finally
                        {
                            isReconnecting = false; // Reset the reconnecting flag after completion
                        }
                    });
                }
                else
                {
                    NJ2CS.Instance.UpdateStatus("Reconnection already in progress...");
                }
            }
            else
            {
                NJ2CS.Instance.UpdateStatus("Server connection is healthy.");
            }
		}
        catch (Exception ex)
        {

            NJ2CS.Instance.UpdateStatus($"Error  CheckWebSocketConnection: {ex.Message}");
        } 
    }
	public bool IsWebSocketConnected()
	{
	    return webSocketClient != null && webSocketClient.State == WebSocketState.Open;
	}
	private async Task ReconnectWebSocketAsync()
	{
	    if (isReconnecting) return;
		string serverString="";
	    isReconnecting = true;
	    try
	    {
			serverString =$"wss://{webserver}/websk/?token={token}";
	        NJ2CS.Instance.UpdateStatus("Attempting to reconnect {token} ");
	        await Task.Delay(1000); // Add a delay to debounce reconnection attempts
	        StartWebSocket();
	    }
	    catch (Exception ex)
	    {
	        NJ2CS.Instance.UpdateStatus($"Error during server {token} reconnection: {ex.Message}");
	    }
	    finally
	    {
	        isReconnecting = false;
	    }
	}
    private async Task DelayWithBackoff(int attemptNumber)
    {
        int delay = (int)Math.Pow(2, attemptNumber) * 1000; // Exponential backoff, starting at 1 second
        await Task.Delay(delay);
    }
	private async Task ConnectWebSocket(CancellationToken ctoken)
	{
	    NJ2CS.Instance.UpdateStatus("Server connection  function called.");

	    if (isReconnecting)
	    {
	        NJ2CS.Instance.UpdateStatus("Connection attempt already in progress. Skipping...");
	        return;
	    }
	
	    if (DateTime.UtcNow < nextReconnectAttempt)
	    {
	        NJ2CS.Instance.UpdateStatus($"Reconnection attempt delayed until {nextReconnectAttempt}.");
	        return;
	    }
	
	    isReconnecting = true;
	    NJ2CS.Instance.UpdateStatus("Starting server connection process...");
	
	    try
	    {
	        if (ctoken.IsCancellationRequested)
	        {
	            NJ2CS.Instance.UpdateStatus("Server connection was canceled.");
	            return;
	        }
	
	        // Close existing connection if it exists
	        if (webSocketClient != null)
	        {
	            NJ2CS.Instance.UpdateStatus("Disposing previous client.");
	            if (webSocketClient.State == WebSocketState.Open || webSocketClient.State == WebSocketState.Connecting)
	            {
	                await webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
	            }
	            webSocketClient.Dispose();
	            webSocketClient = null;
	        }
	
	        webSocketClient = new ClientWebSocket();
	        webSocketClient.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
	
	        string serverString = $"wss://{webserver}/websk/?token={token}";
	       // NJ2CS.Instance.UpdateStatus($"Attempting connection to: {serverString}");
	
	        var serverUri = new Uri(serverString);
	        await webSocketClient.ConnectAsync(serverUri, ctoken);
	
	        if (webSocketClient.State == WebSocketState.Open)
	        {
	            NJ2CS.Instance.UpdateStatus($"Connected successfully to server using token: {token}");
	            _ = Task.Run(() => ListenForWebSocketMessages(ctoken), ctoken);
	        }
	        else
	        {
	            NJ2CS.Instance.UpdateStatus($"Server connection failed: State={webSocketClient.State}");
	            ScheduleReconnect();
	        }
	    }
	    catch (Exception ex)
	    {
	        NJ2CS.Instance.UpdateStatus($"Error connecting to server: {ex.Message}");
	        ScheduleReconnect();
	    }
	    finally
	    {
	        isReconnecting = false;
	    }
	}
	public async Task RestartWebSocketAsync()
	{
		NJ2CS.Instance.UpdateStatus($"token is {token}");
	    if (string.IsNullOrEmpty(token) || token == "freetrial")
	    {
	        NJ2CS.Instance.UpdateStatus("Please enter a valid token before reconnecting.");
	        return;
	    }
	
	    if (await ConnectionSemaphore.WaitAsync(0))
	    {
	        try
	        {
	            isReconnecting = true;
	            NJ2CS.Instance.UpdateStatus("Stopping existing server connection...");
	            StopWebSocket();
	            await Task.Delay(500);  // Ensure cleanup
	
	            string serverString = $"wss://{webserver}/websk/?token={token}";
	            NJ2CS.Instance.UpdateStatus($"Starting new server connection with token: {token}...");
	
	            webSocketClient = new ClientWebSocket();
	            webSocketCts = new CancellationTokenSource();
	            await webSocketClient.ConnectAsync(new Uri(serverString), webSocketCts.Token);
	
	            if (webSocketClient.State == WebSocketState.Open)
	            {
	                NJ2CS.Instance.UpdateStatus("Server connection established successfully----.");
	
	                // Store the new client in dictionary
	                TokenConnections[token] = webSocketClient;
	                NJ2CS.Instance.UpdateStatus("Client added to dictionary");
	
	                _ = Task.Run(() => ListenForWebSocketMessages(webSocketCts.Token), webSocketCts.Token);
	                NJ2CS.Instance.UpdateStatus("Started server listener.");
	            }
	            else
	            {
	                NJ2CS.Instance.UpdateStatus($"Server connection failed. State: {webSocketClient.State}");
	            }
	        }
	        catch (Exception ex)
	        {
	            NJ2CS.Instance.UpdateStatus($"Error during Server reconnection: {ex.Message}");
	        }
	        finally
	        {
	            isReconnecting = false;
	            ConnectionSemaphore.Release();
	        }
	    }
	    else
	    {
	        NJ2CS.Instance.UpdateStatus("Connection attempt already in progress.");
	    }
	}
	private void AbortReconnect()
	{
	    StopWebSocket();
	    isTokenInvalid = true;
	    nextReconnectAttempt = DateTime.UtcNow.AddMinutes(tokenBanDurationMinutes);
	    NJ2CS.Instance.UpdateStatus($"Reconnection banned until {nextReconnectAttempt}. Please enter a valid token.");
	}	
	private void ScheduleReconnect()
	{
	    nextReconnectAttempt = DateTime.UtcNow.AddSeconds(reconnectDelaySeconds);
	    NJ2CS.Instance.UpdateStatus($"Reconnection delayed for {reconnectDelaySeconds} seconds.");
	}
	private async Task SendPingAsync()
	{
	    while (webSocketClient != null && webSocketClient.State == WebSocketState.Open)
	    {
	        try
	        {
	            var pingMessage = Encoding.UTF8.GetBytes("PING");
	            var buffer = new ArraySegment<byte>(pingMessage);
	            await webSocketClient.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
	            await Task.Delay(TimeSpan.FromMinutes(1)); // Send a ping every minute
	        }
	        catch (Exception ex)
	        {
	            NJ2CS.Instance.UpdateStatus($"Error sending ping to server: {ex.Message}");
	            break;
	        }
	    }
	}
   	public void  UpdateStatus(string message)
    {
        try
        {
            string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";

            // Invoke the event to notify subscribers (Base Class UI)
            OnMessageReceived?.Invoke(message);

            // Ensure NJ2CSLogManager is initialized
            if (NJ2CSLogManager.LogTab == null)
            {
                NJ2CSLogManager.Initialize(); // Attempt to initialize if LogTab is null
            }

            // Log the status message
            if (NJ2CSLogManager.LogTab != null)
            {
                NJ2CSLogManager.LogMessage(message);
            }
            else
            {
                // Fallback logging if LogTab is still null
                NinjaTrader.Code.Output.Process($"{timestamp} LogManager.LogTab is still null. Cannot log message: {message}", PrintTo.OutputTab1);
            }
        }
        catch (Exception ex)
        {
            // Fallback logging for unexpected errors
            string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
            NinjaTrader.Code.Output.Process($"{timestamp} Error in  UpdateStatus: {ex.Message}", PrintTo.OutputTab1);
        }
    }
    public async Task ConnectAsync()
    {
        try
        {
            await _webSocket.ConnectAsync(_socketUri, _cts.Token);
            NJ2CS.Instance.UpdateStatus($"Connected to Schwab WebSocket at {_socketUri}");
            _ = ReceiveMessagesAsync(); // Start receiving messages
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"WebSocket connection error: {ex.Message}");
        }
    }
	private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[4096];
        NJ2CS.Instance.UpdateStatus($"Listening for TV signal, buffer size: {buffer.Length}");

        try
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    NJ2CS.Instance.UpdateStatus("WebSocket connection closed by server.");
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                NJ2CS.Instance.UpdateStatus($"Received message: {message}");
            }
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error receiving WebSocket message: {ex.Message}");
        }
    }
    public async Task DisconnectAsync()
    {
        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", _cts.Token);
                NJ2CS.Instance.UpdateStatus("WebSocket connection closed.");
            }
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error disconnecting WebSocket: {ex.Message}");
        }
    }
	private void notused()
	{
		/*	
	private async Task ListenForWebSocketMessages1(CancellationToken cancellationToken)
    {
       // var buffer = new byte[4096];
		NJ2CS.Instance.UpdateStatus("Listening For Json signals..  ");
		var buffer = new byte[8192];
		NJ2CS.Instance.UpdateStatus($"{buffer}..  ");
        try
        {
            while (TokenConnections.TryGetValue(token, out var client) && client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    NJ2CS.Instance.UpdateStatus("Server connection closed by the server. Attempting to reconnect...");
                    await RestartWebSocketAsync();
                    break;
                }

                // Process received messages
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
			    Console.WriteLine($"Received message from server: {message}");
			    NJ2CS.Instance.UpdateStatus($"Received message from server: {message}");


                // Parse and process the signal
                try
                {
                    var signal = NJ2CS.Instance.ParseSignal(message);
                    if (signal != null)
                    {
                        NJ2CS.Instance.HandleSignal(signal.Symbol, signal.Action, signal.Quantity, signal.AccountName);
                    }
                    else
                    {
                        NJ2CS.Instance.UpdateStatus($"Failed to parse signal: {message}");
                    }
                }
                catch (Exception ex)
                {
                    NJ2CS.Instance.UpdateStatus($"Error while processing message: {message}. Exception: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error while receiving server messages: {ex.Message}. Attempting to reconnect...");
            await RestartWebSocketAsync();
        }
    }

	private async Task ListenForWebSocketMessages2(CancellationToken cancellationToken)
	{
	    var buffer = new byte[4096];
	    NJ2CS.Instance.UpdateStatus($"Listening for TV signal, buffer size: {buffer.Length}");
	
	    try
	    {
	        if (!TokenConnections.TryGetValue(token, out var client))
	        {
	            NJ2CS.Instance.UpdateStatus("Client not found in dictionary!");
	            return;
	        }
	
	        if (client.State != WebSocketState.Open)
	        {
	            NJ2CS.Instance.UpdateStatus($"Client not open: {client.State}");
	            return;
	        }
	
	        while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
	        {
	            NJ2CS.Instance.UpdateStatus("Waiting for server messages...");
	
	            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
	            NJ2CS.Instance.UpdateStatus($"Message received: {result.Count} bytes");

	            if (result.MessageType == WebSocketMessageType.Close)
	            {
	                NJ2CS.Instance.UpdateStatus("Server connection closed by the server. Attempting to reconnect...");
	                await RestartWebSocketAsync();
	                break;
	            }
	
	            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
	            NJ2CS.Instance.UpdateStatus($"Received message: {message}");
				string serverMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
				NinjaTrader.Code.Output.Process(serverMessage, PrintTo.OutputTab1);
	            try
	            {
	                var signal = NJ2CS.Instance.ParseSignal(message);
	                if (signal != null)
	                {
	                    NJ2CS.Instance.HandleSignal(signal.Symbol, signal.Action, signal.Quantity, signal.AccountName);
	                }
	                else
	                {
	                    NJ2CS.Instance.UpdateStatus($"Failed to parse signal: {message}");
	                }
	            }
	            catch (Exception ex)
	            {
	                NJ2CS.Instance.UpdateStatus($"Error while processing message: {message}. Exception: {ex.Message}");
	            }
	        }
	        NJ2CS.Instance.UpdateStatus("Exiting message listener...");
	    }
	    catch (Exception ex)
	    {
	        NJ2CS.Instance.UpdateStatus($"Error while receiving server messages: {ex.Message}. Attempting to reconnect...");
	        await RestartWebSocketAsync();
	    }
	}
		
		private async Task RestartWebSocketAsync1()
		{

		    if (string.IsNullOrEmpty(token) || token == "freetrial")
		    {
		        NJ2CS.Instance.UpdateStatus("Please enter a valid token before reconnecting.");
		        return;
		    }
			 string serverString = $"wss://{webserver}/websk/?token={token}";
		            webSocketClient = new ClientWebSocket();
		            webSocketCts = new CancellationTokenSource();
		            await webSocketClient.ConnectAsync(new Uri(serverString), webSocketCts.Token);
			NJ2CS.Instance.UpdateStatus($"Server state {webSocketClient.State}.");
		    if (await ConnectionSemaphore.WaitAsync(0))
		    {
		        try
		        {
		            isReconnecting = true;
		            NJ2CS.Instance.UpdateStatus("Stopping existing server connection...");
		            StopWebSocket();
		            await Task.Delay(500);  // Ensure cleanup
		
		          //  string serverString = $"wss://{webserver}/websk/?token={token}";
		            NJ2CS.Instance.UpdateStatus($"Starting new server connection with token: {token}...");
		
		            webSocketClient = new ClientWebSocket();
		            webSocketCts = new CancellationTokenSource();
		            await webSocketClient.ConnectAsync(new Uri(serverString), webSocketCts.Token);
					 NJ2CS.Instance.UpdateStatus($"Server state: {webSocketClient.State}.");
		            if (webSocketClient.State == WebSocketState.Open)
		            {
		                NJ2CS.Instance.UpdateStatus("Server connection established successfully----.");
		                
		                // Explicitly call the listener
		                _ = Task.Run(() => ListenForWebSocketMessages(webSocketCts.Token), webSocketCts.Token);
		                NJ2CS.Instance.UpdateStatus("Started server listener.");
		            }
		            else
		            {
		                NJ2CS.Instance.UpdateStatus($"Server connection failed. State: {webSocketClient.State}");
		            }
		        }
		        catch (Exception ex)
		        {
		            NJ2CS.Instance.UpdateStatus($"Error during server reconnection: {ex.Message}");
		        }
		        finally
		        {
		            isReconnecting = false;
		            ConnectionSemaphore.Release();
		        }
		    }
		    else
		    {
		        NJ2CS.Instance.UpdateStatus("Connection attempt already in progress.");
		    }
		}
		private async Task RestartWebSocketAsync2()
		{
		    if (string.IsNullOrEmpty(token) || token == "freetrial")
		    {
		        NJ2CS.Instance.UpdateStatus("Please enter a valid token before reconnecting.");
		        return;
		    }
		
		    if (await ConnectionSemaphore.WaitAsync(0))
		    {
		        try
		        {
		            isReconnecting = true;
		            NJ2CS.Instance.UpdateStatus("Stopping existing server connection...");
		            StopWebSocket();
		
		            string serverString = $"wss://{webserver}/websk/?token={token}";
		            NJ2CS.Instance.UpdateStatus($"Starting new server connection with token: {token}...");
		
		            var serverUri = new Uri(serverString);
		            webSocketClient = new ClientWebSocket();
		            webSocketCts = new CancellationTokenSource();
		
		            await webSocketClient.ConnectAsync(serverUri, webSocketCts.Token);
		
		            if (webSocketClient.State == WebSocketState.Open)
		            {
		                NJ2CS.Instance.UpdateStatus($"Server connection successfully reestablished with token: {token}");
		                _ = Task.Run(() => ListenForWebSocketMessages(webSocketCts.Token));
		            }
		            else
		            {
		                NJ2CS.Instance.UpdateStatus($"Server reconnection failed. State: {webSocketClient.State}");
		            }
		        }
		        catch (Exception ex)
		        {
		            NJ2CS.Instance.UpdateStatus($"Error during server reconnection: {ex.Message}");
		        }
		        finally
		        {
		            isReconnecting = false;
		            ConnectionSemaphore.Release();
		        }
		    }
		    else
		    {
		        NJ2CS.Instance.UpdateStatus("Connection attempt already in progress.");
		    }
		}
		*/
	}
}