using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using MQTTnet;
using MQTTnet.Diagnostics.PacketInspection;
using MQTTnet.Protocol;

using Polly;

namespace NRuuviTag.Mqtt;

/// <summary>
/// <see cref="IMqttClient"/> implementation that adds support for automatic reconnections and
/// queued message publishing.
/// </summary>
/// <remarks>
///
/// <para>
///   Calling <see cref="ConnectAsync"/> uses a resilience pipeline to ensure that the client
///   keeps retrying in the event of transient connection failures. Once connected, if the client
///   becomes disconnected unexpectedly it automatically starts reconnecting in the background.
/// </para>
/// 
/// <para>
///   Call <see cref="EnqueueAsync"/> instead of <see cref="IMqttClient.PublishAsync"/> to enqueue
///   messages for publishing. The client automatically publishes queued messages in a background
///   task when connected to the broker. Use <see cref="ManagedMqttClientOptions"/> to configure
///   the message queue behaviour.
/// </para>
///
/// <para>
///   The client uses a resilience pipeline to ensure that queued messages are published
///   successfully even in the event of transient failures. Note that messages that use QoS 0 are
///   never retried as they do not guarantee delivery.
/// </para>
/// 
/// </remarks>
public sealed partial class ManagedMqttClient : IMqttClient {

    private bool _disposed;
    
    private readonly ILogger<ManagedMqttClient> _logger;
    
    private readonly IMqttClient _mqttClient;

    /// <summary>
    /// Polly pipeline for connect operations.
    /// </summary>
    private readonly ResiliencePipeline<MqttClientConnectResult> _connectPipeline;

    /// <summary>
    /// Polly pipeline for publish operations.
    /// </summary>
    private readonly ResiliencePipeline _publishPipeline;
    
    private readonly Nito.AsyncEx.AsyncLock _lock = new Nito.AsyncEx.AsyncLock();
    
    private readonly Nito.AsyncEx.AsyncManualResetEvent _connected = new Nito.AsyncEx.AsyncManualResetEvent();
    
    private readonly Nito.AsyncEx.AsyncManualResetEvent _disconnected = new Nito.AsyncEx.AsyncManualResetEvent(set: true);

    private int _publishTaskRunning;
    
    private int _reconnectTaskRunning;

    private readonly Channel<MqttApplicationMessage> _queuedMessages;
    
    private readonly CancellationTokenSource _lifetimeTokenSource = new CancellationTokenSource();
    
    private CancellationTokenSource _disconnectRequested = new CancellationTokenSource();

    /// <inheritdoc />
    public bool IsConnected => _mqttClient.IsConnected;

    /// <inheritdoc />
    public MqttClientOptions Options => _mqttClient.Options;

    /// <inheritdoc />
    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync {
        add => _mqttClient.ApplicationMessageReceivedAsync += value;
        remove => _mqttClient.ApplicationMessageReceivedAsync -= value;
    }

    /// <inheritdoc />
    public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync {
        add => _mqttClient.ConnectedAsync += value;
        remove => _mqttClient.ConnectedAsync -= value;
    }

    /// <inheritdoc />
    public event Func<MqttClientConnectingEventArgs, Task>? ConnectingAsync {
        add => _mqttClient.ConnectingAsync += value;
        remove => _mqttClient.ConnectingAsync -= value;
    }

    /// <inheritdoc />
    public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync {
        add => _mqttClient.DisconnectedAsync += value;
        remove => _mqttClient.DisconnectedAsync -= value;
    }

    /// <inheritdoc />
    public event Func<InspectMqttPacketEventArgs, Task>? InspectPacketAsync {
        add => _mqttClient.InspectPacketAsync += value;
        remove => _mqttClient.InspectPacketAsync -= value;
    }
    
    
    /// <summary>
    /// Creates a new <see cref="ManagedMqttClient"/> instance.
    /// </summary>
    /// <param name="mqttClient">
    ///   The underlying <see cref="IMqttClient"/> to use.
    /// </param>
    /// <param name="options">
    ///   The <see cref="ManagedMqttClientOptions"/> to use.
    /// </param>
    /// <param name="logger">
    ///   The logger for the client.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="mqttClient"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public ManagedMqttClient(IMqttClient mqttClient, ManagedMqttClientOptions options, ILogger<ManagedMqttClient>? logger = null) {
        _mqttClient = mqttClient ?? throw new ArgumentNullException(nameof(mqttClient));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ManagedMqttClient>.Instance;

