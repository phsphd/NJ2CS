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
using WinForms = System.Windows.Forms;
using NinjaTrader.NinjaScript.AddOns;

public sealed class NJCSSocketClient
{
    #region Singleton Implementation

    private static NJCSSocketClient _instance;
    private static readonly object _instanceLock = new object();

    /// <summary>
    /// Gets the singleton instance of NJSocketClient.
    /// </summary>
    public static NJCSSocketClient Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        // Initialize using a default socket URL.
                        _instance = new NJCSSocketClient("wss://excellgen.com/websk/?token=freetrial");
                    }
                }
            }
            return _instance;
        }
    }

    // Private constructor so external code cannot instantiate directly.
    private NJCSSocketClient(string socketUrl)
    {
        _webSocket = new ClientWebSocket();
        _webSocketClient = new ClientWebSocket();
        _socketUri = new Uri(socketUrl);
        websocketUrl = socketUrl;
        _cts = new CancellationTokenSource();
        _webSocketCts = new CancellationTokenSource();
    }

    #endregion

    #region Fields and Objects

    private readonly ClientWebSocket _webSocket;
    private ClientWebSocket _webSocketClient;
    private CancellationTokenSource _webSocketCts;
    private string apiKey;
    private string accountNumber;
    private string sessionToken;
    private readonly Uri _socketUri;
    private readonly CancellationTokenSource _cts;
    public event Action<string> OnMessageReceived; // For sending messages to the UI
    private static readonly Dictionary<string, ClientWebSocket> TokenConnections = new Dictionary<string, ClientWebSocket>();
    private static readonly Dictionary<string, CancellationTokenSource> TokenCancellationSources = new Dictionary<string, CancellationTokenSource>();
    private static readonly SemaphoreSlim ConnectionSemaphore = new SemaphoreSlim(1, 1);
    private readonly object positionLock = new object();
    private DateTime nextReconnectAttempt = DateTime.UtcNow;
    private System.Timers.Timer connectionCheckTimer = new System.Timers.Timer(10000);
    private int reconnectDelaySeconds = 300;  // 300 seconds delay (5 minutes)
    private int tokenBanDurationMinutes = 15;
    private int reconnectAttempts = 0;
    private const int maxReconnectAttempts = 3;
    private bool isReconnecting = false;
    private bool isTokenInvalid = false;
    private bool _isReconnecting = false;
    private readonly object _reconnectLock = new object();
    private string websocketUrl = "wss://excellgen.com/websk/?token=freetrial";
    private static readonly Uri WebSocketUri = new Uri("wss://excellgen.com/websk/?token=freetrial");
    private readonly string webserver = "excellgen.com";
    private string token = "freetrial", _token = "freetrial";
    private string previousToken = "freetrial";
    private string passcode = "freetrial";
    private string defaultAccountName = "Sim101";
    private string alltickers = "NVDA,SPY,QQQ,AAPL,MSFT,TSLA,AMZN,META,GOOGL";
    private Task _receiveTask;
    private readonly object _receiveLock = new object();
    // Fields for invalid token handling.
    private string invalidToken = null;
    private DateTime nextRetryForInvalidToken = DateTime.MinValue;
    private readonly int[] retryIntervalsInSeconds = { 60, 60, 60 }; // 3 attempts at 1 minute intervals.
    private int retryAttemptForInvalidToken = 0;
    private bool isInvalidToken = false;
    // Count consecutive failed connection attempts.
    private int consecutiveFailedAttempts = 0;
    #endregion
    #region Public Methods

    public bool IsReconnecting => isReconnecting;
    public bool IsTokenInvalid => isInvalidToken;
    /// <summary>
    /// Updates the token and refreshes the connection URL.
    /// Aborts any current connection if the token is changed.
    /// </summary>
    public void SetToken(string newToken)
    {
        if (!string.IsNullOrEmpty(newToken))
        {
            if ((newToken == invalidToken || isInvalidToken) && DateTime.UtcNow < nextRetryForInvalidToken)
            {
                UpdateStatus($"Token {newToken} was previously invalid. Wait until {nextRetryForInvalidToken} to retry.");
                return;
            }
            token = newToken;
            websocketUrl = $"wss://{webserver}/websk?token={token}";
            UpdateStatus($"🔗 Token updated: {token}");
            StopWebSocket(); // Abort current connection.
            // Reset invalid token tracking.
            invalidToken = null;
            retryAttemptForInvalidToken = 0;
            nextRetryForInvalidToken = DateTime.MinValue;
            consecutiveFailedAttempts = 0;
            isInvalidToken = false;
            UpdateStatus($"Token {newToken} is now marked as invalid? {isInvalidToken}");
        }
        else
        {
            UpdateStatus("⚠️ Invalid token. Token not updated.");
        }
    }
    /// <summary>
    /// Starts services: WebSocket connection and connection check timer.
    /// If the token is marked invalid, connection attempts are aborted.
    /// </summary>
    public void StartServices()
    {
        websocketUrl = $"wss://{webserver}/websk?token={token}";
        if (isInvalidToken)
        {
            UpdateStatus($"Token {token} is marked as invalid; aborting connection attempts.");
            return;
        }
        StartWebSocket();
        StartConnectionCheckTimer();
    }
    /// <summary>
    /// Stops all services.
    /// </summary>
    public void StopServices()
    {
        StopWebSocket();
        StopConnectionCheckTimer();
    }
    public bool IsWebSocketConnected()
    {
        return _webSocketClient != null && _webSocketClient.State == WebSocketState.Open;
    }
    public void SetReconnecting(bool status)
    {
        isReconnecting = status;
    }
    /// <summary>
    /// Restarts the WebSocket connection asynchronously.
    /// If connection attempts fail three times, the token is marked as invalid and further reconnection attempts are aborted.
    /// </summary>
 
	public async Task RestartWebSocketAsync()
	{
	    // Immediately abort if token is already marked invalid.
	    if (isInvalidToken && DateTime.UtcNow < nextRetryForInvalidToken)
	    {
	        UpdateStatus($"Token {token} is marked as invalid. Next retry at {nextRetryForInvalidToken}.");
	        return;
	    }
	
	    if (string.IsNullOrEmpty(token) || token == "freetrial")
	    {
	        UpdateStatus("Please enter a valid token before reconnecting.");
	        return;
	    }
	
	    if (await ConnectionSemaphore.WaitAsync(0))
	    {
	        try
	        {
	            isReconnecting = true;
	            UpdateStatus("Stopping existing server connection...");
	            await StopWebSocketAsync();
	            await Task.Delay(500);  // Allow cleanup
	
	            string serverString = $"wss://{webserver}/websk/?token={token}";
	            UpdateStatus($"Starting new server connection with token: {token}...");
	
	            // Attempt connection up to 3 times with 1‑minute intervals.
	            int attempts = 0;
	            bool connected = false;
	            while (attempts < 3 && !connected)
	            {
	                attempts++;
	                _webSocketClient = new ClientWebSocket();
	                _webSocketCts = new CancellationTokenSource();
	                try
	                {
	                    await _webSocketClient.ConnectAsync(new Uri(serverString), _webSocketCts.Token);
	                    if (_webSocketClient.State == WebSocketState.Open)
	                    {
	                        connected = true;
	                        isInvalidToken = false;
	                        invalidToken = null;
	                        consecutiveFailedAttempts = 0;
	                        retryAttemptForInvalidToken = 0;
	                        nextRetryForInvalidToken = DateTime.MinValue;
	                        break;
	                    }
	                }
	                catch (Exception ex)
	                {
	                    // If error indicates token rejection, mark token as invalid immediately.
	                    if (ex.Message.ToLower().Contains("invalid") ||
	                        ex.Message.ToLower().Contains("rejected"))
	                    {
	                        invalidToken = token;
	                        isInvalidToken = true;
	                        nextRetryForInvalidToken = DateTime.UtcNow.AddSeconds(60);
	                        UpdateStatus($"Token {token} appears invalid due to error: {ex.Message}. Aborting connection attempts.");
	                        return;
	                    }
	                    UpdateStatus($"Connection attempt {attempts} failed: {ex.Message}");
	                }
	                UpdateStatus($"Retrying in 60 seconds... (Attempt {attempts} of 3)");
	                await Task.Delay(60000);
	            }
	
	            if (!connected)
	            {
	                invalidToken = token;
	                isInvalidToken = true;
	                nextRetryForInvalidToken = DateTime.UtcNow.AddSeconds(60);
	                UpdateStatus($"Token {token} appears invalid after {attempts} attempts. Aborting further connection attempts.");
	                return;
	            }
	
	            if (_webSocketClient.State == WebSocketState.Open)
	            {
	                UpdateStatus("Server connection established successfully----.");
	                TokenConnections[token] = _webSocketClient;
	                UpdateStatus("Client added to dictionary");
	                _ = Task.Run(() => ListenForMessagesAsync(), _webSocketCts.Token);
	                UpdateStatus("Started server listener.");
	            }
	            else
	            {
	                UpdateStatus($"Server connection failed. State: {_webSocketClient.State}");
	            }
	        }
	        catch (Exception ex)
	        {
	            UpdateStatus($"Error during server reconnection: {ex.Message}");
	        }
	        finally
	        {
	            isReconnecting = false;
	            ConnectionSemaphore.Release();
	        }
	    }
	    else
	    {
	        UpdateStatus("Connection attempt already in progress.");
			if (_webSocketClient.State == WebSocketState.Open)
			{
				UpdateStatus($"Server connected using token {token} ");
			}
	    }
	}

    #endregion

    #region Connection Methods

    private void StartWebSocket()
    {
        try
        {
            if (isInvalidToken)
            {
                NJ2CS.Instance.UpdateStatus($"Token {token} is marked as invalid; aborting connection start.");
                return;
            }
            StopWebSocket(); // Clean up any existing connection

            if (TokenConnections.TryGetValue(token, out var existingClient))
            {
                if (existingClient.State == WebSocketState.Open)
                {
                    NJ2CS.Instance.UpdateStatus($"Connection for token {token} already exists and is open.");
                    return;
                }
                TokenConnections.Remove(token);
            }

            _webSocketClient = new ClientWebSocket();
            _webSocketCts = new CancellationTokenSource();

            TokenConnections[token] = _webSocketClient;
            TokenCancellationSources[token] = _webSocketCts;

            Task.Run(() => ConnectWebSocket(_webSocketCts.Token));
            NJ2CS.Instance.UpdateStatus($"Attempting connection to server with token: {token}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error starting Server: {ex.Message}");
        }
    }

    private void StopWebSocket()
    {
        try
        {
            if (_webSocketClient != null)
            {
                if (TokenConnections.ContainsKey(token) && TokenConnections[token] == _webSocketClient)
                {
                    TokenConnections.Remove(token);
                }
                _webSocketClient.Abort();
                _webSocketClient.Dispose();
            }
            if (_webSocketCts != null)
            {
                if (TokenCancellationSources.ContainsKey(token) && TokenCancellationSources[token] == _webSocketCts)
                {
                    TokenCancellationSources.Remove(token);
                }
                _webSocketCts.Cancel();
                _webSocketCts.Dispose();
            }
            _webSocketClient = null;
            _webSocketCts = null;
            NJ2CS.Instance.UpdateStatus("Server connection stopped and cleaned up.");
        }
        catch (Exception ex)
        {
            NJ2CS.Instance.UpdateStatus($"Error stopping Server connection: {ex.Message}");
        }
    }

    private async Task StopWebSocketAsync()
    {
        try
        {
            if (_webSocketClient != null)
            {
                _webSocketCts.Cancel();
                if (_receiveTask != null)
                {
                    try { await _receiveTask; } catch { }
                }
                _webSocketClient.Abort();
                _webSocketClient.Dispose();
            }
            if (_webSocketCts != null)
            {
                _webSocketCts.Dispose();
            }
            _webSocketClient = null;
            _webSocketCts = null;
            lock (_receiveLock)
            {
                _receiveTask = null;
            }
            UpdateStatus("Server connection stopped and cleaned up.");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error stopping Server connection: {ex.Message}");
        }
    }

