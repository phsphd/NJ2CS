using System;
using System.IO;
using System.Data;
 // Needed for DbConnection
//using Microsoft.Data.Sqlite; 
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Data.SQLite;
using NinjaTrader.Cbi;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using NinjaTrader.NinjaScript.AddOns;
using System.Runtime.InteropServices; // Ensure this using directive is present
using Microsoft.Web.WebView2.Core; // ✅ Fixes CoreWebView2NavigationCompletedEventArgs error
using Microsoft.Web.WebView2.Wpf;  // ✅ Fixes WebView2 component usage
using Newtonsoft.Json;
using System.Linq;
using System.Windows.Media;            // For Pen, Brushes, and Stretch
using System.Windows.Media.Imaging; 
using SchwabApiCS; 
public class SchwabPnL
{
    #region Fields

    // Path to the SQLite database
    private readonly string dbPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            @"NinjaTrader 8\bin\Custom\AddOns\CSPnLData.sqlite"); 
/*	private readonly string baseFolder = System.IO.Path.Combine(
		    Environment.ExpandEnvironmentVariables("%USERPROFILE%"),
		    "Documents", 
		    "NinjaTrader 8", 
		    "bin", 
		    "Custom", 
		    "AddOns"
		);*/
	private readonly string htmlContent = @"
	<!DOCTYPE html>
	<html lang='en'>
	<head>
	    <meta charset='UTF-8'>
	    <title>PnL Chart</title>
	    <script src='https://cdn.jsdelivr.net/npm/chart.js'></script>
	    <style>
		    body { 
		        font-family: Arial, sans-serif; 
		        margin: 0; 
		        padding: 0; 
		        display: flex;
		        justify-content: flex-start; /* ✅ Align everything to the left */
		        align-items: flex-start;
		        height: 100vh;
		    }
	        canvas { 
	            background: #f9f9f9; 
	            border: 1px solid #ccc; 
	            display: block; 
	            margin-left: 1px;  /* ✅ Move canvas to the left */
	            width: 650px; 
	            height: 600px;
	
	        }
	    </style>
	</head>
	<body>
	 
	    <canvas id='pnlChart'></canvas>
	
	    <script>
	        console.log('✅ WebView2 JavaScript Loaded.');
	
	        function ensureCanvasVisibility() {
	            let canvas = document.getElementById('pnlChart');
	            if (!canvas) return;
	            canvas.style.display = 'block';
	            canvas.style.visibility = 'visible';
	            canvas.style.opacity = '1';
	            canvas.width = 550;
	            canvas.height = 400;
	        }
	
	        function updateChart(xLabels, pnlValues) {
	            console.log('✅ updateChart() function called with:', xLabels.length, 'labels,', pnlValues.length, 'data points.');
	            let canvas = document.getElementById('pnlChart');
	            if (!canvas) {
	                console.error('❌ ERROR: Canvas not found!');
	                return;
	            }
	            let ctx = canvas.getContext('2d');
	            ensureCanvasVisibility();
	            
	            // 🔄 Destroy previous chart if exists
	            if (window.myChart) {
	                window.myChart.destroy();
	            }
	
	            // 🔍 Set border color dynamically (green if last pnl is positive, red if negative)
	            let lastPnL = pnlValues[pnlValues.length - 1];
	            let borderColor = lastPnL >= 0 ? 'green' : 'red';
	
	            // 🔍 Define segment background colors (green for positive, red for negative)
	            let segmentBackgroundColors = [];
	            for (let i = 0; i < pnlValues.length; i++) {
	                if (i === 0) {
	                    segmentBackgroundColors.push(pnlValues[i] >= 0 ? 'rgba(0, 255, 0, 0.2)' : 'rgba(255, 0, 0, 0.2)');
	                } else {
	                    if ((pnlValues[i - 1] >= 0 && pnlValues[i] < 0) || (pnlValues[i - 1] < 0 && pnlValues[i] >= 0)) {
	                        segmentBackgroundColors.push('rgba(255, 165, 0, 0.4)');  // 🔄 Orange transition color for zero crossing
	                    } else {
	                        segmentBackgroundColors.push(pnlValues[i] >= 0 ? 'rgba(0, 255, 0, 0.2)' : 'rgba(255, 0, 0, 0.2)');
	                    }
	                }
	            }
	
	            window.myChart = new Chart(ctx, {
	                type: 'line',
	                data: {
	                    labels: xLabels,
	                    datasets: [{
	                        label: 'PnL Over Time',
	                        data: pnlValues,
	                        borderColor: borderColor, 
	                        borderWidth: 0.5,
	                        pointRadius: 0.5,
	                        pointHoverRadius: 2,
	                        fill: true,
	                        segment: {
	                            backgroundColor: ctx => {
	                                let index = ctx.p1DataIndex;
	                                return segmentBackgroundColors[index] || 'rgba(255, 0, 0, 0.2)';
	                            }
	                        }
	                    }]
	                },
	                options: {
	                    responsive: false,
	                    maintainAspectRatio: false,
	                    animation: false,
	                    scales: {
	                        x: { title: { display: true, text: 'Time' } },
	                        y: { 
	                            title: { display: true, text: 'PnL' },
	                            suggestedMin: Math.min(...pnlValues) - 100,
	                            suggestedMax: Math.max(...pnlValues) + 100
	                        }
	                    }
	                }
	            });
	
	            window.myChart.update();
	        }
	
	        // ✅ Expose updateChart function to WebView2
	        window.addEventListener('DOMContentLoaded', () => {
	            window.updateChart = updateChart;
	        });
	    </script>
	</body>
	</html>
	";
 
	private readonly string htmlfile = System.IO.Path.Combine(
	    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
	    "OneDrive",
	    "Documents",
	    "NinjaTrader 8",
	    "bin",
	    "Custom",
	    "AddOns",
	    "pnl.html"
	);

	//private readonly string htmlFilePath;
   //  private readonly string htmlfile = System.IO.Path.Combine(  Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"NinjaTrader 8\bin\Custom\AddOns\pnl.html"); 