        var onMessageDropped = options.OnMessageDropped;

        _queuedMessages = options.QueueSize < 1
            ? Channel.CreateUnbounded<MqttApplicationMessage>(
                new UnboundedChannelOptions() {
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                })
            : Channel.CreateBounded<MqttApplicationMessage>(
                new BoundedChannelOptions(options.QueueSize) {
                    FullMode = options.QueueFullMode,
                    SingleReader = true,
                    SingleWriter = false,
                    AllowSynchronousContinuations = false
                },
                item => {
                    LogMessageDropped(item.Topic);
                    onMessageDropped?.Invoke(item);
                });
        
        _connectPipeline = new ResiliencePipelineBuilder<MqttClientConnectResult>().AddRetry(new Polly.Retry.RetryStrategyOptions<MqttClientConnectResult>() { 
            MaxRetryAttempts = int.MaxValue,
            BackoffType = DelayBackoffType.Exponential,
            MaxDelay = TimeSpan.FromSeconds(30),
            Delay = TimeSpan.FromSeconds(1),
            UseJitter = true,
            ShouldHandle = result => {
                var shouldRetry = result.Outcome switch {
                    { Exception: ArgumentException } => false,
                    { Exception: InvalidOperationException } => false,
                    { Exception: OperationCanceledException } => false,
                    { Exception: null } => false,
                    _ => true
                };
                return new ValueTask<bool>(shouldRetry);
            }
        }).Build();

        _publishPipeline = new ResiliencePipelineBuilder().AddRetry(new Polly.Retry.RetryStrategyOptions() {
            MaxRetryAttempts = int.MaxValue,
            OnRetry = args => {
                args.Context.Properties.Set(new ResiliencePropertyKey<int>("AttemptNumber"), args.AttemptNumber);
                return default;
            }
        }).Build();

        RegisterEventHandlers();

