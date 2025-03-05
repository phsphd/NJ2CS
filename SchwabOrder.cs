using System;
using System.Collections.Generic;

using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using NinjaTrader.NinjaScript.AddOns;
using System.Net.Http;
using System.Net.Http.Headers;
using NinjaTrader.NinjaScript;
using System.Globalization;
using SchwabApiCS;
namespace SchwabApiCS
{
    public partial class SchwabApi
    {
		private DateTime expirationDate = new DateTime(2025, 3, 19);
        /// <summary>
        /// Places an order using the Schwab API.
        /// If the response body is empty, it checks the Location header to extract the order ID.
        /// </summary>
        /// <param name="accountHash">The encrypted account hash obtained from the accountNumbers service.</param>
        /// <param name="symbol">The trading symbol (e.g., "NVDA").</param>
        /// <param name="action">Buy or Sell action.</param>
        /// <param name="quantity">Number of shares.</param>
        /// <param name="orderType">Order type: "LIMIT", "STOP", or "MARKET".</param>
        /// <param name="limitPrice">Limit price if applicable.</param>
        /// <param name="takeProfitPoints">Optional take-profit points (not used in this simple example).</param>
        /// <param name="stopLossPoints">Optional stop-loss points (not used in this simple example).</param>
        /// <returns>An ApiResponseWrapper containing the order ID if successful.</returns>
        public async Task<ApiResponseWrapper<long?>> PlaceOrders(
            string accountHash,
            string symbol,
            OrderAction action,
            int quantity,
            string orderType = "LIMIT",
            decimal? limitPrice = null,
            decimal? takeProfitPoints = null,
            decimal? stopLossPoints = null
        )
        {
		    // Check if the software is expired.
		    if (DateTime.Today > expirationDate)
		    {
		        UpdateStatus("Software expired.");
		        return new ApiResponseWrapper<long?>(null, true, 403, "Software expired.");
		    }
            try
            {
                // Validate inputs
                if (SchwabApi.Instance == null)
                {
                    UpdateStatus("❌ ERROR: Schwab API instance is NULL.");
                    return new ApiResponseWrapper<long?>(null, true, 500, "Schwab API instance is NULL.");
                }
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    UpdateStatus("❌ ERROR: Symbol cannot be empty.");
                    return new ApiResponseWrapper<long?>(null, true, 400, "Symbol cannot be empty.");
                }
                if (quantity <= 0)
                {
                    UpdateStatus("❌ ERROR: Order quantity must be greater than zero.");
                    return new ApiResponseWrapper<long?>(null, true, 400, "Order quantity must be greater than zero.");
                }
                if (string.IsNullOrWhiteSpace(accountHash))
                {
                    UpdateStatus("❌ ERROR: Account hash is required.");
                    return new ApiResponseWrapper<long?>(null, true, 400, "Account hash is required.");
                }

                // Get a valid access token
                string accessToken = await SchwabTokens.Instance.GetValidAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    UpdateStatus("❌ ERROR: Could not retrieve a valid access token.");
                    return new ApiResponseWrapper<long?>(null, true, 401, "Could not retrieve a valid access token.");
                }

                // Upper-case order type for consistency
                string upperOrderType = orderType.ToUpper();
                if ((upperOrderType == "LIMIT" || upperOrderType == "STOP") && (!limitPrice.HasValue || limitPrice.Value <= 0))
                {
                    UpdateStatus("❌ ERROR: Limit price must be set for limit or stop orders.");
                    return new ApiResponseWrapper<long?>(null, true, 400, "Limit price must be set for limit or stop orders.");
                }

                // Construct order payload session	sessionstring Enum:[ NORMAL, AM, PM, SEAMLESS ]


                var orderPayload = new Dictionary<string, object>
                {
                    { "session", "SEAMLESS" },
                    { "duration", "DAY" },
                    { "orderType", upperOrderType },
                    { "orderStrategyType", "SINGLE" },
                    { "orderLegCollection", new[]
                        {
                            new
                            {
                                orderLegType = "EQUITY",
                                instrument = new
                                {
                                    symbol = symbol,
                                    assetType = "EQUITY"  // API expects "assetType"
                                },
                                instruction = action == OrderAction.Buy ? "BUY" : "SELL",
                                positionEffect = "OPENING",
                                quantity = quantity
                            }
                        }
                    }
                };

                // Include limit price if needed
                if (upperOrderType == "LIMIT" || upperOrderType == "STOP")
                {
                    orderPayload["price"] = limitPrice;
                }

                // (Optional) If you need to add child orders for take profit/stop loss,
                // you could add a "childOrderStrategies" element here.

                // Serialize payload to JSON
                string jsonPayload = JsonConvert.SerializeObject(orderPayload, Formatting.Indented);
                UpdateStatus($"📜 Order JSON Sent: {jsonPayload}");

                // Build API URL
                string apiUrl = $"https://api.schwabapi.com/trader/v1/accounts/{accountHash}/orders";

                // Send POST request
                var postResponse = await MakePostRequest(apiUrl, jsonPayload, accessToken);

