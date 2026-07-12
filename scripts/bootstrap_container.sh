#!/usr/bin/env bash

local_bin="$HOME/.local/bin"
mkdir -p "$local_bin"

tool_path="$local_bin/nruuvitag"
cat > "$tool_path" <<'EOF'
#!/usr/bin/env bash

# See GHCR for available image tags
image="ghcr.io/wazzamatazz/nruuvitag:latest"

# Run `nruuvitag update` to pull the latest container image
if [[ $1 == "update" ]]; then
  docker pull $image
  exit 0
fi

# nruuvitag uses the XDG Base Directory Specification to determine where to 
# store data files. If XDG_DATA_HOME is not set, ~/.local/share is used by 
# default.
if [[ -z "$XDG_DATA_HOME" ]]; then
    XDG_DATA_HOME="$HOME/.local/share"
fi

mkdir -p "$XDG_DATA_HOME/nruuvitag"

docker run -it --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v $XDG_DATA_HOME/nruuvitag:/root/.local/share/nruuvitag \
    $image \
    "$@"
EOF

chmod +x "$tool_path"