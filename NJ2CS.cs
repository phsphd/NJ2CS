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
 
namespace NinjaTrader.NinjaScript.AddOns
{
	// LoadConfigContent(StackPanel stackPanel)-->click save settings button--> RestartWebSocketAsync()--> ListenForWebSocketMessages
	/*
	listening for incoming WebSocket signals in your provided code is: ListenForWebSocketMessages()
	StartWebSocket(): Initializes a WebSocket connection.
	StopWebSocket(): Cleans up the WebSocket connection.
	ReconnectWebSocketAsync(): Handles WebSocket reconnections.
		
		*/
    public class NJ2CS : AddOnBase
    {
        public static NJ2CS Instance { get; private set; }
		private System.Timers.Timer debounceTimer;
        private NTMenuItem existingMenuItems;
        private NTMenuItem newMenuItemLog;
        private NTMenuItem newMenuItemConfig;
		private NJ2CSConfigTab configTabInstance;
		private NJ2CSConfigTab settingsTab;	
		private NJ2CSLogTab logTabInstance;
		private NJ2CSLogTab logsPage;
		private NJ2CSLoginTab loginTab;
		private NJ2CSTradeTab tradeTab;
		private NJCSSocketClient socketClient;
		private   SchwabPnL pnlInstance = null;
		private TabControl configTabControl;
		private ScrollViewer pnlScrollViewer = null;
        private static Window configWindowInstance;
		private NJ2CS baseInstance;
		private Settings settings;
		private static string _settingsJson;
        private static readonly Dictionary<string, ClientWebSocket> TokenConnections = new Dictionary<string, ClientWebSocket>();
        private static readonly Dictionary<string, CancellationTokenSource> TokenCancellationSources = new Dictionary<string, CancellationTokenSource>();
        private static readonly SemaphoreSlim ConnectionSemaphore = new SemaphoreSlim(1, 1);		private readonly object positionLock = new object();
		private Dictionary<string, Dictionary<string, int>> positionStore = new Dictionary<string, Dictionary<string, int>>();
		private readonly Dictionary<string, int> activePositions = new Dictionary<string, int>();
		private NTMenuItem addOnFrameworkMenuItem;
		private NTMenuItem existingMenuItemInControlCenter;
		private TimeSpan tradingStartTime = new TimeSpan(18, 0, 0); // Default: 9 PM
		private TimeSpan tradingEndTime = new TimeSpan(17, 0, 0);   // Default: 5 PM (next day)
        public TimeSpan TradingStartTime { get => settings.TradingStartTime; set => settings.TradingStartTime = value; }
        public TimeSpan TradingEndTime { get => settings.TradingEndTime; set => settings.TradingEndTime = value; }
        private HttpListener httpListener;
        private ClientWebSocket webSocketClient= new ClientWebSocket();
        private string websocketUrl = "wss://excellgen.com/websk/?token=freetrial"; //ajxm1963 kl939
		private string webserver = "excellgen.com";
		private string token = "freetrial"; //ajxm1963  kl939
		private	string previousToken = "freetrial";
		private string passcode = "freetrial"; //ajxm1963  kl939
		private string defaultAccountName="Sim101";
		private string alltickers= "NVDA,SPY,QQQ,AAPL,MSFT,TSLA,AMZN,META,GOOGL";
        private HashSet<string> allowedTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private ComboBox accountSelector;
        private TextBox tickerTextBox;
        public static TextBlock statusText;
		private ListBox selectedAccountsListBox;	
		private double stopLossPoints = 3; // Default stop loss
		private double takeProfitPoints = 4; // Default take profit
		private const int DefaultPort = 80;
		private bool allowDCA = true;
		private bool allowRev= false;
		private bool allowLong= true;
		private bool allowShort= true;
		private bool enableOCO = false;
		private bool useATM =false;
		private int maxLongPositionAllowedOCO = 100; // Set default max allowed long positions
		private int maxShortPositionAllowedOCO = 100; // Set default max allowed short positions
		private int maxLongPositionAllowedDCA = 200; // Set default max allowed long positions
		private int maxShortPositionAllowedDCA = 200; // Set default max allowed short positions
		private double latestBid = 0;
		private double latestAsk = 0;
        private int previousPositionCount = 0; // Track the previous number of positions
        private string previousSignal = ""; // Track the last signal direction (buy/sell)
		private string previousOrder = ""; // Track the last order  (buy/sell)
        private string currentSignal = ""; // Store the current signal received via webhook
		private bool manualClosePosition = false;
		private double currentAccountPnL=0;
		private bool enableMaxProfitLossPerDay=false;
		private double profitTarget =1500;
		private double maxLoss = 1500;
		private bool enableMaxProfitLossPerTrade=false;
		private double maxLossPerTrade = 300;
		private double maxProfitPerTrade = 500;
		private bool closePositionsBeforeMarketClose = false;
		private System.Timers.Timer marketCloseTimer;
		private bool pauseSignal = false;
	    private bool isAtmStrategyCreated = false;
	    private string atmStrategyId = string.Empty;
	    private string orderId = string.Empty;
		private double _lastPrice;
		private double _askPrice;
		private double _bidPrice;
		private Account submissionAccount;
		private NinjaTrader.Cbi.Order  profitTargetOrder;
		private NinjaTrader.Cbi.Order stopLossOrder;
		private double				currentPtPrice, currentSlPrice, tickSize;
		private NinjaTrader.Cbi.Order		entryBuyMarketOrder;
		private NinjaTrader.Cbi.Order		entrySellMarketOrder;
		private BarsRequest barsRequest;
        public static Account selectedAccount;//for single account
		//private List<Account> selectedAccounts = new List<Account>();	
		public static List<Account> selectedAccounts { get; private set; } = new List<Account>();
	    public static SchwabApi schwabApi { get; set; }
	    public static SchwabTokens schwabTokens { get; set; }
 		private StackPanel pnlStack;
        public NJ2CS()
        {
            // Ensure only one instance is created
		    if (Instance == null)
		    {
		        Instance = this;
		    }
		    // Use the singleton instance of NJSocketClient.

		    socketClient = NJCSSocketClient.Instance;
		    socketClient.StartServices();
	        Application.Current.Dispatcher.Invoke(() =>
	        {
	            pnlInstance = SchwabPnL.Instance(this);  // ✅ Ensures STA thread for UI
	        });
			UpdateStatus("✅ PnL Tracking Initialized on NinjaTrader Start!");
            LoadSettings();
        }
		public static class NJ2CSState
		{
		    private static volatile bool _pauseSignal = false;
		    private static readonly object lockObj = new object();
		    public static bool PauseSignal
		    {
		        get
		        {
		            lock (lockObj)
		            {
		                return _pauseSignal;
		            }
		        }
		        set
		        {
		            lock (lockObj)
		            {
		                _pauseSignal = value;
		            }
		        }
		    }
		}
		private string GenerateUniqueId()
	    {
	        // Implement a method to generate unique IDs
	        return Guid.NewGuid().ToString();
	    }
		private void OnApplicationExit(object sender, ExitEventArgs e)
		{
		    // Stop the market close monitor
		    StopMarketCloseMonitor();
		}	
		private void StartMarketCloseMonitor()
		{
		    marketCloseTimer = new System.Timers.Timer(1000); // Check every second
		    marketCloseTimer.Elapsed += (s, e) =>
		    {
		        try
		        {
		            var currentTime = DateTime.UtcNow - TimeSpan.FromHours(5); // Adjust for EST
		            if (closePositionsBeforeMarketClose &&
		                currentTime.Hour == 15 &&
		                currentTime.Minute == 59 &&
		                currentTime.Second == 30)
		            {
		                //CloseAllPositionsBeforeMarketClose();
		            }
		        }
		        catch (Exception ex)
		        {
		            UpdateStatus($"Error during market close monitoring: {ex.Message}");
		        }
		    };
		    marketCloseTimer.Start();
		}	
		private void StopMarketCloseMonitor()
		{
		    if (marketCloseTimer != null)
		    {
		        marketCloseTimer.Stop();
		        marketCloseTimer.Dispose();
		        marketCloseTimer = null;
		    }
		}
        public Signal ParseSignal(string json)
        {
            try
            {
                var signal = new Signal();

                json = json.Trim('{', '}');
                var keyValuePairs = json.Split(',');

                foreach (var pair in keyValuePairs)
                {
                    var keyValue = pair.Split(':');
                    if (keyValue.Length != 2) continue;

                    var key = keyValue[0].Trim(' ', '"');
                    var value = keyValue[1].Trim(' ', '"');

                    switch (key.ToLower())
                    {
                        case "symbol":
                            signal.Symbol = value;
                            break;
                        case "action":
                            signal.Action = value.ToLower();
                            break;
                        case "username":
                            signal.UserName = value.ToLower();
                            break;
                        case "accountname":
                            signal.AccountName = value;
                            break;
                        case "quantity":
                            if (int.TryParse(value, out int quantity))
                                signal.Quantity = quantity;
                            break;
                    }
                }

                if (string.IsNullOrEmpty(signal.Symbol))
                    throw new Exception("Symbol is null or empty");
				currentSignal = signal.Action;
                return signal;
            }
            catch (Exception ex)
            {
                UpdateStatus($"NJ2CS is null");
                return null;
            }
        }
		private bool IsTradingTime()
		{
		    var currentTime = DateTime.UtcNow - TimeSpan.FromHours(5); // Adjust for EST
		    var currentDay = currentTime.TimeOfDay;
		
		    // Handle next-day trading end time
		    if (tradingStartTime > tradingEndTime)
		    {
		        return currentDay >= tradingStartTime || currentDay <= tradingEndTime;
		    }
		    else
		    {
		        return currentDay >= tradingStartTime && currentDay <= tradingEndTime;
		    }
		}
		public void HandleSignal(Signal signal)
		{
		    UpdateStatus($"HandleSignal: Symbol={signal.Symbol}, Action={signal.Action}, Quantity={signal.Quantity}, AccountName={signal.AccountName}");
		
		    try
		    {
	       // if (pauseSignal)
				if (NJ2CSState.PauseSignal)
		        {
		            UpdateStatus($"Signal processing is paused. Symbol={signal.Symbol}, Action={signal.Action}, Quantity={signal.Quantity}, AccountName={signal.AccountName}");
		            return;
		        }
		
		        if (!IsTradingTime())
		        {
		            UpdateStatus("Signal ignored due to outside trading hours.");
		            return;
		        }
		
		        // Process signal for the specified account if accountName is provided
		        if (!string.IsNullOrEmpty(signal.AccountName))
		        {
		            if (GlobalVariables.csAccounts.TryGetValue(signal.AccountName, out var accountHash))
		            {
		                UpdateStatus($"Processing signal for specific account: {signal.AccountName} (Hash: {accountHash})");
		              //  ProcessSignalForSpecificAccount(accountHash, symbol, action, quantity);
		            }
		            else
		            {
		                UpdateStatus($"Account {signal.AccountName} not found in csAccounts. Ignoring signal for Symbol={signal.Symbol}, Action={signal.Action}, Quantity={signal.Quantity}.");
		            }
		            return; // Exit after processing specific account
		        }
		
		        // Process signal for all accounts if no specific account is provided
		        UpdateStatus($"Processing signal for all accounts: Symbol={signal.Symbol}, Action={signal.Action}, Quantity={signal.Quantity}");
		        foreach (var kvp in GlobalVariables.csAccounts)
		        {
		            try
		            {
		                string accountHash = kvp.Value; // Get the hashed account ID from dictionary
		                if (string.IsNullOrEmpty(accountHash))
		                {
		                    UpdateStatus($"Account {kvp.Key} has an invalid hash ID. Skipping.");
		                    continue;
		                }
		
		               ProcessSignalForSpecificAccount(accountHash, signal.Symbol, signal.Action, signal.Quantity);
		            }
		            catch (Exception ex)
		            {
		                UpdateStatus($"Error handling signal for Account: {kvp.Key} (Hash: {kvp.Value}). Error: {ex.Message}");
		            }
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error handling signal: {ex.Message}");
		    }
		}
		private void ProcessSignalForSpecificAccount(string accountHash, string symbol, string action, int quantity )
		{
		    try
		    {
		        UpdateStatus($"Processing signal:  Action={action}, Symbol={symbol},  Quantity={quantity}");
		        switch (action.ToLower())
		        {
		            case "buy":
		                HandleBuySignal( symbol, quantity );
		                break;
		            case "sell":
		                HandleSellSignal(symbol, quantity );
		                break;
		            default:
		                UpdateStatus($"Unhandled signal action={action} for Symbol={symbol}");
		                break;
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in ProcessSignalForSpecificAccount: {ex.Message}");
		    }
		}
		private async void HandleBuySignal(string symbol, int quantity)
		{
		    try
		    {
		        // ✅ Get the default account number
		        string accountNumber = await NJ2CS.Instance.GetDefaultAccountNumber();
		        
		        // ✅ Get account hash from GlobalVariables (fallback to API call if missing)
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await NJ2CS.Instance.GetAccountHash(accountNumber);
		        }
		
		        // ✅ Ensure valid accountHash
		        if (string.IsNullOrEmpty(accountHash))
		        {
		            UpdateStatus($"❌ Error: No valid account hash found for {accountNumber}. Aborting.");
		            return;
		        }
		
		        // ✅ Get the current market price
		        decimal limitPrice = await GetMarketPrice(symbol) ?? 100.00m;
		
		        // ✅ Define stop loss and take profit
		        decimal stopLossPoints = 3.00m;
		        decimal takeProfitPoints = 4.00m;
		        decimal takeProfit = limitPrice + takeProfitPoints;
		        decimal stopLoss = limitPrice - stopLossPoints;
		
		        // ✅ Fetch position details
		        string positionType = (await SchwabApi.Instance.GetPositionTypeAsync(accountHash, symbol)).ToString();
		        double unrealizedPnL = await SchwabApi.Instance.GetUnrealizedPnL(accountHash);
		        double totalProfitLoss = await SchwabApi.Instance.GetTotalPnL(accountNumber);
		
		        int maxLongAllowed = IsOCOActive() ? maxLongPositionAllowedOCO : maxLongPositionAllowedDCA;
		
		        UpdateStatus($"🚀 Buy Signal | Symbol={symbol}, PositionType={positionType}, CurrentQty={quantity}, MaxLongAllowed={maxLongAllowed}");
		
		        // ✅ Check profit/loss limits
		        if (enableMaxProfitLossPerTrade && (unrealizedPnL >= maxProfitPerTrade || unrealizedPnL <= -maxLossPerTrade))
		        {
		            UpdateStatus($"❌ Trade limit reached. Closing position.");
		            CloseAllPositionsAndOCOs(accountHash, "Trade Limit Reached");
		            return;
		        }
		
		        // ✅ Handle different position scenarios
		        if (positionType == "SHORT")
		        {
		            CloseAllPositionsAndOCOs(accountHash, "🔄 Close Short Due to Buy Signal");
		
		            if (allowRev)
		            {
		                if (quantity > maxLongAllowed)
		                {
		                    UpdateStatus($"⚠️ Max long positions reached.");
		                    return;
		                }
		
		                await SchwabApi.Instance.PlaceOrderWithOCO(accountNumber,accountHash,  symbol, SchwabApiCS.OrderAction.Buy, quantity, limitPrice, takeProfit, stopLoss, "Limit");
		                UpdateStatus($"✅ Reversed to Long Position | Symbol={symbol}, Quantity={quantity}");
		            }
		            else
		            {
		                UpdateStatus("⚠️ AllowRev is disabled.");
		            }
		        }
		        else if (positionType == "NONE")
		        {
		            if (quantity > maxLongAllowed || (enableMaxProfitLossPerDay && (totalProfitLoss <= -maxLoss || totalProfitLoss >= profitTarget)))
		            {
		                UpdateStatus($"⚠️ Trade conditions not met.");
		                return;
		            }	
		            await SchwabApi.Instance.PlaceOrderWithOCO(accountNumber,accountHash,  symbol, SchwabApiCS.OrderAction.Buy, quantity, limitPrice, takeProfit, stopLoss, "Limit");
		            UpdateStatus($"✅ Opened Long Position | Symbol={symbol}, Quantity={quantity}");
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error in HandleBuySignal: {ex.Message}");
		    }
		}	
		private async void HandleSellSignal(string symbol, int quantity)
		{
		    try
		    {
		        // ✅ Get the default account number
		        string accountNumber = await NJ2CS.Instance.GetDefaultAccountNumber();
		        
		        // ✅ Get account hash from GlobalVariables (fallback to API call if missing)
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await NJ2CS.Instance.GetAccountHash(accountNumber);
		        }
		
		        // ✅ Ensure valid accountHash
		        if (string.IsNullOrEmpty(accountHash))
		        {
		            UpdateStatus($"❌ Error: No valid account hash found for {accountNumber}. Aborting.");
		            return;
		        }
		
		        // ✅ Get the current market price
		        decimal limitPrice = await GetMarketPrice(symbol) ?? 100.00m;
		
		        // ✅ Define stop loss and take profit
		        decimal stopLossPoints = 3.00m;
		        decimal takeProfitPoints = 4.00m;
		        decimal takeProfit = limitPrice - takeProfitPoints;
		        decimal stopLoss = limitPrice + stopLossPoints;
		
		        // ✅ Fetch position details
		        string positionType = (await SchwabApi.Instance.GetPositionTypeAsync(accountHash, symbol)).ToString();
		        double unrealizedPnL = await SchwabApi.Instance.GetUnrealizedPnL(accountHash);
		        double totalProfitLoss = await SchwabApi.Instance.GetTotalPnL(accountNumber);
		
		        int maxShortAllowed = enableOCO ? maxShortPositionAllowedOCO : maxShortPositionAllowedDCA;
		
		        UpdateStatus($"📉 Sell Signal | Symbol={symbol}, PositionType={positionType}, CurrentQty={quantity}, MaxShortAllowed={maxShortAllowed}");
		
		        // ✅ Check profit/loss limits
		        if (enableMaxProfitLossPerTrade && (unrealizedPnL >= maxProfitPerTrade || unrealizedPnL <= -maxLossPerTrade))
		        {
		            UpdateStatus($"❌ Trade limit reached. Closing position.");
		            await SchwabApi.Instance.CloseAllPositionsAndOCOs(accountHash, "Trade Limit Reached");
		            return;
		        }
		
		        // ✅ Handle different position scenarios
		        if (positionType == "LONG")
		        {
		            await SchwabApi.Instance.CloseAllPositionsAndOCOs(accountHash, "🔄 Close Long Due to Sell Signal");
		
		            if (allowRev)
		            {
		                if (quantity > maxShortAllowed)
		                {
		                    UpdateStatus($"⚠️ Max short positions reached.");
		                    return;
		                }
		
		                await SchwabApi.Instance.PlaceOrderWithOCO(accountNumber, accountHash, symbol, SchwabApiCS.OrderAction.Sell, quantity, limitPrice, takeProfit, stopLoss, "Limit");
		                UpdateStatus($"✅ Reversed to Short Position | Symbol={symbol}, Quantity={quantity}");
		            }
		            else
		            {
		                UpdateStatus("⚠️ AllowRev is disabled.");
		            }
		        }
		        else if (positionType == "NONE")
		        {
		            if (!allowShort || quantity > maxShortAllowed || (enableMaxProfitLossPerDay && (totalProfitLoss <= -maxLoss || totalProfitLoss >= profitTarget)))
		            {
		                UpdateStatus($"⚠️ Trade conditions not met.");
		                return;
		            }
		
		            await SchwabApi.Instance.PlaceOrderWithOCO(accountNumber, accountHash, symbol, SchwabApiCS.OrderAction.Sell, quantity, limitPrice, takeProfit, stopLoss, "Limit");
		            UpdateStatus($"✅ Opened Short Position | Symbol={symbol}, Quantity={quantity}");
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error in HandleSellSignal: {ex.Message}");
		    }
		}
		private async Task<decimal?> GetMarketPrice(string symbol)
		{
		    try
		    {
		        var quoteResponse = await SchwabApi.Instance.GetQuoteAsync(symbol);
		        if (quoteResponse != null && !quoteResponse.HasError && quoteResponse.Data != null)
		        {
		            // ✅ Check for the last traded price inside the correct nested object
		            decimal? lastPrice = quoteResponse.Data.quote?.lastPrice ?? quoteResponse.Data.regular?.regularMarketLastPrice;
		
		            if (lastPrice.HasValue)
		                return lastPrice.Value;
		            
		            UpdateStatus($"⚠️ No valid last price found for {symbol}.");
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"⚠️ Failed to fetch market price: {ex.Message}");
		    }
		    return null; // If failed, fallback will use default price
		}
				/// <summary>
				/// Places an order using Schwab API.
				/// </summary>		
		public async Task PlaceTrade(string action, string symbol, int quantity, decimal limitPrice)
		{
		    try
		    {
		        // Ensure AccountHash is retrieved from Schwab API or settings
		 
		        string accountNumber = await NJ2CS.Instance.GetDefaultAccountNumber();
		        string accountHash = await NJ2CS.Instance.GetAccountHash(accountNumber);
		        if (string.IsNullOrEmpty(accountHash))
		        {
		            UpdateStatus("⚠️ AccountHash is missing. Unable to place order.");
		            return;
		        }
		
				var orderAction = action.ToLower() == "buy" 
				    ? SchwabApiCS.OrderAction.Buy 
				    : SchwabApiCS.OrderAction.Sell;
				
				UpdateStatus($"📌 Placing {action.ToUpper()} order: {quantity} {symbol} at {limitPrice}");
				
				// ✅ Correct argument types
				await SchwabApi.Instance.PlaceOrders(
					accountHash,
				    symbol, 
				    orderAction,                      // ✅ Fix 1: Ensure OrderAction is passed as an enum
				    Convert.ToInt32(quantity),        // ✅ Fix 2: Ensure quantity is an integer
				    "Limit",                          // ✅ Fix 3: Ensure order type is a string
				    Convert.ToDecimal(limitPrice),    // ✅ Fix 4: Ensure limitPrice is a decimal
				    Convert.ToDecimal(takeProfitPoints), // ✅ Convert TP to decimal
				    Convert.ToDecimal(stopLossPoints)    // ✅ Convert SL to decimal
				);
		        UpdateStatus($"✅ Order placed: {action.ToUpper()} {quantity} {symbol} at {limitPrice}");
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error placing order: {ex.Message}");
		    }
		}	
		public async Task<string> GetDefaultAccountNumber()
		{
		    if (GlobalVariables.csAccounts != null && GlobalVariables.csAccounts.Count > 0)
		    {
		        return GlobalVariables.csAccounts.Keys.First(); // ✅ Return first available account
		    }
		
		    // ✅ Fetch account numbers properly
		    var accountNumbers = await SchwabApi.Instance.GetAccountNumbersAsync();
		
		    // ✅ Validate list contents
		    if (accountNumbers == null || accountNumbers.Count == 0)
		    {
		        throw new Exception("No accounts found");
		    }
		
		    // ✅ Convert List<AccountNumber> to Dictionary<string, string>
		    GlobalVariables.csAccounts = accountNumbers.ToDictionary(a => a.accountNumber, a => a.hashValue);
		
		    // ✅ Get the first account number
		    return GlobalVariables.csAccounts.Keys.First();
		}
		public async Task<string> GetAccountHash(string accountNumber)
		{
		    try
		    {
		        return await SchwabApi.Instance.GetAccountNumberHash(accountNumber);
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error fetching account hash: {ex.Message}");
		        return string.Empty;
		    }
		}
		/// <summary>
		/// Closes all positions and OCOs for the given account using Schwab API.
		/// </summary>
		private async Task CloseAllPositionsAndOCOs(string accountHash, string reason)
		{
		    try
		    {
		        UpdateStatus($"Closing all positions for {accountHash}. Reason: {reason}");
		
		        // Calls SchwabApi's CloseAllPositionsAndOCOs method
		        await SchwabApi.Instance.CloseAllPositionsAndOCOs(accountHash, reason);
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in CloseAllPositionsAndOCOs: {ex.Message}");
		    }
		}
		private bool IsOCOActive()
		{
		    // Logic to determine if OCO is active, e.g., based on a configuration or runtime condition
		    return enableOCO; // Replace with actual logic
		}
        protected override void OnWindowCreated(Window window)
        {
            ControlCenter controlCenter = window as ControlCenter;

            if (controlCenter == null)
                return;

            existingMenuItems = controlCenter.FindFirst("ControlCenterMenuItemNew") as NTMenuItem;

            if (existingMenuItems == null)
                return;

            // Add menu item for NJ2CS Window
       //     newMenuItemLog = new NTMenuItem
      //      {
      //          Header = "NJ2CS",
       //         Style = Application.Current.TryFindResource("MainMenuItem") as Style
      //      };
     //       newMenuItemLog.Click += OnLogMenuItemClick;
     //       existingMenuItems.Items.Add(newMenuItemLog);

            // Add menu item for Configuration Window
            newMenuItemConfig = new NTMenuItem
            {
                Header = "NJ2CS",
                Style = Application.Current.TryFindResource("MainMenuItem") as Style
            };
            newMenuItemConfig.Click += OnConfigMenuItemClick;
            existingMenuItems.Items.Add(newMenuItemConfig);
        }
		private void OnLogMenuItemClick(object sender, RoutedEventArgs e)
		{
		    if (configWindowInstance == null || !configWindowInstance.IsVisible)
		    {
		      //  NJ2CSShowConfigWindow();
				//NJ2CSShowConfigWindow1();
		    }
		    else
		    {
		        configWindowInstance.Activate(); // Bring to front if already open
		    }
		}
		protected override void OnWindowDestroyed(Window window)
		{
		    if (newMenuItemLog != null)
		    {
		        if (!(window is ControlCenter) ||
		            existingMenuItems == null ||
		            !existingMenuItems.Items.Contains(newMenuItemLog))
		            return;
		
		        newMenuItemLog.Click -= OnLogMenuItemClick;
		        existingMenuItems.Items.Remove(newMenuItemLog);
		        newMenuItemLog = null;
		    }
		
		    if (newMenuItemConfig != null)
		    {
		        if (!(window is ControlCenter) ||
		            existingMenuItems == null ||
		            !existingMenuItems.Items.Contains(newMenuItemConfig))
		            return;
		
		        newMenuItemConfig.Click -= OnConfigMenuItemClick;
		        existingMenuItems.Items.Remove(newMenuItemConfig);
		        newMenuItemConfig = null;
		    }
		}	
		private void OnConfigMenuItemClick(object sender, RoutedEventArgs e)
		{
		    if (configWindowInstance == null || !configWindowInstance.IsVisible)
		    {
		        // Ensure 'baseInstance' is initialized correctly before calling the function
		        if (baseInstance == null)
		        {
		            baseInstance = new NJ2CS(); // Initialize it if not already done
		        }
		
		        NJ2CSShowConfigWindow(baseInstance);
		    }
		    else
		    {
		        configWindowInstance.Activate(); // Bring to front if already open
		    }
		}
		private void AddAccountGroupPanel(Grid parentGrid, StackPanel accountPanel, string title, int columnIndex)
		{
		    var groupPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5) };
		
		    groupPanel.Children.Add(new TextBlock
		    {
		        Text = title,
		        FontWeight = FontWeights.Bold,
		        Margin = new Thickness(0, 5, 0, 10)
		    });
		
		    groupPanel.Children.Add(accountPanel);
		    parentGrid.Children.Add(groupPanel);
		    Grid.SetColumn(groupPanel, columnIndex);
		}		 
		private int ExtractNumericSuffix(string accountName)
		{
		    // Match numeric characters at the end of the string
		    var match = System.Text.RegularExpressions.Regex.Match(accountName, @"\d+$");
		    if (match.Success && int.TryParse(match.Value, out int numericSuffix))
		    {
		        return numericSuffix;
		    }
		    return 0; // Return 0 if no numeric suffix is found
		}
		private void UpdateConfigurationValues(TextBox stopLossTextBox,TextBox takeProfitTextBox,TextBox maxLongPositionDCATextBox,   TextBox maxShortPositionDCATextBox,    TextBox webServerTextBox,    TextBox tokenTextBox)
		{
		    try
		    {
		        // Parse Stop Loss Points
		        if (double.TryParse(stopLossTextBox.Text, out double stopLoss))
		        {
		            stopLossPoints = stopLoss;
		            UpdateStatus($"Stop Loss Points updated to: {stopLossPoints}");
		        }
		        else
		        {
		            stopLossPoints = 10; // Default value
		            UpdateStatus("Invalid Stop Loss value. Set to default value: 10.");
		        }
		
		        // Parse Take Profit Points
		        if (double.TryParse(takeProfitTextBox.Text, out double takeProfit))
		        {
		            takeProfitPoints = takeProfit;
		            UpdateStatus($"Take Profit Points updated to: {takeProfitPoints}");
		        }
		        else
		        {
		            takeProfitPoints = 10; // Default value
		            UpdateStatus("Invalid Take Profit value. Set to default value: 10.");
		        }
		
				// Parse Max Long Position
				if (int.TryParse(maxLongPositionDCATextBox.Text, out int maxLong))
				{
				    maxLongPositionAllowedDCA = maxLong;
				    UpdateStatus($"Max Long Position for DCA updated to: {maxLongPositionAllowedDCA}");
				}
				else
				{
				    UpdateStatus($"Invalid Max Long Position value: '{maxLongPositionDCATextBox.Text}'. Retaining previous value: {maxLongPositionAllowedDCA}.");
				}
				
				// Parse Max Short Position
				if (int.TryParse(maxShortPositionDCATextBox.Text, out int maxShort))
				{
				    maxShortPositionAllowedDCA = maxShort;
				    UpdateStatus($"Max Short Position for DCA updated to: {maxShortPositionAllowedDCA}");
				}
				else
				{
				    UpdateStatus($"Invalid Max Short Position value: '{maxShortPositionDCATextBox.Text}'. Retaining previous value: {maxShortPositionAllowedDCA}.");
				}

		
		        // Update Web Server and Token
		        webserver = webServerTextBox.Text.Trim();
		        token = tokenTextBox.Text.Trim();
		
		        UpdateStatus($"Web Server updated to: {webserver}");
		        UpdateStatus($"Token updated successfully.");
		
		        UpdateStatus("Configuration values updated successfully.");
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error updating configuration values: {ex.Message}");
		    }
		}
		private int ExtractNumber(string accountName)
		{
		    var match = System.Text.RegularExpressions.Regex.Match(accountName, @"\d+");
		    return match.Success ? int.Parse(match.Value) : 0;
		}		
		private StackPanel CreateAccountGroupPanel(string header, StackPanel accountsPanel)
		{
		    return new StackPanel
		    {
		        Orientation = Orientation.Vertical,
		        Children =
		        {
		            new TextBlock { Text = header, FontWeight = FontWeights.Bold, Margin = new Thickness(5) },
		            accountsPanel
		        }
		    };
		}
		private string GenerateFutureTickers()
		{
	/*	    string[] instruments = { "NVDA", "TSLA", "MSFT", "META", "AAPL", "AMZN", "GOOGL",  "SPY", "QQQ", "NVDX" };
		
		    // Get current year and month
		    DateTime now = DateTime.Now;
		    int currentMonth = now.Month;
		    int currentYear = now.Year % 100; // Get last two digits of the year
		
		    // Futures contracts roll in March (03), June (06), September (09), December (12)
		    int[] rollMonths = { 3, 6, 9, 12 };
		    
		    // Determine the nearest valid contract month
		    int contractMonth = rollMonths.First(m => m >= currentMonth);
		    
		    // If the current month is beyond the last roll month (December), switch to next year
		    if (contractMonth == 3 && currentMonth > 12)
		    {
		        contractMonth = 3;
		        currentYear += 1;
		    }
		
		    // Format the month and year to create the ticker string
		    string contractSuffix = $"{contractMonth:D2}-{currentYear:D2}";
		    
		    // Generate tickers for all instruments
		    return string.Join(",", instruments.Select(instr => $"{instr} {contractSuffix}"));
			*/
			return  "NVDA,TSLA,MSFT,META,AAPL,AMZN,GOOGL,SPY,QQQ,NVDX";
		
		}
		protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
				alltickers = GenerateFutureTickers();
                Name = "NJ2CS";
                Description = "TradingView to Schwab";
				websocketUrl = $"wss://{webserver}/websk?token={token}";		
            }
            else if (State == State.Configure)
            {
                //StartWebhookServer($"http://localhost:{DefaultPort}/");
				websocketUrl = $"wss://{webserver}/websk?token={token}";
		        if (socketClient == null)
		        {
		            UpdateStatus("Creating server socketClient singleton instance...");
		            socketClient = NJCSSocketClient.Instance; // Retrieve the singleton.
		            socketClient.StartServices();
		        }
 

                //NJ2CSShowConfigWindow();
				//NJ2CSShowConfigWindow1();
				var baseInstance = new NJ2CS();
				 InitializeLogging();
				// Show the configuration window
				NJ2CSShowConfigWindow(baseInstance);
            }
 
			
			else if (State == State.Realtime)
			{
			    try
			    {
					socketClient.StartServices(); 
			    }
			    catch (Exception ex)
			    {
 					UpdateStatus($"Error starting Server: {ex.Message}");
			    }
			}
            else if (State == State.Terminated)
            {
				socketClient.StopServices();
				 _settingsJson = null;
            }
        }            
        public class Signal
        {
            public string Symbol { get; set; }
            public string Action { get; set; }
            public int Quantity { get; set; }
			public string AccountName { get; set; }	
			public string SignalName { get; set; }
			public string UserName { get; set; }
			public string TP { get; set; }
			public string SL { get; set; }
        }
		private void InitializeLogging()
		{
		    try
		    {
		        // Ensure the log manager initializes the log tab
		        NJ2CSLogManager.Initialize();
		
		        // Log a message to confirm initialization
		        NJ2CSLogManager.LogMessage("Logging tab initialized successfully.");
		    }
		    catch (Exception ex)
		    {
		        // Log detailed error information in case of failure
		        string ts = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
		        string errorDetails = $"{ts} Failed to initialize logging tab: {ex.Message}\nStack Trace: {ex.StackTrace}";
		        NinjaTrader.Code.Output.Process(errorDetails, PrintTo.OutputTab1);
		    }
		}
		public void UpdateStatus(string message)
		{
		    try
		    {
		        // ✅ Ensure UI updates happen in the UI thread
		        Application.Current.Dispatcher.Invoke(() =>
		        {
		            if (NJ2CS.statusText != null)
		            {
		                NJ2CS.statusText.Text = $"{message}";
		            }
		        });
		
		        // ✅ Ensure NJ2CSLogManager is initialized
		        if (NJ2CSLogManager.LogTab == null)
		        {
		            NJ2CSLogManager.Initialize(); // Attempt to initialize if LogTab is null
		        }
		
		        // ✅ Log message to NJ2CSLogManager
		        if (NJ2CSLogManager.LogTab != null)
		        {
		            NJ2CSLogManager.LogMessage(message);
		        }
		        else
		        {
		            // 🔥 Fallback logging if LogTab is still null
		            string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
		            NinjaTrader.Code.Output.Process($"{timestamp} LogManager.LogTab is still null. Cannot log message: {message}", PrintTo.OutputTab1);
		        }
		    }
		    catch (Exception ex)
		    {
		        // 🔥 Fallback logging for unexpected errors
		        string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
		        NinjaTrader.Code.Output.Process($"{timestamp} Error in UpdateStatus: {ex.Message}", PrintTo.OutputTab1);
		    }
		}
		private void ActivateSettingsTab()
		{
		    var configTabControl = FindConfigTabControl();
		    if (configTabControl != null)
		    {
		        configTabControl.SelectedIndex = 0; // Activate the settings tab
		    }
		}
		private TabControl FindConfigTabControl()
		{
		    return Application.Current.Windows
		        .OfType<Window>()
		        .SelectMany(w => GetVisualChildren(w.Content as DependencyObject))
		        .OfType<TabControl>()
		        .FirstOrDefault();
		}
		private IEnumerable<DependencyObject> GetVisualChildren(DependencyObject parent)
		{
		    if (parent == null) yield break;
		
		    int count = VisualTreeHelper.GetChildrenCount(parent);
		    for (int i = 0; i < count; i++)
		    {
		        var child = VisualTreeHelper.GetChild(parent, i);
		        yield return child;
		
		        foreach (var grandChild in GetVisualChildren(child))
		        {
		            yield return grandChild;
		        }
		    }
		}
		private void NJ2CSShowConfigWindow(NJ2CS baseInstance)
		{
		    try
		    {
		        Application.Current.Dispatcher.BeginInvoke(new Action(async () =>
		        {
		            // 1. Initialize the TabControl if needed.
		            if (configTabControl == null)
		            {
		                UpdateStatus("⚠️ configTabControl was null. Initializing...");
		                configTabControl = new TabControl();
		            }
		            SchwabPnL.configTabControl = configTabControl;
		
		            // 2. Initialize pnlInstance if not already done.
		            if (pnlInstance == null)
		            {
		                UpdateStatus("✅ Initializing pnlInstance...");
		                pnlInstance = SchwabPnL.Instance(this);
		            }
		
		            // 3. Initialize the trade tab and wrap it in a ScrollViewer.
		            if (tradeTab == null)
		            {
		                tradeTab = new NJ2CSTradeTab(baseInstance);
		            }
		            ScrollViewer tradeScrollViewer = new ScrollViewer
		            {
		                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
		                Content = tradeTab
		            };
		
		            // 4. Get (or create) the PnL tab.
		            // Use the cached tab so that its content (and the WebView2 instance) are reused.
		            TabItem pnlTab = pnlInstance.GetPnLTab();
		            if (!configTabControl.Items.Contains(pnlTab))
		            {
		                UpdateStatus("✅ Adding PnL tab to configTabControl...");
		                configTabControl.Items.Add(pnlTab);
		            }
		            configTabControl.SelectedItem = pnlTab;
		
		            // 5. Initialize Settings, Login, and Logs tabs.
		            if (settingsTab == null)
		            {
		                settingsTab = new NJ2CSConfigTab(baseInstance);
		            }
		            if (loginTab == null)
		            {
		                loginTab = new NJ2CSLoginTab();
		            }
		            if (logsPage == null)
		            {
		                logsPage = new NJ2CSLogTab();
		                NJ2CSLogManager.LogTab = logsPage;
		            }
		
		            // 6. Load saved settings.
		            baseInstance.LoadSettings();
		
		            // 7. Create the configuration window.
		            Window configWindow = new Window
		            {
		                Title = "TradingView to Schwab Configuration",
		                Width = 750,
		                Height = 500,
		                WindowStartupLocation = WindowStartupLocation.CenterScreen
		            };
		
		            // 8. Create a main grid with two columns.
		            Grid mainGrid = new Grid();
		            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
		            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		
		            // 9. Create the left (navigation) panel.
		            StackPanel navPanel = new StackPanel { Background = Brushes.LightGray };
		            Grid.SetColumn(navPanel, 0);
		
		            // 10. Create ScrollViewers for each section.
		            ScrollViewer loginScrollViewer = new ScrollViewer
		            {
		                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
		                Content = loginTab
		            };
		
		            ScrollViewer settingsScrollViewer = new ScrollViewer
		            {
		                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
		                Content = settingsTab
		            };
		
		            // IMPORTANT: Use the cached PnL content instead of recreating it.
		            // (Assume that SchwabPnL.GetCachedPnLContent() returns the same UI element created on first call.)
		            ScrollViewer pnlScrollViewer = new ScrollViewer
		            {
		                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
		                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
		                Content = pnlInstance.GetCachedPnLContent()
		            };
		
		            ScrollViewer logsScrollViewer = new ScrollViewer
		            {
		                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
		                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
		                Content = logsPage
		            };
		
		            // 11. Create a content holder grid and add all section ScrollViewers.
		            Grid contentHolder = new Grid();
		            Grid.SetColumn(contentHolder, 1);
		            contentHolder.Children.Add(loginScrollViewer);
		            contentHolder.Children.Add(settingsScrollViewer);
		            contentHolder.Children.Add(tradeScrollViewer);
		            contentHolder.Children.Add(pnlScrollViewer);
		            contentHolder.Children.Add(logsScrollViewer);
		
		            // 12. Set initial visibility (show settings by default).
		            loginScrollViewer.Visibility = Visibility.Collapsed;
		            settingsScrollViewer.Visibility = Visibility.Visible;
		            tradeScrollViewer.Visibility = Visibility.Collapsed;
		            pnlScrollViewer.Visibility = Visibility.Collapsed;
		            logsScrollViewer.Visibility = Visibility.Collapsed;
		
		            // 13. Create navigation buttons.
		            Button loginButton = new Button
		            {
		                Content = "Login",
		                Height = 40,
		                Margin = new Thickness(5),
		                Foreground = Brushes.Black,
		                HorizontalAlignment = HorizontalAlignment.Stretch
		            };
		            Button tradeButton = new Button
		            {
		                Content = "Trade",
		                Height = 40,
		                Margin = new Thickness(5),
		                HorizontalAlignment = HorizontalAlignment.Stretch
		            };
		            Button pnlButton = new Button
		            {
		                Content = "PnL Chart",
		                Height = 40,
		                Margin = new Thickness(5),
		                HorizontalAlignment = HorizontalAlignment.Stretch
		            };
		            Button settingsButton = new Button
		            {
		                Content = "Settings",
		                Height = 40,
		                Margin = new Thickness(5),
		                Foreground = Brushes.Black,
		                HorizontalAlignment = HorizontalAlignment.Stretch
		            };
		            Button logsButton = new Button
		            {
		                Content = "Logs",
		                Height = 40,
		                Margin = new Thickness(5),
		                Foreground = Brushes.Black,
		                HorizontalAlignment = HorizontalAlignment.Stretch
		            };
		
		            // 14. Add the buttons to the navigation panel.
		            navPanel.Children.Add(loginButton);
		            navPanel.Children.Add(tradeButton);
		            navPanel.Children.Add(pnlButton);
		            navPanel.Children.Add(settingsButton);
		            navPanel.Children.Add(logsButton);
		
		            // 15. Wire up button click events.
		            settingsButton.Click += (s, e) =>
		            {
		                loginScrollViewer.Visibility = Visibility.Collapsed;
		                settingsScrollViewer.Visibility = Visibility.Visible;
		                tradeScrollViewer.Visibility = Visibility.Collapsed;
		                pnlScrollViewer.Visibility = Visibility.Collapsed;
		                logsScrollViewer.Visibility = Visibility.Collapsed;
		
		                loginButton.Background = null;
		                tradeButton.Background = null;
		                pnlButton.Background = null;
		                settingsButton.Background = Brushes.LightBlue;
		                logsButton.Background = null;
		            };
		
		            tradeButton.Click += (s, e) =>
		            {
		                loginScrollViewer.Visibility = Visibility.Collapsed;
		                settingsScrollViewer.Visibility = Visibility.Collapsed;
		                tradeScrollViewer.Visibility = Visibility.Visible;
		                pnlScrollViewer.Visibility = Visibility.Collapsed;
		                logsScrollViewer.Visibility = Visibility.Collapsed;
		
		                loginButton.Background = null;
		                tradeButton.Background = Brushes.LightBlue;
		                pnlButton.Background = null;
		                settingsButton.Background = null;
		                logsButton.Background = null;
		            };
		
		            pnlButton.Click += async (s, e) =>
		            {
		                UpdateStatus("📊 PnL Tab Clicked.");
		
		                // Show only the PnL section.
		                loginScrollViewer.Visibility = Visibility.Collapsed;
		                settingsScrollViewer.Visibility = Visibility.Collapsed;
		                tradeScrollViewer.Visibility = Visibility.Collapsed;
		                pnlScrollViewer.Visibility = Visibility.Visible;
		                logsScrollViewer.Visibility = Visibility.Collapsed;
		
		                loginButton.Background = null;
		                tradeButton.Background = null;
		                pnlButton.Background = Brushes.LightBlue;
		                settingsButton.Background = null;
		                logsButton.Background = null;
		
		                // Refresh the cached PnL content (in case it changed) and reassign it.
		                pnlTab.Content = pnlInstance.GetCachedPnLContent();
		                pnlScrollViewer.Content = pnlTab.Content;
		
		                // Wait briefly for the WebView2 control to update.
		                await Task.Delay(500);
		
		                UpdateStatus("ShowPnLChartForAccount Sim101");
		                pnlInstance.ShowPnLChartForAccount("Sim101");
		                UpdateStatus("ShowPnLChartForAccount Sim101---");
		            };
		
		            logsButton.Click += (s, e) =>
		            {
		                loginScrollViewer.Visibility = Visibility.Collapsed;
		                settingsScrollViewer.Visibility = Visibility.Collapsed;
		                tradeScrollViewer.Visibility = Visibility.Collapsed;
		                pnlScrollViewer.Visibility = Visibility.Collapsed;
		                logsScrollViewer.Visibility = Visibility.Visible;
		
		                loginButton.Background = null;
		                tradeButton.Background = null;
		                pnlButton.Background = null;
		                settingsButton.Background = null;
		                logsButton.Background = Brushes.LightBlue;
		            };
		
		            loginButton.Click += (s, e) =>
		            {
		                loginScrollViewer.Visibility = Visibility.Visible;
		                settingsScrollViewer.Visibility = Visibility.Collapsed;
		                tradeScrollViewer.Visibility = Visibility.Collapsed;
		                pnlScrollViewer.Visibility = Visibility.Collapsed;
		                logsScrollViewer.Visibility = Visibility.Collapsed;
		
		                loginButton.Background = Brushes.LightBlue;
		                tradeButton.Background = null;
		                pnlButton.Background = null;
		                settingsButton.Background = null;
		                logsButton.Background = null;
		            };
		
		            // 16. Add the navigation panel and content holder to the main grid.
		            mainGrid.Children.Add(navPanel);
		            mainGrid.Children.Add(contentHolder);
		
		            // 17. Set initial button state.
		            settingsButton.Background = Brushes.LightBlue;
		
		            // 18. Set the window content and closing behavior.
		            configWindow.Content = mainGrid;
		            configWindow.Closing += (sender, e) =>
		            {
		                baseInstance.SaveSettings();
		            };
		
		            // 19. Show and activate the configuration window.
		            configWindow.Show();
		            configWindow.Activate();
		        }));
		    }
		    catch (Exception ex)
		    {
		        MessageBox.Show($"Error displaying configuration window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		    }
		}
		private TabItem WrapInTabItem(object control, string header)
		{
		    // If the control is already a TabItem, return it; otherwise wrap it in one.
		    if (control is TabItem tab)
		        return tab;
		    else
		        return new TabItem { Header = header, Content = control };
		}
		public void LoadConfigContent(StackPanel stackPanel)
		{
		    try
		    {
		        Application.Current.Dispatcher.Invoke(() =>
		        {
		            stackPanel.Children.Clear();
		            stackPanel.Margin = new Thickness(10);
		
		            // Token input
		            stackPanel.Children.Add(new TextBlock { Text = "Token (passcode):" });
		            var tokenTextBox = new TextBox { Text = token };
		            stackPanel.Children.Add(tokenTextBox);	
 					//tokenTextBox.TextChanged += OnTokenInputChanged;
					
		            // WebSocket Connection Status
		            var connectionStatusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5) };
		            var connectionStatusIndicator = new Ellipse
		            {
		                Width = 15,
		                Height = 15,
		                Fill = Brushes.Red
		            };
		            connectionStatusPanel.Children.Add(new TextBlock { Text = "Server Connection Status: ", VerticalAlignment = VerticalAlignment.Center });
		            connectionStatusPanel.Children.Add(connectionStatusIndicator);
		
		            // Timer to update WebSocket connection status
		            var statusTimer = new System.Windows.Threading.DispatcherTimer
		            {
		                Interval = TimeSpan.FromSeconds(2) // Check status every 2 seconds
		            };
		
					statusTimer.Tick += (s, e) =>
					{
					    try
					    {
					        if (socketClient == null)
					        {
					            UpdateStatus("Creating server socketClient singleton instance...");
					            socketClient = NJCSSocketClient.Instance; // Retrieve the singleton.
					            socketClient.StartServices();
					        }
					
					        bool isConnected = socketClient.IsWebSocketConnected();
					        //UpdateStatus($"🔍 Checking WebSocket connection: {(isConnected ? "Connected ✅" : "Disconnected ❌")}");
					
					        // ✅ Force UI update
					        Application.Current.Dispatcher.Invoke(() =>
					        {
					            connectionStatusIndicator.Fill = isConnected ? Brushes.Green : Brushes.Red;
					        });
					    }
					    catch (Exception ex)
					    {
					        UpdateStatus($"❌ Error refreshing connection status: {ex.Message}");
					    }
					};
					statusTimer.Start();
					stackPanel.Children.Add(connectionStatusPanel);
		            // Buttons Panel
		            var buttonsPanel = new StackPanel
		            {
		                Orientation = Orientation.Horizontal,
		                HorizontalAlignment = HorizontalAlignment.Center,
		                Margin = new Thickness(0, 10, 0, 10)
		            };
		
		            // Save Settings Button (Green)
		            var saveButton = new Button
		            {
		                Content = "Save Settings",
		                Background = Brushes.Green,
		                Foreground = Brushes.White,
		                Margin = new Thickness(5)
		            };		
		            // Save Button at the top
		         //   var saveButton = new Button { Content = "Save Settings", Margin = new Thickness(0, 0, 0, 10) };
		         //   stackPanel.Children.Add(saveButton);
			        // Allowed Tickers (alltickers)
          // Pause Signals Button (Red to Yellow Toggle)
		            var pauseButton = new Button
		            {
		                Content = "Pause Signals",
		                Background = Brushes.Red,
		                Foreground = Brushes.White,
		                Margin = new Thickness(5)
		            };
		
					pauseButton.Click += (s, e) =>
					{
					    //pauseSignal = !pauseSignal; // Toggle pauseSignal
						NJ2CSState.PauseSignal = !NJ2CSState.PauseSignal; 
					    
					    UpdateStatus($"🛑 Pause Button Clicked! pauseSignal is now: {NJ2CSState.PauseSignal}");
					
					    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
					    {
					      //  if (pauseSignal)
							if (NJ2CSState.PauseSignal)
					        {
					            pauseButton.Content = "Resume Signals";
					            pauseButton.Background = Brushes.Yellow;
					            pauseButton.Foreground = Brushes.Black;
					            UpdateStatus("✅ Signals have been paused.");
					        }
					        else
					        {
					            pauseButton.Content = "Pause Signals";
					            pauseButton.Background = Brushes.Red;
					            pauseButton.Foreground = Brushes.White;
					            UpdateStatus("▶️ Signals have been resumed.");
					        }
					    }));
					};
					
		
		            // Add buttons to the panel
		            buttonsPanel.Children.Add(saveButton);
		            buttonsPanel.Children.Add(pauseButton);
		
		            // Add buttons panel to the stack
		            stackPanel.Children.Add(buttonsPanel);		
			        stackPanel.Children.Add(new TextBlock { Text = "Allowed Tickers (comma-separated):" });
			        var tickersTextBox = new TextBox { Text = alltickers };
			        stackPanel.Children.Add(tickersTextBox);	
                    // Validate and update trading times
		            stackPanel.Children.Add(new TextBlock { Text = "Trading Start Time (e.g., 21:00 for 9 PM):" });
		            var tradingStartTextBox = new TextBox { Text = tradingStartTime.ToString(@"hh\:mm") };
		            stackPanel.Children.Add(tradingStartTextBox);
		
		            stackPanel.Children.Add(new TextBlock { Text = "Trading End Time (e.g., 17:00 for 5 PM):" });
		            var tradingEndTextBox = new TextBox { Text = tradingEndTime.ToString(@"hh\:mm") };
		            stackPanel.Children.Add(tradingEndTextBox);
                    if (TimeSpan.TryParse(tradingStartTextBox.Text, out var startTime))
                    {
                        tradingStartTime = startTime;
                    }

                    if (TimeSpan.TryParse(tradingEndTextBox.Text, out var endTime))
                    {
                        tradingEndTime = endTime;
                    }

                    UpdateStatus($"Trading Time updated: Start - {tradingStartTime}, End - {tradingEndTime}");
		            // Close Positions Before Market Closing
		            stackPanel.Children.Add(new TextBlock { Text = "Close All Positions Before Market Closing:" });
		            var closePositionsCheckBox = new CheckBox { IsChecked = closePositionsBeforeMarketClose };
		            closePositionsCheckBox.Checked += (s, e) => closePositionsBeforeMarketClose = true;
		            closePositionsCheckBox.Unchecked += (s, e) => closePositionsBeforeMarketClose = false;
		            stackPanel.Children.Add(closePositionsCheckBox);
		            // WebSocket Server   Settings
				//	stackPanel.Children.Add(new TextBlock { Text = "Server Name:" });
					stackPanel.Children.Add(new TextBlock { Text = "" });
					var webServerTextBox = new TextBox { Text = webserver, Visibility = Visibility.Collapsed };
					stackPanel.Children.Add(webServerTextBox);
 
					// Manual Close Position Setting
					stackPanel.Children.Add(new TextBlock { Text = "Manual Close Position:" });
					var enableManualCloseCheckBox = new CheckBox { IsChecked = manualClosePosition };
					enableManualCloseCheckBox.Checked += (s, e) => manualClosePosition = true;
					enableManualCloseCheckBox.Unchecked += (s, e) => manualClosePosition = false;
					stackPanel.Children.Add(enableManualCloseCheckBox);
					// Group for ATM Strategy
					var atmGroup = new StackPanel
					{
					    Orientation = Orientation.Vertical,
					    Margin = new Thickness(0, 10, 0, 10)
					};
					
					/* Title for ATM Strategy
					atmGroup.Children.Add(new TextBlock
					{
					    Text = "Use ATM Strategy:",
					    FontWeight = FontWeights.Bold,
					    Margin = new Thickness(0, 5, 0, 2)
					});
					
					// ATM Strategy Checkbox
					var useATMCheckBox = new CheckBox
					{
					    IsChecked = useATM,
					    Content = "Enable ATM Strategy for Orders",
					    Margin = new Thickness(0, 5, 0, 10)
					};
					*/
					var enableOcoCheckBox = new CheckBox
					{
					    IsChecked = enableOCO,
					    Content = "Enable OCO Protection for Orders",
					    Margin = new Thickness(0, 5, 0, 10)
					};
					/*useATMCheckBox.Checked += (s, e) =>
					{
					    useATM = true;
					    enableOCO = false;
					    enableOcoCheckBox.IsChecked = false; // Uncheck OCO
					};
					useATMCheckBox.Unchecked += (s, e) =>
					{
					    useATM = false;
					};
					
					// Add ATM group to main stack panel
					atmGroup.Children.Add(useATMCheckBox);
					stackPanel.Children.Add(atmGroup);
					*/
					// Group for OCO Protection
					var ocoGroup = new StackPanel
					{
					    Orientation = Orientation.Vertical,
					    Margin = new Thickness(0, 10, 0, 10)
					};
					
					// Title for OCO Protection
					ocoGroup.Children.Add(new TextBlock
					{
					    Text = "Enable OCO Protection:",
					    FontWeight = FontWeights.Bold,
					    Margin = new Thickness(0, 5, 0, 2)
					});
					
					// OCO Protection Checkbox

					enableOcoCheckBox.Checked += (s, e) =>
					{
					    enableOCO = true;
					  //  useATM = false;
					 //   useATMCheckBox.IsChecked = false; // Uncheck ATM
					};
					enableOcoCheckBox.Unchecked += (s, e) =>
					{
					    enableOCO = false;
					};
					
					// Add OCO group to main stack panel
					ocoGroup.Children.Add(enableOcoCheckBox);
					stackPanel.Children.Add(ocoGroup);
		            stackPanel.Children.Add(new TextBlock { Text = "OCO Stop Loss (points):" });
		            var stopLossTextBox = new TextBox { Text = stopLossPoints.ToString() };
		            stackPanel.Children.Add(stopLossTextBox);
		
		            stackPanel.Children.Add(new TextBlock { Text = "OCO Take Profit (points):" });
		            var takeProfitTextBox = new TextBox { Text = takeProfitPoints.ToString() };
		            stackPanel.Children.Add(takeProfitTextBox);					
		            // Allow Reverse Setting
		            stackPanel.Children.Add(new TextBlock { Text = "Allow Reversal of Positions:" });
		            var allowRevCheckBox = new CheckBox { IsChecked = allowRev };
		            allowRevCheckBox.Checked += (s, e) => allowRev = true;
		            allowRevCheckBox.Unchecked += (s, e) => allowRev = false;
		            stackPanel.Children.Add(allowRevCheckBox);
		
		            // Allow Long Setting
		            stackPanel.Children.Add(new TextBlock { Text = "Allow Long Positions:" });
		            var allowLongCheckBox = new CheckBox { IsChecked = allowLong };
		            allowLongCheckBox.Checked += (s, e) => allowLong = true;
		            allowLongCheckBox.Unchecked += (s, e) => allowLong = false;
		            stackPanel.Children.Add(allowLongCheckBox);
		
		            // Allow Short Setting
		            stackPanel.Children.Add(new TextBlock { Text = "Allow Short Positions:" });
		            var allowShortCheckBox = new CheckBox { IsChecked = allowShort };
		            allowShortCheckBox.Checked += (s, e) => allowShort = true;
		            allowShortCheckBox.Unchecked += (s, e) => allowShort = false;
		            stackPanel.Children.Add(allowShortCheckBox);
			        // Max Long Position Allowed
			        stackPanel.Children.Add(new TextBlock { Text = "OCO Max Long Position Allowed:" });
			        var maxLongPositionOCOTextBox = new TextBox { Text = maxLongPositionAllowedOCO.ToString() };
			        stackPanel.Children.Add(maxLongPositionOCOTextBox);
			
			        // Max Short Position Allowed
			        stackPanel.Children.Add(new TextBlock { Text = "OCO Max Short Position Allowed:" });
			        var maxShortPositionOCOTextBox = new TextBox { Text = maxShortPositionAllowedOCO.ToString() };
			        stackPanel.Children.Add(maxShortPositionOCOTextBox);	
					
			        // Allow DCA
			        stackPanel.Children.Add(new TextBlock { Text = "Allow DCA or Accumulate Positions:" });
			        var allowDcaCheckBox = new CheckBox { IsChecked = allowDCA };
			        allowDcaCheckBox.Checked += (s, e) => allowDCA = true;
			        allowDcaCheckBox.Unchecked += (s, e) => allowDCA = false;
			        stackPanel.Children.Add(allowDcaCheckBox);
			        // Max Long Position Allowed
			        stackPanel.Children.Add(new TextBlock { Text = "DCA Max Long Position Allowed:" });
			        var maxLongPositionDCATextBox = new TextBox { Text = maxLongPositionAllowedDCA.ToString() };
			        stackPanel.Children.Add(maxLongPositionDCATextBox);
			
			        // Max Short Position Allowed
			        stackPanel.Children.Add(new TextBlock { Text = "DCA Max Short Position Allowed:" });
			        var maxShortPositionDCATextBox = new TextBox { Text = maxShortPositionAllowedDCA.ToString() };
			        stackPanel.Children.Add(maxShortPositionDCATextBox);	

	            	// Enable Max Profit/Loss Per Day
		            stackPanel.Children.Add(new TextBlock { Text = "Enable Daily Max Profit/Loss" });
		            var enableMaxProfitLossPerDayCheckBox = new CheckBox { IsChecked = enableMaxProfitLossPerDay };
		            enableMaxProfitLossPerDayCheckBox.Checked += (s, e) => enableMaxProfitLossPerDay  = true;
		            enableMaxProfitLossPerDayCheckBox.Unchecked += (s, e) => enableMaxProfitLossPerDay  = false;
		            stackPanel.Children.Add(enableMaxProfitLossPerDayCheckBox);		
					
		            stackPanel.Children.Add(new TextBlock { Text = "Daily Profit Target (in currency):" });
		            var profitTargetTextBox = new TextBox { Text = profitTarget.ToString() };
		            stackPanel.Children.Add(profitTargetTextBox);
		
		            stackPanel.Children.Add(new TextBlock { Text = "Daily Maximum Loss (in currency):" });
		            var maxLossTextBox = new TextBox { Text = maxLoss.ToString() };
		            stackPanel.Children.Add(maxLossTextBox);	
					
		            // Enable Max Profit/Loss Per Trade
		            stackPanel.Children.Add(new TextBlock { Text = "Enable Max Profit/Loss Per Trade:" });
		            var enableMaxProfitLossCheckBox = new CheckBox { IsChecked = enableMaxProfitLossPerTrade };
		            enableMaxProfitLossCheckBox.Checked += (s, e) => enableMaxProfitLossPerTrade = true;
		            enableMaxProfitLossCheckBox.Unchecked += (s, e) => enableMaxProfitLossPerTrade = false;
		            stackPanel.Children.Add(enableMaxProfitLossCheckBox);
					
		            // Max Profit Per Trade
		            stackPanel.Children.Add(new TextBlock { Text = "Maximum Profit Per Trade (in currency):" });
		            var maxProfitPerTradeTextBox = new TextBox { Text = maxProfitPerTrade.ToString() };
		            stackPanel.Children.Add(maxProfitPerTradeTextBox);
		            stackPanel.Children.Add(new TextBlock { Text = "Maximum Loss Per Trade (in currency):" });
		            var maxLossPerTradeTextBox = new TextBox { Text = maxLossPerTrade.ToString() };
		            stackPanel.Children.Add(maxLossPerTradeTextBox);
		            // Account Selector (Grouped and Sorted)
					 
		            // Selected accounts display

		
            stackPanel.Children.Add(new TextBlock
            {
                Text = "Selected Accounts:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 5, 10)
            });

 
 

		    // ✅ Initialize `selectedAccountsListBox` at class level
		    selectedAccountsListBox = new ListBox
		    {
		        Margin = new Thickness(0, 10, 0, 10)
		    };
			
            // Populate accounts from csAccounts dictionary
            foreach (var account in GlobalVariables.csAccounts)
            {
                string accountDisplayText = $"Account: {account.Key}, Hash: {account.Value}";
                selectedAccountsListBox.Items.Add(accountDisplayText);
            }

            stackPanel.Children.Add(selectedAccountsListBox);

            // Refresh Button
            var refreshButton = new Button
            {
                Content = "Refresh Accounts",
                Background = Brushes.LightBlue,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 10, 0, 10)
            };
			refreshButton.Click += async (s, e) => await RefreshAccountsAsync();





            		stackPanel.Children.Add(refreshButton);
 
					saveButton.Click += async (s, e) =>
					{
					    try
					    {
					        string newToken = tokenTextBox.Text.Trim();
							 
					        UpdateStatus($"📝 Entered Token: {newToken}");
					   		UpdateStatus($"Token changed from {previousToken} to {newToken}. Reinitializing connection...");
        					socketClient.SetToken(newToken);
						/*	if (IsWebSocketConnected())
							{
								UpdateStatus($"Already connected with newToken {newToken}  ");
								_ = Task.Run(() => ListenForWebSocketMessages(webSocketCts.Token), webSocketCts.Token);

							}
						
				           else if (newToken  != "freetrial"  && !IsWebSocketConnected())
							   */
							if (newToken != "freetrial")
							{
							    UpdateStatus($"Token changed from {previousToken} to {newToken }. Reinitializing connection...");
							    
							    websocketUrl = $"wss://{webserver}/websk?token={newToken}"; // ✅ Update before stopping old WebSocket
								//UpdateStatus($"{websocketUrl}..");
							    socketClient = NJCSSocketClient.Instance;
							    socketClient.SetToken(newToken);
							    
							    // Optionally, restart services:
							    socketClient.StartServices();
								/*
								try
							    {
							        socketClient.SetReconnecting(true);
							        UpdateStatus($"Starting new server connection {token}...");
									socketClient.StartWebSocket();
									UpdateStatus("✅ Server connection successfully reestablished.");
							   }
							    catch (Exception ex)
							    {
							        UpdateStatus($"❌ Error restarting server connection: {ex.Message}");
							    }
							    finally
							    {
							        socketClient.SetReconnecting(false);
							    }
							*/
								Task.Run(async () =>
							{
							    try
							    {
							        socketClient.SetReconnecting(true);
							        UpdateStatus($"Starting new server connection {token}...");
							        await socketClient.RestartWebSocketAsync(); // ✅ Use await inside async lambda
							        UpdateStatus("✅ Server connection successfully reestablished.");
							    }
							    catch (Exception ex)
							    {
							        UpdateStatus($"❌ Error restarting server connection: {ex.Message}");
							    }
							    finally
							    {
							        socketClient.SetReconnecting(false);
							    }
							});
							
							}
							previousToken = newToken;
							
 							
						    UpdateStatus("Save Settings button clicked.");
					        // Log selected accounts
					     //   var selectedAccountsLog = selectedAccounts?.Select(a => a.Name).ToList() ?? new List<string>();
					     //   UpdateStatus($"Accounts selected: {string.Join(", ", selectedAccountsLog)}");
	 
		                   // if (accountID.StartsWith("Sim", StringComparison.OrdinalIgnoreCase)||accountID.StartsWith("PRACTICE", StringComparison.OrdinalIgnoreCase))
							//	  maxLoss = 3000;
					        // Validate and update settings
					        string allTickers = tickersTextBox.Text.Trim();
					        allowedTickers = new HashSet<string>(alltickers.Split(','), StringComparer.OrdinalIgnoreCase);
					        if (string.IsNullOrEmpty(allTickers))
					        {
					            UpdateStatus("⚠️ No tickers entered. Keeping previous tickers.");
					        }
					
					        // ✅ Convert input to a List (avoiding duplicates, trimming spaces)
					       var tickerList = new List<string>(
					            allTickers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
					                      .Select(t => t.Trim()) // 🔹 Trim manually instead of using `TrimEntries`
					                      .Distinct(StringComparer.OrdinalIgnoreCase) // Ensure uniqueness
					        );
					
					        if (tickerList.Count == 0)
					        {
					            UpdateStatus("⚠️ No valid tickers entered. Keeping previous tickers.");
					        }
				            if (tradeTab == null)
				            {
				                tradeTab = new NJ2CSTradeTab(baseInstance);
				            }							 
							NJ2CSTradeTab.UpdateTickerDropdown();
							UpdateStatus($"tickers: {allTickers}");
					        // ✅ Update Global Tickers List
					        GlobalVariables.Tickers = tickerList;

        					NJ2CSLogManager.LogMessage($"🔍 Updated GlobalVariables.Tickers: {string.Join(", ", GlobalVariables.Tickers)}");
							
					        if (int.TryParse(maxLongPositionOCOTextBox.Text, out int ocomlp))
					        {
					            maxLongPositionAllowedOCO = ocomlp;
					        }
					        if (int.TryParse(maxShortPositionOCOTextBox.Text, out int ocomsp))
					        {
					            maxShortPositionAllowedOCO = ocomsp;
					        }

							// Parse Max Long Position
							if (int.TryParse(maxLongPositionDCATextBox.Text, out int maxLong))
							{
							    maxLongPositionAllowedDCA = maxLong;
							    UpdateStatus($"Max Long Position for DCA updated to: {maxLongPositionAllowedDCA}");
							}
							else
							{
							    UpdateStatus($"Invalid Max Long Position value: '{maxLongPositionDCATextBox.Text}'. Retaining previous value: {maxLongPositionAllowedDCA}.");
							}
							
							// Parse Max Short Position
							if (int.TryParse(maxShortPositionDCATextBox.Text, out int maxShort))
							{
							    maxShortPositionAllowedDCA = maxShort;
							    UpdateStatus($"Max Short Position for DCA updated to: {maxShortPositionAllowedDCA}");
							}
							else
							{
							    UpdateStatus($"Invalid Max Short Position value: '{maxShortPositionDCATextBox.Text}'. Retaining previous value: {maxShortPositionAllowedDCA}.");
							} 
					        // Validate and save the Allow DCA state
		                    if (double.TryParse(profitTargetTextBox.Text, out double newProfitTarget))
		                    {
		                        profitTarget = newProfitTarget;
		                    }
		                    else
		                    {
		                        UpdateStatus("Invalid Profit Target value. Retaining previous value.");
		                    }
		
		                    if (double.TryParse(maxLossTextBox.Text, out double newMaxLoss))
		                    {
		                        maxLoss = newMaxLoss;
		                    }
		                    else
		                    {
		                        UpdateStatus("Invalid Maximum Loss value. Retaining previous value.");
		                    }
							UpdateStatus($"Enable Max ProfitLoss Per Day: {enableMaxProfitLossPerDay}");
		                    UpdateStatus($"Daily Profit Target updated to: {profitTarget}, Max Loss updated to: {maxLoss}");
		                    enableMaxProfitLossPerTrade = enableMaxProfitLossCheckBox.IsChecked ?? false;
		                    UpdateStatus($"Enable Max Profit/Loss Per Trade: {enableMaxProfitLossPerTrade}");

							if (double.TryParse(maxProfitPerTradeTextBox.Text, out double newMaxProfitPerTrade))
		                    {
		                        maxProfitPerTrade = newMaxProfitPerTrade;
		                        UpdateStatus($"Max Profit Per Trade updated to: {maxProfitPerTrade}");
		                    }
		                    else
		                    {
		                        UpdateStatus("Invalid Max Profit Per Trade value. Retaining previous value.");
		                    }

							if (double.TryParse(maxLossPerTradeTextBox.Text, out double newMaxLossPerTrade))
		                    {
		                        maxLossPerTrade = newMaxLossPerTrade;
		                        UpdateStatus($"Max Loss Per Trade updated to: {maxLossPerTrade}");
		                    }
		                    else
		                    {
		                        UpdateStatus("Invalid Max Loss Per Trade value. Retaining previous value.");
		                    }	

							manualClosePosition = enableManualCloseCheckBox.IsChecked ?? true;
							UpdateStatus($"manualClosePosition: {manualClosePosition }.");
					        allowDCA = allowDcaCheckBox.IsChecked ?? false;							
					        if (allowDCA)
					        {
								if (!int.TryParse(maxLongPositionDCATextBox.Text, out maxLongPositionAllowedDCA))
								{
								    // Use the previous value or prompt user to correct the input
								    UpdateStatus("Invalid DCA Max Long Position. Retaining previous value.");
								}
								
								if (!int.TryParse(maxShortPositionDCATextBox.Text, out maxShortPositionAllowedDCA))
								{
								    // Use the previous value or prompt user to correct the input
								    UpdateStatus("Invalid DCA Max Short Position. Retaining previous value.");
								}
					        }
							
							
							allowRev = allowRevCheckBox.IsChecked ?? true;
		                    allowLong = allowLongCheckBox.IsChecked ?? true;
		                    allowShort = allowShortCheckBox.IsChecked ?? true;
					        // Save the checkbox state
					        closePositionsBeforeMarketClose = closePositionsCheckBox.IsChecked ?? false;
					
					        // Validate and update trading times
					        if (TimeSpan.TryParse(tradingStartTextBox.Text, out var startTime))
					        {
					            tradingStartTime = startTime;
					        }
					        if (TimeSpan.TryParse(tradingEndTextBox.Text, out var endTime))
					        {
					            tradingEndTime = endTime;
					        }
					        UpdateStatus($"Trading Time updated: Start - {tradingStartTime}, End - {tradingEndTime}");

					        if (double.TryParse(stopLossTextBox.Text, out double sl))
					        {
					            stopLossPoints = sl;
					        }
					        if (double.TryParse(takeProfitTextBox.Text, out double tp))
					        {
					            takeProfitPoints = tp;
					        }
							//UpdateStatus($"ATM Settings - use ATM: {useATM} ");
							UpdateStatus($"AllowRev: {allowRev}, AllowLong  {allowLong}, AllowShort {allowShort}");
							UpdateStatus($"OCO Settings - Enabled: {enableOCO}, Stop Loss: {stopLossPoints}, Take Profit: {takeProfitPoints}");

							//InitializePositionStore();
                			SaveSettings();  // Save the updated settings
 
					        UpdateStatus("Settings saved successfully.");
					
					        // Start market close monitoring
					        StartMarketCloseMonitor();

		                }
		                catch (Exception ex)
		                {
		                    MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		                    UpdateStatus($"Error saving settings: {ex.Message}");
		                }
		            };
		 			
		          //  mainScrollViewer.Content = stackPanel;
		            //configWindow.Content = mainScrollViewer;
		           // configWindow.Show();
					//configWindow.Activate();
					 ActivateSettingsTab();
 
		        });
		    }
		    catch (Exception ex)
		    {
		        MessageBox.Show($"Error displaying configuration window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		    }
		}
		// ✅ Move Refresh Logic to a Separate Async Function
		private async Task RefreshAccountsAsync()
		{
		    try
		    {
		        UpdateStatus("🔄 Refreshing accounts...");
		
		        // ✅ Use Global Token Storage
		        if (GlobalVariables.SchwabTokensInstance == null || string.IsNullOrEmpty(GlobalVariables.SchwabTokensInstance.tokens.AccessToken))
		        {
		            UpdateStatus("❌ Error: No access token found. Please log in first.");
		            return;
		        }
		
		        UpdateStatus($"✅ Access Token Found: {GlobalVariables.SchwabTokensInstance.tokens.AccessToken.Substring(0, 5)}...");
		
		        // ✅ Ensure API instance is assigned
		        if (NJ2CS.schwabApi == null)
		        {
		            UpdateStatus("⚠️ schwabApi is null. Reinitializing SchwabApi...");
		            NJ2CS.schwabApi = new SchwabApi(GlobalVariables.SchwabTokensInstance);
		        }
		
		        var accountNumbers = await NJ2CS.schwabApi.GetAccountNumbersAsync();
		        if (accountNumbers == null || !accountNumbers.Any())
		        {
		            UpdateStatus("⚠ No accounts retrieved.");
		            return;
		        }
		
		        // ✅ Store in Global Dictionary
		        GlobalVariables.csAccounts.Clear();
		        GlobalVariables.csAccounts = accountNumbers.ToDictionary(a => a.accountNumber, a => a.hashValue);
		
		        // ✅ Fetch and Display Account Balances
		        var balanceResults = await NJ2CS.schwabApi.GetAccountBalancesAsync();
		        foreach (var result in balanceResults)
		        {
		            UpdateStatus(result);
		        }
		
		        // ✅ Update UI: Refresh Selected Accounts ListBox
		        selectedAccountsListBox.Dispatcher.Invoke(() =>
		        {
		            selectedAccountsListBox.ItemsSource = null; // ✅ Reset binding
		            selectedAccountsListBox.Items.Clear(); // ✅ Clear existing items before updating
		            
		            var accountList = GlobalVariables.csAccounts.Keys.Select(account => $"Account: {account}").ToList();
		            selectedAccountsListBox.ItemsSource = accountList; // ✅ Set new list
		        });
		
		        UpdateStatus($"✅ {GlobalVariables.csAccounts.Count} accounts refreshed successfully.");
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error refreshing accounts: {ex.Message}");
		    }
		}
		private void UpdateSelectedAccountsUI()
		{
		    selectedAccountsListBox.Dispatcher.Invoke(() =>
		    {
		        selectedAccountsListBox.Items.Clear();
		        foreach (var account in GlobalVariables.csAccounts)
		        {
		            selectedAccountsListBox.Items.Add($"Account: {account.Key}");
		            UpdateStatus($"✅ UI Updated: Added Account {account.Key}");
		        }
		    });
		}
		private void SelectSettingsTab(TabControl configTabControl)
		{
		    if (configTabControl != null)
		    {
		        foreach (TabItem tab in configTabControl.Items)
		        {
		            if (tab.Header.ToString() == "Settings")
		            {
		                configTabControl.SelectedItem = tab;
		                break;
		            }
		        }
		    }
		}
		private void OnTokenInputChanged(object sender, TextChangedEventArgs e)
		{
			try
			{
			    if (debounceTimer == null)
			    {
			        debounceTimer = new System.Timers.Timer(1000); // 1 second debounce delay
			        debounceTimer.Elapsed += async (s, ev) =>  // Use async lambda here
			        {
			            debounceTimer.Stop();
			            await Application.Current.Dispatcher.InvokeAsync(async () =>
			            {
			                token = ((TextBox)sender).Text.Trim(); // Update the token variable
			                UpdateStatus($"New token entered: {token}. Attempting to reconnect...");
			                await socketClient.RestartWebSocketAsync();  // Ensure we wait for the connection to complete
			                UpdateStatus("WebSocket reconnection attempt completed.");
			            });
			        };
			        debounceTimer.AutoReset = false;
			    }
			
			    debounceTimer.Stop();  // Stop any ongoing timer to reset the countdown
			    debounceTimer.Start(); // Restart the timer with the new value
		    }
		    catch (Exception ex)
		    {
		        // Log detailed error information in case of failure
		        string ts = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
		        string errorDetails = $"{ts} OnTokenInputChanged: {ex.Message}\nStack Trace: {ex.StackTrace}";
		        NinjaTrader.Code.Output.Process(errorDetails, PrintTo.OutputTab1);
		    }
		}
	    public static void ClearSettings()
	    {
	        _settingsJson = null;
	    }
        private class Settings
        {
            public TimeSpan TradingStartTime { get; set; }
            public TimeSpan TradingEndTime { get; set; }
            public string Token { get; set; }
            public string PreviousToken { get; set; }
            public string Passcode { get; set; }
            public string DefaultAccountName { get; set; }
            public string AllTickers { get; set; }
            public bool AllowDCA { get; set; }
            public bool AllowRev { get; set; }
            public bool AllowLong { get; set; }
            public bool AllowShort { get; set; }
            public bool EnableOCO { get; set; }
            public bool UseATM { get; set; }
            public int MaxLongPositionAllowedOCO { get; set; }
            public int MaxShortPositionAllowedOCO { get; set; }
            public int MaxLongPositionAllowedDCA { get; set; }
            public int MaxShortPositionAllowedDCA { get; set; }
            public bool ManualClosePosition { get; set; }
            public bool EnableMaxProfitLossPerDay { get; set; }
            public double ProfitTarget { get; set; }
            public double MaxLoss { get; set; }
            public bool EnableMaxProfitLossPerTrade { get; set; }
            public double MaxLossPerTrade { get; set; }
            public double MaxProfitPerTrade { get; set; }
        }
		private static Type GetJsonConvertType()
	    {
	        // Specify the exact assembly name and version of Newtonsoft.Json you're using
	        var assemblyName = new AssemblyName("Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed");
	        var assembly = Assembly.Load(assemblyName);
	        return assembly?.GetType("Newtonsoft.Json.JsonConvert");
	    }    
		private static object DeserializeObject(string json, Type type)
	    {
	        var jsonConvertType = GetJsonConvertType();
	        if (jsonConvertType == null)
	            throw new Exception("Could not load JsonConvert type from Newtonsoft.Json.");
	
	        var deserializeMethod = jsonConvertType.GetMethod("DeserializeObject", new Type[] { typeof(string), typeof(Type) });
	        if (deserializeMethod == null)
	            throw new Exception("Could not find DeserializeObject method in JsonConvert.");
	
	        return deserializeMethod.Invoke(null, new object[] { json, type });
	    }
	    private static void SerializeObject(object obj)
	    {
	        var jsonConvertType = GetJsonConvertType();
	        if (jsonConvertType == null)
	            throw new Exception("Could not load JsonConvert type from Newtonsoft.Json.");
	
	        var serializeMethod = jsonConvertType.GetMethod("SerializeObject", new Type[] { typeof(object) });
	        if (serializeMethod == null)
	            throw new Exception("Could not find SerializeObject method in JsonConvert.");
	
	        _settingsJson = (string)serializeMethod.Invoke(null, new object[] { obj });
	    }
	    private void SaveSettings()
	    {
	        SerializeObject(settings);
	    }
        private void LoadSettings()
        {
            if (string.IsNullOrEmpty(_settingsJson))
            {
                // If no settings are saved, initialize with defaults
                settings = new Settings
                {
                    TradingStartTime = new TimeSpan(18, 0, 0), // Default: 9 PM
                    TradingEndTime = new TimeSpan(17, 0, 0),   // Default: 5 PM (next day)
                    Token = "freetrial",
                    PreviousToken = "freetrial",
                    Passcode = "freetrial",
                    DefaultAccountName = "Sim101",
                    AllTickers = "MNQ 03-25,MES 03-25,GC 02-25,ES 03-25,NQ 03-25,RTY 03-25",
                    AllowDCA = true,
                    AllowRev = false,
                    AllowLong = true,
                    AllowShort = true,
                    EnableOCO = false,
                    UseATM = false,
                    MaxLongPositionAllowedOCO = 1,
                    MaxShortPositionAllowedOCO = 1,
                    MaxLongPositionAllowedDCA = 2,
                    MaxShortPositionAllowedDCA = 2,
                    ManualClosePosition = false,
                    EnableMaxProfitLossPerDay = false,
                    ProfitTarget = 1500,
                    MaxLoss = 1500,
                    EnableMaxProfitLossPerTrade = false,
                    MaxLossPerTrade = 300,
                    MaxProfitPerTrade = 500
                };
            }
            else
            {
                settings = (Settings)DeserializeObject(_settingsJson, typeof(Settings));
            }
        }
        public void UpdateSetting(string settingName, object value)
        {
            switch (settingName)
            {
                case "Token":
                    settings.Token = (string)value;
                    break;
                // Add cases for other settings...
                default:
                    // Handle unknown settings or log errors
                    break;
            }
            SaveSettings();
        }
	}
}
