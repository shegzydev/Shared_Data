using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if UNITY_WEBGL || NETSTANDARD || WASM
using System.Net.WebSockets;
#else
using WebSocketSharp;
#endif

public class SimpleWebSocket
{
    public event Action OnOpen;
    public event Action<byte[]> OnMessage;
    public event Action<string> OnError;
    public event Action<ushort, string> OnClose;

    private string _url;
    private CancellationTokenSource _cts;

#if UNITY_WEBGL || NETSTANDARD || WASM
    private ClientWebSocket _client;
#else
    private WebSocket _client;
#endif

    public SimpleWebSocket(string url)
    {
        _url = url;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();

#if UNITY_WEBGL || NETSTANDARD || WASM
        _client = new ClientWebSocket();
        try
        {
            await _client.ConnectAsync(new Uri(_url), _cts.Token);
            OnOpen?.Invoke();
            // _ = ReceiveLoop();
            _ = Task.Run(ReceiveLoop);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
#else
        _client = new WebSocket(_url);

        _client.OnOpen += (_, __) => OnOpen?.Invoke();
        _client.OnMessage += (_, e) => OnMessage?.Invoke(e.RawData);
        _client.OnError += (_, e) => OnError?.Invoke(e.Message);
        _client.OnClose += (_, e) =>
        {
            OnClose?.Invoke((ushort)e.Code, e.Reason ?? "No reason");
        };

        try
        {
            _client.ConnectAsync();
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
#endif
    }

#if UNITY_WEBGL || NETSTANDARD || WASM
    byte[] buffer = null;

    private async Task ReceiveLoop()
    {
        if (buffer == null) buffer = new byte[8192];

        try
        {
            while (_client.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                int offset = 0;

                do
                {
                    result = await _client.ReceiveAsync(
                        new ArraySegment<byte>(buffer, offset, buffer.Length - offset),
                        _cts.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        var code = (ushort)(result.CloseStatus ?? WebSocketCloseStatus.Empty);
                        var reason = result.CloseStatusDescription ?? "Closed";
                        OnClose?.Invoke(code, reason);
                        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                        return;
                    }

                    offset += result.Count;

                    // Safety: resize if message is larger than buffer
                    if (offset >= buffer.Length)
                    {
                        Array.Resize(ref buffer, buffer.Length * 2);
                    }

                } while (!result.EndOfMessage);

                // COPY ONLY AFTER FULL MESSAGE
                var data = new byte[offset];
                Buffer.BlockCopy(buffer, 0, data, 0, offset);

                // IMPORTANT: do NOT block here
                OnMessage?.Invoke(data);
            }
        }
        catch (OperationCanceledException)
        {
            // normal on close
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
    }

    /*
    private async Task ReceiveLoop()
    {
        if (buffer == null) buffer = new byte[4096];
        try
        {
            while (_client.State == WebSocketState.Open)
            {
                var result = await _client.ReceiveAsync(buffer, _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    var code = (ushort)(result.CloseStatus ?? WebSocketCloseStatus.Empty);
                    var reason = result.CloseStatusDescription ?? "Closed";
                    OnClose?.Invoke(code, reason);
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                }
                else
                {
                    byte[] data = new byte[result.Count];
                    Array.Copy(buffer, data, result.Count);
                    OnMessage?.Invoke(data);
                }
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
    }*/
#endif

    public async Task SendAsync(byte[] data)
    {
#if UNITY_WEBGL || NETSTANDARD || WASM
        if (_client?.State == WebSocketState.Open)
            await _client.SendAsync(data, WebSocketMessageType.Binary, true, _cts.Token);
#else
        if (_client?.ReadyState == WebSocketState.Open)
            _client.Send(data);
#endif
    }

    public async Task CloseAsync(ushort code = 1000, string reason = "Normal Closure")
    {
        try
        {
#if UNITY_WEBGL || NETSTANDARD || WASM
            if (_client?.State == WebSocketState.Open)
                await _client.CloseAsync((WebSocketCloseStatus)code, reason, CancellationToken.None);
#else
            if (_client?.ReadyState == WebSocketState.Open)
                _client.Close((WebSocketSharp.CloseStatusCode)code, reason);
#endif
            OnClose?.Invoke(code, reason);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
        }
    }

    public WebSocketState State
    {
        get
        {
#if UNITY_WEBGL || NETSTANDARD || WASM
            return _client?.State ?? WebSocketState.None;
#else
            return (WebSocketState)_client?.ReadyState;
#endif
        }
    }
}
