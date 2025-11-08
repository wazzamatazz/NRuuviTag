using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

using Jaahas.CertificateUtilities;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MQTTnet;
using MQTTnet.Formatter;

using NRuuviTag.Mqtt;

using Spectre.Console;
using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

/// <summary>
/// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts and publishing the
/// samples to an MQTT broker.
/// </summary>
public class PublishMqttCommand : AsyncCommand<PublishMqttCommandSettings> {

    /// <summary>
    /// The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </summary>
    private readonly IRuuviTagListener _listener;

    /// <summary>
    /// The <see cref="MqttFactory"/> that is used to create an MQTT client.
    /// </summary>
    private readonly MqttFactory _mqttFactory;

    /// <summary>
    /// The known RuuviTag devices.
    /// </summary>
    private readonly IOptionsMonitor<DeviceCollection> _devices;

    /// <summary>
    /// The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </summary>
    private readonly IHostApplicationLifetime _appLifetime;

    /// <summary>
    /// The <see cref="ILoggerFactory"/> for the application.
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;


    /// <summary>
    /// Creates a new <see cref="PublishMqttCommand"/> object.
    /// </summary>
    /// <param name="listener">
    ///   The <see cref="IRuuviTagListener"/> to listen to broadcasts with.
    /// </param>
    /// <param name="mqttFactory">
    ///   The <see cref="MqttFactory"/> that is used to create an MQTT client.
    /// </param>
    /// <param name="devices">
    ///   The known RuuviTag devices.
    /// </param>
    /// <param name="appLifetime">
    ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </param>
    /// <param name="loggerFactory">
    ///   The <see cref="ILoggerFactory"/> for the application.
    /// </param>
    public PublishMqttCommand(
        IRuuviTagListener listener, 
        MqttFactory mqttFactory, 
        IOptionsMonitor<DeviceCollection> devices, 
        IHostApplicationLifetime appLifetime, 
        ILoggerFactory loggerFactory
    ) {
        _listener = listener;
        _mqttFactory = mqttFactory;
        _devices = devices;
        _loggerFactory = loggerFactory;
        _appLifetime = appLifetime;
    }


    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, PublishMqttCommandSettings settings, CancellationToken cancellationToken) {
        if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
            try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        var version = MqttProtocolVersion.Unknown;
        if (Enum.TryParse<MqttProtocolVersion>(settings.ProtocolVersion, out var vEnum)) {
            version = vEnum;
        }
        else if (Version.TryParse(settings.ProtocolVersion, out var v)) {
            version = v.ToString(3) switch {
                "3.1.0" => MqttProtocolVersion.V310,
                "3.1.1" => MqttProtocolVersion.V311,
                "5.0.0" => MqttProtocolVersion.V500,
                _ => version
            };
        }

        IEnumerable<Device> devices = null!;

        UpdateDevices(_devices.CurrentValue);

        var publisherOptions = new MqttPublisherOptions() {
            Hostname = settings.Hostname,
            ClientId = settings.ClientId,
            UserName = settings.UserName,
            Password = settings.Password,
            ProtocolVersion = version,
            TopicName = settings.TopicName,
            PublishInterval = TimeSpan.FromSeconds(settings.PublishInterval),
            PerDevicePublishBehaviour = settings.PublishBehaviour,
            PublishType = settings.PublishType,
            KnownDevicesOnly = settings.KnownDevicesOnly,
            TlsOptions = new MqttPublisherTlsOptions() { 
                UseTls = settings.UseTls,
                AllowUntrustedCertificates = settings.AllowUntrustedCertificates,
                IgnoreCertificateChainErrors = settings.IgnoreCertificateChainErrors,
                ClientCertificates = settings.GetClientCertificates()
            },
            GetDeviceInfo = addr => {
                lock (this) {
                    return devices.FirstOrDefault(x => MacAddressComparer.Instance.Equals(addr, x.MacAddress));
                }
            }
        };

        await using var publisher = new MqttPublisher(_listener, publisherOptions, _mqttFactory, _loggerFactory);

        using (_devices.OnChange(UpdateDevices))
        using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
            try {
                await publisher.RunAsync(ctSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ctSource.IsCancellationRequested) { }
        }

        return 0;

        void UpdateDevices(DeviceCollection? devicesFromConfig) {
            lock (this) {
                devices = devicesFromConfig?.GetDevices() ?? [];
            }
        }
    }

}


/// <summary>
/// Settings for <see cref="PublishMqttCommand"/>.
/// </summary>
public class PublishMqttCommandSettings : CommandSettings {

    [CommandArgument(0, "[HOSTNAME_OR_URL]")]
    [DefaultValue("localhost")]
    [Description("The hostname, IP address, or URL for the MQTT broker (e.g. 'my-broker.local:21883', 'ws://mybroker.local:8080/mqtt'). The port number is only required when connecting on a non-default port. When specifying a URL, the '--use-tls' flag is implied if the 'https' or 'wss' scheme is specified in the URL.")]
    public string? Hostname { get; set; }

    [CommandOption("--client-id <CLIENT_ID>")]
    [Description("The MQTT client ID to use. If not specified, a client ID will be generated.")]
    public string? ClientId { get; set; }

    [CommandOption("--username <USER_NAME>")]
    [Description("The MQTT user name.")]
    public string? UserName { get; set; }

    [CommandOption("--password <PASSWORD>")]
    [Description("The MQTT password.")]
    public string? Password { get; set; }

