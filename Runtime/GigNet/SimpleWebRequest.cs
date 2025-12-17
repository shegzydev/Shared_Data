/*using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

using HEADER = System.Collections.Generic.Dictionary<string, string>;

internal class SimpleWebRequest
{
    public static void Get(string url, HEADER headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false, int retries = 5)
    {
        Debug.Log($"Getting from {url} with headers {JsonConvert.SerializeObject(headers)}");

        var request = UnityWebRequest.Get(url);
        ApplyHeaders(request, headers);
        NetworkManager.Instance.StartCoroutine(SendRequest(request, onSuccess, onFailure, shouldRetry));
    }

    public static void Post(string url, string jsonBody, HEADER headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false, int retries = 5)
    {
        Debug.Log($"Posting {jsonBody} to {url} with headers {JsonConvert.SerializeObject(headers)}");

        var request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        ApplyHeaders(request, headers);
        request.SetRequestHeader("Content-Type", "application/json");
        NetworkManager.Instance.StartCoroutine(SendRequest(request, onSuccess, onFailure, shouldRetry));
    }

    public static void Patch(string url, string jsonBody, HEADER headers, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false, int retries = 5)
    {
        Debug.Log($"Patching {jsonBody} to {url} with headers {JsonConvert.SerializeObject(headers)}");

        var request = new UnityWebRequest(url, "PATCH");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        ApplyHeaders(request, headers);
        request.SetRequestHeader("Content-Type", "application/json");
        NetworkManager.Instance.StartCoroutine(SendRequest(request, onSuccess, onFailure, shouldRetry));
    }

    private static IEnumerator SendRequest(UnityWebRequest request, Action<string, long> onSuccess, Action<string, long> onFailure, bool shouldRetry = false, int retries = 5)
    {
        yield return request.SendWebRequest();

        long statusCode = request.responseCode;
        string resultText = request.downloadHandler?.text;

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(resultText, statusCode);
        }
        else
        {
            onFailure?.Invoke(request.error, statusCode);
        }
        request.Dispose();
    }

    private static void ApplyHeaders(UnityWebRequest request, HEADER headers)
    {
        if (headers == null) return;

        foreach (var header in headers)
            request.SetRequestHeader(header.Key, header.Value);
    }

    public static string ComputeHmacSHA512(string data, string key)
    {
        using (var hmac = new System.Security.Cryptography.HMACSHA512(Encoding.UTF8.GetBytes(key)))
        {
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
            foreach (byte b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}*/

#if SERVER || CLIENT
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

using HEADER = System.Collections.Generic.Dictionary<string, string>;
using Newtonsoft.Json;
using System.Threading;

internal static class SimpleWebRequest
{
    private static readonly HttpClient httpClient = new HttpClient();

    public static async Task Get(
        string url,
        HEADER headers,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken = "")
    {
        GigNet.Log?.Invoke($"GET {url} with headers {JsonConvert.SerializeObject(headers)}");
        await SendRequest(HttpMethod.Get, url, null, headers, onSuccess, onFailure, authToken);
    }

    public static async Task Post(
        string url,
        string jsonBody,
        HEADER headers,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken = "")
    {
        GigNet.Log?.Invoke($"POST {jsonBody} to {url} with headers {JsonConvert.SerializeObject(headers)}");
        await SendRequest(HttpMethod.Post, url, jsonBody, headers, onSuccess, onFailure, authToken);
    }

    public static async Task Patch(
        string url,
        string jsonBody,
        HEADER headers,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken = "")
    {
        GigNet.Log?.Invoke($"PATCH {jsonBody} to {url} with headers {JsonConvert.SerializeObject(headers)}");
        await SendRequest(new HttpMethod("PATCH"), url, jsonBody, headers, onSuccess, onFailure, authToken);
    }

    public static async Task Delete(
        string url,
        HEADER headers,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken = "")
    {
        GigNet.Log?.Invoke($"DELETE {url} with headers {JsonConvert.SerializeObject(headers)}");
        await SendRequest(HttpMethod.Delete, url, null, headers, onSuccess, onFailure, authToken);
    }

    private static async Task SendRequest(
        HttpMethod method,
        string url,
        string body,
        HEADER headers,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken)
    {
        try
        {
            using (var cts = new CancellationTokenSource(10000))
            using (var request = new HttpRequestMessage(method, url))
            {
                // Add headers - separate content headers from request headers
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        // Content headers need to be added to Content.Headers, not Request.Headers
                        if (IsContentHeader(kvp.Key))
                        {
                            if (body != null)
                            {
                                if (request.Content == null)
                                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                                request.Content.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                            }
                        }
                        else
                        {
                            request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                        }
                    }
                }

                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + authToken);

                // Add body content if not already added
                if (body != null && request.Content == null)
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                GigNet.Log?.Invoke("Request packed");

                using (var response = await httpClient.SendAsync(request, cts.Token))
                {
                    string responseText = await response.Content.ReadAsStringAsync();
                    long statusCode = (long)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        GigNet.Log?.Invoke($"Request done - Status: {statusCode}");
                        onSuccess?.Invoke(responseText, statusCode);
                    }
                    else
                    {
                        GigNet.LogError?.Invoke($"Request failed - Status: {statusCode}, Response: {responseText}");
                        onFailure?.Invoke(responseText, statusCode);
                    }

                    if (statusCode == 401)
                    {
                        GigNet.OnAuthFailed?.Invoke();
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            GigNet.LogError?.Invoke($"❌ Network error: {ex.Message}");
            onFailure?.Invoke(ex.Message, 0);
        }
        catch (TaskCanceledException ex)
        {
            GigNet.LogError?.Invoke($"❌ Request timeout: {ex.Message}");
            onFailure?.Invoke("Request timed out", 0);
        }
        catch (Exception ex)
        {
            GigNet.LogError?.Invoke($"❌ Request failed: {ex.Message}\n{ex.StackTrace}");
            onFailure?.Invoke(ex.Message, 0);
        }
    }

    private static bool IsContentHeader(string headerName)
    {
        var contentHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Content-Type", "Content-Length", "Content-Encoding", "Content-Language",
            "Content-Location", "Content-MD5", "Content-Range", "Content-Disposition",
            "Expires", "Last-Modified"
        };
        return contentHeaders.Contains(headerName);
    }

    public static string ComputeHmacSHA512(string data, string key)
    {
        using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
        {
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            var sb = new StringBuilder(hashBytes.Length * 2);
            foreach (byte b in hashBytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
#endif