// 	private readonly string htmlFilePath = System.IO.Path.Combine(baseFolder, "pnl.html");
	private NJ2CS baseInstance;
    // UI elements
    private TabItem pnlTab;
    private ComboBox accountComboBox;
    private Button showPnLButton;
    private DataGrid pnlDataGrid;
	private StackPanel pnlStack;  // ✅ Declare pnlStack at the class level
    private static SchwabPnL _instance;
	private UIElement _cachedPnLContent;
    private static readonly object _lock = new object();
 	private bool webViewInitialized = false;
	public Image pnlChart { get; private set; } 
	private WebView2 webView; 
	public static TabControl configTabControl;

    // Data points to be rendered (each point's X is the timestamp as an integer and Y is the pnl)
 //   private List<SharpDX.Vector2> dataPoints = new List<SharpDX.Vector2>();

    // SharpDX objects
   // private SharpDX.Direct2D1.Factory factory;
  //  private WindowRenderTarget renderTarget;
 //   private HwndRenderTargetProperties hwndRenderTargetProperties;

    // Timers
    private DispatcherTimer pnlTimer;
    private DispatcherTimer cleanupTimer;
    // For logging status (wire this to your NJ2CS logging method if desired)
    public event Action<string> OnMessageReceived;
    #endregion
    #region Constructor
    public static SchwabPnL Instance(NJ2CS baseInstance = null)
    {
        lock (_lock)
        {
            if (_instance == null)
            {
                if (baseInstance == null)
                {
                    throw new ArgumentNullException(nameof(baseInstance), "NJ2CS instance cannot be null on first initialization!");
                }
                _instance = new SchwabPnL(baseInstance);
            }
            return _instance;
        }
    }
    // Private Constructor (Ensures only one instance)
    private SchwabPnL(NJ2CS baseInstance)
    {
        this.baseInstance = baseInstance ?? throw new ArgumentNullException(nameof(baseInstance));
        InitializeDatabase();
        StartPnLTracking();  // ✅ Runs only ONCE
        StartDataCleanup();  // ✅ Runs only ONCE
    }
    #endregion
    #region Database Methods

    // Create the PnL table if it does not exist and seed dummy data if empty.
    private void InitializeDatabase()
    {
        try
        {
            using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                connection.Open();
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = @"CREATE TABLE IF NOT EXISTS PnL (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        date TEXT NOT NULL,
                        timestamp TEXT NOT NULL,
                        accountName TEXT NOT NULL,
                        pnl REAL NOT NULL
                    );";
                    command.ExecuteNonQuery();
                }
                // Insert dummy data if the table is empty.
                using (var checkCommand = new SQLiteCommand("SELECT COUNT(*) FROM PnL;", connection))
                {
                    int count = Convert.ToInt32(checkCommand.ExecuteScalar());
                    if (count == 0)
                    {
                        using (var insertCommand = new SQLiteCommand(connection))
                        {
                            Random rand = new Random();
                            for (int i = 0; i < 10; i++)
                            {
                                string timestamp = DateTime.UtcNow.AddMinutes(-i * 10).ToString("HHmm");
								TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
								DateTime estTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddMinutes(-i * 10), estZone);
								
								// Store in SQLite
								string currentDate = estTime.ToString("yyyy-MM-dd HH:mm:ss");
                                insertCommand.CommandText = "INSERT INTO PnL (date, timestamp, accountName, pnl) VALUES (@date, @timestamp, @accountName, @pnl);";
                                insertCommand.Parameters.AddWithValue("@date", currentDate);
                                insertCommand.Parameters.AddWithValue("@timestamp", timestamp);
                                insertCommand.Parameters.AddWithValue("@accountName", "Sim101");
                                insertCommand.Parameters.AddWithValue("@pnl", rand.Next(-500, 500));
                                insertCommand.ExecuteNonQuery();
                                insertCommand.Parameters.Clear();
                            }
                        }
                        UpdateStatus("Inserted dummy PnL data for Sim101.");
                    }
                    else
                    {
                        UpdateStatus($"Current PnL data count: {count}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error initializing database: {ex.Message}");
        }
    }
	private List<string> GetAvailableAccounts()
	{
	    List<string> accounts = new List<string>();
	    try
	    {
	        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
	        {
	            connection.Open();
	            using (var command = new SQLiteCommand("SELECT DISTINCT accountName FROM PnL;", connection))
	            {
	                using (var reader = command.ExecuteReader())
	                {
	                    while (reader.Read())
	                    {
	                        accounts.Add(reader["accountName"].ToString());
	                    }
	                }
	            }
	        }
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"Error fetching accounts: {ex.Message}");
	    }
	    return accounts;
	}
	private List<Tuple<string, float>> GetPnLData(string accountName)
	{
	    List<Tuple<string, float>> points = new List<Tuple<string, float>>();
	
	    try
	    {
	        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
	        {
	            connection.Open();
	            using (var command = new SQLiteCommand(connection))
	            {
	                // ✅ Order by `date ASC` to maintain correct time order
	                command.CommandText = "SELECT date, pnl FROM PnL WHERE accountName = @accountName ORDER BY date ASC;";
	                command.Parameters.AddWithValue("@accountName", accountName);
	
	                using (var reader = command.ExecuteReader())
	                {
	                    while (reader.Read())
	                    {
	                        // ✅ Convert timestamp to "DD HH:MM" format
	                        DateTime fullTimestamp = DateTime.Parse(reader["date"].ToString());
	                        string formattedTimestamp = fullTimestamp.ToString("dd HH:mm"); // ✅ Correct format
	
	                        float pnl = Convert.ToSingle(reader["pnl"]);
	                        points.Add(new Tuple<string, float>(formattedTimestamp, pnl));
	                    }
	                }
	            }
	        }
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"Error in GetPnLData: {ex.Message}");
	    }
	
	    return points;
	}
	private void StorePnLData(string accountName, double unrealizedPnL, double realizedPnL)
	{
	    try
	    {
	        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
	        {
	            connection.Open();
	            using (var command = new SQLiteCommand(connection))
	            {
	                command.CommandText = @"
	                    INSERT INTO PnL (date, timestamp, accountName, pnl)
	                    VALUES (@date, @timestamp, @accountName, @pnl);";
					TimeZoneInfo estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
					DateTime estTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, estZone);	                
	               // command.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
					command.Parameters.AddWithValue("@date", estTime.ToString("yyyy-MM-dd HH:mm:ss"));
	                command.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("HHmm"));
	                command.Parameters.AddWithValue("@accountName", accountName);
	                command.Parameters.AddWithValue("@pnl", unrealizedPnL + realizedPnL);  // Storing total PnL
	
	                command.ExecuteNonQuery();
	            }
	        }
	        UpdateStatus($"✅ Stored PnL for {accountName}: Unrealized={unrealizedPnL}, Realized={realizedPnL}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in StorePnLData: {ex.Message}");
	    }
	}
    // Starts a timer to clean up old data every 24 hours.
    private void StartDataCleanup()
    {
        try
        {
            cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
            cleanupTimer.Tick += (s, e) =>
            {
                using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(connection))
                    {
                        command.CommandText = "DELETE FROM PnL WHERE date < datetime('now', '-2 days');";
                        command.ExecuteNonQuery();
                    }
                }
            };
            cleanupTimer.Start();
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ ERROR in StartDataCleanup {ex.Message}");
        }
    }
	// Retrieves PnL data for the given account.
	    // (For testing purposes, we remove the date filter so that dummy data is always returned.)	
	    #endregion
	#region  
	
	    // Initializes SharpDX objects (must run on the UI thread).
 

