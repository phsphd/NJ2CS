using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using NinjaTrader.NinjaScript.AddOns;
using SchwabApiCS;
using AccountInfo = SchwabApiCS.SchwabApi.AccountInfo;
 
public class NJ2CSTradeTab : UserControl
{
    private NJ2CS baseInstance;
    private Grid mainGrid;
    private TextBox accountNumberTextBox;
    private TextBox accountBalanceTextBox;
    private ComboBox tickerComboBox;
    private TextBox quantityTextBox;
    private TextBox limitPriceTextBox;
	private TextBox takeProfitTextBox; // ✅ Declare take profit text box
	private TextBox stopLossTextBox;   // ✅ Declare stop loss text box
    private Label statusLabel;

    public NJ2CSTradeTab(NJ2CS instance)
    {
        baseInstance = instance;
        InitializeTradeTab();
 	    Application.Current.Dispatcher.Invoke(() =>
	    {
	        if (!GlobalVariables.TradeTabs.Contains(this))
	        {
	            GlobalVariables.TradeTabs.Add(this);
	            NJ2CSLogManager.LogMessage($"✅ TradeTab registered. Total active tabs: {GlobalVariables.TradeTabs.Count}");
	        }
	    });
	
	    Loaded += (s, e) =>
	    {
	        NJ2CSLogManager.LogMessage("🔄 Trade Tab Loaded. Updating ticker dropdown...");
	        UpdateTickerDropdown();
	    };
	
	    Unloaded += (s, e) =>
	    {
	        Application.Current.Dispatcher.Invoke(() =>
	        {
	            GlobalVariables.TradeTabs.Remove(this);
	            NJ2CSLogManager.LogMessage($"❌ TradeTab removed. Remaining active tabs: {GlobalVariables.TradeTabs.Count}");
	        });
	    };
    }
	private void InitializeTradeTab()
	{
	    mainGrid = new Grid
	    {
	        Margin = new Thickness(10)
	    };
	
	    // Define Two Equal Columns
	    mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Left Column (Labels)
	    mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Right Column (Inputs)
	
	    // Define Rows (Auto Adjusting Height)
	    for (int i = 0; i < 10; i++)  // 🔹 Adjusted to 10 rows to accommodate all inputs
	    {
	        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
	    }
	
	    // ✅ Account Number
	    AddLabelAndInput("Account #:", ref accountNumberTextBox, isReadOnly: true, row: 0);
	
	    // ✅ Account Balance
	    AddLabelAndInput("Balance:", ref accountBalanceTextBox, isReadOnly: true, row: 1);
	
	    // ✅ Symbol (Ticker)
	    var tickerLabel = CreateLabel("Symbol:");
	    tickerComboBox = new ComboBox
	    {
	        Margin = new Thickness(5),
	        HorizontalAlignment = HorizontalAlignment.Stretch
	    };
	
	    if (GlobalVariables.Tickers?.Count > 0)
	    {
	        foreach (var ticker in GlobalVariables.Tickers)
	        {
	            tickerComboBox.Items.Add(ticker);
	        }
	        tickerComboBox.SelectedIndex = 0;
	    }
	
	    AddToGrid(tickerLabel, tickerComboBox, 2);
	
	    // ✅ Quantity (Default: 1)
	    AddLabelAndInput("Quantity:", ref quantityTextBox, isReadOnly: false, row: 3, defaultValue: "1");
	
	    // ✅ Limit Price
	    AddLabelAndInput("Limit Price:", ref limitPriceTextBox, isReadOnly: false, row: 4);
	
	    // ✅ Take Profit (Default: 2)
	    AddLabelAndInput("Take Profit:", ref takeProfitTextBox, isReadOnly: false, row: 5, defaultValue: "2");
	
	    // ✅ Stop Loss (Default: 2)
	    AddLabelAndInput("Stop Loss:", ref stopLossTextBox, isReadOnly: false, row: 6, defaultValue: "2");
	
	    // Buttons
	    var buyButton = CreateButton("Buy", Brushes.Green);
	    var sellButton = CreateButton("Sell", Brushes.Red);
	    var cancelOrdersButton = CreateButton("Cancel Orders", Brushes.Orange);
	    var ocoButton = CreateButton("Add/Change OCO", Brushes.Blue);
	    var flattenButton = CreateButton("Flatten Account", Brushes.Purple);
	    var refreshButton = CreateButton("Refresh Account", Brushes.Gray);
	
	    // Button Grid Layout
	    var buttonGrid = new Grid();
	    buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
	    buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
	
	    Grid.SetRow(buttonGrid, 7);
	    Grid.SetColumnSpan(buttonGrid, 2);
	    mainGrid.Children.Add(buttonGrid);
	
	    AddButtonPair(buttonGrid, buyButton, sellButton, 0);
	    AddButtonPair(buttonGrid, cancelOrdersButton, ocoButton, 1);
	    AddButtonPair(buttonGrid, flattenButton, refreshButton, 2);
	
	    // Status Label
	    statusLabel = new Label
	    {
	        Content = "Status: Waiting for trade...",
	        Foreground = Brushes.Yellow,
	        Background = Brushes.DarkBlue,
	        Margin = new Thickness(5),
	        HorizontalAlignment = HorizontalAlignment.Left
	    };
	    Grid.SetRow(statusLabel, 9);
	    Grid.SetColumnSpan(statusLabel, 2);
	    mainGrid.Children.Add(statusLabel);
	
	    // Event Handlers
	    buyButton.Click += BuyButton_Click;
	    sellButton.Click += SellButton_Click;
	    cancelOrdersButton.Click += CancelOrdersButton_Click;
	    ocoButton.Click += OcoButton_Click;
	    flattenButton.Click += FlattenButton_Click;
	    refreshButton.Click += RefreshButton_Click;
	
	    Content = mainGrid;
	}
	private void AddLabelAndInput(string labelText, ref TextBox textBox, bool isReadOnly, int row, string defaultValue = "")
	{
	    var label = CreateLabel(labelText);
	    textBox = CreateTextBox(isReadOnly);
	    textBox.Text = defaultValue; // Set the default value
	
	    Grid.SetRow(label, row);
	    Grid.SetColumn(label, 0);
	    mainGrid.Children.Add(label);
	
	    Grid.SetRow(textBox, row);
	    Grid.SetColumn(textBox, 1);
	    mainGrid.Children.Add(textBox);
	}
	private Label CreateLabel(string text)
	{
	    return new Label
	    {
	        Content = text,
	        Background = Brushes.DarkBlue,
	        Foreground = Brushes.White,
	        VerticalAlignment = VerticalAlignment.Center,
	        HorizontalAlignment = HorizontalAlignment.Right,
	        Margin = new Thickness(5),
	        Padding = new Thickness(5)
	    };
	}
	private TextBox CreateTextBox(bool isReadOnly)
	{
	    return new TextBox
	    {
	        Margin = new Thickness(5),
	        HorizontalAlignment = HorizontalAlignment.Stretch,
	        IsReadOnly = isReadOnly,
	        Background = Brushes.White,
	        Foreground = Brushes.Black
	    };
	}
	private void AddLabelAndInput(string labelText, ref TextBox textBox, bool isReadOnly, int row)
	{
	    var label = CreateLabel(labelText);
	    textBox = CreateTextBox(isReadOnly);
	
	    Grid.SetRow(label, row);
	    Grid.SetColumn(label, 0);
	    mainGrid.Children.Add(label);
	
	    Grid.SetRow(textBox, row);
	    Grid.SetColumn(textBox, 1);
	    mainGrid.Children.Add(textBox);
	}
	private void AddToGrid(Label label, UIElement inputElement, int row)
	{
	    Grid.SetRow(label, row);
	    Grid.SetColumn(label, 0);
	    mainGrid.Children.Add(label);
	
	    Grid.SetRow(inputElement, row);
	    Grid.SetColumn(inputElement, 1);
	    mainGrid.Children.Add(inputElement);
	}	
	private void AddButtonPair(Grid buttonGrid, Button leftButton, Button rightButton, int row)
	{
	    buttonGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
	    
	    Grid.SetRow(leftButton, row);
	    Grid.SetColumn(leftButton, 0);
	    Grid.SetRow(rightButton, row);
	    Grid.SetColumn(rightButton, 1);
	
	    buttonGrid.Children.Add(leftButton);
	    buttonGrid.Children.Add(rightButton);
	}
	private Button CreateButton(string text, Brush color)
	{
	    return new Button
	    {
	        Content = text,
	        Margin = new Thickness(5),
	        Background = color,
	        Foreground = Brushes.White,
	        HorizontalAlignment = HorizontalAlignment.Stretch,
	        MinHeight = 30
	    };
	}
	private async void BuyButton_Click(object sender, RoutedEventArgs e)
	{
	    try
	    {
	        UpdateStatus("🟡 Buy Button Clicked: Checking inputs...");
	
	        // ✅ Ensure UI Components Exist
	        if (tickerComboBox == null || quantityTextBox == null || limitPriceTextBox == null || accountNumberTextBox == null)
	        {
	            UpdateStatus("❌ UI Elements not initialized! Check tickerComboBox, quantityTextBox, limitPriceTextBox, accountNumberTextBox.");
	            return;
	        }
	
	        // ✅ Get Account Number from Input Box
	        string accountNumber = accountNumberTextBox.Text?.Trim();
	        if (string.IsNullOrEmpty(accountNumber))
	        {
	            UpdateStatus("⚠️ No account number provided in the input box.");
	            return;
	        }
	
	        // ✅ Get the Hash Value Directly from GlobalVariables
	        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string hashValue) || string.IsNullOrEmpty(hashValue))
	        {
	            UpdateStatus($"❌ ERROR: No hashValue found for account {accountNumber}. Ensure it exists in GlobalVariables.csAccounts.");
	            return;
	        }
	
