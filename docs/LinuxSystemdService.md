# Running a systemd Linux Service

The `nruuvitag` tool can be run as a systemd service on Linux.


## Publishing the Executable

Publish the [NRuuviTag.Cli.Linux](/src/NRuuviTag.Cli.Linux) project using the build script:

**PowerShell**

```pwsh
./build.ps1 --target Publish --configuration Release
``` 

**Bash**

```sh
./build.sh --target Publish --configuration Release
``` 

The publish profiles create self-contained, trimmed, single-file executables that can be deployed without requiring the .NET SDK or runtime to be installed on the target machine. The published files can be found at `artifacts/publish/<Configuration>/NRuuviTag.Cli.Linux/<Runtime Identifier>` under the root of your checked-out code.

See [here](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog#linux-rids) for information about the runtime identifiers.

> The `linux-arm` runtime identifier is suitable for use with 32-bit ARM CPUs such as the ARMv7 used in older Raspberry Pi models running Raspberry Pi OS.


## Configuring the Service

Once you have built the executable, follow the instructions below.

Copy the following files to the destination machine:

- `nruuvitag`
- `appsettings.json`
- `nruuvitag.service`

On the destination machine, move the first two files to the correct location and set the user that will run the service to be the owner:

```sh
sudo mkdir -p /usr/local/services/nruuvitag
sudo mv nruuvitag appsettings.json /usr/local/services/nruuvitag
sudo chown <user> -R /usr/local/services/nruuvitag
sudo chmod u+x /usr/local/services/nruuvitag/nruuvitag
sudo ln -s /usr/local/services/nruuvitag/nruuvitag /usr/local/bin/nruuvitag
```

Create file `/etc/nruuvitag.d/nruuvitag.conf` and set the `NRUUVITAG_OPTIONS` environment variable to specify the arguments to pass to `nruuvitag` when the service starts, customising them to fit your requirements:

```sh
echo "NRUUVITAG_OPTIONS=publish az <MY_AZURE_EVENT_HUB_CONNECTION_STRING> <MY_EVENT_HUB_NAME> --sample-interval 15" > nruuvitag.conf
sudo mkdir /etc/nruuvitag.d
sudo mv nruuvitag.conf /etc/nruuvitag.d
```

Edit `nruuvitag.service` (the systemd unit configuration file) and set the `User` property to the user that will run the service:

```ini
# User to run the service as.
User=<user>
```

Install the service definition:

```sh
sudo mv nruuvitag.service /usr/lib/systemd/user
sudo ln -s /usr/lib/systemd/user/nruuvitag.service /etc/systemd/system/nruuvitag.service
sudo systemctl daemon-reload
sudo systemctl enable nruuvitag
sudo systemctl start nruuvitag
```

Finally, query systemd to confirm that the service started up correctly:

```sh
systemctl status nruuvitag
```  

You should see output similar to the following:

```
● nruuvitag.service - NRuuviTag IoT data collector
   Loaded: loaded (/usr/lib/systemd/user/nruuvitag.service; linked; vendor preset: enabled)
   Active: active (running) since DAY YYYY-MM-DD HH:MM:SS TZ; Xmin ago
 Main PID: 12345 (nruuvitag)
    Tasks: 18 (limit: 2062)
   CGroup: /system.slice/nruuvitag.service
           └─20406 /usr/local/bin/nruuvitag publish az <MY_AZURE_EVENT_HUB_CONNECTION_STRING> ...
```
