using System;
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
}
