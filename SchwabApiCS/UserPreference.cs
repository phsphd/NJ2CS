// <copyright file="UserPreference.cs" company="ZPM Software Inc">
// Copyright © 2024 ZPM Software Inc. All rights reserved.
// This Source Code is subject to the terms MIT Public License
// </copyright>

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
 


namespace SchwabApiCS
{
    public partial class SchwabApi
    {
        /// <summary>
        /// Synchronously fetches user preferences.
        /// </summary>
        /// <returns>User preferences data.</returns>
        public UserPreferences GetUserPreferences()
        {
            return WaitForCompletion(GetUserPreferencesAsync());
        }

        /// <summary>
        /// Asynchronously fetches user preferences.
        /// </summary>
        /// <returns>Task containing the API response wrapper for user preferences.</returns>
		/// 
        /// <summary>
        /// Asynchronously fetches user preferences.
        /// </summary>
        /// <returns>Task containing an ApiResponseWrapper for UserPreferences.</returns>
        public async Task<ApiResponseWrapper<UserPreferences>> GetUserPreferencesAsync()
        {
            try
            {
                // Define the API endpoint for user preferences
                string url = AccountsBaseUrl + "/userPreference";

                // Fetch the user preferences from the API
                var response = await Get<UserPreferences>(url);

                // Use reflection to serialize the response for logging
                var jsonConvertType = JsonReflection.GetJsonConvertType();
                var serializeObjectMethod = jsonConvertType.GetMethod("SerializeObject", new Type[] { typeof(object) });
                if (serializeObjectMethod == null)
                {
                    throw new Exception("SerializeObject method not found in JsonConvert type.");
                }

                string serializedResponse = (string)serializeObjectMethod.Invoke(null, new object[] { response });
                NinjaTrader.Code.Output.Process($"User Preferences API Response: {serializedResponse}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);

                // Handle errors in the API response
                if (response.HasError)
                {
                    NinjaTrader.Code.Output.Process($"Error fetching user preferences: {response.ResponseText}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    return response;
                }

                // Check if the response data is null
                if (response.Data == null)
                {
                    NinjaTrader.Code.Output.Process("User preferences data is null. Check API response for issues.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    return new ApiResponseWrapper<UserPreferences>(null, true, 0, "User preferences data is null.");
                }

                // Successfully return the response
                return response;
            }
            catch (Exception ex)
            {
                // Log exceptions for debugging
                NinjaTrader.Code.Output.Process($"Exception in GetUserPreferencesAsync: {ex.Message}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                return new ApiResponseWrapper<UserPreferences>(null, true, 0, ex.Message);
            }
        }

/*	public async Task<ApiResponseWrapper<UserPreferences>> GetUserPreferencesAsync()
		{
		    try
		    {
		        // Log token state
		        if (schwabTokens == null || schwabTokens.tokens == null)
		        {
		            NinjaTrader.Code.Output.Process("Schwab tokens are not initialized.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            throw new NullReferenceException("Schwab tokens are not initialized.");
		        }
		
		        if (string.IsNullOrEmpty(schwabTokens.tokens.AccessToken))
		        {
		            NinjaTrader.Code.Output.Process("Access token is null or empty.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            throw new NullReferenceException("Access token is null or empty.");
		        }
		
		        // Fetch user preferences
		        var response = await Get<UserPreferences>(AccountsBaseUrl + "/userPreference");
		
		        if (response.HasError)
		        {
		            NinjaTrader.Code.Output.Process($"Error fetching user preferences: {response.ResponseText}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            return response;
		        }
		
		        if (response.Data == null)
		        {
		            NinjaTrader.Code.Output.Process("User preferences data is null. Check API response for issues.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            return new ApiResponseWrapper<UserPreferences>(null, true, response.ResponseCode, "User preferences data is null.");
		        }
		
		        return response;
		    }
		    catch (Exception ex)
		    {
		        NinjaTrader.Code.Output.Process($"Exception in GetUserPreferencesAsync: {ex.Message}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		        return new ApiResponseWrapper<UserPreferences>(null, true, 0, $"Exception: {ex.Message}", ex);
		    }
		}

	 		public async Task<ApiResponseWrapper<UserPreferences>> GetUserPreferencesAsync()
		{
		    try
		    {
		        // Fetch user preferences from the API
		        var response = await Get<UserPreferences>(AccountsBaseUrl + "/userPreference");
		
		        // Serialize and log the raw API response for debugging
		        if (response != null)
		        {
		            var jsonConvertType = JsonReflection.GetJsonConvertType();
		            var serializeObjectMethod = jsonConvertType?.GetMethod("SerializeObject", new Type[] { typeof(object) });
		            var serializedResponse = serializeObjectMethod != null
		                ? (string)serializeObjectMethod.Invoke(null, new object[] { response })
		                : "Serialization failed: JsonConvert type or method not found.";
		
		            NinjaTrader.Code.Output.Process($"User Preferences API Response: {serializedResponse}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		        }
		
		        // Check if the response has an error
		        if (response.HasError)
		        {
		            NinjaTrader.Code.Output.Process($"Error fetching user preferences: {response.ResponseText}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            return response;
		        }
		
		        // Validate the response data
		        if (response.Data == null)
		        {
		            NinjaTrader.Code.Output.Process("User preferences data is null. Check API response for issues.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            return new ApiResponseWrapper<UserPreferences>(null, true, response.ResponseCode, "User preferences data is null.");
		        }
		
		        // Validate mandatory fields in UserPreferences (e.g., accounts)
		        if (response.Data.accounts == null || response.Data.accounts.Count == 0)
		        {
		            NinjaTrader.Code.Output.Process("User preferences returned, but accounts list is empty or null.", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		            return new ApiResponseWrapper<UserPreferences>(null, true, response.ResponseCode, "Accounts list is empty or null.");
		        }
		
		        // Successful response
		        return response;
		    }
		    catch (Exception ex)
		    {
		        // Log and return the exception as part of the response
		        NinjaTrader.Code.Output.Process($"Exception in GetUserPreferencesAsync: {ex.Message}", NinjaTrader.NinjaScript.PrintTo.OutputTab1);
		        return new ApiResponseWrapper<UserPreferences>(null, true, 0, $"Exception: {ex.Message}", ex);
		    }
		}
*/
        /// <summary>
        /// Represents user preferences data returned by the Schwab API.
        /// </summary>
        public class UserPreferences
        {
            public List<Account> accounts { get; set; }
            public List<StreamerInfo> streamerInfo { get; set; }
            public List<Offer> offers { get; set; }

            /// <summary>
            /// Represents an account in the user preferences.
            /// </summary>
            public class Account
            {
                public string accountNumber { get; set; }
                public bool primaryAccount { get; set; }
                public string type { get; set; }
                public string nickName { get; set; }
                public string displayAcctId { get; set; }
                public bool autoPositionEffect { get; set; }
                public string accountColor { get; set; }

                public override string ToString()
                {
                    return $"{accountNumber} {nickName} {type}";
                }
            }

            /// <summary>
            /// Represents an offer in the user preferences.
            /// </summary>
            public class Offer
            {
                public bool level2Permissions { get; set; }
                public string mktDataPermission { get; set; }
            }

            /// <summary>
            /// Represents streamer information in the user preferences.
            /// </summary>
            public class StreamerInfo
            {
                public string streamerSocketUrl { get; set; }
                public string schwabClientCustomerId { get; set; }
                public string schwabClientCorrelId { get; set; }
                public string schwabClientChannel { get; set; }
                public string schwabClientFunctionId { get; set; }
            }
        }
    }
}