	        // ✅ Validate Symbol Selection
	        if (tickerComboBox.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ No ticker selected in dropdown.");
	            return;
	        }
	        string symbol = tickerComboBox.SelectedItem.ToString();
	
	        // ✅ Parse Quantity
	        if (!int.TryParse(quantityTextBox.Text, out int quantity) || quantity <= 0)
	        {
	            UpdateStatus("⚠️ Invalid quantity! Must be a positive number.");
	            return;
	        }
	
	        // ✅ Parse Limit Price (Check if Empty or Zero)
	        decimal? limitPrice = null;
	        if (!string.IsNullOrEmpty(limitPriceTextBox.Text) && decimal.TryParse(limitPriceTextBox.Text, out decimal parsedPrice))
	        {
	            limitPrice = parsedPrice > 0 ? parsedPrice : null;
	        }
	
	        // ✅ Determine Order Type
	        string orderType = limitPrice.HasValue ? "Limit" : "Market";
	
	        // 🔥 Log Order Details Before Sending API Request
	        UpdateStatus($"📌 Placing BUY order for {quantity} {symbol} at {limitPrice?.ToString("F2") ?? "Market"} ({orderType}) on Account: {accountNumber} (Hash: {hashValue})");
	
	        // ✅ Make API Call
	        try
	        {
	            var response = await SchwabApi.Instance.PlaceOrders(
	                hashValue,  // ✅ Use the correct hash value directly
	                symbol,
	                OrderAction.Buy,
	                quantity,
	                orderType,
	                limitPrice
	            );
	
	            // ✅ Handle Response
	            if (response == null)
	            {
	                UpdateStatus("❌ ERROR: API response is NULL.");
	                return;
	            }
	
	            if (response.HasError)
	            {
	                UpdateStatus($"❌ Order Failed: {response.Message}");
	            }
	            else
	            {
	                UpdateStatus($"✅ Order Placed Successfully: {response.Data}");
	            }
	        }
	        catch (Exception apiEx)
	        {
	            UpdateStatus($"❌ ERROR while calling API: {apiEx.Message}");
	        }
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ CRITICAL ERROR: {ex.Message}");
	    }
	}
	private async void SellButton_Click(object sender, RoutedEventArgs e)
	{
	    try
	    {
	        UpdateStatus("🟡 Sell Button Clicked: Checking inputs...");
	
	        // ✅ Ensure UI Components Exist
	        if (tickerComboBox == null || quantityTextBox == null || limitPriceTextBox == null || accountNumberTextBox == null)
	        {
	            UpdateStatus("❌ UI Elements not initialized! Check tickerComboBox, quantityTextBox, limitPriceTextBox, accountNumberTextBox.");
	            return;
	        }
	
	        // ✅ Ensure API Instance Exists
	        if (SchwabApi.Instance == null)
	        {
	            UpdateStatus("❌ ERROR: Schwab API instance is NULL.");
	            return;
	        }
	
	        // ✅ Get Account Number from Input Box
	        string accountNumber = accountNumberTextBox.Text?.Trim();
	        if (string.IsNullOrEmpty(accountNumber))
	        {
	            UpdateStatus("⚠️ No account number provided in the input box.");
	            return;
	        }
	
	        // ✅ Get the Hash Value (Directly from GlobalVariables)
	        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string hashValue) || string.IsNullOrEmpty(hashValue))
	        {
	            UpdateStatus($"❌ ERROR: No hashValue found for account {accountNumber}. Ensure it exists in GlobalVariables.csAccounts.");
	            return;
	        }
	
	        // ✅ Validate Symbol Selection
	        if (tickerComboBox.SelectedItem == null)
	        {
	            UpdateStatus("⚠️ No ticker selected in dropdown.");
	            return;
	        }
	        string symbol = tickerComboBox.SelectedItem.ToString();
	
	        // ✅ Parse Quantity
	        if (!int.TryParse(quantityTextBox.Text, out int quantity) || quantity <= 0)
	        {
	            UpdateStatus("⚠️ Invalid quantity! Must be a positive number.");
	            return;
	        }
	
	        // ✅ Parse Limit Price (Check if Empty or Zero)
	        decimal? limitPrice = null;
	        if (!string.IsNullOrEmpty(limitPriceTextBox.Text) && decimal.TryParse(limitPriceTextBox.Text, out decimal parsedPrice))
	        {
	            limitPrice = parsedPrice > 0 ? parsedPrice : null;
	        }
	
	        // ✅ Determine Order Type
	        string orderType = limitPrice.HasValue ? "Limit" : "Market";
	
	        // 🔥 Log Order Details Before Sending API Request
	        UpdateStatus($"📌 Placing SELL order for {quantity} {symbol} at {limitPrice?.ToString("F2") ?? "Market"} ({orderType}) on Account: {accountNumber} (Hash: {hashValue})");
	
	        // ✅ Make API Call
	        try
	        {
	            var response = await SchwabApi.Instance.PlaceOrders(
	                hashValue,  // 🔥 Use the correct hash value from GlobalVariables
	                symbol,
	                OrderAction.Sell,
	                quantity,
	                orderType,
	                limitPrice
	            );
	
	            // ✅ Handle Response
	            if (response == null)
	            {
	                UpdateStatus("❌ ERROR: API response is NULL.");
	                return;
	            }
	
	            if (response.HasError)
	            {
	                UpdateStatus($"❌ Order Failed: {response.Message}");
	            }
	            else
	            {
	                UpdateStatus($"✅ Order Placed Successfully: {response.Data}");
	            }
	        }
	        catch (Exception apiEx)
	        {
	            UpdateStatus($"❌ ERROR while calling API: {apiEx.Message}");
	        }
	    }
	    catch (NullReferenceException ex)
	    {
	        UpdateStatus($"❌ CRITICAL ERROR (NullReferenceException): {ex.Message}");
	    }
	    catch (FormatException ex)
	    {
	        UpdateStatus($"❌ CRITICAL ERROR (FormatException): {ex.Message}");
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ CRITICAL ERROR (Unhandled Exception): {ex.Message}");
	    }
	}
	public static void UpdateTickerDropdown()
	{
	    Application.Current.Dispatcher.Invoke(() =>
	    {
	        if (GlobalVariables.TradeTabs.Count == 0)
	        {
	            NJ2CSLogManager.LogMessage("⚠️ No active Trade Tabs found. Skipping ticker update.");
	            return;
	        }
	
	        NJ2CSLogManager.LogMessage($"🔍 Found {GlobalVariables.TradeTabs.Count} Trade Tab instances.");
	
	        foreach (var tradeTab in GlobalVariables.TradeTabs)
	        {
	            if (tradeTab.tickerComboBox == null)
	            {
	                NJ2CSLogManager.LogMessage("⚠️ Trade Tab instance has no tickerComboBox.");
	                continue;
	            }
	
	            tradeTab.tickerComboBox.Items.Clear();
	            foreach (var ticker in GlobalVariables.Tickers)
	            {
	                tradeTab.tickerComboBox.Items.Add(ticker);
	            }
	
	            tradeTab.tickerComboBox.Items.Refresh();
	            tradeTab.tickerComboBox.UpdateLayout();
	            tradeTab.InvalidateVisual();
	
	            if (tradeTab.tickerComboBox.Items.Count > 0)
	            {
	                tradeTab.tickerComboBox.SelectedIndex = 0;
	            }
	
	            NJ2CSLogManager.LogMessage($"✅ Updated Tickers: {string.Join(", ", GlobalVariables.Tickers)}");
	        }
	    });
	}
    private async void CancelOrdersButton_Click(object sender, RoutedEventArgs e)
    {
        string accountNumber = accountNumberTextBox.Text;
        if (string.IsNullOrEmpty(accountNumber))
        {
            statusLabel.Content = "⚠️ Error: No account selected.";
            return;
        }
		string accountHash = GetAccountHashFromAccountNumber(accountNumber);
        statusLabel.Content = $"📌 Canceling all orders for {accountNumber}...";
        await SchwabApi.Instance.CancelAllOrdersByHash(accountHash);
        statusLabel.Content = "✅ All orders canceled.";
    }

 private async void OcoButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        UpdateStatus("🟡 OCO Button Clicked: Checking inputs...");

        // Validate UI components
        if (tickerComboBox == null || quantityTextBox == null || limitPriceTextBox == null ||
            takeProfitTextBox == null || stopLossTextBox == null || accountNumberTextBox == null)
        {
            UpdateStatus("❌ UI Elements not fully initialized.");
            return;
        }

        // Retrieve input values.
        string accountNumber = accountNumberTextBox.Text.Trim();
        if (string.IsNullOrEmpty(accountNumber))
        {
            UpdateStatus("❌ No account number provided.");
            return;
        }

        // Retrieve the encrypted account hash from GlobalVariables (which stores plain account -> hash mappings).
        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash) ||
            string.IsNullOrWhiteSpace(accountHash))
        {
            UpdateStatus($"❌ ERROR: Account hash not found for account {accountNumber}.");
            return;
        }

        if (tickerComboBox.SelectedItem == null)
        {
            UpdateStatus("❌ No ticker selected.");
            return;
        }
        string symbol = tickerComboBox.SelectedItem.ToString();

        if (!int.TryParse(quantityTextBox.Text.Trim(), out int quantity) || quantity <= 0)
        {
            UpdateStatus("❌ Invalid quantity.");
            return;
        }

        if (!decimal.TryParse(limitPriceTextBox.Text.Trim(), out decimal limitPrice) || limitPrice <= 0)
        {
            UpdateStatus("❌ Invalid limit price.");
            return;
        }

        if (!decimal.TryParse(takeProfitTextBox.Text.Trim(), out decimal takeProfitPoints) || takeProfitPoints <= 0)
        {
            UpdateStatus("❌ Invalid take profit value.");
            return;
        }

        if (!decimal.TryParse(stopLossTextBox.Text.Trim(), out decimal stopLossPoints) || stopLossPoints <= 0)
        {
            UpdateStatus("❌ Invalid stop loss value.");
            return;
        }

        // Determine if there is an existing position for the symbol.
        var positions = await SchwabApi.Instance.GetPositionsAsync(accountNumber, accountHash);
        var existingPosition = positions?.FirstOrDefault(p =>
            p.Instrument?.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true);

        if (existingPosition != null)
        {
            // Protect the existing position.
            UpdateStatus($"✅ {accountNumber} Existing position found for {symbol}.");
          //  await SchwabApi.Instance.ProtectExistingPositionAsync(accountHash, symbol, existingPosition, takeProfitPoints, stopLossPoints);
			  await SchwabApi.Instance.ProtectExistingPositionAsync(accountNumber, accountHash, symbol, existingPosition, takeProfitPoints, stopLossPoints);
        }
        else
        {
            // No position exists: place an entry order, then attach OCO protection.
            UpdateStatus($"No existing position for {symbol}. Placing entry order...");
           // await SchwabApi.Instance.PlaceEntryOrderWithProtectionAsync(accountHash, symbol, quantity, limitPrice, takeProfitPoints, stopLossPoints);
			await SchwabApi.Instance.PlaceEntryOrderWithProtectionAsync(accountNumber, accountHash, symbol, quantity, limitPrice, takeProfitPoints, stopLossPoints);

        }
    }
    catch (Exception ex)
    {
        UpdateStatus($"❌ Critical error in OcoButton_Click: {ex.Message}");
    }
}
        private string GetAccountHashFromAccountNumber(string accountNumber)
        {
            if (GlobalVariables.csAccounts.TryGetValue(accountNumber, out string hashValue) && !string.IsNullOrWhiteSpace(hashValue))
            {
                return hashValue;
            }
            // If not found, assume accountIdentifier is already the hash.
            return hashValue;
        }
	private async void FlattenButton_Click(object sender, RoutedEventArgs e)
	{
        string accountNumber = accountNumberTextBox.Text;
        if (string.IsNullOrEmpty(accountNumber))
        {
            statusLabel.Content = "⚠️ Error: No account selected.";
            return;
        }
		string accountHash = GetAccountHashFromAccountNumber(accountNumber);
 
	
	    statusLabel.Content = "📌 Flattening account...";
	    // Call the method on SchwabApi.Instance (which uses the unified ApiResponseWrapper type)
	    //await SchwabApi.Instance.CloseAllPositionsAndOCOsAsync(accountNumber, "User requested flattening");
		await SchwabApi.Instance.CloseAllPositionsAndOCOsByHash(accountHash, "User requested flattening");
	    statusLabel.Content = "✅ Account flattened.";
	}
	
	private async void RefreshButton_Click(object sender, RoutedEventArgs e)
	{
	    try
	    {
	        	statusLabel.Content = "📌 Refreshing account data...";
	
	        // ✅ Check if API Key & Secret are set in Global Variables
	        if (string.IsNullOrEmpty(GlobalVariables.SchwabAppKey) || string.IsNullOrEmpty(GlobalVariables.SchwabSecret))
	        {
	            UpdateStatus("⚠️ API Key & Secret not found. Attempting to load from saved tokens...");
				statusLabel.Content = "⚠️ API Key & Secret not found. Attempting to load from saved tokens...";
	
	            if (GlobalVariables.SchwabTokensInstance?.tokens == null)
	            {
	                GlobalVariables.SchwabTokensInstance = new SchwabTokens(GlobalVariables.TokenDataFilePath);
	                GlobalVariables.SchwabTokensInstance.LoadTokens();
	            }
	
	            if (GlobalVariables.SchwabTokensInstance.tokens != null)
	            {
	                GlobalVariables.SchwabAppKey = GlobalVariables.SchwabTokensInstance.tokens.AppKey;
	                GlobalVariables.SchwabSecret = GlobalVariables.SchwabTokensInstance.tokens.Secret;
	                UpdateStatus("✅ API Key & Secret loaded.");
					statusLabel.Content = "✅ API Key & Secret loaded.";
	
	            }
	            else
	            {
	                UpdateStatus("❌ API Key & Secret missing. Please log in first.");
					statusLabel.Content = "❌ API Key & Secret missing. Please log in first.";
	                return;
	            }
	        }
	
	        // ✅ Ensure Schwab API instance exists
	        var schwabApiInstance = GlobalVariables.SchwabApiInstance;
	
	        if (schwabApiInstance == null)
	        {
	            UpdateStatus("⚠️RefreshButton_Click  No Schwab API instance found. Attempting to log in...");
				statusLabel.Content = "⚠️ No Schwab API instance found. Attempting to log in...";
	            var loginTab = new NJ2CSLoginTab();
	            await loginTab.LoginAndFetchAccountsAsync();
	
	            schwabApiInstance = GlobalVariables.SchwabApiInstance;
	            if (schwabApiInstance == null)
	            {
	                UpdateStatus("❌ Error: Schwab API instance is still NULL after login.");
					statusLabel.Content = "❌ Error: Schwab API instance is still NULL after login.";
	                return;
	            }
				else
				{
					UpdateStatus("Schwab API instance created after login.");
				}
	        }
	
	        // ✅ If no accounts are stored, log in first
	        if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	        {
	            UpdateStatus("⚠️ No accounts found. Attempting to log in and retrieve account numbers...");
				statusLabel.Content = "⚠️ No accounts found. Attempting to log in and retrieve account numbers...";
	            
				var loginTab = new NJ2CSLoginTab();
	            await loginTab.LoginAndFetchAccountsAsync();
	
	            if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	            {
	                UpdateStatus("❌ No accounts retrieved after login. Please check your credentials.");
					statusLabel.Content = "❌ No accounts retrieved after login. Please check your credentials.";
	                return;
	            }
	        }
	
	        // ✅ Refresh Account Data
	        await RefreshAccountDataAsync();
	
	        statusLabel.Content = "✅ Account data refreshed.";
	    }
	    catch (Exception ex)
	    {
	        UpdateStatus($"❌ Error in RefreshButton_Click: {ex.Message}");
	    }
	}
 
	public async Task RefreshAccountDataAsync()
	{
	    try
	    {
	        statusLabel.Content = "📌 Fetching account numbers...";
	        
	        // ✅ Ensure accounts are available
	        if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	        {
	            await SchwabApi.Instance.GetAccountNumbersAsync();
	        }
	
	        if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
	        {
	            statusLabel.Content = "⚠️ No accounts retrieved.";
	            return;
	        }
	
	        // ✅ Get the first available account
	        var firstAccount = GlobalVariables.csAccounts.FirstOrDefault();
	        string accountNumber = firstAccount.Key;
	        string accountHash = firstAccount.Value;
	
	        if (string.IsNullOrEmpty(accountNumber) || string.IsNullOrEmpty(accountHash))
	        {
	            statusLabel.Content = "⚠️ Account data missing.";
	            return;
	        }
	
	        statusLabel.Content = $"📌 Fetching balance for Account: {accountNumber}";
	
	        try
	        {
	            // ✅ Fetch account details using the working API call
	            var accountInfo = await SchwabApi.Instance.GetAccountAsync(accountNumber, accountHash);
	
	            if (accountInfo?.securitiesAccount == null)
	            {
	                statusLabel.Content = "❌ No data retrieved for the account.";
	                return;
	            }
	
	            // ✅ Extract Balance using the correct hierarchy
	            decimal balance = accountInfo.securitiesAccount.aggregatedBalance?.liquidationValue
	                            ?? accountInfo.securitiesAccount.currentBalances?.cashBalance
	                            ?? accountInfo.securitiesAccount.initialBalances?.cashBalance
	                            ?? 0;
	
	            // ✅ Populate the UI fields
	            accountNumberTextBox.Text = accountNumber;
	            accountBalanceTextBox.Text = balance.ToString("C");
	
	            statusLabel.Content = $"✅ Account {accountNumber} updated.";
	        }
	        catch (Exception ex)
	        {
	            statusLabel.Content = $"❌ Error fetching account {accountNumber}: {ex.Message}";
	            return;
	        }
	
	        // ✅ Fetch Quote for Selected Ticker (Limit Price)
	        if (tickerComboBox.SelectedItem == null)
	        {
	            statusLabel.Content = "⚠️ No ticker selected.";
	            return;
	        }
	
	        string selectedSymbol = tickerComboBox.SelectedItem.ToString();
	        statusLabel.Content = $"📌 Fetching quote for {selectedSymbol}...";
	
	        try
	        {
	            var quoteResponse = await SchwabApi.Instance.GetQuoteAsync(selectedSymbol, "quote");
	
	            if (quoteResponse.HasError || quoteResponse.Data == null)
	            {
	                statusLabel.Content = $"❌ Quote fetch failed for {selectedSymbol}.";
	                return;
	            }
	
	            decimal limitPrice = quoteResponse.Data.quote?.mark ?? quoteResponse.Data.quote?.lastPrice ?? 0;
	
	            // ✅ Populate Limit Price Input Box
	            limitPriceTextBox.Text = limitPrice.ToString("F2");
	
	            statusLabel.Content = $"✅ Quote updated for {selectedSymbol} - Limit Price: ${limitPrice:F2}";
	        }
	        catch (Exception ex)
	        {
	            statusLabel.Content = $"❌ Error fetching quote for {selectedSymbol}: {ex.Message}";
	        }
	    }
	    catch (Exception ex)
	    {
	        statusLabel.Content = $"❌ Error: {ex.Message}";
	    }
	}





    private bool ValidateInput(out string symbol, out int quantity, out decimal limitPrice)
    {
        symbol = tickerComboBox.SelectedItem?.ToString();
        limitPrice = 0;
        quantity = 0;

        if (string.IsNullOrWhiteSpace(symbol))
        {
            statusLabel.Content = "⚠️ Error: Please select a ticker.";
            return false;
        }

        if (!int.TryParse(quantityTextBox.Text.Trim(), out quantity) || quantity <= 0)
        {
            statusLabel.Content = "⚠️ Error: Invalid quantity.";
            return false;
        }

        if (!decimal.TryParse(limitPriceTextBox.Text.Trim(), out limitPrice) || limitPrice <= 0)
        {
            statusLabel.Content = "⚠️ Error: Invalid limit price.";
            return false;
        }

        return true;
    }

	private void HandleOrderResponse(ApiResponseWrapper<long?> response)
	{
	    if (response.HasError)
	    {
	        statusLabel.Content = $"❌ Error: {response.Message}";
	        statusLabel.Foreground = Brushes.Red;
	    }
	    else
	    {
	        statusLabel.Content = $"✅ Order placed successfully: {response.Data}";
	        statusLabel.Foreground = Brushes.Green;
	    }
	}
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
}