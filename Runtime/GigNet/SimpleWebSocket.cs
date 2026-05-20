using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketDemo
{
    /// <summary>
    /// A binary-only client-side WebSocket wrapper.
    /// All sends and receives operate exclusively on raw byte arrays.
    /// </summary>
    public class WebSocketClient : IAsyncDisposable
    {
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly Uri _serverUri;
        private readonly int _receiveBufferSize;

        // --- Events ---
        public event Func<byte[], Task>? OnDataReceived;
        public event Func<Task>? OnConnected;
        public event Func<WebSocketCloseStatus?, string?, Task>? OnDisconnected;
        public event Func<Exception, Task>? OnError;

        public WebSocketState State => _socket?.State ?? WebSocketState.None;

        public WebSocketClient(string serverUrl, int receiveBufferSize = 4096)
        {
            _serverUri = new Uri(serverUrl);
            _receiveBufferSize = receiveBufferSize;
            _socket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
        }

        // -------------------------------------------------------------------------
        // Connect
        // -------------------------------------------------------------------------

        /// <summary>
        /// Connects to the WebSocket server and starts the binary receive loop.
        /// </summary>
        public async Task ConnectAsync(CancellationToken externalToken = default)
        {
            if (_socket.State != WebSocketState.None &&
                _socket.State != WebSocketState.Closed)
            {
                throw new InvalidOperationException(
                    $"Cannot connect while socket is in '{_socket.State}' state.");
            }

            _socket = new ClientWebSocket();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

            try
            {
                await _socket.ConnectAsync(_serverUri, _cts.Token);
                await RaiseEventAsync(OnConnected);

                // Fire-and-forget the receive loop
                _ = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
            }
            catch (OperationCanceledException ex)
            {
                await RaiseEventAsync(OnDisconnected, WebSocketCloseStatus.InternalServerError, ex.Message);
            }
            catch (Exception ex)
            {
                await RaiseEventAsync(OnError, ex);
                throw;
            }
        }

        // -------------------------------------------------------------------------
        // Send
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends raw binary data to the server.
        /// </summary>
        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cts.Token);

            await _socket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken: linkedCts.Token);
        }

        /// <summary>
        /// Sends a slice of a buffer as binary data, avoiding an extra allocation.
        /// </summary>
        public async Task SendAsync(byte[] data, int offset, int count,
            CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cts.Token);

            await _socket.SendAsync(
                new ArraySegment<byte>(data, offset, count),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken: linkedCts.Token);
        }

        /// <summary>
        /// Sends a ReadOnlyMemory buffer as binary data (.NET 5+).
        /// </summary>
        public async Task SendAsync(ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default)
        {
            EnsureConnected();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cts.Token);

            await _socket.SendAsync(
                data,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken: linkedCts.Token);
        }

        // -------------------------------------------------------------------------
        // Receive Loop
        // -------------------------------------------------------------------------

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            // Rent a reusable buffer from the pool for each frame read
            var frameBuffer = ArrayPool<byte>.Shared.Rent(_receiveBufferSize);

            try
            {
                while (_socket.State == WebSocketState.Open &&
                       !cancellationToken.IsCancellationRequested)
                {
                    // Accumulate frames into a list of segments until EndOfMessage
                    var segments = new List<byte[]>();
                    int totalBytes = 0;

                    WebSocketReceiveResult result;

                    do
                    {
                        var segment = new ArraySegment<byte>(frameBuffer);

                        result = await _socket.ReceiveAsync(segment, cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _socket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Acknowledged server close",
                                CancellationToken.None);

                            await RaiseEventAsync(OnDisconnected,
                                result.CloseStatus, result.CloseStatusDescription);
                            return;
                        }

                        if (result.MessageType != WebSocketMessageType.Binary)
                        {
                            // Reject non-binary frames
                            await _socket.CloseAsync(
                                WebSocketCloseStatus.InvalidMessageType,
                                "Only binary messages are accepted",
                                CancellationToken.None);

                            await RaiseEventAsync(OnDisconnected,
                                WebSocketCloseStatus.InvalidMessageType,
                                "Non-binary frame received");
                            return;
                        }

                        // Copy this frame's bytes into a dedicated array
                        var chunk = new byte[result.Count];
                        Buffer.BlockCopy(frameBuffer, 0, chunk, 0, result.Count);
                        segments.Add(chunk);
                        totalBytes += result.Count;

                    } while (!result.EndOfMessage);

                    // Assemble all chunks into one contiguous byte array
                    var message = AssembleMessage(segments, totalBytes);
                    await RaiseEventAsync(OnDataReceived, message);
                }
            }
            catch (Exception ex)
            {
                await RaiseEventAsync(OnError, ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(frameBuffer);
            }
        }

        /// <summary>
        /// Flattens a list of byte-array chunks into a single contiguous array.
        /// For single-chunk messages (the common case) no copy is needed.
        /// </summary>
        private static byte[] AssembleMessage(List<byte[]> segments, int totalBytes)
        {
            if (segments.Count == 1)
                return segments[0];

            var assembled = new byte[totalBytes];
            int offset = 0;
            foreach (var chunk in segments)
            {
                Buffer.BlockCopy(chunk, 0, assembled, offset, chunk.Length);
                offset += chunk.Length;
            }
            return assembled;
        }

        // -------------------------------------------------------------------------
        // Disconnect
        // -------------------------------------------------------------------------

        /// <summary>
        /// Gracefully closes the WebSocket connection.
        /// </summary>
        public async Task DisconnectAsync(
            WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
            string statusDescription = "Client disconnecting",
            CancellationToken cancellationToken = default)
        {
            if (_socket.State != WebSocketState.Open)
            {
                await RaiseEventAsync(OnDisconnected, closeStatus, statusDescription);
                return;
            }
            _cts.Cancel();

            try
            {
                await _socket.CloseAsync(closeStatus, statusDescription, cancellationToken);
            }
            catch (WebSocketException)
            {
                // Socket may already be gone — safe to ignore
            }

            await RaiseEventAsync(OnDisconnected, closeStatus, statusDescription);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void EnsureConnected()
        {
            if (_socket.State != WebSocketState.Open)
                throw new InvalidOperationException(
                    $"WebSocket is not open. Current state: {_socket.State}");
        }

        private static async Task RaiseEventAsync(Func<Task>? handler)
        {
            if (handler != null) await handler();
        }

        private static async Task RaiseEventAsync<T>(Func<T, Task>? handler, T arg)
        {
            if (handler != null) await handler(arg);
        }

        private static async Task RaiseEventAsync<T1, T2>(Func<T1, T2, Task>? handler, T1 a, T2 b)
        {
            if (handler != null) await handler(a, b);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            _socket.Dispose();
            _cts.Dispose();
        }
    }
}