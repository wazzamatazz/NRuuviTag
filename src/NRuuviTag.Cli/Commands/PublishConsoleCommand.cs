using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using Spectre.Console.Cli;

namespace NRuuviTag.Cli.Commands;

/// <summary>
/// <see cref="CommandApp"/> command for listening to RuuviTag broadcasts without forwarding 
/// them to an MQTT broker.
/// </summary>
public class PublishConsoleCommand : AsyncCommand<PublishConsoleCommand.Settings> {

    /// <summary>
    /// The <see cref="IRuuviTagListenerFactory"/> to create listeners with.
    /// </summary>
    private readonly IRuuviTagListenerFactory _listenerFactory;

    /// <summary>
    /// The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </summary>
    private readonly IHostApplicationLifetime _appLifetime;


    /// <summary>
    /// Creates a new <see cref="PublishConsoleCommand"/> object.
    /// </summary>
    /// <param name="listenerFactory">
    ///   The <see cref="IRuuviTagListenerFactory"/> to create listeners with.
    /// </param>
    /// <param name="appLifetime">
    ///   The <see cref="IHostApplicationLifetime"/> for the .NET host application.
    /// </param>
    public PublishConsoleCommand(IRuuviTagListenerFactory listenerFactory, IHostApplicationLifetime appLifetime) {
        _listenerFactory = listenerFactory;
        _appLifetime = appLifetime;
    }


    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
        // Wait until the host application has started if required.
        if (!_appLifetime.ApplicationStarted.IsCancellationRequested) {
            try { await Task.Delay(-1, _appLifetime.ApplicationStarted).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        
        var listener = _listenerFactory.CreateListener(settings.Bind);

        var publisher = new ConsoleJsonPublisher(listener);
        
        using (var ctSource = CancellationTokenSource.CreateLinkedTokenSource(_appLifetime.ApplicationStopped, _appLifetime.ApplicationStopping)) {
            try {
                await publisher.RunAsync(ctSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        Console.WriteLine();
        return 0;
    }
    
    
    /// <summary>
    /// Settings for <see cref="PublishConsoleCommand"/>.
    /// </summary>
    public class Settings : ListenerCommandSettings {

        [CommandOption("--known-devices")]
        [Description("Specifies if only samples from pre-registered devices should be observed.")]
        public bool KnownDevicesOnly { get; set; }

    }
    
}