    [CommandOption("--version <VERSION>")]
    [DefaultValue("5")]
    [Description("MQTT protocol version to use.")]
    public string? ProtocolVersion { get; set; }

    [CommandOption("--publish-interval <INTERVAL>")]
    [Description("The publish to use, in seconds. When a publish interval is specified, the '--publish-behaviour' setting controls if all observed samples for a device are included in the next publish, or if only the most-recent reading for each device are included. If a publish inteval is not specified, samples will be published to the MQTT server as soon as they are observed.")]
    public int PublishInterval { get; set; }
    
    [CommandOption("--publish-behaviour <BEHAVIOUR>")]
    [Description("The per-device publish behaviour to use when a non-zero publish interval is specified. Possible values are: " + nameof(BatchPublishDeviceBehaviour.AllSamples) + " (default), " + nameof(BatchPublishDeviceBehaviour.LatestSampleOnly))]
    [DefaultValue(BatchPublishDeviceBehaviour.AllSamples)]
    public BatchPublishDeviceBehaviour PublishBehaviour { get; set; }

    [CommandOption("--publish-type <PUBLISH_TYPE>")]
    [DefaultValue(PublishType.SingleTopic)]
    [Description("The type of MQTT publish to perform. Possible values are: " + nameof(PublishType.SingleTopic) + " (default), " + nameof(PublishType.TopicPerMeasurement))]
    public PublishType PublishType { get; set; }

    [CommandOption("--topic <TOPIC>")]
    [DefaultValue(MqttPublisherOptions.DefaultTopicName)]
    [Description("The MQTT topic to publish messages to. In topic-per-measurement mode, this is used as the topic prefix.")]
    public string TopicName { get; set; } = default!;

    [CommandOption("--known-devices")]
    [Description("Specifies if only samples from pre-registered devices should be published to the MQTT broker.")]
    public bool KnownDevicesOnly { get; set; }

    [CommandOption("--use-tls")]
    [Description("Specifies if TLS should be used for the connection.")]
    public bool UseTls { get; set; }

    [CommandOption("--allow-untrusted-certificates")]
    [Description("Specifies if untrusted server certificates are allowed. This setting is ignored if TLS is not being used.")]
    public bool AllowUntrustedCertificates { get; set; }

    [CommandOption("--ignore-certificate-chain-errors")]
    [Description("Specifies if certificate chain errors should be ignored. This setting is ignored if TLS is not being used.")]
    public bool IgnoreCertificateChainErrors { get; set; }

    [CommandOption("--client-certificate <PATH>")]
    [Description(@"The path to the client certificate to use for the MQTT connection. This setting is ignored if TLS is not being used. The path can specify a PFX file, a DER- or PEM-encoded certificate file, or a location in a certificate store. When a PFX file is specified, the '--client-certificate-password' setting must also be specified. When a DER- or PEM-encoded certificate file is specified, the '--client-certificate-key' setting must also be specified. Certificate store paths are specified in the format 'cert:\{location}\{store}\{thumbprint_or_subject}' e.g. 'cert:\CurrentUser\My\localhost'.")]
    public string? ClientCertificateFile { get; set; }

    [CommandOption("--client-certificate-key <PATH>")]
    [Description("The path to a file containing the private key for the client certificate specified by the '--client-certificate' setting. This setting is ignored if '--client-certificate' specifies a PFX file or certificate store path.")]
    public string? ClientCertificateKeyFile { get; set; }

    [CommandOption("--client-certificate-password <PASSWORD>")]
    [Description("The password for the PFX file specified by the '--client-certificate' setting, or for the private key file specified by the '--client-certificate-key' setting.")]
    public string? ClientCertificatePassword { get; set; }


    public override ValidationResult Validate() {
        var baseResult = base.Validate();
        if (!baseResult.Successful) {
            return baseResult;
        }

        var clientCertLocation = GetClientCertificateLocation(this);
        if (clientCertLocation != null) {
            try {
                var cert = GetCertificateLoader().LoadCertificate(clientCertLocation, CertificateLoader.ClientAuthenticationOid);
                if (cert == null) {
                    return ValidationResult.Error("Client certificate was not found or was invalid.");
                }
            }
            catch (Exception e) {
                return ValidationResult.Error(e.Message);
            }
        }

        return ValidationResult.Success();
    }


    private static CertificateLoader GetCertificateLoader() => new CertificateLoader(new CertificateLoaderOptions() { 
        CertificateRootPath = Environment.CurrentDirectory
    });


    private static CertificateLocation? GetClientCertificateLocation(PublishMqttCommandSettings settings) {
        if (string.IsNullOrWhiteSpace(settings?.ClientCertificateFile)) {
            return null;
        }

        var location = CertificateLocation.CreateFromPath(settings.ClientCertificateFile!);
            
        if (location.IsFileCertificate) {
            location.KeyPath = settings.ClientCertificateKeyFile;
            location.Password = settings.ClientCertificatePassword;
        }

        return location;
    }


    internal IEnumerable<X509Certificate2>? GetClientCertificates() {
        var clientCertLocation = GetClientCertificateLocation(this);
        if (clientCertLocation == null) {
            return null;
        }

        var cert = GetCertificateLoader().LoadCertificate(clientCertLocation, CertificateLoader.ClientAuthenticationOid);

        if (cert == null) {
            return null;
        }

        return [cert];
    }

}
