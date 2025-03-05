using SchwabApiCS;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SchwabApiCS
{
    public class ApiAuthorize
    {
        private static SchwabTokens _schwabTokens;

        /// <summary>
        /// Opens the Schwab authorization URL in the system's default browser.
        /// The user must manually copy and enter the authorization code.
        /// </summary>
        /// <param name="tokenDataFileName">Path to the token data file</param>
		public static async Task OpenAsync(string tokenDataFileName)
		{
		    try
		    {
		        // ✅ Initialize SchwabTokens if not already initialized
		        if (SchwabTokens.Instance == null)
		        {
		            SchwabTokens.Initialize(tokenDataFileName);
		        }
		
		        // ✅ Assign initialized instance
		        _schwabTokens = SchwabTokens.Instance;
		
		        // Open the authorization URL in the system's default web browser
		        Process.Start(new ProcessStartInfo
		        {
		            FileName = _schwabTokens.AuthorizeUri.ToString(),
		            UseShellExecute = true
		        });
		
		        // Prompt user to enter authorization code manually
		        string authCode = ShowInputDialog("Enter the authorization code from Schwab:", "Schwab Authorization");
		
		        if (string.IsNullOrEmpty(authCode))
		        {
		            MessageBox.Show("Authorization failed. No code provided.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		            return;
		        }
		
		        // ✅ Exchange auth code for tokens
		        await ExchangeAuthorizationCodeAsync(authCode);
		        MessageBox.Show("Authorization successful! Tokens have been saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		    }
		    catch (Exception ex)
		    {
		        MessageBox.Show($"Error during authorization: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		    }
		}


        /// <summary>
        /// Exchanges the authorization code for access tokens and saves them.
        /// </summary>
        /// <param name="authCode">The authorization code received from Schwab</param>
        private static async Task ExchangeAuthorizationCodeAsync(string authCode)
        {
            try
            {
                using var httpClient = new HttpClient();
                var content = new StringContent($"code={authCode}&redirect_uri={_schwabTokens.tokens.RedirectUri}&grant_type=authorization_code",
                                                Encoding.UTF8, "application/x-www-form-urlencoded");

                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_schwabTokens.tokens.AppKey}:{_schwabTokens.tokens.Secret}")));

                var response = await httpClient.PostAsync($"{SchwabTokens.baseUrl}/token", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to exchange authorization code for access token. Response: {errorResponse}");
                }

                // ✅ Save tokens
                _schwabTokens.SaveTokens(response, nameof(ExchangeAuthorizationCodeAsync));
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in ExchangeAuthorizationCodeAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays an input dialog box to collect the authorization code.
        /// </summary>
        /// <param name="text">Prompt message</param>
        /// <param name="caption">Window title</param>
        /// <returns>Authorization code entered by the user</returns>
        private static string ShowInputDialog(string text, string caption)
        {
            Window prompt = new Window
            {
                Width = 400,
                Height = 200,
                Title = caption,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            Label textLabel = new Label { Content = text, Margin = new Thickness(10) };
            TextBox inputBox = new TextBox { Width = 350, Margin = new Thickness(10) };
            Button confirmation = new Button { Content = "OK", Width = 100, Margin = new Thickness(10) };
            confirmation.Click += (sender, e) => { prompt.DialogResult = true; prompt.Close(); };

            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(textLabel);
            stackPanel.Children.Add(inputBox);
            stackPanel.Children.Add(confirmation);
            prompt.Content = stackPanel;

            return prompt.ShowDialog() == true ? inputBox.Text : string.Empty;
        }
    }
}
