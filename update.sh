#!/usr/bin/env bash
# Updates Second Sight on Linux: pulls the latest code and rebuilds ~/.local/share/SecondSight/app.
# Quit Claude (Claude Code session or Claude Desktop) first, so the old version isn't running.
#
#   ./update.sh            # or: ./update.sh /some/other/folder
set -euo pipefail

OUTPUT="${1:-$HOME/.local/share/SecondSight/app}"
cd "$(dirname "$0")"

if pgrep -x SecondSight >/dev/null 2>&1; then
    echo "SecondSight is still running. Quit Claude, then run this again." >&2
    exit 1
fi

echo "Pulling the latest code..."
git checkout main
git pull --ff-only

echo "Building to $OUTPUT..."
dotnet publish src/SecondSight -c Release -o "$OUTPUT"

echo "Done. The program is $OUTPUT/SecondSight. Restart Claude to use the new version."