private async Task ConnectWebSocket(CancellationToken ctoken)
{
    UpdateStatus("Server connection function called.");
    if (isInvalidToken)
    {
        UpdateStatus("Aborting connection attempt due to invalid token.");
        return;
    }

    if (isReconnecting)
    {
        UpdateStatus("Connection attempt already in progress. Skipping...");
        return;
    }

    if (DateTime.UtcNow < nextReconnectAttempt)
    {
        UpdateStatus($"Reconnection attempt delayed until {nextReconnectAttempt}.");
        return;
    }

    isReconnecting = true;
    UpdateStatus("Starting server connection process...");

    try
    {
        if (ctoken.IsCancellationRequested)
        {
            UpdateStatus("Server connection was canceled.");
            return;
        }

        if (_webSocketClient != null)
        {
            UpdateStatus("Disposing previous client.");
            if (_webSocketClient.State == WebSocketState.Open || _webSocketClient.State == WebSocketState.Connecting)
            {
                await _webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
            }
            _webSocketClient.Dispose();
            _webSocketClient = null;
        }

        _webSocketClient = new ClientWebSocket();
        _webSocketClient.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        string serverStringLocal = $"wss://{webserver}/websk/?token={token}";
        var serverUri = new Uri(serverStringLocal);
        await _webSocketClient.ConnectAsync(serverUri, ctoken);

        if (_webSocketClient.State == WebSocketState.Open)
        {
            UpdateStatus($"Connected successfully to server using token: {token}");
            _ = Task.Run(() => ListenForWebSocketMessages(ctoken), ctoken);
        }
        else
        {
            UpdateStatus($"Server connection failed: State={_webSocketClient.State}");
            ScheduleReconnect();
        }
    }
    catch (Exception ex)
    {
        UpdateStatus($"Error connecting to server: {ex.Message}");
        if (ex.Message.ToLower().Contains("invalid") ||
            ex.Message.ToLower().Contains("rejected"))
        {
            invalidToken = token;
            isInvalidToken = true;
            nextRetryForInvalidToken = DateTime.UtcNow.AddSeconds(60);
            UpdateStatus($"Token {token} appears invalid. Aborting further connection attempts.");
        }
        else
        {
            ScheduleReconnect();
        }
    }
    finally
    {
        isReconnecting = false;
    }
}

    private void ScheduleReconnect()
    {
        if (isInvalidToken)
        {
            UpdateStatus("Aborting reconnect because token is marked as invalid.");
            return;
        }
        nextReconnectAttempt = DateTime.UtcNow.AddSeconds(reconnectDelaySeconds);
        UpdateStatus($"Reconnection delayed for {reconnectDelaySeconds} seconds.");
    }

    #endregion

    #region Listener Methods

    // Primary guarded listener using InternalReceiveLoopAsync.
    public Task ListenForMessagesAsync()
    {
        UpdateStatus("221");
        if (isInvalidToken)
        {
            UpdateStatus("Aborting receive loop due to invalid token.");
            return null;
        }
        lock (_receiveLock)
        {
            if (_receiveTask != null && !_receiveTask.IsCompleted)
            {
                return _receiveTask;
            }
            _receiveTask = InternalReceiveLoopAsync();
            return _receiveTask;
        }
    }

    // Internal listener loop that accumulates message fragments.
