// <copyright file="OrderOCOBracket.cs" company="ZPM Software Inc">
// Copyright © 2024 ZPM Software Inc. All rights reserved.
// This Source Code is subject to the terms MIT Public License
// </copyright>

using static SchwabApiCS.Order;
using System.Threading.Tasks;
// https://json2csharp.com/

namespace SchwabApiCS
{
    public partial class SchwabApi
    {
        // ========== OCO (one cancels the other) Bracket. =========================
        // a limit order and a stop order.  Use to close a position
        // The first order to fill cancels the other.
        

        public long? OrderOCOBracket(string accountNumber, string symbol, Order.AssetType assetType, Order.Duration duration,
                                     Order.Session session, decimal quantity, decimal? limitPrice, decimal? stopPrice)
        {
            return WaitForCompletion(OrderOCOBracketAsync(accountNumber, symbol, assetType, duration, session,
                                                          quantity, limitPrice, stopPrice));
        }
        public long? OrderOCOBracketHash(string accountHash, string symbol, Order.AssetType assetType, Order.Duration duration,
                                     Order.Session session, decimal quantity, decimal? limitPrice, decimal? stopPrice)
        {
            return WaitForCompletion(OrderOCOBracketHashAsync(accountHash, symbol, assetType, duration, session,
                                                          quantity, limitPrice, stopPrice));
        }
        /// <summary>
        /// OCO Braket order: to close limit order + stop(market) order
        /// Use to close a long order (use negative quantity to sell) or close a short position (use positive quantity to buy)
        /// </summary> 
        /// <param name="accountNumber"></param>
        /// <param name="symbol"></param>
        /// <param name="assetType"></param>
        /// <param name="quantity">positive to buy, negative to sell</param>
        /// <param name="duration"></param>
        /// <param name="session"></param>
        /// <param name="limitPrice"></param>
        /// <param name="stopPrice"></param>
        /// <returns></returns>
        public async Task<ApiResponseWrapper<long?>> OrderOCOBracketAsync1(
                                     string accountNumber, string symbol, Order.AssetType assetType, Order.Duration duration,
                                     Order.Session session, decimal quantity, decimal? limitPrice, decimal? stopPrice)
        {
            var limitOrder = new Order.ChildOrderStrategy(OrderType.LIMIT, OrderStrategyTypes.SINGLE, session, duration, limitPrice);
            limitOrder.Add(new OrderLeg(symbol, assetType, Position.TO_CLOSE, quantity));

            var stopOrder = new Order.ChildOrderStrategy(OrderType.STOP, OrderStrategyTypes.SINGLE, session, duration, stopPrice);
            stopOrder.Add(new OrderLeg(symbol, assetType, Position.TO_CLOSE, quantity));

            var ocoOrder = new Order() {
                orderStrategyType = OrderStrategyTypes.OCO.ToString(),
            };
            ocoOrder.Add(limitOrder);
            ocoOrder.Add(stopOrder);

            return await OrderExecuteNewAsync(accountNumber, ocoOrder);
        }
		public async Task<ApiResponseWrapper<long?>> OrderOCOBracketAsync(
		    string plainAccountNumber,    // plain account number for order placement
		    string symbol,
		    Order.AssetType assetType,
		    Order.Duration duration,
		    Order.Session session,
		    decimal quantity,
		    decimal? limitPrice,
		    decimal? stopPrice)
		{
		    // Build the child orders.
		    var takeProfitOrder = new Order.ChildOrderStrategy(Order.OrderType.LIMIT, Order.OrderStrategyTypes.SINGLE, session, duration, limitPrice);
		    // Use Position.TO_CLOSE for protection orders (assuming you are closing an existing position)
		    takeProfitOrder.Add(new Order.OrderLeg(symbol, assetType, Order.Position.TO_CLOSE, quantity));
		
		    var stopLossOrder = new Order.ChildOrderStrategy(Order.OrderType.STOP, Order.OrderStrategyTypes.SINGLE, session, duration, stopPrice);
		    stopLossOrder.Add(new Order.OrderLeg(symbol, assetType, Order.Position.TO_CLOSE, quantity));
		
		    // Build the OCO order.
		    var ocoOrder = new Order()
		    {
		        orderStrategyType = Order.OrderStrategyTypes.OCO.ToString(),
		    };
		    ocoOrder.Add(takeProfitOrder);
		    ocoOrder.Add(stopLossOrder);
		
		    // Execute the order – note that OrderExecuteNewAsync should expect the plain account number.
		    return await OrderExecuteNewAsync(plainAccountNumber, ocoOrder);
		}
		public async Task<ApiResponseWrapper<long?>> OrderOCOBracketHashAsync(
		    string accountHash,    // plain account number for order placement
		    string symbol,
		    Order.AssetType assetType,
		    Order.Duration duration,
		    Order.Session session,
		    decimal quantity,
		    decimal? limitPrice,
		    decimal? stopPrice)
		{
		    // Build the child orders.
		    var takeProfitOrder = new Order.ChildOrderStrategy(Order.OrderType.LIMIT, Order.OrderStrategyTypes.SINGLE, session, duration, limitPrice);
		    // Use Position.TO_CLOSE for protection orders (assuming you are closing an existing position)
		    takeProfitOrder.Add(new Order.OrderLeg(symbol, assetType, Order.Position.TO_CLOSE, quantity));
		
		    var stopLossOrder = new Order.ChildOrderStrategy(Order.OrderType.STOP, Order.OrderStrategyTypes.SINGLE, session, duration, stopPrice);
		    stopLossOrder.Add(new Order.OrderLeg(symbol, assetType, Order.Position.TO_CLOSE, quantity));
		
		    // Build the OCO order.
		    var ocoOrder = new Order()
		    {
		        orderStrategyType = Order.OrderStrategyTypes.OCO.ToString(),
		    };
		    ocoOrder.Add(takeProfitOrder);
		    ocoOrder.Add(stopLossOrder);
		
		    // Execute the order – note that OrderExecuteNewAsync should expect the plain account number.
		    return await OrderExecuteHashAsync(accountHash, ocoOrder);
		}
    }
}
