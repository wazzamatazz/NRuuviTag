using Microsoft.Extensions.Hosting;

using NRuuviTag.Cli;
using NRuuviTag.Cli.Linux;

return await NRuuviTagHostBuilder.CreateHostBuilder<BlueZListenerFactory>(args)
    .UseSystemd()
    .BuildHostAndRunNRuuviTagAsync(args)
    .ConfigureAwait(false);
