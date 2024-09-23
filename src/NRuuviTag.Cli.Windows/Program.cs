using Microsoft.Extensions.Hosting;

using NRuuviTag.Cli;
using NRuuviTag.Listener.Windows;

return await NRuuviTagHostBuilder
    .CreateHostBuilder<WindowsSdkListener>(args)
    .BuildAndRunCommandApp(args)
    .ConfigureAwait(false);
