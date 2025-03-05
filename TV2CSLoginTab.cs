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
    public class TV2CSLoginTab : NTTabPage
    {
        private TextBox usernameTextBox;
        private PasswordBox passwordBox;
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
		public SchwabApi schwabApi ;
        private SchwabTokens schwabTokens;
        public Dictionary<string, string> csAccounts = new(); 
        private string tokenDataFilePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            @"NinjaTrader 8\bin\Custom\AddOns\schwab_tokens.json"
        );
            public TV2CSLoginTab()
            {
                CreateUI();
                schwabTokens = new SchwabTokens(tokenDataFilePath);
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
                    if (string.IsNullOrWhiteSpace(appKeyTextBox.Text) || string.IsNullOrWhiteSpace(callbackUrlTextBox.Text))
                    {
            
            
                        AppendStatusMessage("App Key and Callback URL are required!", Brushes.Red);
                        
                        return;
                    }
            
                    schwabTokens.tokens.AppKey = appKeyTextBox.Text.Trim();
                    schwabTokens.tokens.RedirectUri = callbackUrlTextBox.Text.Trim();
            
                    schwabTokens.SaveTokens();
            
                    AppendStatusMessage("Getting refresh token...", Brushes.Blue);
            
            
                    ApiAuthorize.OpenAsync(tokenDataFilePath);
            
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
            
                    // Initialize Schwab API instance
                    var schwabApi = new SchwabApi(schwabTokens);
            
                    // Fetch account numbers and hash values
                    var accountNumbers = await schwabApi.GetAccountNumbersAsync();
                    if (accountNumbers == null || !accountNumbers.Any())
                    {
                        AppendStatusMessage("No accounts retrieved.", Brushes.Red);
                        return;
                    }
            
                    // Clear previous dictionary and UI list
                    this.csAccounts.Clear();
                    accountsListBox.Items.Clear();
            
                    // Fetch account balances and store data
                    foreach (var account in accountNumbers)
                    {
                        try
                        {
                            var accountNumber = account.accountNumber;
                            var accountHash = account.hashValue;
            
                            // Store account number & hash in dictionary
                            this.csAccounts[accountNumber] = accountHash;
            
                            // Fetch balance
                            var accountInfo = await schwabApi.GetAccountAsync(accountNumber, accountHash);
                            decimal balance = accountInfo?.securitiesAccount?.aggregatedBalance?.liquidationValue
                                            ?? accountInfo?.securitiesAccount?.currentBalances?.cashBalance
                                            ?? accountInfo?.securitiesAccount?.initialBalances?.cashBalance
                                            ?? 0;
            
                            // Display in ListBox
                            string displayText = $"Account: {accountNumber}, Balance: ${balance:N2}";
                            accountsListBox.Items.Add(displayText);
                        }
                        catch (Exception ex)
                        {
                            AppendStatusMessage($"Error retrieving balance for an account: {ex.Message}", Brushes.Red);
                        }
                    }
            
                    AppendStatusMessage($"{accountNumbers.Count} accounts retrieved and updated.", Brushes.Green);
                }
                catch (Exception ex)
                {
                    AppendStatusMessage($"Error: {ex.Message}", Brushes.Red);
                }
            }
            private async void SaveButton_Click(object sender, RoutedEventArgs e)
            {
                await SaveTokensAsync();
            }
            private async Task FetchAndDisplayAccountBalancesAsync()
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
            
                    // Display account balances
                    if (accountBalances != null && accountBalances.Any())
                    {
                        foreach (var balanceInfo in accountBalances)
                        {
                            AppendStatusMessage(balanceInfo, Brushes.Green);
                        }
                    }
                    else
                    {
                        AppendStatusMessage("No account balances found or response was null.", Brushes.Red);
                    }
                }
                catch (Exception ex)
                {
                    AppendStatusMessage($"TV2CS Error retrieving account balances: {ex.Message}", Brushes.Red);
                }
            }
            private async Task<Dictionary<string, string>> GetAccountsFromSchwabAsync()
            {
                try
                {
                    var schwabApi = new SchwabApi(schwabTokens);
                    if (schwabApi == null)
                    {
                        AppendStatusMessage("schwabApi is null....", Brushes.Red);
                        NinjaTrader.Code.Output.Process($"schwabApi is null...", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                        return new Dictionary<string, string>();
                    }
            
                    var accountNumbers = await schwabApi.GetAccountNumbersAsync();
                    if (accountNumbers == null || !accountNumbers.Any())
                    {
                        AppendStatusMessage("No accounts found.", Brushes.Red);
                        return new Dictionary<string, string>();
                    }
            
                    // Return a dictionary where the key is the account number and the value is its corresponding hash
                    return accountNumbers.ToDictionary(a => a.accountNumber, a => a.hashValue);
                }
                catch (Exception ex)
                {
                    AppendStatusMessage($"Error fetching accounts: {ex.Message}", Brushes.Red);
                    return new Dictionary<string, string>();
                }
            }
			private async void LoginButton_Click(object sender, RoutedEventArgs e)
			{
			    try
			    {
			        // Load token details from UI
			        schwabTokens.tokens.Username = usernameTextBox.Text.Trim();
			        schwabTokens.tokens.Password = passwordBox.Password.Trim();
			        schwabTokens.tokens.AppKey = appKeyTextBox.Text.Trim();
			        schwabTokens.tokens.Secret = secretTextBox.Text.Trim();
			        schwabTokens.tokens.Redirect_uri = callbackUrlTextBox.Text.Trim();
			        schwabTokens.tokens.RefreshToken = refreshTokenTextBox.Text.Trim();
			
			        LogRequestData();
			
			        if (string.IsNullOrEmpty(schwabTokens.tokens.AppKey) ||
			            string.IsNullOrEmpty(schwabTokens.tokens.Secret) ||
			            string.IsNullOrEmpty(schwabTokens.tokens.Redirect_uri) ||
			            string.IsNullOrEmpty(schwabTokens.tokens.RefreshToken))
			        {
			            AppendStatusMessage("❌ Error: App Key, Secret, Callback URL, and Refresh Token are required.", Brushes.Red);
			            return;
			        }
			
			        AppendStatusMessage("🔄 Authenticating...", Brushes.Blue);
			
			        // Get Access Token
			        string accessToken = await GetAccessTokenAsync();
			
			        if (!string.IsNullOrEmpty(accessToken))
			        {
			            // ✅ Store Access Token in Base Class (`NJ2CS`)
			            schwabTokens.tokens.AccessToken = accessToken;
			            schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30);
			            schwabTokens.SaveTokens();
			
			            AppendStatusMessage("✅ Login successful!", Brushes.Green);
			            refreshButton.IsEnabled = true;
			
			            // ✅ Store Schwab API in `NJ2CS`
			            NJ2CS.schwabTokens = schwabTokens;
			            NJ2CS.schwabApi = new SchwabApi(schwabTokens);
			        //    NJ2CS.csAccounts = new Dictionary<string, string>();
			
			            // ✅ Fetch Account Data
			            await FetchAndStoreAccountNumbers();
			            await FetchAndDisplayAccountBalancesAsync();
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


            /// <summary>
            /// Fetch account numbers and store them in csAccounts.
            /// </summary>
            private async Task FetchAndStoreAccountNumbers()
            {
                try
                {
                    AppendStatusMessage("Fetching account numbers...", Brushes.Blue);
            
                    if (schwabApi == null || schwabTokens == null || string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
                    {
                        AppendStatusMessage("Error: No valid API instance or access token found.", Brushes.Red);
                        return;
                    }
            
                    var accountNumbers = await schwabApi.GetAccountNumbersAsync();
                    if (accountNumbers == null || !accountNumbers.Any())
                    {
                        AppendStatusMessage("No accounts retrieved.", Brushes.Red);
                        return;
                    }
            
                    // Store accounts globally
                    csAccounts.Clear();
                    foreach (var account in accountNumbers)
                    {
                        csAccounts[account.accountNumber] = account.hashValue;
                    }
            
                    AppendStatusMessage($"Successfully stored {csAccounts.Count} accounts.", Brushes.Green);
                }
                catch (Exception ex)
                {
                    AppendStatusMessage($"Error fetching accounts: {ex.Message}", Brushes.Red);
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
            protected override string GetHeaderPart(string name)
            {
                return "Auth";
            }
            protected override void Save(System.Xml.Linq.XElement element) { }
            protected override void Restore(System.Xml.Linq.XElement element) { }
            public override void Cleanup() { }
    }
}