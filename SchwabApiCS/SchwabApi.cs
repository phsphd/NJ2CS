// <copyright file="SchwabApi.cs" company="ZPM Software Inc">
// Copyright � 2024 ZPM Software Inc. All rights reserved.
// This Source Code is subject to the terms MIT Public License
// </copyright>

// Version 7.4.2 - released 2024-12-13 Beta - Price Charts, fixes in OrderExecuteReplace() and OrderStopLoss()
// Version 7.4.1 - released 2024-12-02 Beta - Price Charts
// Version 7.4.0 - released 2024-11-26 Beta - Price Charts
// Version 7.3.1 - released 2024-08-25
// Version 7.3.0 - released 2024-08-11
// Version 7.2.0 - released 2024-08-01
// Version 7.1.0 - released 2024-07-15
// Version 7.0.0 - released 2024-07-09?
// Version 6.0.2 - released 2024-07-05
// Version 6.0.1 - released 2024-07-04
// Version 6.0.0 - released 2024-07-03
// Version 05 - released 2024-06-28
// Version 04 - released 2024-06-20
// Version 03 - released 2024-06-13
 
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using  System.Collections.Generic ;
using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;

using NinjaTrader.NinjaScript.AddOns;
namespace SchwabApiCS
{
    // handy tools:  https://json2csharp.com and https://jsonformatter.org
    // https://kdiff3.sourceforge.net/ - great tool for showing version code changes - compare current release with a new one

  	public partial class SchwabApi
    {
        public const string Version = "7.4.2";

        /* ============= Accounts and Trading Production ===============================================================
         *   Method                     Endpoint                                     Description
         *   ** = not implemented
         * ACCOUNTS                     Accounts.cs
         *   GetAccountNumbers()        GET /accounts/accountNumbers                 Get list of account numbers and their encrypted values
         *   GetAccounts()              GET /accounts                                Get linked account(s) balances and positions for the logged in user.
         *   GetAccount()               GET /accounts/{accountNumbers}               Get a specific account balance and positions for the logged in user.
         * 
         * ORDERS                       GetOrders.cs
         *   GetOrders({account#})      GET    /accounts/{account#}/orders           Get all orders for a specific account.
         *   GetOrder()                 GET    /accounts/{account#}/orders/{orderId} Get a specific order by its ID, for a specific account
         *   GetOrders()                GET    /orders                               Get all orders for all accounts
         *   
         *                              Orders/OrderBase.cs
         *   OrderExecuteNew()          POST   /accounts/{account#}/orders           Place order for a specific account - All new order variations use this
         *   OrderExecuteReplace()      POST   /accounts/{account#}/orders/{orderId} Place an order - All replace order variations use this
         *   OrderExecuteDelete()       DELETE /accounts/{account#}/orders/{orderId} Cancel an order for a specific account- All replace order variations should use this
         *   
         *                              See Orders folder for common order types that can be used as examples for more complex orders.
         *   OrderBuySingle()                                                        Place a simple limit or market buy order.
         *   OrderSellSorder()                                                       Place a simple limit or market sell order.
         *   OrderStopLoss()                                                         Place a simple stop loss sell order.
         *   OrderFirstTriggers()       Good example for a more complex order        Place a simple buy/sell order with a triggered second order
         *   
         *   **                         POST   /accounts/{account#}/previewOrder     Preview order for a specific account. **Coming Soon**.
         *  
         * TRANSACTIONS                 Transactions.cs
         *   GetAccountTransactions()   GET  /accounts/{account#}/transactions                  Get all transactions information for a specific account.
         *   GetAccountTransaction(id)  GET  /accounts/{account#}/transactions/{transactionId}  Get user preference information for the logged in user.
         *  
         * USER PREFERENCES             UserPreference.cs
         *   GetUserPreferences()       GET  /UserPreference                         Get user preference information for the logged in user.
         *  
         *  
         * ========== MARKET DATA  ====================================================================================
         * QUOTES                       MarketData.cs
         *   GetQuotes()                GET /quotes                                  Get Quotes by list of symbols.
         *   GetQuote()                 GET /{symbol_id}/quotes                      Get Quote by single symbol.
         *  
         * OPTION CHAINS                Options.cs
         *   GetOptionChain()           GET /chains                                  Get option chain for an optionable Symbol
         *  
         * OPTION EXPERIRATION CHAIN    Options.cs
         *   GetOptionExpirationChain() GET /expirationchain                         Get option expiration chain for an optionable symbol
         *  
         * PRICE HISTORY                MarketData.cs
         *   GetPriceHistory()          GET /pricehistory                            Get PriceHistory for a single symbol and date ranges.
         *  
         * MOVERS                       MarketData.cs
         *   GetMovers()                GET /movers/{index_symbol}                   Get Movers for a specific index.
         *  
         * MARKET HOURS                 MarketData.cs
         *   GetMarketHours()           GET /markets                                 Get Market Hours for all markets.
         *   ** not needed              GET /markets/{market_id}                     Get Market Hours for a single market.
         *  
         * INSTRUMENTS                  Instruments.cs
         *   GetInstrumentsBySymbol()   GET /instruments                             Get Instruments by symbols and projections.
         *   GetInstrumentsByCusipId()  GET /instruments/{cusip_id}                  Get Instrument by specific cusip
         *   
         * ========== STREAMERS  ====================================================================================  
         * AccountActivities
         * LevelOneEquities
         * LevelOneOptions
         * LevelOneFutures
         * LevelOneFuturesOptions -- Not implemented by Schwab yet 
         * LevelOneForexes
         * NasdaqBooks -- level 2 Nasdaq
         * NyseBooks -- level 2 Nyse
         * OptionsBooks -- level 2 options
         * ChartEquities -- minute candles stream
         * ChartFutures -- minute candles stream
         * ScreenerEquities -- Not implemented by Schwab yet
         * ScreenerOptions  -- Not implemented by Schwab yet
         */

        public UserPreferences? userPreferences; // load once

