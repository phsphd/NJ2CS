import base64
import requests
from loguru import logger


def construct_headers_and_payload(app_key, app_secret, refresh_token, redirect_uri):
    """Prepare headers and payload for refresh token request."""
    
    # Encode client credentials
    credentials = f"{app_key}:{app_secret}"
    base64_credentials = base64.b64encode(credentials.encode("utf-8")).decode("utf-8")

    headers = {
        "Authorization": f"Basic {base64_credentials}",
        "Content-Type": "application/x-www-form-urlencoded",
    }

    payload = {
        "grant_type": "refresh_token",
        "refresh_token": refresh_token,
        "redirect_uri": redirect_uri,
    }

    return headers, payload


def retrieve_access_token(headers, payload):
    """Send request to obtain a new access token using the refresh token."""
    
    token_request_url = "https://api.schwabapi.com/v1/oauth/token"

    logger.info("Sending request to obtain new access token...")
    logger.info(f"Request URL: {token_request_url}")
    logger.info(f"Payload: {payload}")

    response = requests.post(url=token_request_url, headers=headers, data=payload)

    if response.status_code == 200:
        logger.info("Access token obtained successfully.")
    else:
        logger.error(f"Failed to obtain access token. Status Code: {response.status_code}, Response: {response.text}")
        return None

    return response.json()


def main():
    """Main function to handle the Schwab OAuth token refresh process."""

    # Replace these with your actual credentials
    app_key = "your app key"
    app_secret = "your secret"
    refresh_token = "your refresh token"
    redirect_uri = "https://127.0.0.1"

    headers, payload = construct_headers_and_payload(app_key, app_secret, refresh_token, redirect_uri)

    token_data = retrieve_access_token(headers=headers, payload=payload)

    if token_data:
        logger.info("New token details:")
        logger.info(token_data)
    else:
        logger.error("Failed to refresh access token.")

    return "Done!"


if __name__ == "__main__":
    main()
