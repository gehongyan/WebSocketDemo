using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocketDemo;

public class BusinessSocketClient
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ConnectionManager _connection;
    private readonly Logger _gatewayLogger;
    private readonly SemaphoreSlim _stateLock;
    public ConnectionState ConnectionState => _connection.State;

    /// <summary>
    ///     Fired when a log message is sent.
    /// </summary>
    public event Func<LogMessage, Task> Log
    {
        add => _logEvent.Add(value);
        remove => _logEvent.Remove(value);
    }

    internal readonly AsyncEvent<Func<LogMessage, Task>> _logEvent = new();


    /// <summary> Fired when connected to the Business gateway. </summary>
    public event Func<Task> Connected
    {
        add => _connectedEvent.Add(value);
        remove => _connectedEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<Task>> _connectedEvent = new();

    /// <summary> Fired when disconnected to the Business gateway. </summary>
    public event Func<Exception, Task> Disconnected
    {
        add => _disconnectedEvent.Add(value);
        remove => _disconnectedEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<Exception, Task>> _disconnectedEvent = new();

    /// <summary>
    ///     Fired when guild data has finished downloading.
    /// </summary>
    public event Func<Task> Ready
    {
        add => _readyEvent.Add(value);
        remove => _readyEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<Task>> _readyEvent = new();

    /// <summary> Fired when a heartbeat is received from the Business gateway. </summary>
    public event Func<int, int, Task> LatencyUpdated
    {
        add => _latencyUpdatedEvent.Add(value);
        remove => _latencyUpdatedEvent.Remove(value);
    }

    private readonly AsyncEvent<Func<int, int, Task>> _latencyUpdatedEvent = new();

    public int Latency { get; protected set; }
    internal WebSocketProvider WebSocketProvider { get; private set; }
    internal BusinessSocketApiClient ApiClient { get; }
    internal LogManager LogManager { get; }
    internal int? HandlerTimeout => Convert.ToInt32(TimeSpan.FromSeconds(3).TotalMilliseconds);

    private static BusinessSocketApiClient CreateApiClient(string gatewayUrl, JsonSerializerOptions jsonOptions)
        => new(DefaultWebSocketProvider.Instance, gatewayUrl, jsonOptions);

    public BusinessSocketClient(string gatewayUrl, JsonSerializerOptions jsonOptions)
    {
        WebSocketProvider = DefaultWebSocketProvider.Instance;
        ApiClient = CreateApiClient(gatewayUrl, jsonOptions);
        LogManager = new LogManager(LogSeverity.Debug);
        LogManager.Message += async msg => await _logEvent.InvokeAsync(msg).ConfigureAwait(false);
        _stateLock = new SemaphoreSlim(1, 1);
        _gatewayLogger = LogManager.CreateLogger("Gateway");
        _connection = new ConnectionManager(_stateLock, _gatewayLogger, Convert.ToInt32(TimeSpan.FromSeconds(5).TotalMilliseconds),
            OnConnectingAsync, OnDisconnectingAsync, x => ApiClient.Disconnected += x);
        _connection.Connected += () => TimedInvokeAsync(_connectedEvent, nameof(Connected));
        _connection.Connected += () => TimedInvokeAsync(_readyEvent, nameof(Ready));
        _connection.Disconnected += (ex, recon) => TimedInvokeAsync(_disconnectedEvent, nameof(Disconnected), ex);
        LatencyUpdated += async (old, val) => await _gatewayLogger.DebugAsync($"Latency = {val} ms").ConfigureAwait(false);

        _serializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        ApiClient.SentGatewayMessage += async socketFrameType =>
            await _gatewayLogger.DebugAsync($"Sent {socketFrameType}").ConfigureAwait(false);
        ApiClient.ReceivedGatewayEvent += ProcessMessageAsync;
    }

    public async Task StartAsync() => await _connection.StartAsync().ConfigureAwait(false);

    public async Task StopAsync() => await _connection.StopAsync().ConfigureAwait(false);

    private async Task OnConnectingAsync()
    {
        try
        {
            await _gatewayLogger.DebugAsync("Connecting ApiClient").ConfigureAwait(false);
            await ApiClient.ConnectAsync().ConfigureAwait(false);
            await _connection.CompleteAsync();
        }
        catch
        {
            // ignored
        }

        await _connection.WaitAsync().ConfigureAwait(false);
    }

    private async Task OnDisconnectingAsync(Exception ex)
    {
        await _gatewayLogger.DebugAsync("Disconnecting ApiClient").ConfigureAwait(false);
        await ApiClient.DisconnectAsync(ex).ConfigureAwait(false);
    }

    private /*async*/ Task ProcessMessageAsync(object payload)
    {
        // 此处处理消息
        // if (your condition)
        // {
        //     await TimedInvokeAsync(_receiveOnePackageEvent, nameof(ReceiveOnePackage), payload).ConfigureAwait(false);
        // }
        // if (another condition)
        // {
        //     await TimedInvokeAsync(_receiveAnotherPackageEvent, nameof(ReceiveAnotherPackage), payload).ConfigureAwait(false);
        // }
        Console.WriteLine($"Received: {JsonSerializer.Serialize(payload, _serializerOptions)}");
        return Task.CompletedTask;
    }

    private async Task TimedInvokeAsync(AsyncEvent<Func<Task>> eventHandler, string name)
    {
        if (eventHandler.HasSubscribers)
        {
            if (HandlerTimeout.HasValue)
                await TimeoutWrap(name, eventHandler.InvokeAsync).ConfigureAwait(false);
            else
                await eventHandler.InvokeAsync().ConfigureAwait(false);
        }
    }

    private async Task TimedInvokeAsync<T>(AsyncEvent<Func<T, Task>> eventHandler, string name, T arg)
    {
        if (eventHandler.HasSubscribers)
        {
            if (HandlerTimeout.HasValue)
                await TimeoutWrap(name, () => eventHandler.InvokeAsync(arg)).ConfigureAwait(false);
            else
                await eventHandler.InvokeAsync(arg).ConfigureAwait(false);
        }
    }

    private async Task TimedInvokeAsync<T1, T2>(AsyncEvent<Func<T1, T2, Task>> eventHandler, string name, T1 arg1,
        T2 arg2)
    {
        if (eventHandler.HasSubscribers)
        {
            if (HandlerTimeout.HasValue)
                await TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2)).ConfigureAwait(false);
            else
                await eventHandler.InvokeAsync(arg1, arg2).ConfigureAwait(false);
        }
    }

    private async Task TimedInvokeAsync<T1, T2, T3>(AsyncEvent<Func<T1, T2, T3, Task>> eventHandler, string name,
        T1 arg1, T2 arg2, T3 arg3)
    {
        if (eventHandler.HasSubscribers)
        {
            if (HandlerTimeout.HasValue)
                await TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3)).ConfigureAwait(false);
            else
                await eventHandler.InvokeAsync(arg1, arg2, arg3).ConfigureAwait(false);
        }
    }

    private async Task TimedInvokeAsync<T1, T2, T3, T4>(AsyncEvent<Func<T1, T2, T3, T4, Task>> eventHandler,
        string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (eventHandler.HasSubscribers)
        {
            if (HandlerTimeout.HasValue)
                await TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3, arg4)).ConfigureAwait(false);
            else
                await eventHandler.InvokeAsync(arg1, arg2, arg3, arg4).ConfigureAwait(false);
        }
    }

    private async Task TimedInvokeAsync<T1, T2, T3, T4, T5>(AsyncEvent<Func<T1, T2, T3, T4, T5, Task>> eventHandler,
        string name, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (eventHandler.HasSubscribers)
        {
            if (HandlerTimeout.HasValue)
                await TimeoutWrap(name, () => eventHandler.InvokeAsync(arg1, arg2, arg3, arg4, arg5))
                    .ConfigureAwait(false);
            else
                await eventHandler.InvokeAsync(arg1, arg2, arg3, arg4, arg5).ConfigureAwait(false);
        }
    }

    private async Task TimeoutWrap(string name, Func<Task> action)
    {
        try
        {
            Task timeoutTask = Task.Delay(HandlerTimeout ?? 0);
            Task handlersTask = action();
            if (await Task.WhenAny(timeoutTask, handlersTask).ConfigureAwait(false) == timeoutTask)
                await _gatewayLogger.WarningAsync($"A {name} handler is blocking the gateway task.")
                    .ConfigureAwait(false);

            await handlersTask.ConfigureAwait(false); //Ensure the handler completes
        }
        catch (Exception ex)
        {
            await _gatewayLogger.WarningAsync($"A {name} handler has thrown an unhandled exception.", ex)
                .ConfigureAwait(false);
        }
    }

}