        /// <summary>
        /// Every 7 days user must sign in and reauthorize.
        /// </summary>
        public bool NeedsReAuthorization { get { return _schwabTokens.NeedsReAuthorization; } }

	  //   private SchwabTokens _schwabTokens;
        private static SchwabTokens _schwabTokens;
 		private SchwabApi() : this(GlobalVariables.SchwabTokensInstance) {}
        private const string AccountsBaseUrl = "https://api.schwabapi.com/trader/v1";
		public static string GetAccountsBaseUrl() => AccountsBaseUrl;
        // ✅ Singleton Instance
        private static readonly Lazy<SchwabApi> _instance = new Lazy<SchwabApi>(() => new SchwabApi());
 
 
        public static SchwabApi Instance => _instance.Value;

        // ✅ Static HttpClient (Reused for efficiency)
        private static readonly HttpClient _httpClient = new HttpClient { BaseAddress = new Uri(AccountsBaseUrl) };

        private static string _httpClientAccessToken;
        private HttpClient _hiddenHttpClient;

	 //   private UserPreferences? userPreferences;
	    private IList<AccountNumber> accountNumberHashs;

      //  internal IList<AccountNumber> accountNumberHashs; // load once
        internal const string utcDateFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
		internal static object jsonSettings = JsonReflection.GetJsonSerializerSettingsWithErrorHandling();