private async Task InternalReceiveLoopAsync()
{
    if (isInvalidToken)
    {
        UpdateStatus("Aborting receive loop due to invalid token.");
        return;
    }

    UpdateStatus("222 - Starting receive loop...");

    try
    {
        // Ensure the WebSocket is open before starting the loop.
        if (_webSocketClient == null || _webSocketClient.State != WebSocketState.Open)
        {
            UpdateStatus($"Connection is not open. State: {_webSocketClient?.State}. Attempting to reconnect...");
            await RestartWebSocketAsync();
            return;
        }

        var buffer = new byte[4096]; // Adjust buffer size if necessary.
        UpdateStatus("223 - Connection is open. Waiting for messages...");

        while (_webSocketClient.State == WebSocketState.Open && !_webSocketCts.IsCancellationRequested)
        {
            UpdateStatus("224 - Waiting for server messages...");

            WebSocketReceiveResult result;
            try
            {
                // Log WebSocket state before receiving.
                UpdateStatus($"Connection state before ReceiveAsync: {_webSocketClient.State}");

                // Receive a message from the WebSocket.
                result = await _webSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), _webSocketCts.Token);

                // Log WebSocket state after receiving.
                UpdateStatus($"Connection state after ReceiveAsync: {_webSocketClient.State}");
                UpdateStatus($"225 - Received {result.Count} bytes. EndOfMessage: {result.EndOfMessage}");
            }
            catch (WebSocketException webSocketEx)
            {
                UpdateStatus($"Connection error while receiving message: {webSocketEx.Message}. Attempting to reconnect...");
                await RestartWebSocketAsync();
                break;
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Receive operation canceled.");
                break;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Unexpected error while receiving message: {ex.Message}. Attempting to reconnect...");
                await RestartWebSocketAsync();
                break;
            }

            // Check if the connection was closed by the server.
            if (result.MessageType == WebSocketMessageType.Close)
            {
                UpdateStatus("226 - Server closed the connection. Attempting to reconnect...");
                await RestartWebSocketAsync();
                break;
            }

            // Process the received message.
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            UpdateStatus($"227 - Received message: {message}");

            try
            {
                // Parse and handle the trading signal.
                var signal = NJ2CS.Instance.ParseSignal(message);
                if (signal != null)
                {
                   // NJ2CS.Instance.HandleSignal(signal.Symbol, signal.Action, signal.Quantity, signal.AccountName, signal.ATMName);
                	NJ2CS.Instance.HandleSignal(signal);
				}
                else
                {
                    UpdateStatus($"Failed to parse signal: {message}");
                }
            }
            catch (Exception parseEx)
            {
                UpdateStatus($"Error parsing signal: {parseEx.Message}");
            }
        }

        UpdateStatus("228 - Exiting receive loop...");
    }
    catch (Exception ex)
    {
        UpdateStatus($"Fatal error in receive loop: {ex.Message}. Attempting to reconnect...");
        await RestartWebSocketAsync();
    }
}

    // Alternate listener method.
    public async Task ListenForWebSocketMessages(CancellationToken cancellationToken)
    {
        if (isInvalidToken)
        {
            UpdateStatus("Aborting alternate listener due to invalid token.");
            return;
        }
        var buffer = new byte[4096];
        UpdateStatus($"Listening for TV signal, buffer size: {buffer.Length}");
        UpdateStatus($"NJ2CS Looking for token: {token}");
        //UpdateStatus($"250 {websocketUrl}");
		UpdateStatus($"550  ");
        try
        {
            if (!TokenConnections.TryGetValue(token, out var client))
            {
                UpdateStatus($"Client not found in dictionary! Token: {token}");
                return;
            }
            if (client.State != WebSocketState.Open)
            {
                UpdateStatus($"Client not open: {client.State}");
				RestartWebSocketAsync();
             //   return;
            }
            using (var ms = new MemoryStream())
            {
                while (client.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result = null;
                    do
                    {
                        result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.Count > 0)
                        {
                            ms.Write(buffer, 0, result.Count);
                        }
                    } while (!result.EndOfMessage);

                    if (ms.Length == 0 || result == null || result.MessageType == WebSocketMessageType.Close)
                    {
                        UpdateStatus("Server connection closed by the server. Attempting to reconnect...");
                        await RestartWebSocketAsync();
                        break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    string message = Encoding.UTF8.GetString(ms.ToArray());
                    UpdateStatus($"Received message: {message}");
                    if (message.IndexOf("Connection rejected: Invalid or inactive token", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        invalidToken = token;
                        isInvalidToken = true;
                        UpdateStatus("Server reported: Connection rejected: Invalid or inactive token. Aborting connection.");
                        StopWebSocket();
                        ScheduleReconnectForInvalidToken();
                        return;
                    }
                    try
                    {
                        var signal = NJ2CS.Instance.ParseSignal(message);
                        if (signal != null)
                        {
                            NJ2CS.Instance.HandleSignal(signal);
                        }
                        else
                        {
                            UpdateStatus($"Failed to parse signal: {message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error processing signal: {ex.Message}");
                    }
                }
            }
            UpdateStatus("Exiting message listener...");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error while receiving server messages: {ex.Message}. Attempting to reconnect...");
            await RestartWebSocketAsync();
        }
    }

    private void ManualStartListening()
    {
        if (IsWebSocketConnected())
        {
            UpdateStatus("Manually starting listener...");
            _ = Task.Run(() => ListenForWebSocketMessages(_webSocketCts.Token), _webSocketCts.Token);
        }
        else
        {
            UpdateStatus("Cannot start listener. Server is not connected.");
        }
    }

    #endregion

    #region Ping & Reconnect Methods

    private async Task SendPingMessages()
    {
        while (IsWebSocketConnected())
        {
            try
            {
                await _webSocketClient.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                UpdateStatus("Ping message sent");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Ping error: {ex.Message}");
                break;
            }
            await Task.Delay(30000);
        }
    }

    private async Task ReconnectWebSocketAsync()
    {
        if (isInvalidToken)
        {
            UpdateStatus("Aborting reconnect due to invalid token.");
            return;
        }
        if (isReconnecting) return;
        string serverString = "";
        isReconnecting = true;
        try
        {
            serverString = $"wss://{webserver}/websk/?token={token}";
            UpdateStatus($"Attempting to reconnect {token}");
            await Task.Delay(1000);
            StartWebSocket();
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error during server {token} reconnection: {ex.Message}");
        }
        finally
        {
            isReconnecting = false;
        }
    }

    private async Task DelayWithBackoff(int attemptNumber)
    {
        int delay = (int)Math.Pow(2, attemptNumber) * 1000;
        await Task.Delay(delay);
    }

    private async Task RestartWebSocketAsync_()
    {
        lock (_reconnectLock)
        {
            if (_isReconnecting) return;
            _isReconnecting = true;
        }
        try
        {
            if (_webSocketClient != null)
            {
                await _webSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                _webSocketClient.Dispose();
                _webSocketClient = null;
            }
            _webSocketClient = new ClientWebSocket();
            await _webSocketClient.ConnectAsync(new Uri(websocketUrl), CancellationToken.None);
            _isReconnecting = false;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Failed to reconnect: {ex.Message}");
            _isReconnecting = false;
        }
    }

    #endregion

    #region Invalid Token Retry Logic

    private void ScheduleReconnectForInvalidToken()
    {
        if (retryAttemptForInvalidToken < retryIntervalsInSeconds.Length)
        {
            int delay = retryIntervalsInSeconds[retryAttemptForInvalidToken];
            nextRetryForInvalidToken = DateTime.UtcNow.AddSeconds(delay);
            UpdateStatus($"Waiting {delay} seconds before next connection attempt due to invalid token.");
            retryAttemptForInvalidToken++;
        }
        else
        {
            UpdateStatus("Max retry attempts for invalid token reached. Manual intervention required.");
        }
    }

    #endregion

    public void UpdateStatus(string message)
    {
        try
        {
            string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
            OnMessageReceived?.Invoke(message);
            if (NJ2CSLogManager.LogTab == null)
            {
                NJ2CSLogManager.Initialize();
            }
            if (NJ2CSLogManager.LogTab != null)
            {
                NJ2CSLogManager.LogMessage(message);
            }
            else
            {
                NinjaTrader.Code.Output.Process($"{timestamp} LogManager.LogTab is still null. Cannot log message: {message}", PrintTo.OutputTab1);
            }
        }
        catch (Exception ex)
        {
            string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
            NinjaTrader.Code.Output.Process($"{timestamp} Error in UpdateStatus: {ex.Message}", PrintTo.OutputTab1);
        }
    }

    #region Connection Check Timer Methods

    private void StartConnectionCheckTimer()
    {
        if (connectionCheckTimer == null)
        {
            connectionCheckTimer = new System.Timers.Timer(5000); // Check every 5 seconds
            connectionCheckTimer.Elapsed += (sender, e) =>
            {
                try
                {
                    if (!CheckWebSocketConnection())
                        StopConnectionCheckTimer();
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error during connection check: {ex.Message}");
                }
            };
            connectionCheckTimer.AutoReset = true;
            connectionCheckTimer.Start();
            UpdateStatus("Server connection checker started.");
        }
    }

    private void StopConnectionCheckTimer()
    {
        if (connectionCheckTimer != null)
        {
            connectionCheckTimer.Stop();
            connectionCheckTimer.Dispose();
            connectionCheckTimer = null;
            UpdateStatus("Server connection checker stopped.");
        }
    }

	private bool CheckWebSocketConnection()
	{
	    if (isInvalidToken)
	    {
	        UpdateStatus("Token is marked as invalid; stopping connection check timer.");
	        StopConnectionCheckTimer(); // Stop the timer if the token is invalid.
	        return false;
	    }
	
	    if (!TokenConnections.TryGetValue(token, out var client) || client.State != WebSocketState.Open)
	    {
	        if (!isReconnecting)
	        {
	            isReconnecting = true;
	            UpdateStatus("Server disconnected. Attempting to reconnect...");
	            Task.Run(async () =>
	            {
	                try
	                {
	                    await RestartWebSocketAsync();
	                }
	                catch (Exception ex)
	                {
	                    UpdateStatus($"Error during server reconnection: {ex.Message}");
	                }
	                finally
	                {
	                    isReconnecting = false;
	                }
	            });
	        }
	        return false;
	    }
	    return true;
	}

    #endregion
}
