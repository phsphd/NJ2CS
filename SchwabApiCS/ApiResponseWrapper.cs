using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace SchwabApiCS
{
    /// <summary>
    /// A generic wrapper to hold API response data or error information.
    /// </summary>
    public class ApiResponseWrapper<T>
    {
        /// <summary>
        /// The raw data returned from the API.
        /// </summary>
        public T RawData { get; set; }

        /// <summary>
        /// Indicates whether an error occurred.
        /// </summary>
        public bool HasError { get; set; }

        /// <summary>
        /// The HTTP response code (e.g. 200, 404).
        /// </summary>
        public int ResponseCode { get; set; }

        /// <summary>
        /// The HTTP response text (e.g. "OK", "Not Found").
        /// </summary>
        public string ResponseText { get; set; }

        /// <summary>
        /// The underlying HttpResponseMessage (if available).
        /// </summary>
        public HttpResponseMessage? ResponseMessage { get; set; }

        /// <summary>
        /// Any exception captured during the API request.
        /// </summary>
        public Exception? ApiException { get; set; }

        /// <summary>
        /// The response headers as a dictionary.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Constructs a new ApiResponseWrapper with the specified data, error flag, response code, and response text.
        /// </summary>
        public ApiResponseWrapper(T data, bool hasError, int responseCode, string responseText)
        {
            RawData = data;
            HasError = hasError;
            ResponseCode = responseCode;
            ResponseText = responseText;
            Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructs a new ApiResponseWrapper with the specified data, error flag, response code, response text, and a HttpResponseMessage.
        /// </summary>
        public ApiResponseWrapper(T data, bool hasError, int responseCode, string responseText, HttpResponseMessage? responseMessage)
        {
            RawData = data;
            HasError = hasError || (responseMessage != null && !responseMessage.IsSuccessStatusCode);
            ResponseCode = responseCode;
            ResponseText = responseText;
            ResponseMessage = responseMessage;
            Headers = responseMessage?.Headers.ToDictionary(h => h.Key, h => string.Join(";", h.Value))
                      ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Constructs a new ApiResponseWrapper using the provided HttpResponseMessage (and optional exception).
        /// </summary>
        public ApiResponseWrapper(T data, bool hasError, HttpResponseMessage? responseMessage, Exception? apiException = null)
        {
            RawData = data;
            ResponseMessage = responseMessage;
            ApiException = apiException;
            Headers = responseMessage?.Headers.ToDictionary(h => h.Key, h => string.Join(";", h.Value))
                      ?? new Dictionary<string, string>();

            if (responseMessage != null)
            {
                HasError = hasError || !responseMessage.IsSuccessStatusCode;
                ResponseCode = (int)responseMessage.StatusCode;
                ResponseText = responseMessage.ReasonPhrase;
            }
            else
            {
                HasError = hasError;
                ResponseCode = 0;
                ResponseText = "";
            }
        }

        /// <summary>
        /// Gets the API response data. If an error occurred, an exception is thrown.
        /// </summary>
        public T Data
        {
            get
            {
                if (HasError)
                {
                    throw new SchwabApiException<T>(this, $"error: {ResponseCode} {ResponseText}");
                }
                return RawData;
            }
            set { RawData = value; }
        }

        /// <summary>
        /// A combined message containing the response code and text.
        /// </summary>
        public string Message => $"{ResponseCode} {ResponseText}";

        /// <summary>
        /// Gets the URL from the underlying HttpResponseMessage.
        /// </summary>
        public string Url => ResponseMessage?.RequestMessage?.RequestUri?.PathAndQuery ?? "";

        /// <summary>
        /// Retrieves the Schwab Client Correlation ID from the HTTP headers, if available.
        /// </summary>
        public string? SchwabClientCorrelId
        {
            get
            {
                if (ResponseMessage == null)
                    return null;
                var header = ResponseMessage.Headers.FirstOrDefault(r =>
                    r.Key.Equals("Schwab-Client-Correlid", StringComparison.OrdinalIgnoreCase));
                if (header.Value == null)
                    return null;
                return header.Value.FirstOrDefault();
            }
        }
    }

    /// <summary>
    /// Non-generic exception class for Schwab API errors.
    /// </summary>
    public class SchwabApiException : Exception
    {
        public object? ApiResponse { get; set; }
        public HttpResponseMessage? Response { get; init; }
        public string? SchwabClientCorrelId { get; init; }

        public SchwabApiException() { }
        public SchwabApiException(string message) : base(message) { }
        public SchwabApiException(string message, Exception inner) : base(message, inner) { }

        public override string Message
        {
            get
            {
                string baseMsg = base.Message;
                string url = Response?.RequestMessage?.RequestUri?.PathAndQuery ?? "";
                string correlId = !string.IsNullOrWhiteSpace(SchwabClientCorrelId)
                    ? "\nSchwabClientCorrelid: " + SchwabClientCorrelId
                    : "";
                return $"{baseMsg}\nURL: {url}{correlId}";
            }
        }
    }

    /// <summary>
    /// Generic exception class for Schwab API errors.
    /// </summary>
    public class SchwabApiException<T> : SchwabApiException
    {
        public SchwabApiException(ApiResponseWrapper<T> apiResponse)
            : base("API error: " + apiResponse.Message)
        {
            ApiResponse = apiResponse;
            Response = apiResponse.ResponseMessage;
            SchwabClientCorrelId = apiResponse.SchwabClientCorrelId;
        }

        public SchwabApiException(ApiResponseWrapper<T> apiResponse, string message)
            : base(message)
        {
            ApiResponse = apiResponse;
            Response = apiResponse.ResponseMessage;
            SchwabClientCorrelId = apiResponse.SchwabClientCorrelId;
        }
    }
}
