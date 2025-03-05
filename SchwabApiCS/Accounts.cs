using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using NinjaTrader.NinjaScript.AddOns;
using Newtonsoft.Json.Linq;
namespace SchwabApiCS
{
    public partial class SchwabApi
    {
        // Singleton instance
		public async Task<List<AccountHashPair>> GetAccountNumbersAsync()
		{
		    try
		    {
		        string url = "https://api.schwabapi.com/trader/v1/accounts/accountNumbers";
		        List<AccountHashPair>? accounts = await MakeAuthorizedRequestAsync<List<AccountHashPair>>(url);
		
		        if (accounts == null || accounts.Count == 0)
		        {
		            NJ2CSLogManager.LogMessage("❌ No accounts retrieved from Schwab API.");
		            return new List<AccountHashPair>();
		        }
		
		        // ✅ Store accountNumber & hashValue for later use
		        foreach (var account in accounts)
		        {
		            NJ2CSLogManager.LogMessage($"✅ Found Account: {account.accountNumber} -> Hash: {account.hashValue}");
		        }
		
		        return accounts;
		    }
		    catch (Exception ex)
		    {
		        NJ2CSLogManager.LogMessage($"❌ ERROR: Exception in GetAccountNumbersAsync: {ex.Message}");
		        return new List<AccountHashPair>();
		    }
		}
		public class AccountNumbersResponse
		{
		    public List<AccountInfo> accountNumbers { get; set; }
		}
		public string GetDefaultAccountHash(string accountNumber)
		{
		    try
		    {
		        // ✅ Fetch the default account number from GlobalVariables
		       // string accountNumber = GetDefaultAccountNumber().Result; // Make sure GetDefaultAccountNumber() is an async Task<string>
		
		        if (string.IsNullOrEmpty(accountNumber))
		        {
		            throw new Exception("Default account number is empty.");
		        }
		
		        // ✅ Retrieve the hash value from GlobalVariables (NO API CALL)
		        if (GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            return accountHash;
		        }
		        else
		        {
		            throw new Exception($"Hash not found for account {accountNumber}.");
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ ERROR in GetDefaultAccountHash: {ex.Message}");
		        throw;
		    }
		}
		public class AccountNumberEntry
		{
		    public string accountNumber { get; set; }
		 
			public string hashValue { get; set; }     // ✅ Hash value
		}
		public async Task<string> GetAccountNumberHash(string accountNumber)
		{
		    try
		    {
		        NinjaTrader.Code.Output.Process($"Fetching account number hash for {accountNumber}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		
		        // ✅ Check in-memory cache first
		        if (GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            return accountHash;
		        }
		
		        // ✅ Fetch all account numbers from API (ensures token is valid)
		        var accountNumbers = await MakeAuthorizedRequestAsync<List<AccountNumber>>($"{AccountsBaseUrl}/accounts/accountNumbers");
		
		        if (accountNumbers == null || !accountNumbers.Any())
		        {
		            throw new Exception("No account numbers retrieved from API.");
		        }
		
		        // ✅ Update Global Cache
		        GlobalVariables.csAccounts = accountNumbers.ToDictionary(acc => acc.accountNumber, acc => acc.hashValue);
		
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out accountHash))
		        {
		            throw new Exception($"Account number {accountNumber} not found.");
		        }
		
		        NinjaTrader.Code.Output.Process($"Found hash for account {accountNumber}: {accountHash}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		        return accountHash;
		    }
		    catch (Exception ex)
		    {
		        NinjaTrader.Code.Output.Process($"Error in GetAccountNumberHash: {ex.Message}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		        throw;
		    }
		}
		public async Task<string> GetAccountHash(string accountNumber)
		{
		    try
		    {
		        // ✅ Check in-memory cache first
		        if (GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            return accountHash;
		        }
		
		        // ✅ Fetch all account numbers from API (ensures token is valid)
		        var accountNumbers = await MakeAuthorizedRequestAsync<List<AccountNumber>>($"{AccountsBaseUrl}/accounts/accountNumbers");
		
		        if (accountNumbers == null || !accountNumbers.Any())
		        {
		            throw new Exception("No account numbers retrieved from API.");
		        }
		
		        // ✅ Update Global Cache
		        GlobalVariables.csAccounts = accountNumbers.ToDictionary(acc => acc.accountNumber, acc => acc.hashValue);
		
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out accountHash))
		        {
		            throw new Exception($"Account number {accountNumber} not found.");
		        }
		
		        return accountHash;
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in GetAccountHash: {ex.Message}");
		        throw;
		    }
		}	
		public async Task<List<string>> GetAccountBalancesAsync()
		{
		    var accountBalanceResults = new List<string>();
		
		    try
		    {
		        if (GlobalVariables.csAccounts == null || GlobalVariables.csAccounts.Count == 0)
		        {
		            UpdateStatus("⚠️ No account numbers available. Fetching from Schwab...");
		            var accountNumbers = await GetAccountNumbersAsync();
		
		            if (accountNumbers == null || accountNumbers.Count == 0)
		            {
		                UpdateStatus("❌ No accounts retrieved after fetch attempt.");
		                accountBalanceResults.Add("❌ No accounts retrieved.");
		                return accountBalanceResults;
		            }
		
		            GlobalVariables.csAccounts = accountNumbers.ToDictionary(a => a.accountNumber, a => a.hashValue);
		        }
		
		        UpdateStatus($"🔄 Processing {GlobalVariables.csAccounts.Count} accounts...");
		
		        foreach (var kvp in GlobalVariables.csAccounts)
		        {
		            string accountNumber = kvp.Key;
		            string accountHash = kvp.Value;
		
		            try
		            {
		                UpdateStatus($"🔍 Fetching balance for Account: {accountNumber} (Hash: {accountHash})...");
		
		                // ✅ Use the correct Trader API endpoint
		                string balanceUrl = $"{AccountsBaseUrl}/accounts/{accountHash}";
		                var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(balanceUrl);
		
		                if (accountInfo?.securitiesAccount == null)
		                {
		                    UpdateStatus($"⚠️ No securities account data found for {accountNumber}.");
		                    continue;
		                }
		
		                decimal? balance = accountInfo.securitiesAccount.aggregatedBalance?.liquidationValue
		                                   ?? accountInfo.securitiesAccount.currentBalances?.cashBalance
		                                   ?? accountInfo.securitiesAccount.initialBalances?.cashBalance;
		
		                if (balance.HasValue)
		                {
		                    accountBalanceResults.Add($"✅ Account: {accountNumber}, Balance: ${balance:N2}");
		                }
		                else
		                {
		                    accountBalanceResults.Add($"⚠️ Account: {accountNumber}, Balance not found.");
		                }
		            }
		            catch (Exception ex)
		            {
		                accountBalanceResults.Add($"❌ Error retrieving balance for {accountNumber} (Hash: {accountHash}): {ex.Message}");
		            }
		        }
		    }
		    catch (Exception ex)
		    {
		        accountBalanceResults.Add($"❌ Critical error: {ex.Message}");
		    }
		
		    return accountBalanceResults;
		}
		public async Task<AccountInfo?> GetAccountAsync(string accountNumber, string accountHash)
		{
		    string url = $"{AccountsBaseUrl}/accounts/{accountHash}";
		    return await MakeAuthorizedRequestAsync<AccountInfo>(url);
		}	
		public async Task<AccountInfo?> GetAccountBalanceAsync(string accountHash)
		{
		    try
		    {
		        string url = $"https://api.schwabapi.com/trader/v1/accounts/{accountHash}";
		        AccountInfo? accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
		
		        if (accountInfo?.securitiesAccount != null)
		        {
		            decimal? balance = accountInfo.securitiesAccount.currentBalances?.cashBalance;
		            NJ2CSLogManager.LogMessage($"✅ Account {accountInfo.securitiesAccount.accountNumber} Balance: ${balance:N2}");
		            return accountInfo;
		        }
		        else
		        {
		            NJ2CSLogManager.LogMessage("❌ Failed to retrieve account balance.");
		            return null;
		        }
		    }
		    catch (Exception ex)
		    {
		        NJ2CSLogManager.LogMessage($"❌ ERROR: Exception in GetAccountBalanceAsync: {ex.Message}");
		        return null;
		    }
		}		
		public async Task<List<AccountPosition>> GetPositionsAsync(string accountNumber, string accountHash)
		{
		    try
		    {
		        // ✅ Get the account hash from GlobalVariables or fetch it if missing
		    /*    if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await GetAccountHash(accountNumber);
		            if (string.IsNullOrEmpty(accountHash))
		            {
		                throw new Exception($"No hash found for account {accountNumber}.");
		            }
		        }
		*/
		        // ✅ Fetch positions from API
		        string url = $"{AccountsBaseUrl}/accounts/{accountHash}?fields=positions";
		        UpdateStatus($"Fetching positions for account: {accountNumber} from {url}");
		
		        var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
		        if (accountInfo?.securitiesAccount?.positions == null)
		        {
		            UpdateStatus($"No positions found for account {accountNumber}");
		            return new List<AccountPosition>();
		        }
		
		        return accountInfo.securitiesAccount.positions;
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error fetching positions: {ex.Message}");
		        throw;
		    }
		}
		public async Task<double> GetUnrealizedPnL(string accountNumber)
		{
		    try
		    {
		        // ✅ Get the account hash from GlobalVariables or fetch it if missing
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await GetAccountHash(accountNumber);
		            if (string.IsNullOrEmpty(accountHash))
		            {
		                return 0;
		            }
		        }
		
		        // ✅ Fetch account details from Schwab API
		        string url = $"{AccountsBaseUrl}/accounts/{accountHash}";
		        var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
		
		        if (accountInfo?.securitiesAccount == null)
		        {
		            UpdateStatus($"Error: No account data found for {accountNumber}.");
		            return 0;
		        }
		
		        // ✅ Sum Unrealized PnL
		        double unrealizedPnL = accountInfo.securitiesAccount.positions?.Sum(p =>
		            (double?)p.longOpenProfitLoss ?? 0 + (double?)p.shortOpenProfitLoss ?? 0) ?? 0;
		
		        UpdateStatus($"Unrealized PnL for Account {accountNumber}: {unrealizedPnL}");
		        return unrealizedPnL;
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in GetUnrealizedPnL for {accountNumber}: {ex.Message}");
		        return 0;
		    }
		}
		public async Task<double> GetRealizedPnL(string accountNumber)
		{
		    try
		    {
		        // ✅ Get the account hash from GlobalVariables or fetch it if missing
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await GetAccountHash(accountNumber);
		            if (string.IsNullOrEmpty(accountHash))
		            {
		                return 0;
		            }
		        }
		
		        // ✅ Fetch account details from Schwab API
		        string url = $"{AccountsBaseUrl}/accounts/{accountHash}";
		        var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
		
		        if (accountInfo?.securitiesAccount == null)
		        {
		            UpdateStatus($"Error: No account data found for {accountNumber}.");
		            return 0;
		        }
		
		        // ✅ Extract Realized PnL
		        double realizedPnL = accountInfo.securitiesAccount.positions?.Sum(p =>
		            (double?)p.currentDayProfitLoss ?? 0) ?? 0;
		
		        UpdateStatus($"Realized PnL for Account {accountNumber}: {realizedPnL}");
		        return realizedPnL;
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in GetRealizedPnL for {accountNumber}: {ex.Message}");
		        return 0;
		    }
		}		
		public async Task<double> GetTotalPnL(string accountNumber)
		{
		    try
		    {
		        // ✅ Get the account hash from GlobalVariables or fetch it if missing
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await GetAccountHash(accountNumber);
		            if (string.IsNullOrEmpty(accountHash))
		            {
		                return 0;
		            }
		        }
		
		        // ✅ Fetch account details from Schwab API
		        string url = $"{AccountsBaseUrl}/accounts/{accountHash}";
		        var accountInfo = await  MakeAuthorizedRequestAsync<AccountInfo>(url);
		
		        if (accountInfo?.securitiesAccount == null)
		        {
		            return 0;
		        }
		
		        // ✅ Sum Realized and Unrealized PnL
		        double realizedPnL = (double?)accountInfo.securitiesAccount.currentBalances?.cashBalance ?? 0;
		        double unrealizedPnL = accountInfo.securitiesAccount.positions?.Sum(p =>
		            (double?)p.currentDayProfitLoss ?? 0 +
		            (double?)p.longOpenProfitLoss ?? 0 +
		            (double?)p.shortOpenProfitLoss ?? 0) ?? 0;
		
		        double totalPnL = realizedPnL + unrealizedPnL;
		
		        UpdateStatus($"Total PnL for Account {accountNumber}: {totalPnL}");
		        return totalPnL;
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in GetTotalPnL for {accountNumber}: {ex.Message}.");
		        return 0;
		    }
		}
		public async Task<string> GetPositionTypeAsync(string accountNumber, string symbol)
		{
		    try
		    {
		        // ✅ Get the account hash from GlobalVariables or fetch it if missing
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash))
		        {
		            accountHash = await GetAccountHash(accountNumber);
		            if (string.IsNullOrEmpty(accountHash))
		            {
		                UpdateStatus($"Error: No hash found for account {accountNumber}.");
		                return "NONE";
		            }
		        }
		
		        // ✅ Fetch account details from Schwab API
		        string url = $"{AccountsBaseUrl}/accounts/{accountHash}?fields=positions";
		        var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
		
		        if (accountInfo?.securitiesAccount == null || accountInfo.securitiesAccount.positions == null)
		        {
		            UpdateStatus($"No positions found for account {accountNumber}.");
		            return "NONE";
		        }
		
		        // ✅ Find position for the given symbol
		        var position = accountInfo.securitiesAccount.positions
		            .FirstOrDefault(p => p.Instrument?.Symbol?.Equals(symbol, StringComparison.OrdinalIgnoreCase) == true);
		
		        if (position == null)
		        {
		            UpdateStatus($"No position found for {symbol} in account {accountNumber}.");
		            return "NONE";
		        }
		
		        UpdateStatus($"Found position for {symbol}: Market Value = {position.MarketValue}");
		
		        // ✅ Determine position type
		        return position.LongQuantity > 0 ? "LONG" :
		               position.ShortQuantity > 0 ? "SHORT" :
		               "NONE";
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in GetPositionTypeAsync for {accountNumber}, {symbol}: {ex.Message}");
		        return "ERROR";
		    }
		}	
		public class ErrorResponse
		{
		    public string code { get; set; }
		    public string error { get; set; }
		    public string message { get; set; }
		}
	        // Models
	     //   public record AccountNumber(string accountNumber, string hashValue);
	// ✅ Account Information Model
	  
		public class AccountInfo
		{
		    public SecuritiesAccount securitiesAccount { get; set; }
		
		    public class SecuritiesAccount
		    {
		        public string accountNumber { get; set; }
		        public string hashValue { get; set; }  // ✅ Include hashValue from API response
		        public AggregatedBalance aggregatedBalance { get; set; }
		        public CurrentBalances currentBalances { get; set; }
		        public InitialBalances initialBalances { get; set; }
		        public List<AccountPosition> positions { get; set; }  
		
		        public class AggregatedBalance
		        {
		            public decimal? liquidationValue { get; set; }
		        }
		
		        public class CurrentBalances
		        {
		            public decimal? cashBalance { get; set; }  
		            public decimal? unrealizedPnL { get; set; }  
		        }
		
		        public class InitialBalances
		        {
		            public decimal? cashBalance { get; set; }
		        }
		    }
		}
		
		// ✅ Account Position Model
		public class AccountPosition  
		{
		    public decimal ShortQuantity { get; set; }
		    public decimal LongQuantity { get; set; }
		    public decimal AveragePrice { get; set; }
		    public decimal MarketValue { get; set; }
		    public Instrument Instrument { get; set; }  
		
		    // ✅ Added Missing Fields
		    public decimal? currentDayProfitLoss { get; set; }          
		    public decimal? currentDayProfitLossPercentage { get; set; } 
		    public decimal? longOpenProfitLoss { get; set; }            
		    public decimal? shortOpenProfitLoss { get; set; }           
		    public decimal? currentDayCost { get; set; }                
		}
		
		// ✅ Instrument Model
		public class Instrument
		{
		    public string Cusip { get; set; }
		    public string Symbol { get; set; }  
		    public string Description { get; set; }
		    public string Type { get; set; }
		}
		
		// ✅ Model for Account Numbers & Hash Values
		public class AccountHashPair
		{
		    public string accountNumber { get; set; }
		    public string hashValue { get; set; }
		}
	}
} 