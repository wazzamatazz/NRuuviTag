using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NRuuviTag.Cli;
using NRuuviTag.Listener.Linux;

return await NRuuviTagHostBuilder.CreateHostBuilder(args, sp => ActivatorUtilities.CreateInstance<BlueZListener>(sp, BlueZListener.DefaultBluetoothAdapter))
    .UseSystemd()
    .BuildAndRunCommandApp(args)
    .ConfigureAwait(false);
