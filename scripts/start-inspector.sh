#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

# Clean stale Inspector instances and listeners that cause disconnected state.
pkill -f '@modelcontextprotocol/inspector' 2>/dev/null || true
pkill -f 'StockOrchestra.Server.csproj' 2>/dev/null || true
pkill -f 'StockOrchestra.Server.dll' 2>/dev/null || true

for port in 6274 6277; do
  pids="$(lsof -tiTCP:${port} -sTCP:LISTEN 2>/dev/null || true)"
  if [[ -n "$pids" ]]; then
    echo "Port ${port} occupied by PID(s): $pids. Stopping..."
    kill $pids 2>/dev/null || true
  fi
done

# Prevent inspector from crashing when browser auto-open command fails.
export BROWSER=/bin/true
export DANGEROUSLY_OMIT_AUTH=true

echo "Building server..."
dotnet build StockOrchestra-MCP.sln >/dev/null

echo "Starting MCP Inspector in no-auth mode..."
echo "Open manually: http://localhost:6274"

exec npx -y @modelcontextprotocol/inspector dotnet src/StockOrchestra.Server/bin/Debug/net8.0/StockOrchestra.Server.dll