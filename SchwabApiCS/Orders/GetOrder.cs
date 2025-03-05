// <copyright file="GetOrder.cs" company="ZPM Software Inc">
// Copyright © 2024 ZPM Software Inc. All rights reserved.
// This Source Code is subject to the terms MIT Public License
// </copyright>
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NinjaTrader.NinjaScript.AddOns;
namespace SchwabApiCS
{
    public partial class SchwabApi
    {
        // ========================= Get Orders ================================

        /// <summary>
        /// Get orders for all accounts
        /// All orders placed in the date range *OR* open during the date range
        /// If you want today's orders, use DateTime.Now, or DateTime.Today.AddDays(1)
        /// </summary>
        /// <param name="apiClient"></param>
        /// <returns></returns>
        public IList<Order> GetOrders(DateTime fromDate, DateTime toDate, Order.Status? status = null, int? maxResults = null)
        {
            return WaitForCompletion(GetOrdersAsync(fromDate, toDate, status, maxResults));
        }

        /// <summary>
        /// Get orders for all accounts async
        /// All orders placed in the date range *OR* open during the date range
        /// If you want today's orders, use DateTime.Now, or DateTime.Today.AddDays(1)
        /// </summary>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <returns></returns>
		public async Task<ApiResponseWrapper<IList<Order>>> GetOrdersAsync(DateTime fromDate, DateTime toDate,
		                                                                   Order.Status? status = null, int? maxResults = null)
		{
		    try
		    {
		        // Convert dates to UTC formatted strings
		        string fDate = fromDate.ToUniversalTime().ToString(utcDateFormat);
		        string tDate = toDate.ToUniversalTime().ToString(utcDateFormat);
		      //  NJ2CSLogManager.LogMessage($"[GetOrdersAsync] fromDate (UTC): {fDate}, toDate (UTC): {tDate}");
		
		        // Build query parameters
		        string parms = $"fromEnteredTime={fDate}&toEnteredTime={tDate}";
		        if (status != null)
		        {
		            parms += "&status=" + status.ToString();
		        //    NJ2CSLogManager.LogMessage($"[GetOrdersAsync] Added status parameter: {status}");
		        }
		        if (maxResults != null)
		        {
		            parms += "&maxResults=" + maxResults.ToString();
		         //   NJ2CSLogManager.LogMessage($"[GetOrdersAsync] Added maxResults parameter: {maxResults}");
		        }
		
		        // Construct URL
		        string url = OrdersBaseUrl + "/orders?" + parms;
		        NJ2CSLogManager.LogMessage($"[GetOrdersAsync] Constructed URL: {url}");
		
		        // Make the GET request
		        var result = await Get<IList<Order>>(url);
		
		        // Log the raw API response text if available
		        if (result != null)
		        {
		            NJ2CSLogManager.LogMessage($"[GetOrdersAsync] API Response: {result.ResponseText}");
		        }
		        else
		        {
		            NJ2CSLogManager.LogMessage("[GetOrdersAsync] No response received from API.");
		        }
		
		        return result;
		    }
		    catch (Exception ex)
		    {
		        NJ2CSLogManager.LogMessage($"[GetOrdersAsync] Exception: {ex.Message}");
		        throw;
		    }
		}



        /// <summary>
        /// Get Orders for a specific account
        /// All orders placed in the date range *OR* open during the date range
        /// If you want today's orders, use DateTime.Now, or DateTime.Today.AddDays(1) 
        /// </summary>
        /// <param name="accountNumber">account number or accountNumberHash</param>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <param name="status">null or single status</param>
        /// <returns></returns>
        public IList<Order> GetOrders(string accountNumber, DateTime fromDate, DateTime toDate,
                                      Order.Status? status = null, int? maxResults = null)
        {
            return WaitForCompletion(GetOrdersAsync(accountNumber, fromDate, toDate, status, maxResults));
        }

        /// <summary>
        /// Get Orders for a specific account async
        /// All orders placed in the date range *OR* open during the date range
        /// If you want today's orders, use DateTime.Now, or DateTime.Today.AddDays(1) 
        /// </summary>
        /// <param name="accountNumber">account number or accountNumberHash</param>
        /// <param name="fromDate">required</param>
        /// <param name="toDate">required</param>
        /// <param name="status">optional or single status</param>
        /// <returns></returns>
        public async Task<ApiResponseWrapper<IList<Order>>> GetOrdersAsync(string accountNumber, DateTime fromDate, DateTime toDate,
                                                                           Order.Status? status = null, int? maxResults=null)
        {
            string fDate = fromDate.ToUniversalTime().ToString(utcDateFormat);
            string tDate = toDate.ToUniversalTime().ToString(utcDateFormat);
            string parms = $"fromEnteredTime={fDate}&toEnteredTime={tDate}";

            if (status != null)
                parms += "&status=" + status.ToString();
            if (maxResults != null)
                parms += "&maxResults=" + maxResults.ToString();
            var t = await Get<IList<Order>>(OrdersBaseUrl + "/accounts/" + GetAccountNumberHash(accountNumber) + "/orders?" + parms);
            return t;
        }
      	public async Task<ApiResponseWrapper<IList<Order>>> GetOrdersByHashAsync(string accountHash, DateTime fromDate, DateTime toDate,
                                                                           Order.Status? status = null, int? maxResults=null)
        {
            string fDate = fromDate.ToUniversalTime().ToString(utcDateFormat);
            string tDate = toDate.ToUniversalTime().ToString(utcDateFormat);
            string parms = $"fromEnteredTime={fDate}&toEnteredTime={tDate}";

            if (status != null)
                parms += "&status=" + status.ToString();
            if (maxResults != null)
                parms += "&maxResults=" + maxResults.ToString();
            var t = await Get<IList<Order>>(OrdersBaseUrl + "/accounts/" + accountHash + "/orders?" + parms);
            return t;
        }


        /// <summary>
        /// Get Order a specific order
        /// </summary>
        /// <param name="accountNumber">account number or accountNumberHash</param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public Order GetOrder(string accountNumber, long orderId)
        {
            return WaitForCompletion(GetOrderAsync(accountNumber, orderId));
        }

        /// <summary>
        /// Get Order a specific order async
        /// </summary>
        /// <param name="accountNumber">account number or accountNumberHash</param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<ApiResponseWrapper<Order>> GetOrderAsync(string accountNumber, long orderId)
        {
            return await Get<Order>(OrdersBaseUrl + "/accounts/" + GetAccountNumberHash(accountNumber) + "/orders/" + orderId.ToString());
        }
    }
}
