using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Collections.Generic;
using NinjaTrader.NinjaScript.AddOns;
namespace SchwabApiCS
{
    public class SchwabTokens
    {
        public const string baseUrl = "https://api.schwabapi.com/v1/oauth";
        public SchwabTokensData tokens;
        private string tokenDataFileName;
		private static SchwabTokens? _instance;
		private static readonly object _lock = new object(); 
	    public bool IsTokenValid()
	    {
	        return DateTime.UtcNow < tokens.AccessTokenExpires;
	    }
	    public static SchwabTokens Instance
	    {
	        get
	        {
	            if (_instance == null)
	            {
	                _instance = GlobalVariables.SchwabTokensInstance ?? throw new Exception("❌ SchwabTokens is not initialized. Call Initialize() first.");
	            }
	            return _instance;
	        }
	    }
        /// ✅ **Ensures `SchwabTokens` is initialized once**
	    public static void Initialize(string tokenDataFileName)
	    {
	        lock (_lock) // ✅ Ensure thread safety
	        {
	            if (_instance == null)
	            {
	                _instance = new SchwabTokens(tokenDataFileName);
	                GlobalVariables.SchwabTokensInstance = _instance;
	                NJ2CSLogManager.LogMessage("✅ SchwabTokens successfully initialized.");
	            }
	        }
	    }	
        /// ✅ **Private constructor to enforce `Initialize()`**
	/*    private SchwabTokens(string tokenDataFileName)
	    {
	        this.tokenDataFileName = tokenDataFileName;
	
	        if (File.Exists(tokenDataFileName))
	        {
	            try
	            {
	                string jsonTokens = File.ReadAllText(tokenDataFileName);
	                tokens = JsonConvert.DeserializeObject<SchwabTokensData>(jsonTokens) ?? new SchwabTokensData();
	            }
	            catch (Exception ex)
	            {
	                NJ2CSLogManager.LogMessage($"⚠️ Error reading tokens file: {ex.Message}");
	                tokens = new SchwabTokensData();
	            }
	        }
	        else
	        {
	            tokens = new SchwabTokensData();
	            SaveTokens(); // ✅ Save default empty tokens file
	        }
	
	        ValidateTokenFields();
	    } */
	   	public SchwabTokens(string tokenDataFileName)
	    {
	        this.tokenDataFileName = tokenDataFileName;
	
	        if (File.Exists(tokenDataFileName))
	        {
	            try
	            {
	                string jsonTokens = File.ReadAllText(tokenDataFileName);
	                tokens = JsonConvert.DeserializeObject<SchwabTokensData>(jsonTokens) ?? new SchwabTokensData();
	            }
	            catch (Exception ex)
	            {
	                NJ2CSLogManager.LogMessage($"⚠️ Error reading tokens file: {ex.Message}");
	                tokens = new SchwabTokensData();
	            }
	        }
	        else
	        {
	            tokens = new SchwabTokensData();
	            SaveTokens(); // ✅ Save default empty tokens file
	        }
	
	        ValidateTokenFields();
	    }
		public void LoadTokens()
		{
		    if (File.Exists(tokenDataFileName))
		    {
		        try
		        {
		            string jsonTokens = File.ReadAllText(tokenDataFileName);
		            tokens = JsonConvert.DeserializeObject<SchwabTokensData>(jsonTokens) ?? new SchwabTokensData();
		        }
		        catch (Exception ex)
		        {
		            NJ2CSLogManager.LogMessage($"⚠️ Error loading tokens: {ex.Message}");
		            tokens = new SchwabTokensData();
		        }
		    }
		    else
		    {
		        tokens = new SchwabTokensData();
		        SaveTokens();  // Save an empty default tokens file
		    }
		
		    ValidateTokenFields();
		}			
        private void ValidateTokenFields()
        {
            if (string.IsNullOrEmpty(tokens.AppKey))
                throw new SchwabApiException("Schwab AppKey is not defined");

            if (string.IsNullOrEmpty(tokens.Secret))
                throw new SchwabApiException("Schwab Secret is not defined");

            if (string.IsNullOrEmpty(tokens.RedirectUri))
                throw new SchwabApiException("Schwab Redirect URI is not defined");
        }
        public bool NeedsReAuthorization => DateTime.Now >= tokens.RefreshTokenExpires;
        public Uri AuthorizeUri => new Uri($"{baseUrl}/authorize?client_id={tokens.AppKey}&redirect_uri={tokens.RedirectUri}");
        public async Task<string> GetAccessTokenAsync()
        {
			// token gets refreshed only when needed
            if (DateTime.Now < tokens.AccessTokenExpires)
                return tokens.AccessToken;

            if (DateTime.Now >= tokens.RefreshTokenExpires)
                throw new SchwabApiException("GetAccessToken: Reauthorization required");

            return await RequestAccessTokenAsync();
        }
		public async Task<bool> RefreshAccessTokenAsync()
		{
		    try
		    {
		        if (string.IsNullOrEmpty(tokens.RefreshToken))
		        {
		            NJ2CSLogManager.LogMessage("❌ Refresh token is missing. Re-authentication required.");
		            return false;
		        }
		
		        var tokenRequestUrl = $"{baseUrl}/token";
		
		        // Create the content for the POST request.
		        var requestData = new FormUrlEncodedContent(new[]
		        {
		            new KeyValuePair<string, string>("grant_type", "refresh_token"),
		            new KeyValuePair<string, string>("refresh_token", tokens.RefreshToken),
		            new KeyValuePair<string, string>("redirect_uri", tokens.RedirectUri)
		        });
		
		        var credentials = $"{tokens.AppKey}:{tokens.Secret}";
		        var encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
		
		        using var httpClient = new HttpClient();
		        // Set the Authorization header
		        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedAuth);
		        // Do NOT add Content-Type header manually; FormUrlEncodedContent sets it.
		
		        var response = await httpClient.PostAsync(tokenRequestUrl, requestData);
		
		        if (!response.IsSuccessStatusCode)
		        {
		            var errorResponse = await response.Content.ReadAsStringAsync();
		            NJ2CSLogManager.LogMessage($"❌ Failed to refresh access token. Status: {response.StatusCode}, Details: {errorResponse}");
		            return false;
		        }
		
		        var responseJson = await response.Content.ReadAsStringAsync();
		        NJ2CSLogManager.LogMessage($"Response received: {responseJson}");
		
		        var tokenResult = JsonConvert.DeserializeObject<SchwabTokens.TokenResult>(responseJson);
		
		        if (tokenResult != null && !string.IsNullOrEmpty(tokenResult.access_token))
		        {
		            // Update the tokens in the local instance.
		            tokens.AccessToken = tokenResult.access_token;
		            tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30); // Set expiration 30 minutes from now
		
		            if (!string.IsNullOrEmpty(tokenResult.refresh_token))
		            {
		                tokens.RefreshToken = tokenResult.refresh_token;
		                tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
		                NJ2CSLogManager.LogMessage("New refresh token saved.");
		            }
		
		            // Save the updated tokens to file.
		            SaveTokens();
		
		            // Update the global tokens instance as well.
		            if (GlobalVariables.SchwabTokensInstance != null)
		            {
		                GlobalVariables.SchwabTokensInstance.tokens.AccessToken = tokens.AccessToken;
		                GlobalVariables.SchwabTokensInstance.tokens.AccessTokenExpires = tokens.AccessTokenExpires;
		                GlobalVariables.SchwabTokensInstance.tokens.RefreshToken = tokens.RefreshToken;
		                GlobalVariables.SchwabTokensInstance.tokens.RefreshTokenExpires = tokens.RefreshTokenExpires;
		                GlobalVariables.SchwabTokensInstance.SaveTokens();
		            }
		
		            NJ2CSLogManager.LogMessage("✅ Access token refreshed successfully.");
		            return true;
		        }
		        else
		        {
		            NJ2CSLogManager.LogMessage("❌ No access token found in response.");
		            return false;
		        }
		    }
		    catch (Exception ex)
		    {
		        NJ2CSLogManager.LogMessage($"❌ Error in RefreshAccessTokenAsync: {ex.Message}");
		        return false;
		    }
		}
        public async Task<string> RequestAccessTokenAsync()
        {
            ValidateTokenFields();

            try
            {
                var tokenRequestUrl = $"{baseUrl}/token";

                // Ensure proper refresh token format
                var cleanRefreshToken = tokens.RefreshToken.Split('&')[0];

                var requestData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", cleanRefreshToken),
                    new KeyValuePair<string, string>("redirect_uri", tokens.RedirectUri)
                });

                var credentials = $"{tokens.AppKey}:{tokens.Secret}";
                var encodedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {encodedAuth}");
                httpClient.DefaultRequestHeaders.Add("Content-Type", "application/x-www-form-urlencoded");

                var response = await httpClient.PostAsync(tokenRequestUrl, requestData);

                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new SchwabApiException($"Failed to obtain access token. Status: {response.StatusCode}, Details: {errorResponse}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var tokenResult = (TokenResult)JsonReflection.DeserializeObject(responseJson, typeof(TokenResult));

                if (tokenResult == null || string.IsNullOrEmpty(tokenResult.access_token))
                {
                    throw new SchwabApiException("Access token not found in response.");
                }

                tokens.AccessToken = tokenResult.access_token;
                tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30);  // Set expiration 30 minutes from now

                if (!string.IsNullOrEmpty(tokenResult.refresh_token))
                {
                    tokens.RefreshToken = tokenResult.refresh_token;
                    tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
                }

                SaveTokens();
		        // ✅ Update Global Variables (Store refreshed tokens globally)
		        GlobalVariables.SchwabTokensInstance.tokens.AccessToken = tokenResult.access_token;
		        GlobalVariables.SchwabTokensInstance.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30); // Expiration in 30 mins
		
		        if (!string.IsNullOrEmpty(tokenResult.refresh_token))
		        {
		            GlobalVariables.SchwabTokensInstance.tokens.RefreshToken = tokenResult.refresh_token;
		            GlobalVariables.SchwabTokensInstance.tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
		        }
		
		        // ✅ Save globally updated tokens
		        GlobalVariables.SchwabTokensInstance.SaveTokens();
		
		        NJ2CSLogManager.LogMessage("✅ Access token obtained and saved globally.");
		        return GlobalVariables.SchwabTokensInstance.tokens.AccessToken;
                //return tokens.AccessToken;
            }
            catch (Exception ex)
            {
                throw new SchwabApiException($"Error during token exchange: {ex.Message}");
            }
        }
		public async Task<string> GetValidAccessTokenAsync()
		{
		    if (SchwabTokens.Instance == null)
		    {
		        throw new Exception("❌ SchwabTokens instance is null. Ensure it is initialized using SchwabTokens.Initialize()");
		    }
		
		    if (SchwabTokens.Instance.tokens == null)
		    {
		        throw new Exception("❌ SchwabTokens.tokens is null. Ensure tokens are properly loaded.");
		    }
		
		    if (string.IsNullOrEmpty(SchwabTokens.Instance.tokens.AccessToken))
		    {
		        NJ2CSLogManager.LogMessage("⚠️ Warning: AccessToken is empty. Attempting to refresh...");
		        await SchwabTokens.Instance.RequestAccessTokenAsync();
		    }
		
		    return SchwabTokens.Instance.tokens.AccessToken;
		}
        public void SaveTokens(HttpResponseMessage response, string callingMethod)
        {
            var responseJson = response.Content.ReadAsStringAsync().Result;
            var result = (TokenResult)JsonReflection.DeserializeObject(responseJson, typeof(TokenResult));

            tokens.AccessToken = result.access_token;
            tokens.AccessTokenExpires = DateTime.Now.AddSeconds(result.expires_in - 10);

            if (!string.IsNullOrEmpty(result.refresh_token) && tokens.RefreshToken != result.refresh_token)
            {
                tokens.RefreshToken = result.refresh_token;
                tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
            }

            SaveTokens();  // Save to file
        }
        public void SaveTokens()
        {
            using (StreamWriter sw = new StreamWriter(tokenDataFileName, false))
            {
                var jsonTokens = JsonReflection.SerializeObject(tokens);
                sw.WriteLine(jsonTokens);
            }
        }
        public class TokenResult
        {
            public int expires_in { get; set; }
            public string token_type { get; set; }
            public string refresh_token { get; set; }
            public string access_token { get; set; }
            public string scope { get; set; }
        }
        public class SchwabTokensData
        {
            public string AccessToken { get; set; } = "";
            public string RefreshToken { get; set; } = "";
            public DateTime AccessTokenExpires { get; set; } = DateTime.MinValue;
            public DateTime RefreshTokenExpires { get; set; } = DateTime.MinValue;
            public string AppKey { get; set; } = "";
            public string Secret { get; set; } = "";
            public string RedirectUri { get; set; } = "";
            public string Redirect_uri { get; set; } = "";
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
        }
    }
}
