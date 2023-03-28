#if DEBUG_PACKETS
using System.Diagnostics;
#endif

using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebSocketDemo.Models;

namespace WebSocketDemo;

public class BusinessSocketApiClient
{
    private readonly string? _gatewayUrl;
    private readonly SemaphoreSlim _stateLock;
    private CancellationTokenSource? _connectCancelToken;

    public event Func<object, Task> SentGatewayMessage
    {
        add => _sentGatewayMessageEvent.Add(value);
        remove => _sentGatewayMessageEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<object, Task>> _sentGatewayMessageEvent = new();

    public event Func<GatewaySocketFrame, Task> ReceivedGatewayEvent
    {
        add => _receivedGatewayEvent.Add(value);
        remove => _receivedGatewayEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<GatewaySocketFrame, Task>> _receivedGatewayEvent = new();

    public event Func<Exception, Task> Disconnected
    {
        add => _disconnectedEvent.Add(value);
        remove => _disconnectedEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new();

    private readonly JsonSerializerOptions _serializerOptions;
    public ConnectionState ConnectionState { get; private set; }
    private IWebSocketClient WebSocketClient { get; }

    public BusinessSocketApiClient(WebSocketProvider webSocketProvider, string? gatewayUrl = null, JsonSerializerOptions? serializerOptions = null)
    {
        _gatewayUrl = gatewayUrl;
        _serializerOptions = serializerOptions
            ?? new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
        _stateLock = new SemaphoreSlim(1, 1);

        WebSocketClient = webSocketProvider();
        WebSocketClient.TextMessage += OnTextMessage;
        WebSocketClient.BinaryMessage += OnBinaryMessage;
        WebSocketClient.Closed += async ex =>
        {
#if DEBUG_PACKETS
            Debug.WriteLine(ex);
#endif

            await DisconnectAsync().ConfigureAwait(false);
            await _disconnectedEvent.InvokeAsync(ex).ConfigureAwait(false);
        };
    }


    private async Task OnBinaryMessage(byte[] data, int index, int count)
    {
        await using MemoryStream decompressed = new();
        using MemoryStream compressed = data[0] == 0x78
            ? new MemoryStream(data, index + 2, count - 2)
            : new MemoryStream(data, index, count);
        await using DeflateStream decompressor = new(compressed, CompressionMode.Decompress);
        await decompressor.CopyToAsync(decompressed);
        decompressed.Position = 0;

        var socketPayload = JsonSerializer.Deserialize<GatewaySocketFrame>(decompressed, _serializerOptions);
        if (socketPayload is not null)
        {
            await _receivedGatewayEvent.InvokeAsync(socketPayload)
                .ConfigureAwait(false);
        }
    }

    private async Task OnTextMessage(string message)
    {
        GatewaySocketFrame? gatewaySocketFrame = JsonSerializer.Deserialize<GatewaySocketFrame>(message, _serializerOptions);
        if (gatewaySocketFrame is not null)
        {
#if DEBUG_PACKETS
            Debug.WriteLine($"<- {JsonSerializer.Serialize(gatewaySocketFrame, new JsonSerializerOptions {WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping})}".TrimEnd('\n'));
#endif
            await _receivedGatewayEvent.InvokeAsync(gatewaySocketFrame)
                .ConfigureAwait(false);
        }
    }

    public async Task ConnectAsync()
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await ConnectInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    internal async Task ConnectInternalAsync()
    {

        if (WebSocketClient == null)
            throw new NotSupportedException("This client is not configured with WebSocket support.");
        if (string.IsNullOrWhiteSpace(_gatewayUrl))
            throw new ArgumentNullException(nameof(_gatewayUrl), "The gateway url must be provided.");

        ConnectionState = ConnectionState.Connecting;

        try
        {
            _connectCancelToken?.Dispose();
            _connectCancelToken = new CancellationTokenSource();
            WebSocketClient?.SetCancelToken(_connectCancelToken.Token);

#if DEBUG_PACKETS
            Debug.WriteLine("Connecting to gateway: " + _gatewayUrl);
#endif

            await WebSocketClient!.ConnectAsync(_gatewayUrl).ConfigureAwait(false);
            ConnectionState = ConnectionState.Connected;
        }
        catch (Exception)
        {
            await DisconnectInternalAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync(Exception? ex = null)
    {
        await _stateLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisconnectInternalAsync(ex).ConfigureAwait(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    internal async Task DisconnectInternalAsync(Exception? ex = null)
    {
        if (WebSocketClient == null) throw new NotSupportedException("This client is not configured with WebSocket support.");

        if (ConnectionState == ConnectionState.Disconnected) return;

        ConnectionState = ConnectionState.Disconnecting;

        try
        {
            _connectCancelToken?.Cancel(false);
        }
        catch
        {
            // ignored
        }

        await WebSocketClient.DisconnectAsync().ConfigureAwait(false);

        ConnectionState = ConnectionState.Disconnected;
    }

    public Task SendGatewayAsync(object payload) => SendGatewayInternalAsync(payload);

    private async Task SendGatewayInternalAsync(object payload)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(SerializeJson(payload));
        await WebSocketClient.SendAsync(bytes, 0, bytes.Length, true).ConfigureAwait(false);
        await _sentGatewayMessageEvent.InvokeAsync(payload).ConfigureAwait(false);

#if DEBUG_PACKETS
        Debug.WriteLine($"-> {JsonSerializer.Serialize(payload, new JsonSerializerOptions {WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping})}".TrimEnd('\n'));
#endif
    }

    private string SerializeJson(object payload) =>
        payload is null
            ? string.Empty
            : JsonSerializer.Serialize(payload, _serializerOptions);

    private async Task<T?> DeserializeJsonAsync<T>(Stream jsonStream)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(jsonStream, _serializerOptions).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            if (jsonStream is MemoryStream memoryStream)
            {
                string json = Encoding.UTF8.GetString(memoryStream.ToArray());
                throw new JsonException($"Failed to deserialize JSON to type {typeof(T).FullName}\nJSON: {json}", ex);
            }

            throw;
        }
    }




}