                // Check response
                if (postResponse == null || string.IsNullOrWhiteSpace(postResponse.Data))
                {
                    // If body is empty, try to extract order ID from Location header
                    if (postResponse != null && postResponse.ResponseMessage?.Headers.Location != null)
                    {
                        string location = postResponse.ResponseMessage.Headers.Location.ToString();
                        UpdateStatus($"✅ Extracted Location header: {location}");
                        string[] tokens = location.Split('/');
                        string orderIdStr = tokens[tokens.Length - 1];
                        if (long.TryParse(orderIdStr, out long orderIdFromLocation))
                        {
                            return new ApiResponseWrapper<long?>(orderIdFromLocation, false, 200, $"✅ Order placed successfully (via Location header): {orderIdFromLocation}");
                        }
                    }
                    UpdateStatus("❌ ERROR: Empty response from API. Order may not have been processed.");
                    return new ApiResponseWrapper<long?>(null, true, 500, "❌ ERROR: Empty response from API.");
                }

                // Log full API response
                UpdateStatus($"📜 Full API Response: {postResponse.Data}");

                // Try to parse order ID from response body
                try
                {
                    var orderResponse = JsonConvert.DeserializeObject<dynamic>(postResponse.Data);
                    // Try common keys: "orderId" or "id"
                    long? orderId = orderResponse?.orderId ?? orderResponse?.id;
                    string status = orderResponse?.status ?? "UNKNOWN";
                    UpdateStatus($"📌 Order Status: {status}");

                    if (orderId == null)
                    {
                        UpdateStatus($"⚠️ DEBUG: Full Response JSON: {JsonConvert.SerializeObject(orderResponse, Formatting.Indented)}");
                        return new ApiResponseWrapper<long?>(null, true, 500, $"❌ ERROR: Order ID missing from response. Order Status: {status}");
                    }

                    return new ApiResponseWrapper<long?>(orderId, false, 200, $"✅ Order placed successfully: {orderId}");
                }
                catch (Exception jsonEx)
                {
                    return new ApiResponseWrapper<long?>(null, true, 500, $"❌ ERROR: Failed to parse order response: {jsonEx.Message}");
                }
            }
            catch (Exception ex)
            {
                return new ApiResponseWrapper<long?>(null, true, 500, $"❌ CRITICAL ERROR: {ex.Message}");
            }
        }
		/// <summary>
		/// Protects an existing position by canceling any existing OCO orders for the symbol,
		/// then placing a new OCO bracket order to protect the position. The plain account number
		/// is used only for logging; the encrypted account hash is used for order placement.
		/// </summary>
		public async Task ProtectExistingPositionAsync(string plainAccountNumber, string accountHash, string symbol, AccountPosition position, decimal takeProfitPoints, decimal stopLossPoints)
		{
		    try
		    {
		        UpdateStatus($"[OCO] Starting protection for symbol {symbol} on account {plainAccountNumber} (hash: {accountHash}).");
		
		        // 1. Cancel any existing OCO orders for this symbol.
		        DateTime fromTime = DateTime.UtcNow.AddDays(-1);
		        DateTime toTime = DateTime.UtcNow.AddDays(1);
		        var ordersResponse = await SchwabApi.Instance.GetOrdersByHashAsync(accountHash, fromTime, toTime, Order.Status.WORKING);
		        UpdateStatus($"[OCO] Retrieved orders. HasError: {ordersResponse.HasError}.");
		        if (!ordersResponse.HasError && ordersResponse.Data != null)
		        {
		            var existingOcoOrders = ordersResponse.Data
		                .Where(o => o.orderStrategyType == Order.OrderStrategyTypes.OCO.ToString() &&
		                            o.orderLegCollection != null &&
		                            o.orderLegCollection.Any(ol => !string.IsNullOrEmpty(ol.instrument?.symbol) &&
		                                ol.instrument.symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
		                .ToList();
		            UpdateStatus($"[OCO] Found {existingOcoOrders.Count} existing OCO orders for {symbol}.");
		            foreach (var oco in existingOcoOrders)
		            {
		                if (oco.orderId.HasValue)
		                {
		                    await SchwabApi.Instance.CancelOrderAsync(plainAccountNumber, oco.orderId.Value);
		                    UpdateStatus($"[OCO] Cancelled existing OCO order: {oco.orderId}");
		                }
		            }
		        }
		        else
		        {
		            UpdateStatus("[OCO] No open orders found for cancellation.");
		        }
		
		        // 2. Determine the net position quantity.
		        int netQuantity = (int)(position.LongQuantity - position.ShortQuantity);
		        if (netQuantity == 0)
		        {
		            UpdateStatus($"No net position for {symbol} to protect.");
		            return;
		        }
		        // For a long position (netQuantity > 0) we want to exit by selling;
		        // for a short position (netQuantity < 0) we want to cover by buying.
		        OrderAction protectionAction = netQuantity > 0 ? OrderAction.Sell : OrderAction.Buy;
				UpdateStatus($"Ticker {symbol} has {netQuantity} position, OCO order action {protectionAction}.");
		        // 3. Retrieve the current market price.
		        var quoteResponse = await SchwabApi.Instance.GetQuoteAsync(symbol, "quote");
		        decimal currentPrice = quoteResponse.Data?.quote?.lastPrice ?? 0;
		        if (currentPrice == 0)
		        {
		            UpdateStatus("❌ Failed to retrieve current market price; cannot place protection orders.");
		            return;
		        }
		        UpdateStatus($"[OCO] Current market price for {symbol}: {currentPrice}");
		
		        // 4. Calculate take profit and stop loss prices.
		 
		
		
		
		        // 5. Place the OCO protection order.
		        var ocoResponse = await PlaceOrderWithOCO(
		            plainAccountNumber,
		            accountHash,
		            symbol,
		            protectionAction,
		            netQuantity,
		            currentPrice,
		            takeProfitPoints,
		            stopLossPoints,
		            "Limit"
		        );
		
		        UpdateStatus($"[OCO] OCO protection order response: {ocoResponse.Message}");
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error protecting existing position: {ex.Message}");
		    }
		}
		
		/// <summary>
		/// Places an OCO order to protect an existing position by creating two child orders:
		/// a LIMIT order for take profit and a STOP order for stop loss.
		/// The payload does NOT include an accountNumber field; the encrypted account hash is used in the URL.
		/// The plain account number is used only for logging.
		/// </summary>
		public async Task<ApiResponseWrapper<long?>> PlaceOrderWithOCO(
		    string plainAccountNumber,    // For logging purposes only.
		    string accountHash,           // Encrypted account hash used for order placement (in URL).
		    string symbol,
		    OrderAction action,
		    int netQuantity,
		    decimal limitPrice,
		    decimal takeProfitPoints,
		    decimal stopLossPoints,
		    string orderType = "Market")
		{
		    try
		    {
		
		 
		        Order.Session session = Order.Session.NORMAL;
		        Order.Duration duration = Order.Duration.DAY;
		
		        // Determine child order instructions based on protection action.
		        Order.Instruction tpInstruction;
		        Order.Instruction slInstruction;
		        if (action == OrderAction.Sell)
		        {
		            // For a long position that is being closed, use SELL instructions.
		            tpInstruction = Order.Instruction.SELL;
		            slInstruction = Order.Instruction.SELL;
		        }
		        else
		        {
		            // For a short position that is being covered, use BUY_TO_COVER.
		            tpInstruction = Order.Instruction.BUY_TO_COVER;
		            slInstruction = Order.Instruction.BUY_TO_COVER;
		        }
				decimal takeProfitPrice, stopLossPrice;
				if (netQuantity > 0) // Long position
				{
				    takeProfitPrice = Math.Round(limitPrice + takeProfitPoints, 2);
				    stopLossPrice = Math.Round(limitPrice - stopLossPoints, 2);
				}
				else if (netQuantity < 0) // Short position
				{
				    takeProfitPrice = Math.Round(limitPrice - takeProfitPoints, 2);
				    stopLossPrice = Math.Round(limitPrice + stopLossPoints, 2);
				}
				else
				{
				    throw new InvalidOperationException("No net position to protect.");
				}
				int quantity=Math.Abs(netQuantity);
		       	UpdateStatus($"Placing OCO Order: {action} {quantity} {symbol} at {limitPrice} | action {tpInstruction }, TP: {takeProfitPrice}, SL: {stopLossPrice}");
		
		        // Create the take profit child order (LIMIT order).
		        var takeProfitOrder = new Order.ChildOrderStrategy(Order.OrderType.LIMIT, Order.OrderStrategyTypes.SINGLE, session, duration, takeProfitPrice);
		        takeProfitOrder.Add(new Order.OrderLeg(symbol, Order.AssetType.EQUITY, tpInstruction, quantity));
		
		        // Create the stop loss child order (STOP order).
		        var stopLossOrder = new Order.ChildOrderStrategy(Order.OrderType.STOP, Order.OrderStrategyTypes.SINGLE, session, duration, stopLossPrice);
		        stopLossOrder.Add(new Order.OrderLeg(symbol, Order.AssetType.EQUITY, slInstruction, quantity));
		
		        // Create the OCO order without setting accountNumber in the payload.
		        var ocoOrder = new Order()
		        {
		            orderStrategyType = Order.OrderStrategyTypes.OCO.ToString()
		        };
		        ocoOrder.Add(takeProfitOrder);
		        ocoOrder.Add(stopLossOrder);
		
		        // Log the JSON payload for debugging.
		        string jsonPayload = JsonConvert.SerializeObject(ocoOrder, Formatting.Indented);
		      //  UpdateStatus($"[OCO] Order JSON Sent: {jsonPayload}");
				var orderResponse = await OrderExecuteHashAsync(accountHash, ocoOrder);
		        if (orderResponse == null || orderResponse.HasError)
		        {
		            UpdateStatus($"❌ ERROR: Failed to place OCO order: {orderResponse?.ResponseText}");
		            return new ApiResponseWrapper<long?>(null, true, orderResponse?.ResponseCode ?? 500, $"Error placing OCO order: {orderResponse?.ResponseText}");
		        }
		
		        long? orderId = orderResponse.RawData;
		        if (orderId == null)
		        {
		            UpdateStatus("❌ ERROR: Order ID missing from response.");
		            return new ApiResponseWrapper<long?>(null, true, 500, "Order ID missing from response.");
		        }
		
		        UpdateStatus($"✅ Order placed successfully: {orderId}");
		        return new ApiResponseWrapper<long?>(orderId, false, 200, $"Order placed successfully: {orderId}");
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ ERROR: Failed to place OCO order: {ex.Message}");
		        return new ApiResponseWrapper<long?>(null, true, 500, $"Error placing OCO order: {ex.Message}");
		    }
		}
		  
		public async Task<ApiResponseWrapper<long?>> PlaceOrderWithOCOOld(
		    string accountHash, string symbol, OrderAction action, int quantity, 
		    decimal limitPrice, decimal takeProfitPoints, decimal stopLossPoints, string orderType = "Market"
		)
		{
		    ApiResponseWrapper<long?> orderResponse = null; // ✅ Ensure orderResponse is accessible in catch
		
		    try
		    {
		        // ✅ Calculate Take Profit & Stop Loss dynamically
		        decimal takeProfit = action == SchwabApiCS.OrderAction.Buy 
		            ? limitPrice + takeProfitPoints 
		            : limitPrice - takeProfitPoints;
		
		        decimal stopLoss = action == SchwabApiCS.OrderAction.Buy 
		            ? limitPrice - stopLossPoints 
		            : limitPrice + stopLossPoints;
		
		        UpdateStatus($"Placing OCO Order: {action} {quantity} {symbol} at {limitPrice} | TP: {takeProfit}, SL: {stopLoss}");
		
		        // ✅ Convert order type
		        Order.OrderType orderTypeEnum = orderType.ToUpper() switch
		        {
		            "LIMIT" => Order.OrderType.LIMIT,
		            "STOP" => Order.OrderType.STOP,
		            _ => Order.OrderType.MARKET
		        };
		
		        Order.Session session = Order.Session.NORMAL;
		        Order.Duration duration = Order.Duration.DAY;
		
		        if (!Enum.TryParse<OrderAction>(action.ToString(), true, out OrderAction parsedAction))
		        {
		            throw new ArgumentException($"Invalid OrderAction value: {action}");
		        }
		
		        Order.Position position = parsedAction == OrderAction.Buy ? Order.Position.TO_OPEN : Order.Position.TO_CLOSE;
		
		        // ✅ **Create Take Profit Order**
		        var takeProfitOrder = new Order.ChildOrderStrategy(Order.OrderType.LIMIT, Order.OrderStrategyTypes.SINGLE, session, duration, takeProfit);
		        takeProfitOrder.Add(new Order.OrderLeg(symbol, Order.AssetType.EQUITY, position, quantity));
		
		        // ✅ **Create Stop Loss Order**
		        var stopLossOrder = new Order.ChildOrderStrategy(Order.OrderType.STOP, Order.OrderStrategyTypes.SINGLE, session, duration, stopLoss);
		        stopLossOrder.Add(new Order.OrderLeg(symbol, Order.AssetType.EQUITY, position, quantity));
		
		        // ✅ **Create OCO Order (One-Cancels-Other)**
		        var ocoOrder = new Order()
		        {
		            orderStrategyType = Order.OrderStrategyTypes.OCO.ToString(),
		        };
		        ocoOrder.Add(takeProfitOrder);
		        ocoOrder.Add(stopLossOrder);
		
		        // ✅ Execute the OCO Order
		        orderResponse = await OrderExecuteNewAsync(accountHash, ocoOrder);
		
		        // ✅ Return the actual response
		        return orderResponse;
		    }
		    catch (Exception ex)
		    {
		        // ✅ Properly handle errors and return a meaningful response
		        return new ApiResponseWrapper<long?>(null, true, 500, $"Error placing OCO order: {ex.Message}");
		    }
		}
		/// <summary>
		/// Protects an existing position by first canceling any working OCO orders for the symbol,
		/// then placing new OCO protection orders using current market price.
		/// </summary>
 
		/// <summary>
		/// Places a new entry order for the symbol and then attaches OCO protection orders.
		/// </summary>
		public async Task PlaceEntryOrderWithProtectionAsync(string accountNumber, string accountHash, string symbol, int quantity, decimal limitPrice, decimal takeProfitPoints, decimal stopLossPoints)
		{
		    try
		    {
		        // Place entry order.
		        var entryResponse = await SchwabApi.Instance.PlaceOrders(
		            accountHash,
		            symbol,
		            OrderAction.Buy,
		            quantity,
		            "Limit",
		            limitPrice
		        );
		        if (entryResponse == null || entryResponse.HasError)
		        {
		            UpdateStatus($"❌ Entry order failed: {entryResponse?.Message}");
		            return;
		        }
		        UpdateStatus("Entry order placed successfully.");
		
		        // Wait briefly to allow position registration.
		        await Task.Delay(2000);
		
		        // Get current market price for the symbol.
		        var quoteResponse = await SchwabApi.Instance.GetQuoteAsync(symbol, "quote");
		        decimal currentPrice = quoteResponse.Data?.quote?.lastPrice ?? limitPrice;
		
		        // Place OCO protection orders.
		        var ocoResponse = await SchwabApi.Instance.PlaceOrderWithOCO(
					accountNumber,
		            accountHash,
		            symbol,
		            OrderAction.Buy,
		            quantity,
		            currentPrice,
		            takeProfitPoints,
		            stopLossPoints,
		            "Limit"
		        );
		        UpdateStatus($"OCO protection orders placed. Response: {ocoResponse.Message}");
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"❌ Error placing entry order with protection: {ex.Message}");
		    }
		}
           /// <summary>
        /// Closes all open positions and cancels all OCO orders for the specified account.
        /// The accountNumber parameter must be the plain account number.
        /// </summary>
        /// <param name="accountNumber">The plain account number (e.g. "81444650").</param>
        /// <param name="reason">A reason for closing positions (for logging).</param>
        public async Task CloseAllPositionsAndOCOs(string accountNumber, string reason)
		{
		    try
		    {
		        // Use the globally stored account hash.
		        if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash) ||
		            string.IsNullOrWhiteSpace(accountHash))
		        {
		            UpdateStatus($"❌ ERROR: Account number {accountNumber} not found in global accounts.");
		            return;
		        }
		        UpdateStatus($"Using account hash: {accountHash} for account: {accountNumber}");
		
		        // --- 1. Cancel any open orders (WORKING orders and OCO orders) ---
		        // Get all orders for the account over a recent date range.
		        var ordersResponse = await GetOrdersAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
		        if (!ordersResponse.HasError && ordersResponse.Data != null)
		        {
		            foreach (var order in ordersResponse.Data)
		            {
		                // Check for orders with status "WORKING" or OCO orders.
		                if (order.cancelable == true && order.orderId.HasValue)
		                {
		                    if (!string.IsNullOrEmpty(order.status) &&
		                        order.status.Equals("WORKING", StringComparison.OrdinalIgnoreCase))
		                    {
		                        bool cancelled = await SchwabApi.Instance.CancelOrderAsync(accountHash, order.orderId.Value);
		                        if (cancelled)
		                        {
		                            UpdateStatus($"Cancelled working order: {order.orderId}");
		                        }
		                        else
		                        {
		                            UpdateStatus($"Failed to cancel working order: {order.orderId}");
		                        }
		                    }
		                    else if (!string.IsNullOrEmpty(order.orderStrategyType) &&
		                        order.orderStrategyType.Equals(Order.OrderStrategyTypes.OCO.ToString(), StringComparison.OrdinalIgnoreCase))
		                    {
		                        bool cancelled = await SchwabApi.Instance.CancelOrderAsync(accountHash, order.orderId.Value);
		                        if (cancelled)
		                        {
		                            UpdateStatus($"Cancelled OCO order: {order.orderId}");
		                        }
		                        else
		                        {
		                            UpdateStatus($"Failed to cancel OCO order: {order.orderId}");
		                        }
		                    }
		                }
		            }
		        }
		        else
		        {
		            UpdateStatus("No open orders found or error retrieving orders.");
		        }
		
		        // --- 2. Flatten open positions ---
		        // Fetch open positions using the account hash.
		        var positions = await GetPositionsAsync(accountNumber, accountHash);
		        if (positions == null || positions.Count == 0)
		        {
		            UpdateStatus("No open positions to close.");
		            return;
		        }
		
		        // For each position, calculate the net quantity and send a market order to flatten the position.
		        List<Task<ApiResponseWrapper<long?>>> closeOrderTasks = new List<Task<ApiResponseWrapper<long?>>>();
		        foreach (var position in positions)
		        {
		            // Calculate net quantity: LongQuantity - ShortQuantity.
		            var netQuantity = position.LongQuantity - position.ShortQuantity;
		            var symbol = position.Instrument?.Symbol;
		            if (string.IsNullOrEmpty(symbol))
		            {
		                UpdateStatus("Skipping position due to missing symbol.");
		                continue;
		            }
		
		            if (netQuantity != 0)
		            {
		                // To flatten, send a market order in the opposite direction.
		                var orderTask = OrderSingleAsync(
		                    accountHash,
		                    symbol,
		                    Order.AssetType.EQUITY,
		                    Order.OrderType.MARKET,
		                    Order.Session.NORMAL,
		                    Order.Duration.DAY,
		                    Order.Position.TO_CLOSE,
		                    -netQuantity, // negative quantity to close the existing position
		                    null
		                );
		                closeOrderTasks.Add(orderTask);
		            }
		        }
		
		        var closeResults = await Task.WhenAll(closeOrderTasks);
		        foreach (var result in closeResults)
		        {
		            if (result.HasError)
		                UpdateStatus($"Failed to close position: {result.Message}");
		            else
		                UpdateStatus($"Successfully closed position: {result.Data}");
		        }
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error closing positions and orders: {ex.Message}");
		    }
		}


        /// <summary>
        /// Cancels all open orders and then flattens (closes) all open positions for the specified account.
        /// The accountNumber parameter must be the plain account number.
        /// </summary>
        public async Task<ApiResponseWrapper<bool>> CloseAllPositionsAndOCOsAsync1(string accountNumber, string reason)
        {
            try
            {
                // --- 1. Cancel Open Orders ---
                DateTime fromDate = DateTime.UtcNow.AddDays(-1);
                DateTime toDate = DateTime.UtcNow.AddDays(1);
                var ordersResponse = await GetOrdersAsync(accountNumber, fromDate, toDate, Order.Status.WORKING);
                if (ordersResponse.HasError)
                {
                    return new ApiResponseWrapper<bool>(
                        false,
                        true,
                        ordersResponse.ResponseCode,
                        ordersResponse.Message,
                        null
                    );
                }

                if (ordersResponse.Data != null)
                {
                    foreach (var order in ordersResponse.Data)
                    {
                        if (order.cancelable == true && order.orderId.HasValue)
                        {
                            await OrderExecuteDeleteAsync(accountNumber, order.orderId.Value);
                        }
                    }
                }

                // --- 2. Flatten Open Positions ---
                // Get the account hash from GlobalVariables using the plain account number.
                if (!GlobalVariables.csAccounts.TryGetValue(accountNumber, out string accountHash) ||
                    string.IsNullOrWhiteSpace(accountHash))
                {
                    return new ApiResponseWrapper<bool>(
                        false,
                        true,
                        404,
                        $"Account number {accountNumber} not found in global accounts.",
                        null
                    );
                }

                string url = $"{AccountsBaseUrl}/accounts/{accountHash}?fields=positions";
                UpdateStatus($"Fetching account positions from: {url}");
                var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
                if (accountInfo?.securitiesAccount?.positions == null)
                {
                    UpdateStatus($"No positions found for account {accountNumber}");
                    return new ApiResponseWrapper<bool>(
                        true,
                        false,
                        200,
                        "No positions found.",
                        null
                    );
                }

                foreach (var pos in accountInfo.securitiesAccount.positions)
                {
                    string symbol = pos.Instrument.Symbol;
                    Order.AssetType assetType;
                    try
                    {
                        assetType = Order.GetAssetType(pos.Instrument.Type);
                    }
                    catch
                    {
                        assetType = Order.AssetType.EQUITY;
                    }

                    if (pos.LongQuantity > 0)
                    {
                        await OrderSingleAsync(
                            accountHash,
                            symbol,
                            assetType,
                            Order.OrderType.MARKET,
                            Order.Session.NORMAL,
                            Order.Duration.DAY,
                            Order.Position.TO_CLOSE,
                            -pos.LongQuantity,
                            null
                        );
                    }
                    else if (pos.ShortQuantity > 0)
                    {
                        await OrderSingleAsync(
                            accountHash,
                            symbol,
                            assetType,
                            Order.OrderType.MARKET,
                            Order.Session.NORMAL,
                            Order.Duration.DAY,
                            Order.Position.TO_CLOSE,
                            pos.ShortQuantity,
                            null
                        );
                    }
                }

                return new ApiResponseWrapper<bool>(
                    true,
                    false,
                    200,
                    "Account flattened successfully.",
                    null
                );
            }
            catch (Exception ex)
            {
                return new ApiResponseWrapper<bool>(
                    false,
                    true,
                    500,
                    $"Error closing positions and OCOs: {ex.Message}",
                    null
                );
            }
        }

        /// <summary>
        /// Sends a DELETE request to cancel all order in an account.
        /// </summary>
		public async Task<bool> CancelAllOrdersByHash(string accountHash)
		{
		    try
		    {
		        // Calculate the time range (last 24 hours)
		        DateTime fromTime = DateTime.UtcNow.AddHours(-24);
		        DateTime toTime = DateTime.UtcNow;
		        string fromTimeStr = Uri.EscapeDataString(fromTime.ToString("o", CultureInfo.InvariantCulture));
		        string toTimeStr = Uri.EscapeDataString(toTime.ToString("o", CultureInfo.InvariantCulture));
		        
		        // Build the URL using the encrypted account hash and time parameters.
		        string url = $"{AccountsBaseUrl}/accounts/{accountHash}/orders?fromEnteredTime={fromTimeStr}&toEnteredTime={toTimeStr}";
		        UpdateStatus($"Fetching orders from: {url}");
		        
		        // Make the GET request to fetch the list of orders.
		        // (Assuming your MakeAuthorizedRequestAsync<T> method returns an instance of T or null.)
		        var orders = await MakeAuthorizedRequestAsync<List<Order>>(url);
		        if (orders == null || orders.Count == 0)
		        {
		            UpdateStatus("No orders found to cancel.");
		            return true;
		        }
		        
		        // Loop over each order and cancel it if it's cancelable.
		        foreach (var order in orders)
		        {
		            if (order.cancelable == true && order.orderId.HasValue)
		            {
		                bool cancelSuccess = await CancelOrderAsync(accountHash, order.orderId.Value);
		                if (cancelSuccess)
		                {
		                    UpdateStatus($"Cancelled order: {order.orderId}");
		                }
		                else
		                {
		                    UpdateStatus($"Failed to cancel order: {order.orderId}");
		                }
		            }
		        }
		        return true;
		    }
		    catch (Exception ex)
		    {
		        UpdateStatus($"Error in CancelAllOrdersByHash: {ex.Message}");
		        return false;
		    }
		}
 
               /// <summary>
        /// Cancels all open (working) orders and flattens all open positions for the account with the given hash,
        /// and returns a wrapper indicating success.
        /// </summary>
        /// <param name="accountHash">The encrypted account hash (not the plain account number).</param>
        /// <param name="reason">A reason string (for logging).</param>
        /// <returns>An ApiResponseWrapper with a boolean result.</returns>
        public async Task<ApiResponseWrapper<bool>> CloseAllPositionsAndOCOsAsyncByHash(string accountHash, string reason)
        {
            try
            {
                // --- 1. Cancel Open Orders ---
                DateTime fromDate = DateTime.UtcNow.AddDays(-1);
                DateTime toDate = DateTime.UtcNow.AddDays(1);
                var ordersResponse = await GetOrdersByHashAsync(accountHash, fromDate, toDate, Order.Status.WORKING);
                if (ordersResponse.HasError)
                {
                    return new ApiResponseWrapper<bool>(
                        false,
                        true,
                        ordersResponse.ResponseCode,
                        ordersResponse.Message,
                        null);
                }
                if (ordersResponse.Data != null)
                {
                    foreach (var order in ordersResponse.Data)
                    {
                        if (order.cancelable == true && order.orderId.HasValue)
                        {
                            await OrderExecuteDeleteAsync(accountHash, order.orderId.Value);
                        }
                    }
                }

                // --- 2. Flatten Open Positions ---
                string url = $"{AccountsBaseUrl}/accounts/{accountHash}?fields=positions";
                UpdateStatus($"Fetching account positions from: {url}");
                var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
                if (accountInfo?.securitiesAccount?.positions == null)
                {
                    UpdateStatus($"No positions found for account hash {accountHash}");
                    return new ApiResponseWrapper<bool>(true, false, 200, "No positions found.", null);
                }
                foreach (var pos in accountInfo.securitiesAccount.positions)
                {
                    string symbol = pos.Instrument.Symbol;
                    Order.AssetType assetType;
                    try { assetType = Order.GetAssetType(pos.Instrument.Type); }
                    catch { assetType = Order.AssetType.EQUITY; }
                    if (pos.LongQuantity > 0)
                    {
                        await OrderSingleAsync(
                            accountHash,
                            symbol,
                            assetType,
                            Order.OrderType.MARKET,
                            Order.Session.NORMAL,
                            Order.Duration.DAY,
                            Order.Position.TO_CLOSE,
                            -pos.LongQuantity,
                            null);
                    }
                    else if (pos.ShortQuantity > 0)
                    {
                        await OrderSingleAsync(
                            accountHash,
                            symbol,
                            assetType,
                            Order.OrderType.MARKET,
                            Order.Session.NORMAL,
                            Order.Duration.DAY,
                            Order.Position.TO_CLOSE,
                            pos.ShortQuantity,
                            null);
                    }
                }
                return new ApiResponseWrapper<bool>(true, false, 200, "Account flattened successfully.", null);
            }
            catch (Exception ex)
            {
                return new ApiResponseWrapper<bool>(false, true, 500, $"Error closing positions and OCOs: {ex.Message}", null);
            }
        }
		
		        /// <summary>
        /// Cancels a specific order using the encrypted account hash.
        /// </summary>
        /// <param name="accountHash">The encrypted account hash (do not use the plain account number).</param>
        /// <param name="orderId">The ID of the order to cancel.</param>
        /// <returns>True if cancellation succeeded; otherwise, false.</returns>
        public async Task<bool> CancelOrderAsync(string accountHash, long orderId)
        {
            try
            {
                string url = $"{AccountsBaseUrl}/accounts/{accountHash}/orders/{orderId}";
                var response = await HttpClient.DeleteAsync(url);
                if (response == null || !response.IsSuccessStatusCode)
                {
                    UpdateStatus($"Failed to cancel order {orderId}: {response?.ReasonPhrase}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error in CancelOrderAsync: {ex.Message}");
                return false;
            }
        }
		       /// <summary>
        /// Cancels all open (working) orders and then flattens (closes) all open positions for an account,
        /// using the encrypted account hash directly.
        /// </summary>
        /// <param name="accountHash">The encrypted account hash (must already be retrieved and stored globally).</param>
        /// <param name="reason">A reason for closing orders/positions (for logging purposes).</param>
         /// <summary>
        /// Closes all open orders (both regular and OCO orders) and then flattens (closes) all open positions 
        /// for the specified account (using the encrypted account hash).
        /// </summary>
        /// <param name="accountHash">The encrypted account hash to be used for API calls.</param>
        /// <param name="reason">A string describing the reason for closing orders/positions (for logging purposes).</param>
        /// <returns>An ApiResponseWrapper&lt;bool&gt; indicating success or failure.</returns>
        public async Task<ApiResponseWrapper<bool>> CloseAllPositionsAndOCOsByHash(string accountHash, string reason)
        {
            try
            {
                // --- 1. Cancel All Open Orders ---
                UpdateStatus("Cancelling all open orders...");
                bool cancelAllResult = await CancelAllOrdersByHash(accountHash);
                if (cancelAllResult)
                {
                    UpdateStatus("All open orders cancelled successfully.");
                }
                else
                {
                    UpdateStatus("Some open orders could not be cancelled.");
                }

                // --- 2. Flatten Open Positions ---
                string url = $"{AccountsBaseUrl}/accounts/{accountHash}?fields=positions";
                UpdateStatus($"Fetching account positions from: {url}");
                var accountInfo = await MakeAuthorizedRequestAsync<AccountInfo>(url);
                if (accountInfo?.securitiesAccount?.positions == null || accountInfo.securitiesAccount.positions.Count == 0)
                {
                    UpdateStatus($"No open positions found for account hash {accountHash}");
                    return new ApiResponseWrapper<bool>(true, false, 200, "No positions found.", null);
                }

                foreach (var pos in accountInfo.securitiesAccount.positions)
                {
                    string symbol = pos.Instrument.Symbol;
                    Order.AssetType assetType;
                    try
                    {
                        assetType = Order.GetAssetType(pos.Instrument.Type);
                    }
                    catch
                    {
                        assetType = Order.AssetType.EQUITY;
                    }
                    // Calculate net quantity: (LongQuantity - ShortQuantity).
                    var netQuantity = pos.LongQuantity - pos.ShortQuantity;
                    if (netQuantity != 0)
                    {
                        // Use a market order to close the position.
                        var closeOrderResponse = await OrderSingleAsync(
                            accountHash,
                            symbol,
                            assetType,
                            Order.OrderType.MARKET,
                            Order.Session.NORMAL,
                            Order.Duration.DAY,
                            Order.Position.TO_CLOSE,
                            -netQuantity,
                            null
                        );
                        if (closeOrderResponse.HasError)
                        {
                            UpdateStatus($"Failed to submit closing order for {symbol}: {closeOrderResponse.Message}");
                        }
                        else
                        {
                            UpdateStatus($"Submitted closing order for {symbol}: {-netQuantity}");
                        }
                    }
                }
                UpdateStatus("Account flattened successfully.");
                return new ApiResponseWrapper<bool>(true, false, 200, "Account flattened successfully.", null);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error closing positions and OCOs: {ex.Message}");
				return new ApiResponseWrapper<bool>(false, true, 500, $"Error closing positions and OCOs: {ex.Message}");
           }
        }
        private string GetAccountHashFromIdentifier(string accountIdentifier)
        {
            if (GlobalVariables.csAccounts.TryGetValue(accountIdentifier, out string hashValue) && !string.IsNullOrWhiteSpace(hashValue))
            {
                return hashValue;
            }
            // If not found, assume accountIdentifier is already the hash.
            return accountIdentifier;
        }
		
		/// <summary>
        /// Cancels a specific order using the account hash.
        /// </summary>
        /// <param name="accountHash">The encrypted account hash.</param>
        /// <param name="orderId">The order ID to cancel.</param>
        /// <returns>True if the cancellation succeeded; otherwise, false.</returns>
  
		public void UpdateStatus(string message)
		{
		    try
		    {
		        // ✅ Use the main NJ2CS instance to handle UI updates
		        if (NJ2CS.Instance != null)
		        {
		            NJ2CS.Instance.UpdateStatus(message);
		        }
		        else
		        {
		            // ✅ Update UI directly if NJ2CS.Instance is unavailable
		            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(() =>
		            {
		                if (NJ2CS.statusText != null)
		                {
		                    NJ2CS.statusText.Text = $"Status: {message}";
		                }
		            });
		        }
		
		        // ✅ Ensure NJ2CSLogManager is initialized before logging
		        if (NJ2CSLogManager.LogTab == null)
		        {
		            NJ2CSLogManager.Initialize();
		        }
		
		        // ✅ Log message using NJ2CSLogManager
		        if (NJ2CSLogManager.LogTab != null)
		        {
		            NJ2CSLogManager.LogMessage(message);
		        }
		        else
		        {
		            // 🔥 Fallback logging if LogTab is still null
		            string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
					NinjaTrader.Code.Output.Process($"{timestamp}: {message}", PrintTo.OutputTab1);
	
	        	}
		    }
		    catch (Exception ex)
		    {
		        // 🔥 Log unexpected errors
		        string timestamp = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}:";
		        NinjaTrader.Code.Output.Process($"{timestamp} Error in UpdateStatus: {ex.Message}", PrintTo.OutputTab1);
		    }
		}
	}
	
	public enum OrderAction
	{
	    Buy,
	    Sell
	}

}