/// <summary>
/// Renders the PnL chart into a WriteableBitmap and assigns it to the pnlChart Image.
/// </summary>
 
    #endregion
    #region UI Creation and Timer Methods
	
    public UIElement GetCachedPnLContent()
    {
        if (_cachedPnLContent == null)
        {
            _cachedPnLContent = CreatePnLContent();
        }
        return _cachedPnLContent;
    }
    // Creates the UI content for the PnL tab.
	// ✅ Modify `CreatePnLContent()`
	public UIElement CreatePnLContent()
	{
	    UpdateStatus("🔹 Creating PnL Content...");
	
	    pnlStack = new StackPanel
	    {
	        Orientation = Orientation.Vertical,
	        VerticalAlignment = VerticalAlignment.Top,
	        Margin = new Thickness(10),
	        Background = Brushes.LightGray // For debugging
	    };
	
	    // Create Account ComboBox
	    accountComboBox = new ComboBox
	    {
	        Margin = new Thickness(10),
	        Visibility = Visibility.Visible
	    };
	    List<string> accounts = GetAvailableAccounts();
	    if (accounts.Any())
	    {
	        foreach (var account in accounts)
	        {
	            accountComboBox.Items.Add(account);
	        }
	        accountComboBox.SelectedIndex = 0;
	    }
	    else
	    {
	        UpdateStatus("⚠️ No accounts found.");
	    }
	    accountComboBox.SelectionChanged += AccountSelectionChanged;
	    pnlStack.Children.Add(accountComboBox);
	    UpdateStatus("✅ Account ComboBox Added");
	
	    // Create Show PnL Button
	    showPnLButton = new Button
	    {
	        Content = "Show PnL",
	        Margin = new Thickness(10),
	        Visibility = Visibility.Visible,
	        Background = Brushes.LightBlue
	    };
	    showPnLButton.Click += ShowPnLButton_Click;
	    pnlStack.Children.Add(showPnLButton);
	    UpdateStatus("✅ Show PnL Button Added & Visible");
	
	    // Create a new WebView2 instance and add it to the UI
	    webView = new WebView2
	    {
	        Margin = new Thickness(10),
	        Height = 450,
	        Width = 700,
	        Visibility = Visibility.Visible
	    };
	    webView.CreationProperties = new CoreWebView2CreationProperties
	    {
	        UserDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NinjaTrader", "WebView2Data")
	    };
	
	    // **Attach a Loaded event handler to wait until the control is fully loaded**
	    webView.Loaded += async (s, e) =>
	    {
	        UpdateStatus("ℹ️ WebView2 Loaded event fired.");
	        if (!webViewInitialized)
	        {
	            bool ok = await InitializeWebViewAsync();
	            if (ok)
	            {
	                webViewInitialized = true;
	                UpdateStatus("✅ WebView2 (Loaded event) initialized successfully.");
	            }
	        }
	    };
	
	    pnlStack.Children.Add(webView);
	    UpdateStatus("✅ New WebView2 instance created and added to UI");
	
	    // Force layout update
	    Application.Current.Dispatcher.Invoke(() => pnlStack.UpdateLayout());
	    UpdateStatus($"🔎 pnlStack children count: {pnlStack.Children.Count}");
	    return pnlStack;
	}
	public async Task<bool> InitializeWebViewAsync()
	{
	    try
	    {
	        // If WebView2 is already initialized, do not reinitialize.
	        if (webView.CoreWebView2 != null)
	        {
	            UpdateStatus("⚠️ WebView2 is already initialized. Skipping reinitialization.");
	            return true;
	        }
	
	        // Create an explicit CoreWebView2Environment.
	        string userDataFolder = Path.Combine(
	            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
	            "NinjaTrader",
	            "WebView2Environment");
	
	        // Create the environment explicitly.
	        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
	        UpdateStatus("⚠️ Calling EnsureCoreWebView2Async() with explicit environment...");
	
	        // Initialize WebView2 with the created environment.
	        await webView.EnsureCoreWebView2Async(env);
	
	        // Double-check initialization.
	        if (webView.CoreWebView2 == null)
	        {
	            UpdateStatus("❌ ERROR: WebView2.Core remains null after EnsureCoreWebView2Async().");
	            return false;
	        }
	
	        UpdateStatus("✅ WebView2.Core initialized successfully.");
	
	        // Apply settings.
	        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
	        webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
	
	        // Navigate to the HTML content.
	        webView.CoreWebView2.NavigateToString(htmlContent);
	        UpdateStatus("✅ WebView2 navigation started.");
	
	        return true;
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in InitializeWebViewAsync: {ex.Message}");
	        return false;
	    }
	}
	private async Task<bool> EnsureWebViewIsReadyAsync()
	{
	    // If the WebView2 control is null, there's nothing we can do.
	    if (webView == null)
	    {
	        UpdateStatus("❌ ERROR: WebView2 control is null.");
	        return false;
	    }
	
	    // If CoreWebView2 is already available, return success.
	    if (webView.CoreWebView2 != null)
	    {
	        UpdateStatus("✅ Using existing WebView2.Core instance.");
	        return true;
	    }
	
	    // Otherwise, wait for initialization without creating a new environment.
	    try
	    {
	        UpdateStatus("⚠️ Calling EnsureCoreWebView2Async()...");
	        await webView.EnsureCoreWebView2Async();
	        if (webView.CoreWebView2 == null)
	        {
	            UpdateStatus("❌ ERROR: WebView2.Core remains null.");
	            return false;
	        }
	        UpdateStatus("✅ WebView2.Core is now initialized.");
	        return true;
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR during EnsureCoreWebView2Async: {ex.Message}");
	        return false;
	    }
	}
	public async void ShowPnLButton_Click(object sender, RoutedEventArgs e)
	{
	    try
	    {
	        UpdateStatus("📊 PnL Tab Clicked.");
	
	        if (accountComboBox?.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ Please select an account.");
	            return;
	        }
	
	        string selectedAccount = accountComboBox.SelectedItem.ToString();
	        UpdateStatus($"📊 Fetching PnL for {selectedAccount} (last 24 hours)...");
	
	        // Ensure the WebView2 control is ready (do not reinitialize if it already exists)
	        bool isReady = await EnsureWebViewIsReadyAsync();
	        if (!isReady)
	        {
	            UpdateStatus("❌ Cannot show PnL chart – WebView2 is not ready.");
	            return;
	        }
	
	        // Fetch the PnL data (this method is assumed to work as in TV2NJ)
	        List<Tuple<string, float>> pnlData = GetPnLData(selectedAccount);
	        if (!pnlData.Any())
	        {
	            UpdateStatus($"⚠️ No PnL data available for {selectedAccount}");
	            return;
	        }
	
	        var xLabels = pnlData.Select(p => p.Item1).ToList();
	        var pnlValues = pnlData.Select(p => p.Item2).ToList();
	        string jsonX = JsonConvert.SerializeObject(xLabels);
	        string jsonY = JsonConvert.SerializeObject(pnlValues);
	
	        // Execute the JavaScript to update the chart
	        string script = $@"
	            try {{
	                if (typeof updateChart === 'function') {{
	                    updateChart({jsonX}, {jsonY});
	                    console.log('✅ updateChart() executed successfully.');
	                }} else {{
	                    console.error('❌ updateChart() function not found.');
	                }}
	            }} catch (error) {{
	                console.error('❌ Chart update failed:', error);
	            }}";
	
	        await webView.CoreWebView2.ExecuteScriptAsync(script);
	        UpdateStatus($"✅ PnL chart updated for {selectedAccount}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in ShowPnLButton_Click: {ex.Message}");
	    }
	}
	private async void InitializeWebView()
	{
	    try
	    {
	        if (webView == null)
	        {
	            UpdateStatus("⚠️ WebView2 is NULL before initialization. Creating a new instance...");
	            webView = new WebView2
	            {
	                //Margin = new Thickness(10),
					Margin = new Thickness(10, 0, 0, 0), 
	                Height = 450,
	                Width = 650,
					HorizontalAlignment = HorizontalAlignment.Left, 
						
						
	            };
	        }
	        else
	        {
	            // ✅ Prevent duplicate UI child error by removing existing parent
	            if (webView.Parent is Panel parentPanel)
	            {
	                parentPanel.Children.Remove(webView);
	            }
	        }
	
	        await webView.EnsureCoreWebView2Async();
	        webView.CoreWebView2.Settings.AreDevToolsEnabled = false; // ✅ Disable DevTools
	        webView.CoreWebView2.NavigationCompleted += WebView_NavigationCompleted;
			webView.HorizontalAlignment = HorizontalAlignment.Stretch;
	        UpdateStatus("✅ WebView2 initialized successfully.");
	        webView.CoreWebView2.NavigateToString(htmlContent);
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in InitializeWebView: {ex.Message}");
	    }
	}
	private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            UpdateStatus("✅ WebView2 navigation completed successfully.");
            // Optionally, delay a bit before updating the chart
            Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
            {
                await Task.Delay(1000);
                if (accountComboBox.SelectedItem != null)
                {
                    ShowPnLChartForAccount(accountComboBox.SelectedItem.ToString());
                }
            });
        }
        else
        {
            UpdateStatus($"❌ ERROR: WebView2 navigation failed. Error Code: {e.WebErrorStatus}");
        }
    }
	private void AccountSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
	    try
	    {
	        UpdateStatus("🔄 AccountSelectionChanged triggered.");
	
	        if (accountComboBox == null)
	        {
	            UpdateStatus("❌ ERROR: accountComboBox is NULL.");
	            return;
	        }
	
	        if (accountComboBox.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ WARNING: accountComboBox.SelectedItem is NULL. No selection made.");
	            return;
	        }
	
	        string selectedAccount = accountComboBox.SelectedItem.ToString();
	        UpdateStatus($"✅ Account changed to: {selectedAccount}");
	
	        // ✅ Ensure WebView2 is initialized before calling `ShowPnLChartForAccount`
	        if (webView == null)
	        {
	            UpdateStatus(" WebView2 is NULL. Cannot update chart.");
	            return;
				
	        }
	
	        if (webView.CoreWebView2 == null)
	        {
	            UpdateStatus("❌ ERROR: WebView2 Core is NULL. Retrying initialization...");
	            webView.NavigationCompleted += (s, ev) =>
	            {
	                ShowPnLChartForAccount(selectedAccount);
	            };
	            return;
	        }
	
	        // ✅ Now safe to proceed
	        ShowPnLChartForAccount(selectedAccount);
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in AccountSelectionChanged: {ex.Message}");
	    }
	}
	// ✅ Define the missing WebView_NavigationCompleted method
	public TabItem GetPnLTab()
	{
		try
		{
		    if (pnlTab == null)
		    {
		        pnlTab = new TabItem { Header = "PnL Chart" };
		        pnlTab.Content = CreatePnLContent();
		
		        pnlTab.GotFocus += (s, e) =>
		        {
		            //InitializeSharpDX();
		            ShowPnLChartForAccount("Sim101");  // Auto-refresh when tab is selected
		        };
		    }
		
		    // ✅ Ensure configTabControl is initialized before using it
		    Application.Current.Dispatcher.Invoke(() =>
		    {
		        if (configTabControl == null)
		        {
		            UpdateStatus("⚠️ configTabControl is null. Cannot add pnlTab.");
		            return;
		        }
		
		        if (!configTabControl.Items.Contains(pnlTab))
		        {
		            configTabControl.Items.Add(pnlTab);
		            UpdateStatus("✅ pnlTab added to configTabControl.");
		        }
		        else
		        {
		            UpdateStatus("✅ pnlTab already exists in configTabControl.");
		        }
		    });
		
		    return pnlTab;
		}
	    catch (Exception ex)
	    {
			return pnlTab;
	        UpdateStatus($"Error GetPnLTab: {ex.Message}");
	    }	
	}
	public async Task ShowPnLChartForAccount(string accountName)
	{
	    try
	    {
	        if (webView == null || webView.CoreWebView2 == null)
	        {
	            UpdateStatus("❌ ERROR: WebView2 is not initialized.");
	            return;
	        }
	
	        List<Tuple<string, float>> pnlData = GetPnLData(accountName);
	        if (!pnlData.Any())
	        {
	            UpdateStatus($"⚠️ No PnL data available for {accountName}");
	            return;
	        }
	
	        var xLabels = pnlData.Select(p => p.Item1).ToList();
	        var pnlValues = pnlData.Select(p => p.Item2).ToList();
	        string jsonX = JsonConvert.SerializeObject(xLabels);
	        string jsonY = JsonConvert.SerializeObject(pnlValues);
	
	        string script = $@"
	            try {{
	                if (typeof updateChart === 'function') {{
	                    updateChart({jsonX}, {jsonY});
	                    console.log('✅ updateChart() executed successfully.');
	                }} else {{
	                    console.error('❌ updateChart() function not found.');
	                }}
	            }} catch (error) {{
	                console.error('❌ Chart update failed:', error);
	            }}";
	
	        await webView.CoreWebView2.ExecuteScriptAsync(script);
	        UpdateStatus($"✅ PnL chart updated for {accountName}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in ShowPnLChartForAccount: {ex.Message}");
	    }
	}	
	public async void ShowPnLChartForAccount1(string accountName)
	{
	    try
	    {
	        // ✅ Create WebView2 instance if it's null
	        if (webView == null)
	        {
	            UpdateStatus("⚠️ WebView2 is NULL. Creating new instance...");
	            webView = new WebView2
	            {
	                Margin = new Thickness(10),
	                Height = 450,
	                Width = 700
	            };
	
	            webView.CreationProperties = new CoreWebView2CreationProperties
	            {
	                UserDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebView2Data")
	            };
	
	            InitializeWebView();
	            await Task.Delay(1000); // ✅ Allow time for WebView2 to initialize
	        }
	
	        // ✅ Ensure CoreWebView2 is initialized before proceeding
	        if (webView.CoreWebView2 == null)
	        {
	            UpdateStatus("⚠️ WebView2 Core is NULL. Initializing...");
	            await webView.EnsureCoreWebView2Async();
	            UpdateStatus("✅ WebView2 Core initialized.");
	        }
	
	        // ✅ Fetch PnL data
	        List<Tuple<string, float>> pnlData = GetPnLData(accountName);
	        if (pnlData.Count == 0)
	        {
	            UpdateStatus($"⚠️ No PnL data available for {accountName}");
	            return;
	        }
	
	        // ✅ Ensure xLabels use "DD HH:MM" format
	        var xLabels = pnlData.Select(p => p.Item1).ToList();
	        var pnlValues = pnlData.Select(p => p.Item2).ToList();
	
	        // ✅ Convert to JSON format
	        string jsonX = JsonConvert.SerializeObject(xLabels);
	        string jsonY = JsonConvert.SerializeObject(pnlValues);
	
	        // ✅ Check if updateChart exists before execution
	        string script = $@"
	            if (typeof updateChart === 'function') {{
	                updateChart({jsonX}, {jsonY});
	                console.log('✅ updateChart() executed successfully.');
	            }} else {{
	                console.error('❌ updateChart() function not found.');
	            }}";
	
	        // ✅ Execute JavaScript in WebView2
	        await webView.CoreWebView2.ExecuteScriptAsync(script);
	        UpdateStatus($"✅ PnL Chart updated for {accountName}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in ShowPnLChartForAccount: {ex.Message}");
	    }
	}
	private async Task LoadWebViewContent()
	{
	    if (webView?.CoreWebView2 == null)
	    {
	        UpdateStatus("❌ ERROR: WebView2 is NULL or not initialized yet.");
	        return;
	    }
	
	    webView.CoreWebView2.NavigationCompleted += async (s, e) =>
	    {
	        if (e.IsSuccess)
	        {
	            UpdateStatus($"✅ WebView2 content loaded from: {webView.Source}");
	            
	            // ✅ Delay JavaScript execution
	            await Task.Delay(1000); // Ensures JavaScript is fully loaded
	
	            ShowPnLChartForAccount(accountComboBox.SelectedItem?.ToString() ?? "Sim101");
	        }
	        else
	        {
	            UpdateStatus("❌ ERROR: WebView2 Navigation failed.");
	        }
	    };
	}
	/// <summary>
	/// Draws text onto the bitmap pixel array (simple approximation for debugging).
	/// </summary>
	
	    // Called when the "Show PnL" button is clicked.
	public void ShowPnLChartForSelectedAccount()
	{
	    try
	    {
	        if (accountComboBox == null || accountComboBox.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ No account selected.");
	            return;
	        }
	
	        string selectedAccount = accountComboBox.SelectedItem.ToString();
	        UpdateStatus($"📊 Fetching PnL for {selectedAccount} (last 24 hours)...");
	
	        // ✅ Fetch PnL Data
	        List<Tuple<string, float>> rawData = GetRecentPnLData(selectedAccount);
	
	        if (rawData == null || rawData.Count == 0)
	        {
	            UpdateStatus("⚠️ No PnL data available for this account.");
	            return;
	        }
	
	        // ✅ Extract X (timestamps) & Y (PnL values)
	        List<string> xLabels = rawData.Select(p => p.Item1).ToList();
	        List<float> yValues = rawData.Select(p => p.Item2).ToList();
	
	        // ✅ Render Chart
	        RenderPnLChart(xLabels, yValues);
	
	        UpdateStatus($"✅ PnL chart updated for {selectedAccount}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in ShowPnLChartForSelectedAccount: {ex.Message}");
	    }
	}
	public List<Tuple<string, float>> GetRecentPnLData(string accountName)
	{
	    List<Tuple<string, float>> dataPoints = new List<Tuple<string, float>>();
	
	    try
	    {
	        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
	        {
	            connection.Open();
	            using (var command = new SQLiteCommand(connection))
	            {
	                command.CommandText = @"
	                    SELECT date, pnl 
	                    FROM PnL 
	                    WHERE accountName = @accountName 
	                    ORDER BY date DESC 
	                    LIMIT 100"; // Limit for performance
	                
	                command.Parameters.AddWithValue("@accountName", accountName);
	
	                using (var reader = command.ExecuteReader())
	                {
	                    while (reader.Read())
	                    {
	                        string timestamp = DateTime.Parse(reader["date"].ToString()).ToString("HH:mm");
	                        float pnlValue = Convert.ToSingle(reader["pnl"]);
	
	                        dataPoints.Add(new Tuple<string, float>(timestamp, pnlValue));
	                    }
	                }
	            }
	        }
	        UpdateStatus($"✅ Loaded {dataPoints.Count} PnL data points for {accountName}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR fetching PnL data: {ex.Message}");
	    }
	
	    return dataPoints;
	}
	private List<string> GetSchwabAccounts()
	{
	    List<string> accounts = new List<string>();
	
	    try
	    {
	        if (GlobalVariables.csAccounts != null && GlobalVariables.csAccounts.Count > 0)
	        {
	            accounts = GlobalVariables.csAccounts.Keys.ToList();
	        }
	        else
	        {
	            UpdateStatus("⚠️ No Schwab accounts found in csAccounts.");
	        }
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR fetching accounts from csAccounts: {ex.Message}");
	    }
	
	    return accounts;
	}
	public void RenderPnLChart(List<string> xLabels, List<float> yValues)
	{
	    try
	    {
	        if (xLabels == null || yValues == null || xLabels.Count == 0 || yValues.Count == 0)
	        {
	            UpdateStatus("⚠️ No data available for rendering.");
	            return;
	        }
	
	        // ✅ Ensure pnlChart is initialized before rendering
	        if (pnlChart == null)
	        {
	            UpdateStatus("⚠️ pnlChart is null. Creating a new instance...");
	            pnlChart = new Image();
	        }
	
	        Application.Current.Dispatcher.Invoke(() =>
	        {
	            try
	            {
	                int width = 600, height = 300;
	                DrawingVisual dv = new DrawingVisual();
	
	                using (DrawingContext dc = dv.RenderOpen())
	                {
	                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
	
	                    if (yValues.Count < 2)
	                    {
	                        UpdateStatus("⚠️ Not enough data points to draw a chart.");
	                        return;
	                    }
	
	                    float minY = yValues.Min();
	                    float maxY = yValues.Max();
	                    float scaleY = (maxY - minY) != 0 ? height / (maxY - minY) : height;
	                    float xSpacing = (float)width / (yValues.Count - 1);
	
	                    Pen chartPen = new Pen(Brushes.Green, 2);
	
	                    for (int i = 1; i < yValues.Count; i++)
	                    {
	                        double x1 = (i - 1) * xSpacing;
	                        double y1 = height - ((yValues[i - 1] - minY) * scaleY);
	                        double x2 = i * xSpacing;
	                        double y2 = height - ((yValues[i] - minY) * scaleY);
	
	                        dc.DrawLine(chartPen, new System.Windows.Point(x1, y1), new System.Windows.Point(x2, y2));
	                    }
	                }
	
	                RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
	                rtb.Render(dv);
	
	                pnlChart.Source = rtb;
	                pnlChart.Stretch = Stretch.Uniform;
	                pnlChart.InvalidateVisual();
	
	                UpdateStatus("✅ PnL Chart Rendered Successfully");
	            }
	            catch (Exception ex)
	            {
	                UpdateStatus($"❌ ERROR in RenderPnLChart (inside dispatcher): {ex.Message}");
	            }
	        });
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in RenderPnLChart: {ex.Message}");
	    }
	}
	   // Starts a timer that periodically refreshes the chart.
	 // Starts a timer that periodically refreshes the chart.
    public void StartPnLTracking()
    {
        if (pnlTimer != null)
        {
            UpdateStatus("⚠️ PnL tracking is already running. Skipping duplicate start.");
            return;
        }

        pnlTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        pnlTimer.Tick += async (s, e) => await RunPnLTrackingOnce();

        pnlTimer.Start();
        UpdateStatus("📈 PnL tracking timer started. Refreshing every 10 minutes.");

        // ✅ Force the first execution immediately
        _ = Task.Run(async () => await RunPnLTrackingOnce());
    }


	/// <summary>
	/// Retrieves all positions for the specified account and sums up the current day PnL and long open PnL.
	/// </summary>
	/// <param name="accountNumber">The plain account number (for logging/display).</param>
	/// <param name="accountHash">The encrypted account hash used for API calls.</param>
	/// <returns>A tuple with total current day PnL and total long open PnL.</returns>
	private async Task<(decimal totalCurrentDayPnL, decimal totalLongOpenPnL)> CalculateTotalPnLAsync(string accountNumber, string accountHash)
	{
	    try
	    {
	        // Retrieve positions using your existing GetPositionsAsync.
	        var positions = await SchwabApi.Instance.GetPositionsAsync(accountNumber, accountHash);
	        if (positions == null || positions.Count == 0)
	        {
	            UpdateStatus($"❌ No positions found for account {accountNumber}.");
	            return (0m, 0m);
	        }
	
	        // Sum up the current day profit/loss and long open profit/loss (treat null as 0).
	        decimal totalCurrentDayPnL = positions.Sum(p => p.currentDayProfitLoss ?? 0m);
	        decimal totalLongOpenPnL = positions.Sum(p => p.longOpenProfitLoss ?? 0m);
	
	        UpdateStatus($"✅ Calculated PnL for account {accountNumber}: Current Day = {totalCurrentDayPnL}, Long Open = {totalLongOpenPnL}");
	        return (totalCurrentDayPnL, totalLongOpenPnL);
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ Error calculating PnL for account {accountNumber}: {ex.Message}");
	        return (0m, 0m);
	    }
	}

	// New helper method to process and display PnL data for one account.
	// Assumes that GetPositionsAsync returns a List<AccountPosition>.
	// You may need to adjust the summing logic based on which PnL fields you want to combine.
	private async Task ProcessPnLTrackingAsync(string accountName, string accountNumber, string accountHash)
	{
	    try
	    {
	        // Retrieve positions using the plain account number and encrypted account hash.
	        var positions = await SchwabApi.Instance.GetPositionsAsync(accountNumber, accountHash);
	        if (positions == null || positions.Count == 0)
	        {
	            UpdateStatus($"⚠️ No positions available for {accountName}");
	            return;
	        }
	
	        // Sum up profit/loss values from each position.
	        decimal totalUnrealized = 0;
	        decimal totalCurrentDay = 0;
	        foreach (var pos in positions)
	        {
	            // If the API returns null, default to 0.
	            totalUnrealized += pos.longOpenProfitLoss ?? 0;
	            totalCurrentDay += pos.currentDayProfitLoss ?? 0;
	        }
	        double totalPnL = (double)(totalUnrealized + totalCurrentDay);
	
	        UpdateStatus($"📊 {accountName} | Unrealized: {totalUnrealized}, Current Day: {totalCurrentDay}, Total PnL: {totalPnL}");
			StorePnLData(accountName, 0, totalPnL)	;
	        // Optionally, update a chart if you have one.
	        var rawData = GetRecentPnLData(accountName); // Assume this returns List<(string, float)>
	        if (rawData.Count > 0)
	        {
	            Application.Current.Dispatcher.Invoke(() =>
	            {
	                List<string> xLabels = rawData.Select(p => p.Item1).ToList();
	                List<float> yValues = rawData.Select(p => p.Item2).ToList();
	
	                RenderPnLChart(xLabels, yValues);
	                UpdateStatus($"✅ PnL chart updated for {accountName}");
	            });
	        }
	        else
	        {
	            UpdateStatus($"⚠️ No PnL chart data available for {accountName}");
	        }
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ProcessPnLTrackingAsync Error processing PnL for {accountName}: {ex.Message}");
	    }
	}

	// Revised RunPnLTrackingOnce which now calls ProcessPnLTrackingAsync for each linked account.
	private async Task RunPnLTrackingOnce()
	{
	    UpdateStatus($"⏳ Running PnL tracking tick at {DateTime.Now}");
	
	    // If no linked accounts, attempt to log in and fetch them.
	    if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	    {
	        UpdateStatus("⚠️ RunPnLTrackingOnce: No linked accounts found. Attempting to log in and fetch accounts...");
	        NJ2CSLoginTab loginTab = new NJ2CSLoginTab();
	        await loginTab.LoginAndFetchAccountsAsync();
	
	        if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	        {
	            UpdateStatus("⚠️ No accounts retrieved after login. Using Sim101 for tracking.");
	            await RunPnLTrackingSim101();
	            return;
	        }
	    }
	
	    // Loop through all linked accounts.
	    foreach (var kvp in GlobalVariables.csAccounts)
	    {
	        string accountNumber = kvp.Key;   // Plain account number (for logging/display)
	        string accountHash = kvp.Value;     // Encrypted account hash (for API calls)
	
	        await ProcessPnLTrackingAsync($"{accountNumber}", accountNumber, accountHash);
	    }
	}

	private async Task RunPnLTrackingSim101()
	{
	    UpdateStatus($"⏳ Running PnL tracking for Sim101 at {DateTime.Now}");
	
	    if (NJ2CS.selectedAccount == null)
	    {
	        NJ2CS.selectedAccount = NinjaTrader.Cbi.Account.All.FirstOrDefault(a => a.Name == "Sim101");
	        if (NJ2CS.selectedAccount == null)
	        {
	            UpdateStatus("❌ Error: Sim101 account not found.");
	            return;
	        }
	    }
	    ProcessSimPnLTracking("Sim101", NJ2CS.selectedAccount);
	} 
	private void ProcessSimPnLTracking(string accountName, Account account)
	{
	    double unrealizedPnL = account.Get(AccountItem.UnrealizedProfitLoss, Currency.UsDollar);
	    double realizedPnL = account.Get(AccountItem.GrossRealizedProfitLoss, Currency.UsDollar);
	    double totalPnL = unrealizedPnL + realizedPnL;
	
	    UpdateStatus($"📊 {accountName} | Unrealized: {unrealizedPnL}, Realized: {realizedPnL}, Total: {totalPnL}");
	
	    if (IsDuplicatePnL(accountName, totalPnL))
	    {
	        UpdateStatus($"🔄 Skipping duplicate PnL entry for {accountName} with value {totalPnL}");
	        return;
	    }
	
	    StorePnLData(accountName, unrealizedPnL, realizedPnL);
	
	    var rawData = GetRecentPnLData(accountName);
	    if (rawData.Count > 0)
	    {
	        Application.Current.Dispatcher.Invoke(() =>
	        {
	            List<string> xLabels = rawData.Select(p => p.Item1).ToList();
	            List<float> yValues = rawData.Select(p => p.Item2).ToList();
	
	            RenderPnLChart(xLabels, yValues);
	            UpdateStatus($"✅ PnL chart updated for {accountName}");
	        });
	    }
	    else
	    {
	        UpdateStatus($"⚠️ No PnL data available for {accountName}");
	    }
	}
    private bool IsDuplicatePnL(string accountName, double totalPnL)
    {
        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();
            using (var checkCommand = new SQLiteCommand(connection))
            {
                checkCommand.CommandText = @"
                    SELECT COUNT(*) FROM PnL 
                    WHERE accountName = @accountName 
                    AND pnl = @pnl 
                    AND timestamp = @timestamp;";
                checkCommand.Parameters.AddWithValue("@accountName", accountName);
                checkCommand.Parameters.AddWithValue("@pnl", totalPnL);
                checkCommand.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("HHmm"));

                int existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());
                return existingCount > 0;
            }
        }
    }
    private void StopPnLTracking()
    {
        if (pnlTimer != null)
        {
            pnlTimer.Stop();
            pnlTimer = null;
            UpdateStatus("⏹️ PnL tracking stopped.");
        }
    }
	private void not_used()
	{
		/*
	private async Task RunPnLTrackingOnce1()
	{
	    UpdateStatus($"⏳ Running PnL tracking tick at {DateTime.Now}");
	
	    if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	    {
	        UpdateStatus("⚠️ No linked accounts found. Using Sim101 for tracking.");
	        await RunPnLTrackingSim101();
	        return;
	    }
	
	    // Loop through all linked accounts.
	    foreach (var kvp in GlobalVariables.csAccounts)
	    {
	        string accountNumber = kvp.Key;   // Plain account number for logging/display
	        string accountHash = kvp.Value;     // Encrypted account hash used for API calls
	
	        // Instead of calling FetchAccountPnLAsync (which used an invalid fields parameter),
	        // we now call our ProcessPnLTrackingAsync method that retrieves positions via GetPositionsAsync.
	        // (ProcessPnLTrackingAsync is assumed to call CalculateTotalPnLAsync internally.)
	        await ProcessPnLTrackingAsync($"Account {accountNumber}", accountNumber, accountHash);
	    }
	}
	public async void ShowPnLButton_Click2(object sender, RoutedEventArgs e)
	{
	    try
	    {
	        UpdateStatus("📊 PnL Button Clicked");
	
	        if (accountComboBox?.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ Please select an account.");
	            return;
	        }
	
	        string selectedAccount = accountComboBox.SelectedItem.ToString();
	        UpdateStatus($"📊 Fetching PnL for {selectedAccount} (last 24 hours)...");
	
	        // Wait for WebView2 to be fully initialized
	        if (!webViewInitialized)
	        {
	            UpdateStatus("⏳ Waiting for WebView2 initialization...");
	            bool initOk = await InitializeWebViewAsync();
	            if (!initOk)
	            {
	                UpdateStatus("❌ WebView2 initialization failed. Cannot show PnL chart.");
	                return;
	            }
	            webViewInitialized = true;
	        }
	
	        // Now update the chart
	        await ShowPnLChartForAccount(selectedAccount);
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in ShowPnLButton_Click: {ex.Message}");
	    }
	}
	public void ShowPnLButton_Click1(object sender, RoutedEventArgs e)
	{
	    try
	    {
	        UpdateStatus("📊 PnL Tab Clicked.");
	
	        if (accountComboBox.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ Please select an account.");
	            return;
	        }
	
	        string selectedAccount = accountComboBox.SelectedItem.ToString(); // ✅ Use this instead of `accountName`
	        UpdateStatus($"📊 Fetching PnL for {selectedAccount} (last 24 hours)...");
	
	        // ✅ Fetch data
	        List<Tuple<string, float>> rawData = GetPnLData(selectedAccount);
	
	        if (rawData == null || rawData.Count == 0)
	        {
	            UpdateStatus("⚠️ No PnL data available for this account.");
	            return;
	        }
	
	        // ✅ Extract timestamps and Y-values (PnL)
	        List<string> xLabels = rawData.Select(p => p.Item1).ToList();
	        List<float> yValues = rawData.Select(p => p.Item2).ToList();
	
	        // ✅ Convert to JSON
	        string jsonX = JsonConvert.SerializeObject(xLabels);
	        string jsonY = JsonConvert.SerializeObject(yValues);
	
	        if (webView == null)
	        {
	            UpdateStatus("❌ ERROR: WebView2 instance is null.");
	            return;
	        }
	
	        if (webView.CoreWebView2 == null)
	        {
	            UpdateStatus("❌ ERROR: WebView2 is not initialized. Retrying initialization...");
	
	            // ✅ Use `selectedAccount` instead of `accountName`
	            string retryAccountName = selectedAccount; 
	
	            webView.CoreWebView2InitializationCompleted += (s, e) =>
	            {
	                if (e.IsSuccess)
	                {
	                    ShowPnLChartForAccount(retryAccountName); // ✅ Now this works
	                }
	                else
	                {
	                    UpdateStatus($"❌ WebView2 failed to initialize again: {e.InitializationException?.Message}");
	                }
	            };
	
	            return;
	        }
	
	        // ✅ Send data to WebView2 JavaScript to update the chart
	        string script = $"updateChart({jsonX}, {jsonY});";
	        webView.CoreWebView2.ExecuteScriptAsync(script);
	
	        UpdateStatus($"✅ PnL chart updated for {selectedAccount}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ ERROR in ShowPnLButton_Click: {ex.Message}");
	    }
	}
		*/
	}
    #endregion
    #region Status Logging
    public void UpdateStatus(string message)
    {
        try
        {
            NJ2CSLogManager.LogMessage(message);
        }
        catch (Exception ex)
        {
            NJ2CSLogManager.LogMessage($"Error in UpdateStatus: {ex.Message}");
        }
    }
    #endregion
}
