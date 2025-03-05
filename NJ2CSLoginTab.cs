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
using System.Web;
using System.Net.Http;
namespace NinjaTrader.NinjaScript.AddOns
{
    public class NJ2CSLoginTab : NTTabPage
    {
        private TextBox usernameTextBox;
        private PasswordBox passwordBox;
 
		//private PasswordBox secretTextBox;
        private TextBox secretTextBox;
        private TextBox appKeyTextBox;
        private TextBox callbackUrlTextBox;
        private TextBox refreshTokenTextBox;
        private TextBox statusTextBox;
        private Button getTokenButton;
        private Button loginButton;
        private Button refreshButton;
        private Button saveButton;
        private ListBox accountsListBox;
		private ListBox selectedAccountsListBox;
		public SchwabApi schwabApi ;
        private SchwabTokens schwabTokens;
        public Dictionary<string, string> csAccounts = new(); 
        private string tokenDataFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            @"NinjaTrader 8\bin\Custom\AddOns\schwab_tokens.json"
        );
        public NJ2CSLoginTab()
        {
            CreateUI();
       // ✅ Use singleton instance instead of creating a new one
		    if (GlobalVariables.SchwabTokensInstance == null)
		    {
		        SchwabTokens.Initialize(GlobalVariables.TokenDataFilePath);
		        GlobalVariables.SchwabTokensInstance = SchwabTokens.Instance;
		    }
		
		    schwabTokens = GlobalVariables.SchwabTokensInstance;
            LoadTokens();
            PopulateUIFromTokens();
			
			
        }
        private void CreateUI()
        {
            var grid = new Grid();
            grid.Margin = new Thickness(20);
        
            for (int i = 0; i < 9; i++)  // Increased row count to accommodate the new Secret field
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
        
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        
            // Username
            var usernameLabel = new Label { Content = "Username:", Margin = new Thickness(5) };
            usernameTextBox = new TextBox { Width = 200, Margin = new Thickness(5) };
        
            // Password
            var passwordLabel = new Label { Content = "Password:", Margin = new Thickness(5) };
            passwordBox = new PasswordBox { Width = 200, Margin = new Thickness(5) };
        
            // App Key
            var appKeyLabel = new Label { Content = "App Key:", Margin = new Thickness(5) };
            appKeyTextBox = new TextBox { Width = 200, Margin = new Thickness(5) };
        
            // Secret
            var secretLabel = new Label { Content = "Secret:", Margin = new Thickness(5) };
            secretTextBox = new TextBox { Width = 200, Margin = new Thickness(5) };  // Using PasswordBox for security
        
            // Callback URL
            var callbackUrlLabel = new Label { Content = "Callback URL:", Margin = new Thickness(5) };
            callbackUrlTextBox = new TextBox { Width = 200, Margin = new Thickness(5) };
        
            // Refresh Token
            var refreshTokenLabel = new Label { Content = "Refresh Token:", Margin = new Thickness(5) };
            refreshTokenTextBox = new TextBox { Width = 200, Margin = new Thickness(5), IsEnabled = false };
        
            // Buttons
            getTokenButton = new Button { Content = "Get Refresh Token", Width = 150, Margin = new Thickness(5) };
            getTokenButton.Click += GetTokenButton_Click;
        
            saveButton = new Button { Content = "Save", Width = 100, Margin = new Thickness(5) };
            saveButton.Click += SaveButton_Click;
        
            loginButton = new Button
            {
                Content = "Login",
                Width = 100,
                Margin = new Thickness(5),
                IsEnabled = false,  // Initially disabled
                Background = Brushes.Gray,
                Foreground = Brushes.White
            };
            loginButton.Click += LoginButton_Click;
        
            refreshButton = new Button { Content = "Refresh Accounts", Width = 150, Margin = new Thickness(5), IsEnabled = false };
            refreshButton.Click += RefreshButton_Click;
        
            accountsListBox = new ListBox { Width = 400, Height = 150, Margin = new Thickness(5) };
        	selectedAccountsListBox = new ListBox { Width = 400, Height = 150, Margin = new Thickness(5) };

            // Status TextBox (Editable and Copyable)
        
            statusTextBox = new TextBox
            {
                IsReadOnly = true,
                IsReadOnlyCaretVisible = true,  // Allow text selection
                Background = Brushes.LightYellow,
                BorderThickness = new Thickness(1),
                Foreground = Brushes.DarkBlue,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(5),
                Height = 100
            };
            // Adding elements to grid
            grid.Children.Add(usernameLabel);
            Grid.SetRow(usernameLabel, 0);
            Grid.SetColumn(usernameLabel, 0);
            grid.Children.Add(usernameTextBox);
            Grid.SetRow(usernameTextBox, 0);
            Grid.SetColumn(usernameTextBox, 1);
        
            grid.Children.Add(passwordLabel);
            Grid.SetRow(passwordLabel, 1);
            Grid.SetColumn(passwordLabel, 0);
            grid.Children.Add(passwordBox);
            Grid.SetRow(passwordBox, 1);
            Grid.SetColumn(passwordBox, 1);
        
            grid.Children.Add(appKeyLabel);
            Grid.SetRow(appKeyLabel, 2);
            Grid.SetColumn(appKeyLabel, 0);
            grid.Children.Add(appKeyTextBox);
            Grid.SetRow(appKeyTextBox, 2);
            Grid.SetColumn(appKeyTextBox, 1);
        
            grid.Children.Add(secretLabel);
            Grid.SetRow(secretLabel, 3);
            Grid.SetColumn(secretLabel, 0);
            grid.Children.Add(secretTextBox);
            Grid.SetRow(secretTextBox, 3);
            Grid.SetColumn(secretTextBox, 1);
        
            grid.Children.Add(callbackUrlLabel);
            Grid.SetRow(callbackUrlLabel, 4);
            Grid.SetColumn(callbackUrlLabel, 0);
            grid.Children.Add(callbackUrlTextBox);
            Grid.SetRow(callbackUrlTextBox, 4);
            Grid.SetColumn(callbackUrlTextBox, 1);
        
            grid.Children.Add(refreshTokenLabel);
            Grid.SetRow(refreshTokenLabel, 5);
            Grid.SetColumn(refreshTokenLabel, 0);
            grid.Children.Add(refreshTokenTextBox);
            Grid.SetRow(refreshTokenTextBox, 5);
            Grid.SetColumn(refreshTokenTextBox, 1);
        
            grid.Children.Add(getTokenButton);
            Grid.SetRow(getTokenButton, 6);
            Grid.SetColumn(getTokenButton, 0);
            grid.Children.Add(saveButton);
            Grid.SetRow(saveButton, 6);
            Grid.SetColumn(saveButton, 1);
        
            grid.Children.Add(loginButton);
            Grid.SetRow(loginButton, 7);
            Grid.SetColumn(loginButton, 0);
            grid.Children.Add(refreshButton);
            Grid.SetRow(refreshButton, 7);
            Grid.SetColumn(refreshButton, 1);
        
            grid.Children.Add(accountsListBox);
            Grid.SetRow(accountsListBox, 8);
            Grid.SetColumnSpan(accountsListBox, 2);
			grid.Children.Add(selectedAccountsListBox);
			Grid.SetRow(selectedAccountsListBox, 10);
			Grid.SetColumnSpan(selectedAccountsListBox, 2);       
            grid.Children.Add(statusTextBox);
            Grid.SetRow(statusTextBox, 9);
            Grid.SetColumnSpan(statusTextBox, 2);
        
            Content = grid;
        }
        private void LogRequestData()
        {
            AppendStatusMessage("Sending token request with:", Brushes.Black);
            AppendStatusMessage($"AppKey: {schwabTokens.tokens.AppKey}", Brushes.Black);
            AppendStatusMessage($"Secret: {schwabTokens.tokens.Secret}", Brushes.Black);
            AppendStatusMessage($"RefreshToken: {schwabTokens.tokens.RefreshToken}", Brushes.Black);
            AppendStatusMessage($"RedirectURI: {schwabTokens.tokens.Redirect_uri}", Brushes.Black);
        }
        private void PopulateUIFromTokens()
        {
            if (schwabTokens.tokens != null)
            {
                appKeyTextBox.Text = !string.IsNullOrEmpty(schwabTokens.tokens.AppKey) ? schwabTokens.tokens.AppKey : "";
                callbackUrlTextBox.Text = !string.IsNullOrEmpty(schwabTokens.tokens.Redirect_uri) ? schwabTokens.tokens.Redirect_uri : "";
            }
        }
		private async void GetTokenButton_Click(object sender, RoutedEventArgs e)
		{
		    try
		    {
		        if (string.IsNullOrWhiteSpace(appKeyTextBox.Text) || string.IsNullOrWhiteSpace(secretTextBox.Text))
		        {
		            AppendStatusMessage("❌ App Key and Secret are required!", Brushes.Red);
		            return;
		        }
		
		        // ✅ Store API Credentials
		        schwabTokens.tokens.AppKey = appKeyTextBox.Text.Trim();
		        schwabTokens.tokens.Secret = secretTextBox.Text.Trim();
		        schwabTokens.tokens.Redirect_uri = callbackUrlTextBox.Text.Trim();
		
		        // ✅ Save tokens locally
		        schwabTokens.SaveTokens();
		
		        // ✅ Update Global Variables
		        GlobalVariables.SchwabAppKey = schwabTokens.tokens.AppKey;
		        GlobalVariables.SchwabSecret = schwabTokens.tokens.Secret;
		        GlobalVariables.SchwabRedirectUri = schwabTokens.tokens.Redirect_uri;
		
		        AppendStatusMessage("✅ API credentials saved!", Brushes.Green);
		
		        // ✅ Open Authentication Page
		        string authUrl = $"https://api.schwabapi.com/v1/oauth/authorize?client_id={schwabTokens.tokens.AppKey}&redirect_uri={schwabTokens.tokens.Redirect_uri}";
		        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
		
		        // ✅ Prompt User for Callback URL
		        string returnedUrl = ShowInputDialog("Paste the returned URL:", "Enter Callback URL");
		
		        if (string.IsNullOrWhiteSpace(returnedUrl))
		        {
		            AppendStatusMessage("❌ No URL provided. Aborting...", Brushes.Red);
		            return;
		        }
		
		        // ✅ Extract Authorization Code
		        string authCode = ExtractAuthorizationCode(returnedUrl);
		        if (string.IsNullOrEmpty(authCode))
		        {
		            AppendStatusMessage("❌ Invalid callback URL. Could not extract authorization code.", Brushes.Red);
		            return;
		        }
		
		        AppendStatusMessage("🔄 Exchanging authorization code for refresh token...", Brushes.Blue);
		
		        // ✅ Get Refresh Token
		        await ExchangeAuthorizationCode(authCode);
		
		        // ✅ Reload Tokens
		        LoadTokens();
		
		        // ✅ Update Global Variables with New Tokens
		        GlobalVariables.SchwabTokensInstance = schwabTokens;
		
		        if (!string.IsNullOrEmpty(schwabTokens.tokens.RefreshToken))
		        {
		            GlobalVariables.SchwabRefreshToken = schwabTokens.tokens.RefreshToken;
		            loginButton.IsEnabled = true;
		
		            AppendStatusMessage("✅ Refresh token acquired successfully!", Brushes.Green);
		        }
		        else
		        {
		            AppendStatusMessage("❌ Refresh token not acquired.", Brushes.Red);
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error: {ex.Message}", Brushes.Red);
		    }
		}

        private void AppendStatusMessage(string message, Brush color)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Preserve existing text and add a new message
                statusTextBox.Text += $"{DateTime.Now}: {message}\n";
                
                // Set the foreground color once (initialization or globally)
                statusTextBox.Foreground = color;
                
                // Scroll to the bottom to show the latest messages
                statusTextBox.CaretIndex = statusTextBox.Text.Length;
                statusTextBox.ScrollToEnd();
            });
        }
        private void LoadTokens()
        {
            if (File.Exists(tokenDataFilePath))
            {
                try
                {
                    var jsonTokens = File.ReadAllText(tokenDataFilePath);
                    schwabTokens.tokens = (SchwabTokens.SchwabTokensData)JsonReflection.DeserializeObject(jsonTokens, typeof(SchwabTokens.SchwabTokensData));
        
                    // Ensure the correct redirect URI is used
                    schwabTokens.tokens.Redirect_uri = string.IsNullOrEmpty(schwabTokens.tokens.Redirect_uri)
                        ? schwabTokens.tokens.RedirectUri
                        : schwabTokens.tokens.Redirect_uri;
        
                    // Load values into UI
                    usernameTextBox.Text = schwabTokens.tokens.Username;
                    passwordBox.Password = schwabTokens.tokens.Password;
                    appKeyTextBox.Text = schwabTokens.tokens.AppKey;
                    secretTextBox.Text = schwabTokens.tokens.Secret;  // Load Secret into UI
                    callbackUrlTextBox.Text = schwabTokens.tokens.Redirect_uri;
                    refreshTokenTextBox.Text = schwabTokens.tokens.RefreshToken;
        
                    // Ensure AccessTokenExpires has a valid value
                    if (schwabTokens.tokens.AccessTokenExpires == DateTime.MinValue)
                    {
                        schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(-1);
                    }
        
                    // Handle refresh token expiration check
                    if (schwabTokens.tokens.RefreshTokenExpires > DateTime.Now)
                    {
                        loginButton.IsEnabled = true;
                        loginButton.Background = Brushes.LightBlue;
                        loginButton.Foreground = Brushes.Yellow;
                        AppendStatusMessage("Status: Ready to login.", Brushes.Green);
                    //   statusTextBox.Text= "Status: Ready to login.";
                    //   statusTextBox.Foreground = Brushes.Green;
                    }
                    else
                    {
                        AppendStatusMessage("Status: Refresh token expired.", Brushes.Red);
                    // statusTextBox.Text= "Status: Refresh token expired.";
                    // statusTextBox.Foreground = Brushes.Red;
                    }
        
                    // Save updated token state (for first-time correction)
                    SaveTokensAsync();
                }
                catch (Exception ex)
                {
                    AppendStatusMessage($"Error loading tokens: {ex.Message}", Brushes.Red);
                    InitializeEmptyTokens();
                }
            }
            else
            {
                InitializeEmptyTokens();
            }
        }
        private void InitializeEmptyTokens()
        {
            schwabTokens.tokens = new SchwabTokens.SchwabTokensData
            {
                AppKey = string.Empty,
                Redirect_uri = string.Empty,
                Username = string.Empty,
                Password = string.Empty,
                RefreshToken = string.Empty,
                AccessTokenExpires = DateTime.Now.AddMinutes(-1),  // Expired by default
                RefreshTokenExpires = DateTime.Now.AddDays(-7)     // Expired by default
            };
        
            SaveTokensAsync();  // Save the initialized empty state to JSON file
        }
        public async Task SaveTokensAsync()
        {
            try
            {            
                schwabTokens.tokens.AppKey = appKeyTextBox.Text.Trim();
                schwabTokens.tokens.Secret = secretTextBox.Text.Trim();  // Added Secret field
                schwabTokens.tokens.Redirect_uri = callbackUrlTextBox.Text.Trim();
                schwabTokens.tokens.Username = usernameTextBox.Text.Trim();
                schwabTokens.tokens.Password = passwordBox.Password.Trim();
                schwabTokens.tokens.RefreshToken = refreshTokenTextBox.Text.Trim();
        
                // Ensure expiration dates are updated before saving
                if (string.IsNullOrEmpty(schwabTokens.tokens.AccessToken) || schwabTokens.tokens.AccessTokenExpires <= DateTime.Now)
                {
                    schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30);  // Default 30 min expiration
                }
        
                schwabTokens.tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);  // 7-day expiration for refresh token
        
                if (string.IsNullOrEmpty(schwabTokens.tokens.AppKey) || 
                    string.IsNullOrEmpty(schwabTokens.tokens.Redirect_uri) || 
                    string.IsNullOrEmpty(schwabTokens.tokens.Secret))
                {
                    throw new Exception("App Key, Secret, or Redirect URI is missing.");
                }
        
                // Ensure consistency in redirect URIs
                schwabTokens.tokens.RedirectUri = schwabTokens.tokens.Redirect_uri;
        
                // Serialize and save to file asynchronously to prevent UI freezing
                var jsonTokens = JsonReflection.SerializeObject(schwabTokens.tokens);
                
                await Task.Run(() => File.WriteAllText(tokenDataFilePath, jsonTokens));
        
                // Update UI status safely on UI thread
                Application.Current.Dispatcher.Invoke(() => 
                {
                    AppendStatusMessage("Tokens saved successfully.", Brushes.Green);
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    AppendStatusMessage($"Error saving tokens: {ex.Message}", Brushes.Red);
                });
            }
        }
		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
		    try
		    {
		        AppendStatusMessage("Fetching accounts and balances...", Brushes.Blue);
		
		        if (schwabTokens == null || string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
		        {
		            AppendStatusMessage("Error: No access token found. Please log in first.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Ensure `SchwabApi` instance exists
		        if (schwabApi == null)
		        {
		            schwabApi = new SchwabApi(schwabTokens);
		        }
		
		        // ✅ Refresh Access Token Before API Calls
		        bool tokenRefreshed = await schwabApi.RefreshAccessTokenAsync();
		        if (!tokenRefreshed)
		        {
		            AppendStatusMessage("❌ Failed to refresh access token. Please log in again.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Fetch account numbers
		        var accountNumbers = await schwabApi.GetAccountNumbersAsync();
		
		        if (accountNumbers == null || accountNumbers.Count == 0)
		        {
		            AppendStatusMessage("❌ No accounts retrieved.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Store accounts globally
		        GlobalVariables.csAccounts.Clear();
		        GlobalVariables.csAccounts = accountNumbers.ToDictionary(a => a.accountNumber, a => a.hashValue);
		
		        // ✅ Log accounts
		        AppendStatusMessage($"✅ {GlobalVariables.csAccounts.Count} accounts retrieved.", Brushes.Green);
		
		        // ✅ Fetch and display account balances
		        await FetchAndDisplayAccountBalancesAsync();
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error: {ex.Message}", Brushes.Red);
		    }
		}
		
		
		public async Task LoginAndFetchAccountsAsync()
		{
		    try
		    {
		        AppendStatusMessage("🔄 Logging in and retrieving account numbers...", Brushes.Blue);
		
		        if (schwabTokens == null)
		        {
		            AppendStatusMessage("❌ Error: SchwabTokens instance is NULL.", Brushes.Red);
		            return;
		        }
		
		        // Request an access token.
		        string accessToken = await GetAccessTokenAsync();
		        if (string.IsNullOrEmpty(accessToken))
		        {
		            AppendStatusMessage("❌ Login failed. No access token received.", Brushes.Red);
		            return;
		        }
		
		        // Save the access token (and expiration) in schwabTokens and persist to file.
		        schwabTokens.tokens.AccessToken = accessToken;
		        schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30);
		        schwabTokens.SaveTokens();
		
		        // Update GlobalVariables with API credentials.
		        GlobalVariables.SchwabAppKey = schwabTokens.tokens.AppKey;
		        GlobalVariables.SchwabSecret = schwabTokens.tokens.Secret;
		        GlobalVariables.SchwabTokensInstance = schwabTokens;
		
		        AppendStatusMessage("✅ API Key & Secret updated in Global Variables.", Brushes.Green);
		
		        // Initialize the Schwab API instance and store it in globals.
		        GlobalVariables.SchwabApiInstance = new SchwabApi(schwabTokens);
		        AppendStatusMessage("✅ Login successful! Fetching account numbers...", Brushes.Green);
		
		        // Fetch and store account numbers (using the dedicated endpoint).
		        await FetchAndStoreAccountNumbers();
		
		        if (GlobalVariables.csAccounts.Count > 0)
		        {
		            AppendStatusMessage($"✅ {GlobalVariables.csAccounts.Count} accounts stored in Global Variables.", Brushes.Green);
		            // Optionally, update your UI with balances:
		            await FetchAndDisplayAccountBalancesAsync();
		        }
		        else
		        {
		            AppendStatusMessage("❌ No accounts retrieved. Please check your credentials.", Brushes.Red);
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error in LoginAndFetchAccountsAsync: {ex.Message}", Brushes.Red);
		    }
		}
		
		/// <summary>
		/// Calls the Schwab API to get the list of account number/hash pairs and updates GlobalVariables.csAccounts.
		/// Uses the dedicated endpoint /accounts/accountNumbers.
		/// </summary>
		public async Task FetchAndStoreAccountNumbers()
		{
		    try
		    {
		        AppendStatusMessage("📌 Fetching account numbers...", Brushes.Blue);
		
		        // Ensure the Schwab API instance and a valid access token exist.
		        if (GlobalVariables.SchwabApiInstance == null ||
		            schwabTokens == null ||
		            string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
		        {
		            AppendStatusMessage("❌ Error: No valid API instance or access token found.", Brushes.Red);
		            return;
		        }
		
		        // Call the dedicated endpoint to get account numbers.
				// Get the raw list returned by the API (of type AccountHashPair)
				List<SchwabApiCS.SchwabApi.AccountHashPair> rawAccounts = 
				    await GlobalVariables.SchwabApiInstance.GetAccountNumbersAsync();
				
				// Convert each AccountHashPair into your AccountNumber struct
				List<AccountNumber> accountNumbers = rawAccounts
				    .Select(r => new AccountNumber(r.accountNumber, r.hashValue))
				    .ToList();
		        if (accountNumbers == null || accountNumbers.Count == 0)
		        {
		            AppendStatusMessage("❌ No accounts retrieved from API.", Brushes.Red);
		            return;
		        }
		
		        // Update global dictionary.
		        GlobalVariables.csAccounts.Clear();
		        foreach (var acc in accountNumbers)
		        {
		            // Here, the plain account number is used as the key, and the corresponding hash value is the value.
		            GlobalVariables.csAccounts[acc.accountNumber] = acc.hashValue;
		            AppendStatusMessage($"✅ Found Account: {acc.accountNumber} -> {acc.hashValue}", Brushes.Gray);
		        }
		
		        AppendStatusMessage($"✅ Successfully stored {GlobalVariables.csAccounts.Count} accounts.", Brushes.Green);
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error fetching accounts: {ex.Message}", Brushes.Red);
		    }
		}
		
		/// <summary>
		/// Updates your UI (for example, a ListBox) with account numbers and fetches/display balances.
		/// This method is assumed to be working from your existing code.
		/// </summary>
		private async Task FetchAndDisplayAccountBalancesAsync()
		{
		    AppendStatusMessage("Fetching account balances...", Brushes.Blue);
		
		    try
		    {
		        var schwabApi = new SchwabApi(schwabTokens);
		        var balanceResults = new List<string>();
		
		        // Clear the accounts list UI control.
		        accountsListBox.Dispatcher.Invoke(() =>
		        {
		            accountsListBox.ItemsSource = null;
		            accountsListBox.Items.Clear();
		        });
		
		        foreach (var kvp in GlobalVariables.csAccounts)
		        {
		            string accountNumber = kvp.Key;
		            string accountHash = kvp.Value;
		
		            try
		            {
		                AppendStatusMessage($"🔍 Fetching balance for Account: {accountNumber} (Hash: {accountHash})", Brushes.Gray);
		
		                var accountInfo = await schwabApi.GetAccountAsync(accountNumber, accountHash);
		                decimal balance = accountInfo?.securitiesAccount?.aggregatedBalance?.liquidationValue
		                                ?? accountInfo?.securitiesAccount?.currentBalances?.cashBalance
		                                ?? accountInfo?.securitiesAccount?.initialBalances?.cashBalance
		                                ?? 0;
		
		                string displayText = $"✅ Account: {accountNumber}, Balance: ${balance:N2}";
		                balanceResults.Add(displayText);
		            }
		            catch (Exception ex)
		            {
		                string errorText = $"❌ Error retrieving balance for {accountNumber} (Hash: {accountHash}): {ex.Message}";
		                balanceResults.Add(errorText);
		                AppendStatusMessage(errorText, Brushes.Red);
		            }
		        }
		
		        // Update the UI ListBox.
		        accountsListBox.Dispatcher.Invoke(() =>
		        {
		            accountsListBox.ItemsSource = balanceResults;
		        });
		
		        AppendStatusMessage("✅ Account balances updated successfully.", Brushes.Green);
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error: {ex.Message}", Brushes.Red);
		    }
		}

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveTokensAsync();
        }
 
		public async Task RefreshAccountListAsync()
		{
		    try
		    {
		        // ✅ Ensure Schwab API Instance Exists
		        if (schwabApi == null)
		        {
		            AppendStatusMessage("❌ Error: Schwab API instance is NULL.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Check & Load API Key & Secret
		        if (string.IsNullOrEmpty(GlobalVariables.SchwabAppKey) || string.IsNullOrEmpty(GlobalVariables.SchwabSecret))
		        {
		            if (GlobalVariables.SchwabTokensInstance?.tokens == null)
		            {
		                // 🔄 Load tokens from JSON file if missing
		                GlobalVariables.SchwabTokensInstance = new SchwabTokens(GlobalVariables.TokenDataFilePath);
		                GlobalVariables.SchwabTokensInstance.LoadTokens();
		                AppendStatusMessage("🔄 Loaded API Key & Secret from JSON file.", Brushes.Blue);
		            }
		
		            // ✅ Set Global API Key & Secret if available
		            if (GlobalVariables.SchwabTokensInstance.tokens != null)
		            {
		                GlobalVariables.SchwabAppKey = GlobalVariables.SchwabTokensInstance.tokens.AppKey;
		                GlobalVariables.SchwabSecret = GlobalVariables.SchwabTokensInstance.tokens.Secret;
		                AppendStatusMessage("✅ API Key & Secret updated from stored tokens.", Brushes.Green);
		            }
		            else
		            {
		                AppendStatusMessage("❌ Error: API Key & Secret are missing and could not be loaded. Please log in again.", Brushes.Red);
		                return;
		            }
		        }
		
		        // ✅ Fetch Account Numbers
		        var accountNumbers = await schwabApi.GetAccountNumbersAsync();
		
		        // ✅ Validate API Response
		        if (accountNumbers == null)
		        {
		            AppendStatusMessage("❌ Error: API returned null for account numbers.", Brushes.Red);
		            return;
		        }
		        if (accountNumbers.Count == 0)
		        {
		            AppendStatusMessage("❌ Error: No accounts retrieved from API.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Populate `GlobalVariables.csAccounts`
		        GlobalVariables.csAccounts.Clear();
		        GlobalVariables.csAccounts = accountNumbers.ToDictionary(a => a.accountNumber, a => a.hashValue);
		
		        AppendStatusMessage($"✅ {GlobalVariables.csAccounts.Count} accounts stored.", Brushes.Green);
		
		        // ✅ Display in UI
		        selectedAccountsListBox.Items.Clear();
		        foreach (var account in GlobalVariables.csAccounts)
		        {
		            selectedAccountsListBox.Items.Add($"Account: {account.Key}");
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error in RefreshAccountListAsync: {ex.Message}", Brushes.Red);
		    }
		}

		private async void LoginButton_Click(object sender, RoutedEventArgs e)
		{
		    try
		    {
		        // ✅ Trim & Validate User Inputs
		        schwabTokens.tokens.Username = usernameTextBox.Text.Trim();
		        schwabTokens.tokens.Password = passwordBox.Password.Trim();
		        schwabTokens.tokens.AppKey = appKeyTextBox.Text.Trim();
		        schwabTokens.tokens.Secret = secretTextBox.Text.Trim();
		        schwabTokens.tokens.Redirect_uri = callbackUrlTextBox.Text.Trim();
		        schwabTokens.tokens.RefreshToken = refreshTokenTextBox.Text.Trim();
		
		        // ✅ Validate Required Fields
		        if (string.IsNullOrEmpty(schwabTokens.tokens.AppKey) ||
		            string.IsNullOrEmpty(schwabTokens.tokens.Secret) ||
		            string.IsNullOrEmpty(schwabTokens.tokens.Redirect_uri) ||
		            string.IsNullOrEmpty(schwabTokens.tokens.RefreshToken))
		        {
		            AppendStatusMessage("❌ Error: App Key, Secret, Callback URL, and Refresh Token are required.", Brushes.Red);
		            return;
		        }
		
		        AppendStatusMessage("🔄 Authenticating...", Brushes.Blue);
		
		        // ✅ Request Access Token
		        string accessToken = await GetAccessTokenAsync();
		        if (!string.IsNullOrEmpty(accessToken))
		        {
		            // ✅ Store Access Token & Expiry
		            schwabTokens.tokens.AccessToken = accessToken;
		            schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30);
		            schwabTokens.SaveTokens();
		
		            AppendStatusMessage("✅ Login successful!", Brushes.Green);
		            refreshButton.IsEnabled = true;
		
		            // ✅ Initialize Schwab API
		            schwabApi = new SchwabApi(schwabTokens);
		            if (schwabApi == null)
		            {
		                AppendStatusMessage("❌ Error: SchwabApi instance is NULL after initialization.", Brushes.Red);
		                return;
		            }
		
		            // ✅ Fetch & Store Account Numbers
		            await FetchAndStoreAccountNumbers();
		            AppendStatusMessage($"✅ {csAccounts.Count} accounts stored.", Brushes.Green);
		
		            // ✅ Fetch & Display Account Balances
		            await FetchAndDisplayAccountBalancesAsync().ConfigureAwait(false);
		        }
		        else
		        {
		            AppendStatusMessage("❌ Login failed. No access token received.", Brushes.Red);
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error: {ex.Message}", Brushes.Red);
		    }
		}
 
		private async Task FetchAndStoreAccountNumbers1()
		{
		    try
		    {
		        AppendStatusMessage("📌 Fetching account numbers...", Brushes.Blue);
		
		        // ✅ Ensure `SchwabApi` & `schwabTokens` are initialized
		        if (schwabApi == null || schwabTokens == null || string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
		        {
		            AppendStatusMessage("❌ Error: No valid API instance or access token found.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Get Account Numbers from API
		        var accountNumbers = await schwabApi.GetAccountNumbersAsync();
		        if (accountNumbers == null || !accountNumbers.Any())
		        {
		            AppendStatusMessage("❌ No accounts retrieved.", Brushes.Red);
		            return;
		        }
		
		        // ✅ Store Accounts in Global Variables
		        GlobalVariables.csAccounts.Clear();
		        foreach (var account in accountNumbers)
		        {
		            GlobalVariables.csAccounts[account.accountNumber] = account.hashValue;
		        }
		
		        AppendStatusMessage($"✅ Successfully stored {GlobalVariables.csAccounts.Count} accounts.", Brushes.Green);
		
		        // ✅ Update UI ListBox (selectedAccountsListBox)
		   /*
				if (selectedAccountsListBox != null)
		        {
		            selectedAccountsListBox.Items.Clear();
		            foreach (var account in GlobalVariables.csAccounts)
		            {
		                selectedAccountsListBox.Items.Add($"Account: {account.Key}");
		            }
		        }
		        else
		        {
		            AppendStatusMessage("⚠️ Warning: selectedAccountsListBox is null.", Brushes.Orange);
		        }
				*/
		
		        // ✅ Fetch and Display Balance for the First Account
		        var firstAccount = GlobalVariables.csAccounts.FirstOrDefault();
		        if (!string.IsNullOrEmpty(firstAccount.Key))
		        {
		            AppendStatusMessage($"📌 Selected Account: {firstAccount.Key} (Hash: {firstAccount.Value})", Brushes.Blue);
		
		            await FetchAndDisplayAccountBalancesAsync();
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error fetching accounts: {ex.Message}", Brushes.Red);
		    }
		}
 
		private async Task<string> GetAccessTokenAsync()
		{
		    try
		    {
		        var tokenRequestUrl = $"{SchwabTokens.baseUrl}/token";
		
		        // Clean and format refresh token properly
		        var cleanRefreshToken = schwabTokens.tokens.RefreshToken.Split(',')[0].Trim();
		        AppendStatusMessage($"Cleaned Refresh Token: {cleanRefreshToken}", Brushes.Black);
		
		        var credentials = $"{schwabTokens.tokens.AppKey}:{schwabTokens.tokens.Secret}";
		        var encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
		
		        var requestData = new Dictionary<string, string>
		        {
		            { "grant_type", "refresh_token" },
		            { "refresh_token", cleanRefreshToken },
		            { "redirect_uri", schwabTokens.tokens.Redirect_uri }
		        };
		
		        using var httpClient = new HttpClient();
		        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedAuth);
		        httpClient.DefaultRequestHeaders.Accept.Clear();
		        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
		
		        var content = new FormUrlEncodedContent(requestData);
		        AppendStatusMessage($"Sending request to {tokenRequestUrl}", Brushes.Blue);
		
		        var response = await httpClient.PostAsync(tokenRequestUrl, content);
		
		        if (!response.IsSuccessStatusCode)
		        {
		            var errorResponse = await response.Content.ReadAsStringAsync();
		            AppendStatusMessage($"Failed to obtain access token. Status: {response.StatusCode}, Details: {errorResponse}", Brushes.Red);
		            throw new Exception($"Failed to obtain access token: {response.StatusCode} - {response.ReasonPhrase}");
		        }
		
		        var responseJson = await response.Content.ReadAsStringAsync();
		        AppendStatusMessage($"Response received: {responseJson}", Brushes.Black);
		
		        var tokenResult = (SchwabTokens.TokenResult)JsonReflection.DeserializeObject(responseJson, typeof(SchwabTokens.TokenResult));
		
		        if (tokenResult != null && !string.IsNullOrEmpty(tokenResult.access_token))
		        {
		            // Save the new tokens into the schwabTokens instance
		            schwabTokens.tokens.AccessToken = tokenResult.access_token;
		            schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30);
		
		            if (!string.IsNullOrEmpty(tokenResult.refresh_token))
		            {
		                schwabTokens.tokens.RefreshToken = tokenResult.refresh_token;
		                schwabTokens.tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
		                AppendStatusMessage("New refresh token saved.", Brushes.Green);
		            }
		
		            schwabTokens.SaveTokens();
		            AppendStatusMessage("Access token retrieved and saved.", Brushes.Green);
		
		            // Update the global tokens instance so that it can be used throughout the application.
		            GlobalVariables.SchwabTokensInstance = schwabTokens;
		
		            return schwabTokens.tokens.AccessToken;
		        }
		        else
		        {
		            throw new Exception("Access token not found in response.");
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"Error during token exchange: {ex.Message}", Brushes.Red);
		        throw new Exception("Error during token exchange.", ex);
		    }
		}

		private async Task ExchangeAuthorizationCode(string authCode)
		{
		    try
		    {
		        using var httpClient = new HttpClient();
		        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{schwabTokens.tokens.AppKey}:{schwabTokens.tokens.Secret}"));
		
		        var headers = new Dictionary<string, string>
		        {
		            { "Authorization", $"Basic {credentials}" },
		            { "Content-Type", "application/x-www-form-urlencoded" }
		        };
		
		        var payload = new Dictionary<string, string>
		        {
		            { "grant_type", "authorization_code" },
		            { "code", authCode },
		            { "redirect_uri", schwabTokens.tokens.Redirect_uri }
		        };
		
		        var content = new FormUrlEncodedContent(payload);
		        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
		
		        AppendStatusMessage("🔄 Sending token request...", Brushes.Blue);
		
		        var response = await httpClient.PostAsync("https://api.schwabapi.com/v1/oauth/token", content);
		        var responseString = await response.Content.ReadAsStringAsync();
		
		        if (!response.IsSuccessStatusCode)
		        {
		            AppendStatusMessage($"❌ Token request failed: {response.StatusCode}", Brushes.Red);
		            AppendStatusMessage($"Response: {responseString}", Brushes.Red);
		            return;
		        }
		
		        var tokenData = JsonConvert.DeserializeObject<TokenResponse>(responseString);
		
		        if (tokenData != null)
		        {
		            schwabTokens.tokens.RefreshToken = tokenData.refresh_token;
		            schwabTokens.tokens.RefreshTokenExpires = DateTime.Now.AddSeconds(tokenData.expires_in);
		
		            schwabTokens.SaveTokens();
		            AppendStatusMessage("✅ Refresh token saved successfully.", Brushes.Green);
		        }
		        else
		        {
		            AppendStatusMessage("❌ Failed to parse token response.", Brushes.Red);
		        }
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error exchanging code: {ex.Message}", Brushes.Red);
		    }
		}
        private string ShowInputDialog(string text, string caption)
        {
            Window prompt = new Window()
            {
                Width = 400,
                Height = 200,
                Title = caption,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
    
            Label textLabel = new Label() { Content = text };
            TextBox inputBox = new TextBox() { Width = 350 };
            Button confirmation = new Button() { Content = "OK", Width = 100 };
            confirmation.Click += (sender, e) => { prompt.DialogResult = true; prompt.Close(); };
    
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(textLabel);
            stackPanel.Children.Add(inputBox);
            stackPanel.Children.Add(confirmation);
            prompt.Content = stackPanel;
    
            return prompt.ShowDialog() == true ? inputBox.Text : string.Empty;
        }
		private string ExtractAuthorizationCode(string callbackUrl)
		{
		    try
		    {
		        Uri uri = new Uri(callbackUrl);
		        string query = uri.Query;
		
		        var queryParams = query.TrimStart('?')
		            .Split('&')
		            .Select(param => param.Split('='))
		            .Where(param => param.Length == 2)
		            .ToDictionary(param => param[0], param => Uri.UnescapeDataString(param[1]));
		
		        if (queryParams.TryGetValue("code", out string authCode))
		        {
		            return authCode;
		        }
		
		        AppendStatusMessage("❌ Authorization code not found in the URL.", Brushes.Red);
		        return string.Empty;
		    }
		    catch (Exception ex)
		    {
		        AppendStatusMessage($"❌ Error extracting auth code: {ex.Message}", Brushes.Red);
		        return string.Empty;
		    }
		}
        protected override string GetHeaderPart(string name)
        {
            return "Auth";
        }
        protected override void Save(System.Xml.Linq.XElement element) { }
        protected override void Restore(System.Xml.Linq.XElement element) { }
        public override void Cleanup() { }
		private class TokenResponse
		{
		    public string access_token { get; set; }
		    public string refresh_token { get; set; }
		    public int expires_in { get; set; }
		}
	 /*  protected override void ConfigTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
	    {
	        base.ConfigTabControl_SelectionChanged(sender, e);
	        UpdateStatus("📊 PnL Tab selected in NJ2CSLoginTab.");
	    }*/
		private void not_used()
		{
				/*
			
       private async Task FetchAndDisplayAccountBalancesAsync1()
        {
            AppendStatusMessage("Fetching account balances...", Brushes.Blue);
        
            try
            {
                if (schwabTokens == null || string.IsNullOrEmpty(schwabTokens.tokens?.AccessToken))
                {
                    AppendStatusMessage("Error: No access token found. Please log in first.", Brushes.Red);
                    return;
                }
                else	
                {
                    AppendStatusMessage("schwabTokens is not null....", Brushes.Red);
                    NinjaTrader.Code.Output.Process($"schwabTokens is not null... ", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                }
                // Directly instantiate SchwabApi with the existing token
                var schwabApi = new SchwabApi(schwabTokens);
                if (schwabApi ==null)
                {
                    AppendStatusMessage("schwabApi is null....", Brushes.Red);
                    NinjaTrader.Code.Output.Process($"schwabApi is   null... ", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                }
                AppendStatusMessage($"SchwabApi instance created with token: {schwabTokens.tokens.AccessToken.Substring(0, 5)}...", Brushes.Blue);
        
                // Fetch account balances
                var accountBalances = await schwabApi.GetAccountBalancesAsync();
        
                AppendStatusMessage($"Account balances response: {accountBalances?.Count ?? 0} items", Brushes.Blue);
        
 
				if (accountBalances == null)
				{
				    AppendStatusMessage("❌ ERROR: GetAccountBalancesAsync() returned null.", Brushes.Red);
				}
				else
				{
				    string rawResponse = JsonConvert.SerializeObject(accountBalances, Formatting.Indented);
				    AppendStatusMessage($"📜 Raw API Response:\n{rawResponse}", Brushes.Gray);
				}
				
				AppendStatusMessage($"Account balances response: {accountBalances?.Count ?? 0} items", Brushes.Blue);
				 
            }
            catch (Exception ex)
            {
                AppendStatusMessage($"NJ2CS Error retrieving account balances: {ex.Message}", Brushes.Red);
            }
        }
        private async void GetTokenButton_Click1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(appKeyTextBox.Text) || string.IsNullOrWhiteSpace(callbackUrlTextBox.Text))
                {
        
        
                    AppendStatusMessage("App Key and Callback URL are required!", Brushes.Red);
                    
                    return;
                }
        
                schwabTokens.tokens.AppKey = appKeyTextBox.Text.Trim();
                schwabTokens.tokens.RedirectUri = callbackUrlTextBox.Text.Trim();
        
                schwabTokens.SaveTokens();
        
                AppendStatusMessage("Getting refresh token...", Brushes.Blue);
        
        
                ApiAuthorize.Open(tokenDataFilePath);
        
                LoadTokens();
                if (!string.IsNullOrEmpty(schwabTokens.tokens.RefreshToken))
                {
        
                    loginButton.IsEnabled = true;
                    AppendStatusMessage("Refresh token acquired.", Brushes.Green);
                }
                else
                {
        
                    AppendStatusMessage("Refresh token not acquired.", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                AppendStatusMessage($"Error: {ex.Message}", Brushes.Red);
            }
        }

	        private async Task ExchangeAuthorizationCode1(string authCode)
	        {
	            try
	            {
	                using var httpClient = new HttpClient();
	                var content = new StringContent(
	                    $"grant_type=authorization_code&code={authCode}&redirect_uri={schwabTokens.tokens.Redirect_uri}",
	                    Encoding.UTF8, "application/x-www-form-urlencoded"
	                );
	    
	                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " +
	                    SchwabApi.Base64Encode(schwabTokens.tokens.AppKey + ":" + schwabTokens.tokens.Secret));
	    
	                var response = await httpClient.PostAsync(SchwabTokens.baseUrl + "/token", content);
	    
	                if (!response.IsSuccessStatusCode)
	                    throw new Exception("Failed to exchange authorization code for access token.");
	    
	                schwabTokens.SaveTokens(response, "ExchangeAuthorizationCode");
	            //  statusTextBox.Text= "Status: Tokens saved successfully.";
	                AppendStatusMessage($"Tokens saved successfully", Brushes.Green);
	            // statusTextBox.Foreground = Brushes.Green;
	            }
	            catch (Exception ex)
	            {
	                throw new Exception("Error during token exchange: " + ex.Message);
	            }
	        }
			*/
		}
    }
}