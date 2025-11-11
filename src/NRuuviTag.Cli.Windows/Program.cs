using Microsoft.Extensions.Hosting;

using NRuuviTag.Cli;
using NRuuviTag.Cli.Windows;

return await NRuuviTagHostBuilder
    .CreateHostBuilder<WindowsSdkListenerFactory>(args)
    .BuildHostAndRunNRuuviTagAsync(args)
    .ConfigureAwait(false);
