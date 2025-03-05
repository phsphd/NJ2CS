using System;
using System.Collections.Generic;
using System.IO;

namespace SchwabApiCS
{
    public static class GlobalVariables
    {
        // ✅ Dictionary (AccountNumber -> Hash)
        public static Dictionary<string, string> csAccounts { get; set; } = new();
        public static List<NJ2CSTradeTab> TradeTabs { get; private set; } = new List<NJ2CSTradeTab>();
		public static string SchwabAccessToken { get; set; } = "";
        // ✅ Store List for reference
        public static List<AccountNumber> csAccountsList { get; set; } = new();

        // ✅ List of tickers with get and set
        private static List<string> _tickers = new List<string>
        {
            "NVDA", "SPY", "QQQ", "AAPL", "MSFT", "TSLA", "AMZN", "META", "GOOGL","NVDX","NVDL"
        };

        public static List<string> Tickers
        {
            get => _tickers;
            set
            {
                if (value != null)
                {
                    _tickers = value;
                }
                else
                {
                    throw new ArgumentNullException(nameof(value), "Tickers list cannot be null.");
                }
            }
        }

        private static SchwabApi? _schwabApiInstance;
        public static SchwabApi? SchwabApiInstance
        {
            get => _schwabApiInstance;
            set => _schwabApiInstance = value;
        }

        private static SchwabTokens? _schwabTokensInstance;
        public static SchwabTokens? SchwabTokensInstance
        {
            get
            {
                if (_schwabTokensInstance == null)
                {
                    _schwabTokensInstance = new SchwabTokens(TokenDataFilePath);
                    _schwabTokensInstance.LoadTokens();
                }
                return _schwabTokensInstance;
            }
            set
            {
                _schwabTokensInstance = value;
            }
        }

        // ✅ Store token file path globally
        public static readonly string TokenDataFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            @"NinjaTrader 8\bin\Custom\AddOns\schwab_tokens.json"
        );

        // ✅ 🔒 Store API Key & Secret Securely
        public static string SchwabAppKey { get; set; } = "YOUR_APP_KEY_HERE";
        public static string SchwabSecret { get; set; } = "YOUR_SECRET_HERE";

        // ✅ Fix: Add Missing Variables
        public static string SchwabRedirectUri { get; set; } = ""; // 🔹 Store Redirect URI
        public static string SchwabRefreshToken { get; set; } = ""; // 🔹 Store Refresh Token

        /// ✅ Loads API credentials from a secure file (optional)
        public static void LoadApiCredentials()
        {
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                @"NinjaTrader 8\bin\Custom\AddOns\schwab_api_credentials.json");

            if (File.Exists(filePath))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var credentials = Newtonsoft.Json.JsonConvert.DeserializeObject<ApiCredentials>(json);

                    if (credentials != null)
                    {
                        SchwabAppKey = credentials.AppKey;
                        SchwabSecret = credentials.Secret;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ ERROR: Failed to load API credentials. {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("⚠️ API credentials file not found. Using default hardcoded values.");
            }
        }
    }

    public struct AccountNumber
    {
        public string accountNumber { get; set; }
        public string hashValue { get; set; }

        public AccountNumber(string accountNumber, string hashValue)
        {
            this.accountNumber = accountNumber;
            this.hashValue = hashValue;
        }
    }

    // ✅ 🔐 API Credentials Struct
    public class ApiCredentials
    {
        public string AppKey { get; set; } = "";
        public string Secret { get; set; } = "";
    }
}

