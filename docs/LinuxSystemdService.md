# Running a systemd Linux Service

The `nruuvitag` tool can be run as a systemd service on Linux.

Firstly, publish the [NRuuviTag.Cli.Linux](/src/NRuuviTag.Cli.Linux) project using one of the available [publish profiles](/src/NRuuviTag.Cli.Linux/Properties/PublishProfiles) using Visual Studio or the `dotnet` command-line tool. For example, to publish the tool for 32-bit ARM CPUs (such as the ARMv7 used in older Raspberry PI models):

```sh
dotnet publish NRuuviTag.Cli.Linux.csproj /p:PublishProfile=Arm
``` 

The publish profiles are configured to create self-contained, trimmed, single-file executables that can be deployed without requiring the .NET SDK or runtime to be installed on the target machine. The published files can be found at `artifacts/publish/NRuuviTag.Cli.Linux/<Runtime Identifier>` under the root of your checked-out code.

Once you have built the executable, follow these instructions on the target machine:

Copy the `nruuvitag` executable, `appsettings.json`, and `nruuvitag.service` (the systemd service definition file) to the destination machine.

On the machine, create a folder at `/usr/local/services/nruuvitag` and move the `nruuvitag` executable and `appsettings.json` to that folder.

Make the user account that will run the service the owner of the folder created in the previous step and ensure that `nruuvitag` is executable: 

```sh
sudo chown <user> -R /usr/local/services/nruuvitag
sudo chmod u+x /usr/local/services/nruuvitag/nruuvitag
sudo ln -s /usr/local/services/nruuvitag/nruuvitag /usr/local/bin/nruuvitag
```

Edit `nruuvitag.service` and set the `User` property to the user that will run the service.

Create `/etc/nruuvitag.conf` and set the `NRUUVITAG_OPTIONS` environment variable to specify the arguments to pass to `nruuvitag` when the service starts:

```sh
echo "NRUUVITAG_OPTIONS=publish az <MY_AZURE_EVENT_HUB_CONNECTION_STRING> <MY_EVENT_HUB_NAME> --sample-interval 15" >> nruuvitag.conf
sudo mv nruuvitag.conf /etc/nruuvitag.conf
```

Move `nruuvitag.service` to `/etc/systemd/system` and start the service:

```sh
sudo mv nruuvitag.service /etc/systemd/system
sudo systemctl daemon-reload
sudo systemctl start nruuvitag
```

Finally, query systemd to confirm that the service started up correctly:

```sh
systemctl status nruuvitag
```  
