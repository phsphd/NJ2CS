import base64
import requests
from loguru import logger


class SchwabAPI:
    def __init__(self, app_key, app_secret, refresh_token, redirect_uri):
        self.app_key = app_key
        self.app_secret = app_secret
        self.refresh_token = refresh_token
        self.redirect_uri = redirect_uri
        self.access_token = self.get_access_token()
        self.base_url = "https://api.schwabapi.com/trader/v1"
        self.headers = {"Authorization": f"Bearer {self.access_token}", "Accept": "application/json"}

    def get_access_token(self):
        """Obtain access token using refresh token."""

        token_request_url = "https://api.schwabapi.com/v1/oauth/token"

        payload = {
            "grant_type": "refresh_token",
            "refresh_token": self.refresh_token,
            "redirect_uri": self.redirect_uri,
        }

        credentials = f"{self.app_key}:{self.app_secret}"
        base64_credentials = base64.b64encode(credentials.encode("utf-8")).decode("utf-8")

        headers = {
            "Authorization": f"Basic {base64_credentials}",
            "Content-Type": "application/x-www-form-urlencoded",
        }

        logger.info("Requesting new access token...")
        response = requests.post(url=token_request_url, headers=headers, data=payload)

        if response.status_code == 200:
            token_response = response.json()
            access_token = token_response.get("access_token")
            logger.info("Access token obtained successfully.")
            return access_token
        else:
            logger.error(f"Failed to obtain access token. Status Code: {response.status_code}, Response: {response.text}")
            raise Exception("Access token retrieval failed.")

    def get_account_hash_values(self):
        """Fetch account hash values (not account numbers)."""

        accounts_url = f"{self.base_url}/accounts/accountNumbers"
        logger.info("Fetching account hash values...")

        response = requests.get(url=accounts_url, headers=self.headers)

        if response.status_code == 200:
            accounts_data = response.json()
            logger.debug(f"Full account response: {accounts_data}")

            if isinstance(accounts_data, list):  # Handle list response
                hash_values = [account["hashValue"] for account in accounts_data if "hashValue" in account]
                logger.info(f"Retrieved account hash values: {hash_values}")
                return hash_values
            else:
                logger.error("Unexpected response structure from Schwab API.")
                return None
        else:
            logger.error(f"Failed to fetch account hash values. Status Code: {response.status_code}, Response:\n{response.text}")
            return None


    def get_account_balances(self):
        """Fetch balances for each account hash value."""

        hash_values = self.get_account_hash_values()
        if not hash_values:
            logger.error("No account hash values found.")
            return None

        account_details = []

        for hash_value in hash_values:
            account_url = f"{self.base_url}/accounts/{hash_value}"

            logger.info(f"Fetching balance for account: {hash_value}")

            response = requests.get(url=account_url, headers=self.headers)

            if response.status_code == 200:
                balance_data = response.json()
                logger.debug(f"Full balance response for {hash_value}: {balance_data}")

                # Prioritize which balance to display (try aggregated first, fallback to initial/current)
                securities_account = balance_data.get("securitiesAccount", {})
                aggregated_balance = securities_account.get("aggregatedBalance", {}).get("liquidationValue")
                current_balance = securities_account.get("currentBalances", {}).get("cashBalance")
                initial_balance = securities_account.get("initialBalances", {}).get("cashBalance")

                if aggregated_balance is not None:
                    account_balance = aggregated_balance
                elif current_balance is not None:
                    account_balance = current_balance
                elif initial_balance is not None:
                    account_balance = initial_balance
                else:
                    logger.warning(f"No valid balance data found for account {hash_value}")
                    account_balance = "N/A"

                logger.info(f"Account Hash: {hash_value}, Balance: ${account_balance}")
                account_details.append({
                    "account_hash": hash_value,
                    "balance": account_balance
                })
            else:
                logger.error(f"Failed to fetch balance for account {hash_value}. Status Code: {response.status_code}, Response: {response.text}")

        return account_details



def main():
    """Main function to handle Schwab API account retrieval."""

    # Replace these with your actual credentials
     app_key = "your_app_key"
     app_secret = "your_app_secret"
     refresh_token = "your_refresh_token"
     redirect_uri = "https://127.0.0.1"

    schwab_api = SchwabAPI(app_key, app_secret, refresh_token, redirect_uri)

    # Retrieve account balances
    account_balances = schwab_api.get_account_balances()

    if account_balances:
        logger.info("All account balances retrieved successfully.")
        for account in account_balances:
            print(f"Account Hash: {account['account_hash']}, Balance: ${account['balance']}")
    else:
        logger.error("Failed to retrieve account balances.")

    return "Done!"


if __name__ == "__main__":
    main()
