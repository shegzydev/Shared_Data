using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;

using HEADER = System.Collections.Generic.Dictionary<string, string>;

#if UNITY_ANDROID || UNITY_IOS
// using NativeGallery;
#endif

internal static class WebImageUploader
{
    private static readonly HttpClient httpClient = new HttpClient();

    private const int TIMEOUT_MS = 5000;
    private const string PNG_MIME = "image/png";

    // =========================
    // PUBLIC API
    // =========================

    public static void PickAndPostImagePng(
        string url,
        string formFieldName,
        HEADER headers,
        Action onUploadStart,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken = "")
    {
        PickImage(async (pngBytes) =>
        {
            onUploadStart?.Invoke();
            await SendMultipartImage(
                HttpMethod.Post,
                url,
                pngBytes,
                formFieldName,
                headers,
                (msg, code) => onSuccess(msg, code),
                (msg, code) => onFailure(msg, code),
                authToken
            );
        }, onFailure);
    }

    public static void PickAndPatchImagePng(
        string url,
        string formFieldName,
        HEADER headers,
        Action onUploadStart,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken = "")
    {
        PickImage(async (pngBytes) =>
        {
            onUploadStart?.Invoke();
            await SendMultipartImage(
                new HttpMethod("PATCH"),
                url,
                pngBytes,
                formFieldName,
                headers,
                onSuccess,
                onFailure,
                authToken
            );
        }, onFailure);
    }

    // =========================
    // IMAGE PICKER
    // =========================

    private static void PickImage(
        Action<byte[]> onImageReady,
        Action<string, long> onFailure)
    {
#if UNITY_ANDROID || UNITY_IOS

        NativeGallery.GetImageFromGallery((path) =>
        {
            if (string.IsNullOrEmpty(path))
            {
                onFailure?.Invoke("Image selection cancelled", 0);
                return;
            }

            try
            {
                byte[] fileBytes = File.ReadAllBytes(path);

                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(fileBytes))
                {
                    onFailure?.Invoke("Failed to load image", 0);
                    return;
                }

                byte[] pngBytes = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);

                onImageReady?.Invoke(pngBytes);
            }
            catch (Exception ex)
            {
                onFailure?.Invoke($"Image load error: {ex.Message}", 0);
            }
        },
        "Select an image");
#else
        onFailure?.Invoke("Gallery not supported on this platform", 0);
#endif
    }

    // =========================
    // NETWORK LAYER (Multipart)
    // =========================

    private static async Task SendMultipartImage(
        HttpMethod method,
        string url,
        byte[] pngBytes,
        string formFieldName,
        HEADER headers,
        Action<string, long> onSuccess,
        Action<string, long> onFailure,
        string authToken)
    {
        try
        {
            // using (var cts = new CancellationTokenSource(TIMEOUT_MS))
            // using (var request = new HttpRequestMessage(method, url))
            // using (var multipart = new MultipartFormDataContent())
            // {
            var request = new HttpRequestMessage(method, url);
            var multipart = new MultipartFormDataContent();

            var imageContent = new ByteArrayContent(pngBytes);
            imageContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(PNG_MIME);

            multipart.Add(
                imageContent,
                formFieldName,
                $"image_{DateTime.UtcNow.Ticks}.png"
            );

            // Request headers
            if (headers != null)
            {
                GigNet.Log?.Invoke("Adding custom headers...");
                foreach (var kvp in headers)
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }

            // Authorization
            if (!string.IsNullOrEmpty(authToken))
            {
                request.Headers.TryAddWithoutValidation(
                    "Authorization", "Bearer " + authToken);
            }

            request.Content = multipart;

            GigNet.Log?.Invoke($"{method} IMAGE {url}");

            httpClient.Timeout = Timeout.InfiniteTimeSpan;
            using (var response = await httpClient.SendAsync(request))
            {
                GigNet.Log?.Invoke($"reading response content...");

                string responseText = await response.Content.ReadAsStringAsync();
                long statusCode = (long)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    GigNet.Log?.Invoke($"Image request success ({statusCode})");
                    onSuccess?.Invoke(responseText, statusCode);
                }
                else
                {
                    GigNet.LogError?.Invoke($"Image request failed ({statusCode}): {responseText}");
                    onFailure?.Invoke(responseText, statusCode);
                }

                if (statusCode == 401)
                {
                    GigNet.OnAuthFailed?.Invoke();
                }
            }
            // }
        }
        catch (TaskCanceledException)
        {
            GigNet.LogError?.Invoke("❌ Image request timeout");
            onFailure?.Invoke("Request timed out", 0);
        }
        catch (HttpRequestException ex)
        {
            GigNet.LogError?.Invoke($"❌ Network error: {ex.Message}");
            onFailure?.Invoke(ex.Message, 0);
        }
        catch (Exception ex)
        {
            GigNet.LogError?.Invoke($"❌ Image request error: {ex.Message}: {ex.StackTrace}");
            onFailure?.Invoke(ex.Message, 0);
        }
    }
}