        if (_mqttClient.IsConnected) {
            OnConnectedCore();
        }
    }


    /// <inheritdoc />
    public async Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        if (_disconnectRequested.IsCancellationRequested) {
            _disconnectRequested = new CancellationTokenSource();
        }
        return await ConnectCoreAsync(options, cts.Token).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public async Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        await _disconnectRequested.CancelAsync().ConfigureAwait(false);
        await _mqttClient.DisconnectAsync(options, cts.Token).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public async Task PingAsync(CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        await _mqttClient.PingAsync(cancellationToken).ConfigureAwait(false);
    }


    /// <inheritdoc />
    async Task<MqttClientPublishResult> IMqttClient.PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken) {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        return await _mqttClient.PublishAsync(applicationMessage, cts.Token).ConfigureAwait(false);
    }


    /// <summary>
    /// Enqueues a message for publishing.
    /// </summary>
    /// <param name="applicationMessage">
    ///   The <see cref="MqttApplicationMessage"/> to enqueue.
    /// </param>
    /// <param name="cancellationToken">
    ///   The cancellation token for the operation.
    /// </param>
    /// <exception cref="ObjectDisposedException">
    ///   The client has been disposed.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="applicationMessage"/> is <see langword="null"/>.
    /// </exception>
    public async Task EnqueueAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(applicationMessage);
        
        // Don't need a linked token source here because the channel will be completed on disposal.
        await _queuedMessages.Writer.WriteAsync(applicationMessage, cancellationToken).ConfigureAwait(false);
        LogMessageEnqueued(applicationMessage.Topic, (int) applicationMessage.QualityOfServiceLevel);
    }


    /// <inheritdoc />
    public async Task SendEnhancedAuthenticationExchangeDataAsync(MqttEnhancedAuthenticationExchangeData data, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        await _mqttClient.SendEnhancedAuthenticationExchangeDataAsync(data, cts.Token).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public async Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var  _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        return await _mqttClient.SubscribeAsync(options, cts.Token).ConfigureAwait(false);
    }


    /// <inheritdoc />
    public async Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken = default) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeTokenSource.Token);
        using var  _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
        
        return await _mqttClient.UnsubscribeAsync(options, cts.Token).ConfigureAwait(false);
    }

    
    private async Task<MqttClientConnectResult> ConnectCoreAsync(MqttClientOptions options, CancellationToken cancellationToken) {
        return await _connectPipeline.ExecuteAsync(async (opts, ct) => await _mqttClient.ConnectAsync(opts, ct).ConfigureAwait(false), options, cancellationToken).ConfigureAwait(false);
    }
    
    
    private async Task ReconnectCoreAsync() {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeTokenSource.Token, _disconnectRequested.Token);

        try {
            while (!cts.IsCancellationRequested) {
                // Wait for disconnection.
                await _disconnected.WaitAsync(cts.Token).ConfigureAwait(false);
                using var _ = await _lock.LockAsync(cts.Token).ConfigureAwait(false);
                LogReconnectStarted();
                await ConnectCoreAsync(_mqttClient.Options, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) {
            // Task was cancelled, exit gracefully
        }
        catch (Exception e) {
            LogReconnectFaulted(e);
        }
        finally {
            Interlocked.Exchange(ref _reconnectTaskRunning, 0);
        }
    }


    private void RegisterEventHandlers() {
        ConnectingAsync += OnConnectingAsync;
        ConnectedAsync += OnConnectedAsync;
        DisconnectedAsync += OnDisconnectedAsync;
    }
    

    private Task OnConnectingAsync(MqttClientConnectingEventArgs args) {
        LogConnecting(GetServerAddress(args.ClientOptions.ChannelOptions)!);
        return Task.CompletedTask;
    }
    
    
    private Task OnConnectedAsync(MqttClientConnectedEventArgs args) {
        OnConnectedCore();
        return Task.CompletedTask;
    }


    private void OnConnectedCore() {
        if (_disposed) {
            return;
        }

        LogConnected();
        _disconnected.Reset();
        _connected.Set();
        if (Interlocked.CompareExchange(ref _publishTaskRunning, 1, 0) == 0) {
            _ = PublishQueuedMessagesAsync(_lifetimeTokenSource.Token);
        }
    }


    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args) {
        LogDisconnected(args.Reason, args.ReasonString);
        
        _connected.Reset();
        _disconnected.Set();
        
        if (!_lifetimeTokenSource.IsCancellationRequested && !_disconnectRequested.IsCancellationRequested && Interlocked.CompareExchange(ref _reconnectTaskRunning, 1, 0) == 0) { 
            // Start reconnect in the background.
            _ = ReconnectCoreAsync();
        }
        
        return Task.CompletedTask;
    }
    

    private async Task PublishQueuedMessagesAsync(CancellationToken cancellationToken) {
        LogPublishLoopStarted();
        
        try {
            while (!cancellationToken.IsCancellationRequested) {
                await foreach (var message in _queuedMessages.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) {
                    ResilienceContext? context = null;

                    try {
                        if (string.IsNullOrWhiteSpace(message.Topic)) {
                            continue;
                        }

                        // QoS 0 doesn't require retry handling.
                        if (message.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce) {
                            using var _ = await _lock.LockAsync(cancellationToken).ConfigureAwait(false);
                            if (!IsConnected) {
                                continue;
                            }

                            LogPublishingMessage(message.Topic, 1);
                            var publishResult = await _mqttClient.PublishAsync(message, cancellationToken).ConfigureAwait(false);
                            LogPublishCompleted(message.Topic, publishResult.IsSuccess, publishResult.ReasonCode, publishResult.ReasonString);
                            continue;
                        }
                        
                        context = ResilienceContextPool.Shared.Get(cancellationToken);

                        await _publishPipeline.ExecuteAsync(
                            async (ctx, msg) => {
                                if (_logger.IsEnabled(LogLevel.Debug)) {
                                    if (!ctx.Properties.TryGetValue(new ResiliencePropertyKey<int>("AttemptNumber"), out var attemptNumber)) {
                                        attemptNumber = 0;
                                    }
                                    LogPublishingMessage(msg.Topic, attemptNumber + 1);
                                }
                                await _connected.WaitAsync(ctx.CancellationToken).ConfigureAwait(false);
                                using var _ = await _lock.LockAsync(ctx.CancellationToken).ConfigureAwait(false);
                                var publishResult = await _mqttClient.PublishAsync(msg, ctx.CancellationToken).ConfigureAwait(false);
                                LogPublishCompleted(msg.Topic, publishResult.IsSuccess, publishResult.ReasonCode, publishResult.ReasonString);
                            },
                            context,
                            message).ConfigureAwait(false);
                    }
                    catch (Exception e) {
                        LogPublishFaulted(message.Topic, e);
                    }
                    finally {
                        if (context is not null) {
                            // Return the context to the pool
                            ResilienceContextPool.Shared.Return(context);
                        }
                    }
                }
            }
            LogPublishLoopStopped();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            LogPublishLoopStopped();
        }
        catch (Exception e) {
            LogPublishLoopFaulted(e);
        }
        finally {
            Interlocked.Exchange(ref _publishTaskRunning, 0);
        }
    }
    
    
    private static string? GetServerAddress(IMqttClientChannelOptions channelOptions) {
        return channelOptions switch {
            MqttClientTcpOptions tcpOptions => tcpOptions.RemoteEndpoint switch {
                System.Net.DnsEndPoint dnsEndpoint => $"{dnsEndpoint.Host}:{dnsEndpoint.Port}",
                System.Net.IPEndPoint ipEndPoint => $"{ipEndPoint.Address}:{ipEndPoint.Port}",
                _ => tcpOptions.RemoteEndpoint.ToString()
            },
            MqttClientWebSocketOptions webSocketOptions => webSocketOptions.Uri,
            _ => null
        };
    }
    
    
    /// <inheritdoc />
    public void Dispose() {
        if (_disposed) {
            return;
        }
        
        _lifetimeTokenSource.Cancel();
        _queuedMessages.Writer.TryComplete();
        _mqttClient.Dispose();
        
        _disposed = true;
    }
    
    
    [LoggerMessage(100, LogLevel.Debug, "Connecting to MQTT broker: {server}")]
    partial void LogConnecting(string server);

    [LoggerMessage(101, LogLevel.Information, "Connected to MQTT broker.")]
    partial void LogConnected();

    [LoggerMessage(102, LogLevel.Debug, "Disconnected from MQTT broker: reason code = {reasonCode}, reason = '{reason}'")]
    partial void LogDisconnected(MqttClientDisconnectReason reasonCode, string reason);

    [LoggerMessage(103, LogLevel.Information, "Reconnecting to MQTT broker.")]
    partial void LogReconnectStarted();

    [LoggerMessage(104, LogLevel.Debug, "MQTT broker reconnect faulted.")]
    partial void LogReconnectFaulted(Exception error);

    [LoggerMessage(200, LogLevel.Debug, "MQTT publish loop started.")]
    partial void LogPublishLoopStarted();

    [LoggerMessage(201, LogLevel.Debug, "MQTT publish loop stopped.")]
    partial void LogPublishLoopStopped();

    [LoggerMessage(202, LogLevel.Error, "MQTT publish loop faulted.")]
    partial void LogPublishLoopFaulted(Exception error);

    [LoggerMessage(203, LogLevel.Debug, "Publishing message to topic '{topic}': attempt = {attempt}", SkipEnabledCheck = true)]
    partial void LogPublishingMessage(string topic, int attempt);

    [LoggerMessage(204, LogLevel.Debug, "Published message to topic '{topic}': success = {success}, reason code = {reasonCode}, reason = '{reasonString}'")]
    partial void LogPublishCompleted(string topic, bool success, MqttClientPublishReasonCode reasonCode, string reasonString);

    [LoggerMessage(205, LogLevel.Error, "Error publishing message to topic '{topic}'.")]
    partial void LogPublishFaulted(string topic, Exception error);

    [LoggerMessage(206, LogLevel.Debug, "Enqueued message for topic '{topic}' with QoS {qos}.")]
    partial void LogMessageEnqueued(string topic, int qos);
    
    [LoggerMessage(207, LogLevel.Warning, "Dropped queued message for topic '{topic}'.")]
    partial void LogMessageDropped(string topic);

}


/// <summary>
/// Options for <see cref="ManagedMqttClient"/>.
/// </summary>
public class ManagedMqttClientOptions {

    /// <summary>
    /// The maximum size of the message queue.
    /// </summary>
    /// <remarks>
    ///   Specify less than 1 for an unbounded queue.
    /// </remarks>
    public int QueueSize { get; set; } = 1000;
    
    /// <summary>
    /// The full mode to use for a bounded message queue.
    /// </summary>
    /// <remarks>
    ///   This property is ignored if <see cref="QueueSize"/> is less than 1.
    /// </remarks>
    public BoundedChannelFullMode QueueFullMode { get; set; } = BoundedChannelFullMode.Wait;
    
    /// <summary>
    /// A callback to invoke when a message is dropped from the queue.
    /// </summary>
    /// <remarks>
    ///   This property is ignored if <see cref="QueueSize"/> is less than 1 or if <see cref="QueueFullMode"/>
    ///   is set to <see cref="BoundedChannelFullMode.Wait"/>.
    /// </remarks>
    public Action<MqttApplicationMessage>? OnMessageDropped { get; set; }

}
