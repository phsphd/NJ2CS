import requests
import json

# 🔥 Replace with your actual values
ACCESS_TOKEN = "I0.your token@"
 

 
ACCOUNT_HASH = "your account hash"
API_URL = f"https://api.schwabapi.com/trader/v1/accounts/{ACCOUNT_HASH}/orders"
 
# ✅ SIMPLE MARKET ORDER PAYLOAD
order_payload = {
    "session": "NORMAL",
    "duration": "DAY",
    "orderType": "MARKET",
    "orderStrategyType": "SINGLE",
    "orderLegCollection": [
        {
            "orderLegType": "EQUITY",
            "instrument": {
                "symbol": "AAPL",
                "assetType": "EQUITY"  # Corrected from "type" to "assetType"
            },
            "instruction": "BUY",
            "quantity": 1
        }
    ]
}

json_payload = json.dumps(order_payload)

headers = {
    "Authorization": f"Bearer {ACCESS_TOKEN}",
    "Content-Type": "application/json"
}

print("🔥 Sending order request to Schwab API...")
response = requests.post(API_URL, headers=headers, data=json_payload)

print("\n📜 API Response:")
print(f"Status Code: {response.status_code}")
print(response.text)