        //internal static JsonSerializerSettings jsonSettings = new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error };
		//var settings = JsonReflection.GetJsonSerializerSettingsWithErrorHandling();
		/// <summary>
        /// Schwab API class
        /// </summary>
        /// <param name="schwabTokens"></param>
//	    private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions
//	    {
//	        PropertyNameCaseInsensitive = true,
//	        // Add other options as needed
//	    };
	    public SchwabApi(SchwabTokens tokens)
	    {
			SchwabTokens.Initialize(GlobalVariables.TokenDataFilePath);
			_schwabTokens = SchwabTokens.Instance;
			
			if (_schwabTokens.tokens == null || string.IsNullOrEmpty(_schwabTokens.tokens.AccessToken))
			{
			    var refreshed = _schwabTokens.RequestAccessTokenAsync().Result;
			    if (string.IsNullOrEmpty(_schwabTokens.tokens.AccessToken))
			    {
			        throw new SchwabApiAuthorizationException(null, "❌ Failed to refresh access token. Reauthorization required.");
			    }
			}
			
			if (_schwabTokens.NeedsReAuthorization)
			{
			    throw new SchwabApiAuthorizationException(null, "❌ Tokens need reauthorization. Please log in again.");
			}
			
			NJ2CSLogManager.LogMessage($"✅ SchwabApi initialized with token: {_schwabTokens.tokens.AccessToken.Substring(0, 10)}...");
	    }
       	private static object jsonOptions = JsonReflection.GetJsonSerializerOptions();
		public async Task<T?> MakeAuthorizedRequestAsync<T>(string url)
		{
		    try
		    {
		        // Get the tokens instance (throws an exception if not initialized)
		        var tokensData = SchwabTokens.Instance.tokens;
		        // Check if the access token is still valid (compare with the expiration time)
		        if (DateTime.Now >= tokensData.AccessTokenExpires)
		        {
		            NJ2CSLogManager.LogMessage("Access token expired. Refreshing token...");
		            bool tokenRefreshed = await SchwabTokens.Instance.RefreshAccessTokenAsync();
		            if (!tokenRefreshed)
		            {
		                throw new Exception("❌ Unable to refresh access token.");
		            }
		            // Reload tokens after refresh
		            tokensData = SchwabTokens.Instance.tokens;
		        }
		
		        // Store the current valid access token in GlobalVariables for future use.
		        GlobalVariables.SchwabAccessToken = tokensData.AccessToken;
		
		        using var client = new HttpClient();
		        // Clear only the Accept headers to avoid conflicts.
		        client.DefaultRequestHeaders.Accept.Clear();
		        // Set the Authorization header with the valid token.
		        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokensData.AccessToken);
		        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		
		        NJ2CSLogManager.LogMessage($"🔄 Making GET request to {url}");
		        using var request = new HttpRequestMessage(HttpMethod.Get, url);
		        var response = await client.SendAsync(request);
		        var content = await response.Content.ReadAsStringAsync();
		        NJ2CSLogManager.LogMessage($"📜 API Response for {url}: {content}");
		
		        if (response.IsSuccessStatusCode)
		        {
		            return JsonConvert.DeserializeObject<T>(content);
		        }
		        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
		        {
		            NJ2CSLogManager.LogMessage("⚠️ Unauthorized (401). Attempting to refresh token...");
		            bool tokenRefreshed = await SchwabTokens.Instance.RefreshAccessTokenAsync();
		            if (tokenRefreshed)
		            {
		                NJ2CSLogManager.LogMessage("🔄 Retrying request after token refresh...");
		                return await MakeAuthorizedRequestAsync<T>(url);
		            }
		            else
		            {
		                throw new Exception("❌ Token refresh failed. Unauthorized access.");
		            }
		        }
		
		        NJ2CSLogManager.LogMessage($"❌ API Request failed. StatusCode: {response.StatusCode}, Response: {content}");
		        throw new Exception($"Failed to fetch data from {url}. StatusCode: {response.StatusCode}");
		    }
		    catch (Exception ex)
		    {
		        NJ2CSLogManager.LogMessage($"❌ Error in API request: {ex.Message}");
		        throw;
		    }
		}
		public async Task<bool> RefreshAccessTokenAsync()
		{
		    try
		    {
		        // Call the instance method on SchwabTokens.Instance
		        bool refreshed = await SchwabTokens.Instance.RefreshAccessTokenAsync();
		        return refreshed;
		    }
		    catch (Exception ex)
		    {
		        // Optionally log the error if needed
		        NJ2CSLogManager.LogMessage($"❌ Error in RefreshAccessTokenWrapperAsync: {ex.Message}");
		        return false;
		    }
		}	 		
		public async Task<ApiResponseWrapper<string>> MakePostRequest(string url, string jsonPayload, string accessToken)
		{
		    try
		    {
		        using var client = new HttpClient();
		        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		        client.DefaultRequestHeaders.Accept.Clear();
		        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		
		        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
		
		        UpdateStatus($"📌 Sending POST Request to {url}");
		        UpdateStatus($"📜 Request JSON Sent: {jsonPayload}");
		
		        var response = await client.PostAsync(url, content);
		        var responseBody = await response.Content.ReadAsStringAsync();
		
		        UpdateStatus($"📜 Full API Response: {responseBody}");
		
		        if (response.IsSuccessStatusCode)
		        {
		            return new ApiResponseWrapper<string>(responseBody, false, (int)response.StatusCode, "Success", response);
		        }
		        else
		        {
		            return new ApiResponseWrapper<string>(null, true, (int)response.StatusCode, $"❌ Error: {responseBody}", response);
		        }
		    }
		    catch (Exception ex)
		    {
		        return new ApiResponseWrapper<string>(null, true, 500, $"Exception: {ex.Message}");
		    }
		}
		public async Task<string> EnsureValidAccessTokenAsync()
		{
		    try
		    {
		        // ✅ Use Global Token Storage
		        var globalTokens = GlobalVariables.SchwabTokensInstance?.tokens;
		        if (globalTokens == null)
		        {
		            throw new SchwabApiAuthorizationException(null, "❌ Global token instance is null. Please log in.");
		        }
		
		        DateTime now = DateTime.Now;
		
		        // ✅ Check Access Token Expiry
		        if (globalTokens.AccessTokenExpires <= now)
		        {
		            NJ2CSLogManager.LogMessage("⚠️ Access token expired. Attempting refresh...");
		
		            // ✅ Check if Refresh Token is also expired
		            if (globalTokens.RefreshTokenExpires <= now)
		            {
		                NJ2CSLogManager.LogMessage("❌ Refresh token expired. Manual reauthentication required.");
		                throw new SchwabApiAuthorizationException(null, "Refresh token expired. Please log in again.");
		            }
		
		            bool refreshed = await RefreshAccessTokenAsync();
		            if (!refreshed)
		            {
		                NJ2CSLogManager.LogMessage("❌ Failed to refresh access token.");
		                throw new SchwabApiAuthorizationException(null, "Failed to refresh access token.");
		            }
		
		            NJ2CSLogManager.LogMessage("✅ Access token successfully refreshed.");
		        }
		
		        return globalTokens.AccessToken;
		    }
		    catch (Exception ex)
		    {
		        NJ2CSLogManager.LogMessage($"❌ Token validation failed: {ex.Message}");
		        throw;
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
		
		    return await SchwabTokens.Instance.GetAccessTokenAsync();
		}
        public static void SetSchwabTokens(SchwabTokens tokens)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens), "SchwabTokens cannot be null.");

            _schwabTokens = tokens;

            // If token is expired, refresh
            if (tokens.tokens.AccessTokenExpires <= DateTime.Now)
            {
                Instance.RefreshAccessTokenAsync().Wait();
            }
        } 
        internal static DateTime? GetDate(string dateStr, ref DateTime? privateDate)
        { // dateStr format = "2024-03-21 00:00:00.0"
            if (privateDate != null)
                return privateDate;
            if (dateStr == null)
                return null;

            privateDate = Convert.ToDateTime(dateStr);
            return privateDate;
        }
        internal static DateTime? GetDate(long dateLong, ref DateTime? privateDate)
        { // dateStr format = "2024-03-21 00:00:00.0"
            if (privateDate != null)
                return privateDate;
            if (dateLong == null)
                return null;

            privateDate = ApiDateTime_to_DateTime(dateLong);
            return privateDate;
        }
		public HttpClient HttpClient
		{
		    get
		    {
		        if (_schwabTokens == null || string.IsNullOrEmpty(_schwabTokens.tokens.AccessToken))
		        {
		            throw new NullReferenceException("Tokens are not initialized or AccessToken is null. Please authenticate.");
		        }
		
		        if (_schwabTokens.tokens.AccessToken != _httpClientAccessToken)
		        {
		            _httpClientAccessToken = _schwabTokens.tokens.AccessToken;
		            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _httpClientAccessToken);
					 NJ2CSLogManager.LogMessage($" _httpClientAccessToken  {_httpClientAccessToken }");
		        }
		
		        return _httpClient;
		    }
		}
		/// <summary>
        /// Method to wait for async operation to complete and return results.
        /// will throw an SchwabApiException error if result.HasError is true
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <param name="memberName">This will get the caller's method name, no need to provide, but can be id desired. Used in error messages.</param>
        /// <returns>results of api servise call</returns>
        /// <exception cref="Exception"></exception>
		private T WaitForCompletion<T>(Task<ApiResponseWrapper<T>> task, [CallerMemberName] string memberName = "")
        {
            task.Wait();
            if (task.Result.HasError)
            {
                throw new SchwabApiException<T>(task.Result, (memberName == "" ? "error: " : memberName + " error: ") + task.Result.ResponseCode + " " + task.Result.ResponseText);
            }
            return task.Result.Data;
        }
        /// <summary>
        /// Used to hold data or error info resulting from a async request
        /// </summary>
        /// <typeparam name="T"></typeparam>
 
        /// <summary>
        /// convert string to base64
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns>base64 string</returns>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        // =========== Date & Time ==================================================================
        /// <summary>
        /// Schwab API start time. add schwab's milliseconds (long) to epoch to get DateTime
        /// Schwab's server is on eastern time
        /// </summary>
        static DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(-5); // adjust for time zone
        /// <summary>
        /// Convert Schwab API time(long) to DateTime
        /// </summary>
        /// <param name="schwabTime"></param>
        /// <returns></returns>
        public static DateTime ApiDateTime_to_DateTime(long schwabTime)
        {
            return epoch.AddMilliseconds(schwabTime);
        }
        /// <summary>
        /// Convert DateTime to milliseconds since epoch
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns>Schwab long, total milliseconds since 1/1/1970 (epoch)</returns>
        public static long DateTime_to_ApiDateTime(DateTime dateTime)
        {

            TimeSpan ts = dateTime.ToUniversalTime() - epoch;
            return (long)Math.Floor(ts.TotalMilliseconds);
        }
        public static DateTime? ConvertDateOnce(long? schwabApiDateTime, ref DateTime? cachedDateTime)
        {
            if (schwabApiDateTime == null)
                return null;

            if (cachedDateTime == null)
                cachedDateTime = SchwabApi.ApiDateTime_to_DateTime((long)schwabApiDateTime);
            return cachedDateTime;
        }
        public static DateTime ConvertDateOnce(long schwabApiDateTime, ref DateTime? cachedDateTime)
        {
            if (cachedDateTime == null)
                cachedDateTime = SchwabApi.ApiDateTime_to_DateTime(schwabApiDateTime);
            return (DateTime)cachedDateTime;
        }
        // =============================================================================
        /// <summary>
        /// method to transform json response string to a json string that can be parsed.
        /// </summary>
        /// <param name="json">json string</param>
        /// <returns>json string</returns>
        public delegate string ResponseTransform(string json);
        /// <summary>
        /// Generic Schwab Get request
        /// </summary>
        /// <typeparam name="T">expected return type. Use string type to return json string response</typeparam>
        /// <param name="url">complete url of request</param>
        /// <param name="responseTransform">optional medthod to tranform json response string before processing</param>
        /// <returns>Task<ApiResponseWrapper<T?>></returns>
      	public async Task<ApiResponseWrapper<T>>? Get<T>(string url, ResponseTransform? responseTransform = null)
        {
            string responseString;
            HttpResponseMessage response = null;
            try
            {
                T? data;
                var taskResponse = HttpClient.GetAsync(url);
  
                taskResponse.Wait();
                response = taskResponse.Result;

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 502)
                        response.ReasonPhrase += ". Possible reason is body buffer overflow (response too big)";
                    return new ApiResponseWrapper<T>(default, true, (int)response.StatusCode, response.ReasonPhrase, response);
                }

                responseString = await response.Content.ReadAsStringAsync();

                if (responseString == null)
                    return new ApiResponseWrapper<T>(default, true, (int)response.StatusCode, response.ReasonPhrase + ", null content.", response);

                if (responseTransform != null)
                    responseString = responseTransform(responseString);

                if (typeof(T) == typeof(String))
                    data = (T)Convert.ChangeType(responseString, typeof(T)); // return json string unchanged
                else
                {
                    try
                    {
                        var type = typeof(T);
                        var settings = JsonReflection.GetJsonSerializerSettingsWithErrorHandling();
                        
                        var jsonConvertType = JsonReflection.GetJsonConvertType();
                        var deserializeMethod = jsonConvertType.GetMethod("DeserializeObject", new Type[] { typeof(string), typeof(Type), settings.GetType() });
                        if (deserializeMethod == null)
                            throw new Exception("Could not find DeserializeObject method with settings in JsonConvert.");

                        data = (T)deserializeMethod.Invoke(null, new object[] { responseString, type, settings });
                    }
                    catch (Exception ex)
                    {
                        return new ApiResponseWrapper<T>(default, true, response, ex);
                    }
                }

                if (data == null)
                    return new ApiResponseWrapper<T>(default, true, (int)response.StatusCode, response.ReasonPhrase + ", null JsonConvert.", response);

                return new ApiResponseWrapper<T>(data, false, (int)response.StatusCode, response.ReasonPhrase, response);
            }
            catch (Exception ex)
            {
                return new ApiResponseWrapper<T>(default, true, response, ex);
            }
        }

        /// <summary>
        /// Generic Schwab Post request
        /// </summary>
        /// <typeparam name="T">expected return type</typeparam>
        /// <param name="url">complete url of request</param>
        /// <param name="content">data to send with request</param>
        /// <returns>Task<ApiResponseWrapper<T?>></returns>
        public async Task<ApiResponseWrapper<T?>> Post<T>(string url, object? content = null)
        {

            var c = new StringContent(content.ToString(), Encoding.UTF8, "application/json");
 
			var response = HttpClient.PostAsync(url, c).Result;

            return new ApiResponseWrapper<T>(default, false, (int)response.StatusCode, response.ReasonPhrase, response);
        }
        /// <summary>
        /// Generic Schwab Put request
        /// </summary>
        /// <typeparam name="T">expected return type</typeparam>
        /// <param name="url">complete url of request</param>
        /// <param name="content">data to send with request</param>
        /// <returns>Task<ApiResponseWrapper<T?>></returns>
		public async Task<ApiResponseWrapper<T?>> Put<T>(string url, object? content = null)
		{
		    var c = new StringContent(content?.ToString() ?? "", Encoding.UTF8, "application/json");
		    var response = HttpClient.PutAsync(url, c).Result; // ✅ Use 'HttpClient' instead of 'httpClient'
		    
		    return new ApiResponseWrapper<T>(default, false, (int)response.StatusCode, response.ReasonPhrase, response);
		}
        /// <summary>
        /// Generic Schwab Delete request
        /// </summary>
        /// <param name="url"></param>
        /// <returns>Task<true/false></true></returns>
        public async Task<ApiResponseWrapper<bool>> Delete(string url)
        {
            var task = HttpClient.DeleteAsync(url);
            task.Wait();
            var r = task.Result;
            var responseString = await r.Content.ReadAsStringAsync();
            return new ApiResponseWrapper<bool>(r.IsSuccessStatusCode, !r.IsSuccessStatusCode, (int)r.StatusCode, r.ReasonPhrase ?? "", r);

        }
        /// <summary>
        /// Enum Convert, used by GetAssetType, GetDuration, GetSession, etc...
        /// </summary>
        /// <typeparam name="T">Enum type to convert to</typeparam>
        /// <param name="enumStringValue">string value to covert from</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T GetEnum<T>(string enumStringValue)
        {
            foreach (var t in (T[])Enum.GetValues(typeof(T)))
            {
                if (t.ToString() == enumStringValue)
                    return t;
            }
            throw new SchwabApiException("Invalid asset type '" + enumStringValue + "'");
        }
        /// <summary>
        /// Format Symbol for Display
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="format">format option</param>
        /// <returns></returns>
		public static string SymbolDisplay(string symbol, int format = 0)
		{
		    if (symbol.Contains(' ')) // parse option symbol
		    {
		        string s;
		        switch (format)
		        {
		            case 0: // SPY 2024-09-20 Call 543.00
		                s = symbol.Substring(0, 6) + "20" + symbol.Substring(6, 2) + "-" + symbol.Substring(8, 2) + "-" +
		                    symbol.Substring(10, 2) + (symbol[12] == 'C' ? " Call " : " Put ") + 
		                    symbol.Substring(13, 5).TrimStart('0') + "." +
		                    (symbol[20] == '0' ? symbol.Substring(18, 2) : symbol.Substring(18));
		                return s;
		
		            case 1: // SPY 09/20/24 543 Call, SPY 09/20/24 543.50 Call
		                s = symbol.Substring(0, 6) + symbol.Substring(8, 2) + "/" + symbol.Substring(10, 2) + "/" +
		                    symbol.Substring(6, 2) + " " + symbol.Substring(13, 5).TrimStart('0');
		                if (symbol.Substring(18, 3) != "000")
		                    s += "." + (symbol[20].ToString() == "0" ? symbol.Substring(18, 2) : symbol.Substring(18));
		                s += (symbol[12].ToString() == "C" ? " Call" : " Put");
		                return s;
		        }
		    }
		    return symbol;
		}

        /// <summary>
        /// Check to see if too many symbols
        /// </summary>
        /// <param name="symbols"></param>
        /// <param name="maxCount"></param>
        /// <returns>true is too many</returns>
        public static bool SymbolMaxCheck(string symbols, int maxCount)
        {
            return (symbols.Length - symbols.Replace(",", "").Length >= maxCount);
        }

		// =========== Schwab Api Exceptions =========================================================================
        #region SchwabApiExceptions

        public class SchwabApiException : Exception
        {
            public SchwabApiException() { }
            public SchwabApiException(string message) : base(message) { }
            public SchwabApiException(string message, Exception inner) : base(message, inner) { }

            public object? ApiResponse;  // is a type of ApiResponseWrapper<T> for inspecting
            public HttpResponseMessage? Response { get; init; }
            public string? SchwabClientCorrelId { get; init; }

            public override string Message { 
                get
                {
                    var msg = base.Message + "\n\n" +
                           Url.Replace("?", "\n?");
                    if (!String.IsNullOrWhiteSpace(SchwabClientCorrelId))
                           msg += "\n\nSchwabClientCorrelId = " + SchwabClientCorrelId;
                    return msg;
                }
            }

            public string Url { 
                get {
                    if (Response == null)
                        return "";
                    return Response.RequestMessage.RequestUri.PathAndQuery;
                }  
            }

        }

        public class SchwabApiException<T> : SchwabApiException
        {
            public SchwabApiException(ApiResponseWrapper<T> apiResponse)
            {
                this.ApiResponse = apiResponse;
                this.Response = apiResponse.ResponseMessage;
                this.SchwabClientCorrelId = apiResponse.SchwabClientCorrelId;
            }

            public SchwabApiException(ApiResponseWrapper<T> apiResponse, string message)
                : base(message)
            {
                this.ApiResponse = apiResponse;
                this.Response = apiResponse.ResponseMessage;
                this.SchwabClientCorrelId = apiResponse.SchwabClientCorrelId;
            }
        }

        public class SchwabApiAuthorizationException : SchwabApiException
        {
            public SchwabApiAuthorizationException(HttpResponseMessage responseMessage, string message)
                : base(message)
            {
                this.Response = responseMessage;
            }
        }

        public static ExceptionMessageResult ExceptionMessage(Exception ex)
        {
            if (ex is AggregateException)
            {
                if (ex.InnerException is SchwabApiException)
                    return new ExceptionMessageResult("SchwabApiException", ((SchwabApiException)ex.InnerException).Message);

                return new ExceptionMessageResult("Exception", ex.InnerException.Message);
            }
            if (ex is SchwabApiException)
                return new ExceptionMessageResult("SchwabApiException", ex.Message);

            return new ExceptionMessageResult("Exception", ex.Message);
        }

        public class ExceptionMessageResult
        {
            public ExceptionMessageResult(string title, string message)
            {
                Title = title;
                Message = message;
            }

            public string Title { get; set; }
            public string Message { get; set; }
        }

        #endregion SchwabApiExceptions
		#region  notused	
		private void not_used()
		{
			/*
				public async Task<bool> RefreshAccessTokenAsync()
			{
			    try
			    {
			        if (string.IsNullOrEmpty(_schwabTokens.tokens.RefreshToken))
			        {
			            throw new SchwabApiAuthorizationException(null, "No refresh token available to refresh access token.");
			        }
			
			        using (var client = new HttpClient())
			        {
			            var content = new StringContent(
			                $"grant_type=refresh_token&refresh_token={_schwabTokens.tokens.RefreshToken}&redirect_uri={_schwabTokens.tokens.Redirect_uri}",
			                Encoding.UTF8,
			                "application/x-www-form-urlencoded"
			            );
			
			            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
			                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_schwabTokens.tokens.AppKey}:{_schwabTokens.tokens.Secret}")));
			
			            var response = await client.PostAsync(SchwabTokens.baseUrl + "/token", content);
			
			            if (response.IsSuccessStatusCode)
			            {
			                var responseContent = await response.Content.ReadAsStringAsync();
			                var tokenResult = JsonConvert.DeserializeObject<SchwabTokens.TokenResult>(responseContent);
			
			                _schwabTokens.tokens.AccessToken = tokenResult.access_token;
			                _schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30); // Update expiration
			                _schwabTokens.tokens.RefreshToken = tokenResult.refresh_token;
			                _schwabTokens.tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
			
			                _schwabTokens.SaveTokens();
			                return true;
			            }
			            else
			            {
			                string errorResponse = await response.Content.ReadAsStringAsync();
			                throw new SchwabApiAuthorizationException(response, 
			                    $"Failed to refresh access token. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}, Details: {errorResponse}");
			            }
			        }
			    }
			    catch (Exception ex)
			    {
			        throw new SchwabApiException("Error while refreshing access token: " + ex.Message, ex);
			    }
			} 
 	
		/// <summary>
		/// Refreshes the access token using the stored refresh token.
		/// </summary>
		/// <returns>Task representing the asynchronous operation.</returns>
	/*	public async Task<bool> RefreshAccessTokenAsync()
		{
		    try
		    {
		        if (DateTime.Now < schwabTokens.tokens.AccessTokenExpires)
		        {
		            // Token is still valid, no need to refresh
		            return false;
		        }
		
		        if (DateTime.Now >= schwabTokens.tokens.RefreshTokenExpires)
		        {
		            throw new SchwabApiAuthorizationException(null, "RefreshAccessTokenAsync: Refresh token expired, reauthorization required.");
		        }
		
		        using (var httpClient = new HttpClient())
		        {
		            var content = new StringContent(
		                $"grant_type=refresh_token&refresh_token={schwabTokens.tokens.RefreshToken}",
		                Encoding.UTF8,
		                "application/x-www-form-urlencoded"
		            );
		
		            httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " +
		                SchwabApi.Base64Encode(schwabTokens.tokens.AppKey + ":" + schwabTokens.tokens.Secret));
		
		            var response = await httpClient.PostAsync(SchwabTokens.baseUrl + "/token", content);
		
		            if (!response.IsSuccessStatusCode)
		            {
		                throw new SchwabApiAuthorizationException(response, 
		                    $"Failed to refresh access token. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
		            }
		
		            var responseContent = await response.Content.ReadAsStringAsync();
		            var result = (SchwabTokens.SchwabTokensData)JsonReflection.DeserializeObject(responseContent, typeof(SchwabTokens.SchwabTokensData));
		
		            schwabTokens.tokens.AccessToken = result.AccessToken;
		            schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddSeconds(result.AccessTokenExpires.Second - 10);
		            schwabTokens.tokens.RefreshToken = result.RefreshToken;
		            schwabTokens.tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
		
		            schwabTokens.SaveTokens(response, "RefreshAccessTokenAsync");
		
		            return true;
		        }
		    }
		    catch (Exception ex)
		    {
		        throw new SchwabApiException("Error while refreshing access token: " + ex.Message, ex);
		    }
		}
 		
			public SchwabApi(SchwabTokens schwabTokens)
				{
				    try
				    {
						 
				        if (schwabTokens == null)
				            throw new ArgumentNullException(nameof(schwabTokens));
				
				       // SchwabApi.schwabTokens = schwabTokens;
						_schwabTokens = schwabTokens;
		       			//this.schwabTokens = schwabTokens ?? throw new ArgumentNullException(nameof(schwabTokens));
		  		
				        if (schwabTokens.NeedsReAuthorization)
				        {
							NinjaTrader.Code.Output.Process( "Tokens need reauthorization. Please log in again", NinjaTrader.NinjaScript.PrintTo.OutputTab1 );
				            throw new SchwabApiException("Tokens need reauthorization. Please log in again.");
						//	NinjaTrader.NinjaScript.AddOns.NJ2CSLogManager.LogMessage($"Tokens need reauthorization. Please log in again.");
		
				        }
				
				        // Initialize fields safely and check for errors
				        var userPreferencesTask = GetUserPreferencesAsync();
				        var accountNumbersTask = GetAccountNumbersAsync();
				        
				        Task.WaitAll(userPreferencesTask, accountNumbersTask);
				
				        if (userPreferencesTask.Result.HasError)
				        {
				            throw new Exception("Failed to fetch user preferences: " + userPreferencesTask.Result.ResponseText);
				        }
				        if (accountNumbersTask.Result.HasError)
				        {
				            throw new Exception("Failed to fetch account numbers: " + accountNumbersTask.Result.ResponseText);
				        }
				
				        userPreferences = userPreferencesTask.Result.Data ?? throw new Exception("User preferences data is null.");
				        accountNumberHashs = accountNumbersTask.Result.Data ?? throw new Exception("Account numbers data is null.");
				    }
				    catch (Exception ex)
				    {
				        throw new Exception($"Error during SchwabApi initialization: {ex.Message}", ex);
						 NinjaTrader.Code.Output.Process( ex.Message, NinjaTrader.NinjaScript.PrintTo.OutputTab1 );
					//	NinjaTrader.NinjaScript.AddOns.NJ2CSLogManager.LogMessage($"Error during SchwabApi initialization: {ex.Message}");
				    }
				}
		
				public static async Task<SchwabApi> CreateAsync(SchwabTokens schwabTokens)
				{
				    if (schwabTokens == null)
				        throw new ArgumentNullException(nameof(schwabTokens));
				
				    if (schwabTokens.NeedsReAuthorization)
				    {
				        NinjaTrader.Code.Output.Process("Tokens need reauthorization. Please log in again", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
				        throw new SchwabApiException("Tokens need reauthorization. Please log in again.");
				    }
				
				    var userPreferencesTask = GetUserPreferencesAsync();
				    var accountNumbersTask = GetAccountNumbersAsync();
				
				    await Task.WhenAll(userPreferencesTask, accountNumbersTask);
				
				    if (userPreferencesTask.Result.HasError)
				    {
				        throw new Exception("Failed to fetch user preferences: " + userPreferencesTask.Result.ResponseText);
				    }
				    if (accountNumbersTask.Result.HasError)
				    {
				        throw new Exception("Failed to fetch account numbers: " + accountNumbersTask.Result.ResponseText);
				    }
				
				    var userPreferences = userPreferencesTask.Result.Data ?? throw new Exception("User preferences data is null.");
				    var accountNumberHashs = accountNumbersTask.Result.Data ?? throw new Exception("Account numbers data is null.");
				
				    return new SchwabApi(schwabTokens, userPreferences, accountNumberHashs);
				}
				
				private SchwabApi(SchwabTokens schwabTokens, UserPreferences userPreferences, List<string> accountNumberHashs)
				{
				    SchwabApi.schwabTokens = schwabTokens;
				    this.userPreferences = userPreferences;
				    this.accountNumberHashs = accountNumberHashs;
				}
		 
		       // private string _httpClientAccessToken = "";
		
		 		private HttpClient httpClient
				{
				    get
				    {
				        // Ensure schwabTokens and tokens are initialized
				        if (schwabTokens == null || schwabTokens.tokens == null)
				        {
				            throw new NullReferenceException("Schwab tokens are not initialized. Ensure authentication is completed.");
				        }
				
				        // Ensure the access token is valid
				        if (string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
				        {
				            throw new NullReferenceException("Access token is null or empty. Please log in and authenticate.");
				        }
				
				        // Check if the access token has changed and recreate the client if necessary
				        if (schwabTokens.tokens.AccessToken != _httpClientAccessToken)
				        {
				            _httpClientAccessToken = schwabTokens.tokens.AccessToken;
				            _hiddenHttpClient = new HttpClient();
				            _hiddenHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _httpClientAccessToken);
				        }
				
				        return _hiddenHttpClient;
				    }
				}  
		       public HttpClient HttpClient
		        {
		            get
		            {
		                if (schwabTokens == null )
		                {
		            		NinjaTrader.Code.Output.Process(" schwabTokens is null", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		
		                    throw new NullReferenceException("Schwab tokens are not initialized. Ensure authentication is completed.");
		                }
		                if (schwabTokens == null )
		                {
		            		NinjaTrader.Code.Output.Process(" schwabTokens is null", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		
		                    throw new NullReferenceException("Schwab tokens are not initialized. Ensure authentication is completed.");
		                }				
		
		                if (string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
		                {
		                    throw new NullReferenceException("Access token is null or empty. Please log in and authenticate.");
		                }
		
		                // Recreate HttpClient if the token changes
		                if (schwabTokens.tokens.AccessToken != _httpClientAccessToken)
		                {
		                    _httpClientAccessToken = schwabTokens.tokens.AccessToken;
		                    _hiddenHttpClient = new HttpClient
		                    {
		                        BaseAddress = new Uri(AccountsBaseUrl)
		                    };
		                    _hiddenHttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _httpClientAccessToken);
		                }
		
		                return _hiddenHttpClient;
		            }
		        }  
		public HttpClient HttpClient
		{
		    get
		    {
		        if (_schwabTokens == null || string.IsNullOrEmpty(_schwabTokens.tokens.AccessToken))
		        {
		            NinjaTrader.Code.Output.Process("Tokens are not initialized or AccessToken is null or empty.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            throw new NullReferenceException("Tokens are not initialized or AccessToken is null or empty. Please authenticate.");
		        }
		
		        if (_schwabTokens.tokens.AccessToken != _httpClientAccessToken)
		        {
		            _httpClientAccessToken = _schwabTokens.tokens.AccessToken;
		            _hiddenHttpClient = new HttpClient
		            {
		                BaseAddress = new Uri(AccountsBaseUrl)
		            };
		            _hiddenHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _httpClientAccessToken);
		        }
		
		        return _hiddenHttpClient;
		    }
		}
		
		 public HttpClient HttpClient
		{
		    get
		    {
		        if (_schwabTokens == null || string.IsNullOrEmpty(_schwabTokens.tokens.AccessToken))
		        {
		            throw new NullReferenceException("Tokens are not initialized or AccessToken is null. Please authenticate.");
		        }
		
		        if (_schwabTokens.tokens.AccessToken != _httpClientAccessToken)
		        {
		            _httpClientAccessToken = _schwabTokens.tokens.AccessToken;
		            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _httpClientAccessToken);
		        }
		
		        return _httpClient;
		    }
		}
		*/
			}
		#endregion notused
    }
	public static class JsonReflection
	{
	    private static readonly AssemblyName JsonAssemblyName = new AssemblyName("Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed");
	
	    public static Type GetJsonConvertType()
	    {
	        try
	        {
	            var assembly = Assembly.Load(JsonAssemblyName);
	            return assembly?.GetType("Newtonsoft.Json.JsonConvert");
	        }
	        catch
	        {
	            var fallbackAssembly = AppDomain.CurrentDomain.GetAssemblies()
	                .FirstOrDefault(a => a.GetName().Name == "Newtonsoft.Json");
	            return fallbackAssembly?.GetType("Newtonsoft.Json.JsonConvert");
	        }
	    }
		public static string SerializeResponseUsingReflection(object response)
		{
		    try
		    {
		        var jsonConvertType = JsonReflection.GetJsonConvertType();
		        if (jsonConvertType == null)
		        {
		            throw new Exception("JsonConvert type could not be loaded.");
		        }
		
		        var serializeMethod = jsonConvertType.GetMethod("SerializeObject", new[] { typeof(object), typeof(object) });
		        if (serializeMethod == null)
		        {
		            throw new Exception("SerializeObject method not found in JsonConvert.");
		        }
		
		        var jsonSerializerSettings = JsonReflection.GetJsonSerializerSettingsWithErrorHandling();
		        return (string)serializeMethod.Invoke(null, new object[] { response, jsonSerializerSettings });
		    }
		    catch (Exception ex)
		    {
		        NinjaTrader.Code.Output.Process($"Serialization error: {ex.Message}. Inner Exception: {ex.InnerException?.Message}", 
		            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		        return $"Error serializing response: {ex.Message}. Inner Exception: {ex.InnerException?.Message}";
		    }
		}
	    public static object GetJsonSerializerOptions()
	    {
	        try
	        {
	            var type = Type.GetType("System.Text.Json.JsonSerializerOptions, System.Text.Json");
	            if (type != null)
	            {
	                var instance = Activator.CreateInstance(type);
	                var property = type.GetProperty("PropertyNameCaseInsensitive");
	                property?.SetValue(instance, true);
	                return instance;
	            }
	        }
	        catch (Exception ex)
	        {
	            throw new Exception("Error loading JsonSerializerOptions dynamically.", ex);
	        }
	
	        throw new Exception("Could not find JsonSerializerOptions type.");
	    }
	
		public static object GetJsonSerializerSettingsWithErrorHandling()
		{
		    var jsonConvertType = GetJsonConvertType();
		    if (jsonConvertType == null)
		        throw new Exception("Could not load JsonConvert type from Newtonsoft.Json.");
		
		    var jsonSerializerSettingsType = jsonConvertType.Assembly.GetType("Newtonsoft.Json.JsonSerializerSettings");
		    if (jsonSerializerSettingsType == null)
		        throw new Exception("Could not find JsonSerializerSettings type.");
		
		    var settingsInstance = Activator.CreateInstance(jsonSerializerSettingsType);
		
		    // Define the error handler method
		    MethodInfo errorHandlerMethod = typeof(JsonReflection).GetMethod(nameof(HandleSerializationError), BindingFlags.Static | BindingFlags.NonPublic);
		    if (errorHandlerMethod == null)
		        throw new Exception("Could not find the error handler method 'HandleSerializationError'.");
		
		    // Create the delegate using reflection
		    var errorHandlerDelegate = Delegate.CreateDelegate(
		        jsonSerializerSettingsType.GetProperty("Error").PropertyType,
		        errorHandlerMethod
		    );
		
		    // Set the Error property with the created delegate
		    jsonSerializerSettingsType.GetProperty("Error").SetValue(settingsInstance, errorHandlerDelegate);
		
		    return settingsInstance;
		}
	
		// Private method to handle serialization errors
		private static void HandleSerializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args)
		{
		    var errorContext = args.ErrorContext;
		    NinjaTrader.Code.Output.Process(
		        $"Serialization Error: {errorContext.Error.Message} - Path: {errorContext.Path}",
		        NinjaTrader.NinjaScript.PrintTo.OutputTab1
		    );
		    errorContext.Handled = true; // Skip problematic properties
		}
	    public static object DeserializeObject(string json, Type type)
	    {
	        var jsonConvertType = GetJsonConvertType();
	        if (jsonConvertType == null)
	            throw new Exception("Could not load JsonConvert type from Newtonsoft.Json.");
	
	        var deserializeMethod = jsonConvertType.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) });
	        if (deserializeMethod == null)
	            throw new Exception("DeserializeObject method not found in JsonConvert.");
	
	        return deserializeMethod.Invoke(null, new object[] { json, type });
	    }
	
		public static string SerializeObject(object obj)
		{
		    try
		    {
		        var jsonConvertType = GetJsonConvertType();
		        if (jsonConvertType == null)
		        {
		            throw new Exception("JsonConvert type could not be loaded.");
		        }
		
		        var serializeMethod = jsonConvertType.GetMethod("SerializeObject", new[] { typeof(object) });
		        if (serializeMethod == null)
		        {
		            throw new Exception("SerializeObject method not found in JsonConvert.");
		        }
		
		        return (string)serializeMethod.Invoke(null, new object[] { obj });
		    }
		    catch (Exception ex)
		    {
		        NinjaTrader.Code.Output.Process(
		            $"Error during serialization: {ex.Message}",
		            NinjaTrader.NinjaScript.PrintTo.OutputTab1
		        );
		        return $"Error serializing object: {ex.Message}";
		    }
		}
		private static void noused()
		{
		/*
				public async Task<bool> RefreshAccessTokenAsync()
			{
			    try
			    {
			        if (string.IsNullOrEmpty(_schwabTokens.tokens.RefreshToken))
			        {
			            throw new SchwabApiAuthorizationException(null, "No refresh token available to refresh access token.");
			        }
			
			        using (var client = new HttpClient())
			        {
			            var content = new StringContent(
			                $"grant_type=refresh_token&refresh_token={_schwabTokens.tokens.RefreshToken}&redirect_uri={_schwabTokens.tokens.Redirect_uri}",
			                Encoding.UTF8,
			                "application/x-www-form-urlencoded"
			            );
			
			            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", 
			                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_schwabTokens.tokens.AppKey}:{_schwabTokens.tokens.Secret}")));
			
			            var response = await client.PostAsync(SchwabTokens.baseUrl + "/token", content);
			
			            if (response.IsSuccessStatusCode)
			            {
			                var responseContent = await response.Content.ReadAsStringAsync();
			                var tokenResult = JsonConvert.DeserializeObject<SchwabTokens.TokenResult>(responseContent);
			
			                _schwabTokens.tokens.AccessToken = tokenResult.access_token;
			                _schwabTokens.tokens.AccessTokenExpires = DateTime.Now.AddMinutes(30); // Update expiration
			                _schwabTokens.tokens.RefreshToken = tokenResult.refresh_token;
			                _schwabTokens.tokens.RefreshTokenExpires = DateTime.Now.AddDays(7);
			
			                _schwabTokens.SaveTokens();
			                return true;
			            }
			            else
			            {
			                string errorResponse = await response.Content.ReadAsStringAsync();
			                throw new SchwabApiAuthorizationException(response, 
			                    $"Failed to refresh access token. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}, Details: {errorResponse}");
			            }
			        }
			    }
			    catch (Exception ex)
			    {
			        throw new SchwabApiException("Error while refreshing access token: " + ex.Message, ex);
			    }
			}*/
		}		
	}

}
