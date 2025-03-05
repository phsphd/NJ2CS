import requests
import json

# --- Configuration ---
ACCESS_TOKEN = "I0.b2F1dGgyLmJkYy5zY2h3YWIuY29t.1PsYc82ucO26QPmYtoHnqIt3t5rqfZV_DJiPCKIYCbM@"  # Replace with your valid access token
ACCOUNT_NUMBER = "81444650"  # Use the plain account number
ACCOUNT_HASH = "60070CBAEBB636E62FA37474ABCFDC12918A9443F980F43A1A3A958D56EF7279"  # Keep for reference

API_URL = f"https://api.schwabapi.com/trader/v1/accounts/{ACCOUNT_HASH}/orders"  # Use plain account number in URL for POST

# --- Construct the OCO order payload ---
# We’re placing an OCO to protect a long position in NVDA (Buy).
payload = {
  "orderStrategyType": "OCO",
  "childOrderStrategies": [
    {
      "orderStrategyType": "SINGLE",
      "orderType": "LIMIT",
      "session": "NORMAL",
      "duration": "DAY",
      "price": 142.03,
      "orderLegCollection": [
        {
          "instruction": "SELL",
          "quantity": 1.0,
          "instrument": {
            "assetType": "EQUITY",
            "symbol": "NVDA"
          }
        }
      ]
    },
    {
      "orderStrategyType": "SINGLE",
      "orderType": "STOP",
      "session": "NORMAL",
      "duration": "DAY",
      "stopPrice": 138.03,
      "orderLegCollection": [
        {
          "instruction": "SELL",
          "quantity": 1.0,
          "instrument": {
            "assetType": "EQUITY",
            "symbol": "NVDA"
          }
        }
      ]
    }
  ]
}

# Serialize payload to JSON
json_payload = json.dumps(payload)

# Set headers
headers = {
    "Authorization": f"Bearer {ACCESS_TOKEN}",
    "Content-Type": "application/json"
}

# Log the request
print("🔥 Sending OCO order payload to Schwab API (POST):")
print(json_payload)

# Send the POST request
try:
    response = requests.post(API_URL, headers=headers, data=json_payload)  # Explicitly using POST

    # Log the response
    print("\n📜 API Response:")
    print(f"Status Code: {response.status_code}")
    print(f"Headers: {response.headers}")  # Include headers for debugging
    print(response.text)

    # Raise an exception for bad status codes
    response.raise_for_status()

    print("OCO order placed successfully!")

except requests.exceptions.RequestException as e:
    print(f"Error: {e}")
    if response is not None:
        if response.status_code == 400:
            print("Detailed 400 Error: ", response.json() if response.text else "No additional details")
        elif response.status_code == 401:
            print("Authentication error: Check ACCESS_TOKEN or account permissions.")
        elif response.status_code == 403:
            print("Forbidden: Check account or API permissions.")
        elif response.status_code == 404:
            print("Not Found: Check API URL or account number.")
        elif response.status_code in (500, 503):
            print(f"Server error (Status {response.status_code}): {response.text}")
        else:
            print(f"Unexpected error (Status {response.status_code}): {response.text}")