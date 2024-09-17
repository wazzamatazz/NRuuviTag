# Running NRuuviTag CLI using Docker

## Building the Container Image

To build a Linux container image for the NRuuviTag CLI application, run the 'PublishContainers' target using the build script:

**PowerShell**

```pwsh
.\build.ps1 --target PublishContainers --configuration Release
``` 

**Bash**

```sh
./build.sh --target PublishContainers --configuration Release
``` 

The default settings publish an x64 Linux image to the local Docker or Podman registry. To publish an image for a different architecture (e.g. `arm64`) specify the `--container-arch` parameter when running the build script. You can also publish the image to an alternative container registry by specifying the `--container-registry` parameter. For example:

**PowerShell**

```pwsh
.\build.ps1 `
    --target Publish `
    --configuration Release `
    --container-arch arm64 `
    --container-registry myregistry.mymachine.local:5000
``` 

**Bash**

```sh
./build.sh \
    --target Publish \
    --configuration Release \
    --container-arch arm64 \
    --container-registry myregistry.mymachine.local:5000
``` 

The container image is created using the .NET SDK container builds feature. Refer to the documentation [here](https://github.com/dotnet/sdk-container-builds) for information about configuring the image properties.

Note that the container image runs as the `root` user. This is required to allow the container to access the Bluetooth device on the host machine via BlueZ.


## Running the Container Image

Create a `$HOME/.nruuvitag` directory if you have not done so already.

The container requires that the following volumes are mapped:

- `/var/run/dbus` - Enables the container to communicate with DBus to receive Bluetooth advertisements.
- `$HOME/.nruuvitag` - Allows the container to read from/write to the `devices.json` file that stores known devices.

Run the application as follows:

```sh
docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $HOME/.nruuvitag:/root/.nruuvitag \
    nruuvitag:latest
```

You can append the command arguments to the end of the call to `docker run` e.g. to list known devices:

```sh
docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $HOME/.nruuvitag:/root/.nruuvitag \
    nruuvitag:latest \
    devices list
```

You can also alias the command as follows:

```sh
alias nruuvitag='docker run -it --rm -v /var/run/dbus:/var/run/dbus -v $HOME/.nruuvitag:/root/.nruuvitag nruuvitag:latest'
export nruuvitag
```

To persist the alias, add the above commands to your shell's configuration file (e.g. `~/.bash_aliases`).

Once you have created the alias, you can invoke the `nruuvitag` command as if it were installed on your host machine:

```sh
nruuvitag devices list
```
