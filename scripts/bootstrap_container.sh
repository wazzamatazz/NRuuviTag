#!/usr/bin/env bash

set -euo pipefail

if ! command -v docker > /dev/null 2>&1; then
    echo "Error: docker was not found. Install Docker and try again." >&2
    exit 1
fi

local_bin="$HOME/.local/bin"
mkdir -p "$local_bin"

tool_path="$local_bin/nruuvitag"
cat > "$tool_path" <<'EOF'
#!/usr/bin/env bash

# See GHCR for available image tags
image="ghcr.io/wazzamatazz/nruuvitag:latest"

# Run `nruuvitag update` to pull the latest container image
if [[ "${1:-}" == "update" ]]; then
    exec docker pull "$image"
fi

# nruuvitag uses the XDG Base Directory Specification to determine where to
# store data files. If XDG_DATA_HOME is not set or is not an absolute path,
# ~/.local/share is used by default.
if [[ "${XDG_DATA_HOME:-}" != /* ]]; then
    XDG_DATA_HOME="$HOME/.local/share"
fi

mkdir -p "$XDG_DATA_HOME/nruuvitag"

# Only allocate a pseudo-TTY when running interactively so that the command
# can also be used from scripts, pipes, cron jobs and systemd services.
tty_args=()
if [[ -t 0 && -t 1 ]]; then
    tty_args=(-it)
fi

exec docker run "${tty_args[@]}" --rm \
    -v /var/run/dbus:/var/run/dbus \
    -v "$XDG_DATA_HOME/nruuvitag:/root/.local/share/nruuvitag" \
    "$image" \
    "$@"
EOF

chmod +x "$tool_path"

echo "Installed nruuvitag to $tool_path"

if [[ ":$PATH:" != *":$local_bin:"* ]]; then
    echo "Warning: $local_bin is not on your PATH. Add it to your shell profile to run nruuvitag directly, e.g.:" >&2
    echo "    export PATH=\"\$HOME/.local/bin:\$PATH\"" >&2
fi
