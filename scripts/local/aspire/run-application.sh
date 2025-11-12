#!/bin/bash

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Navigate to project root (3 levels up from scripts/local/aspire/)
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

# Graceful shutdown handler
cleanup() {
    echo ""
    echo "Shutting down gracefully..."
    exit 0
}

# Trap SIGINT (Ctrl+C) and SIGTERM
trap cleanup SIGINT SIGTERM

# Check for Azure Functions Core Tools
if ! command -v func &> /dev/null; then
    echo "Error: Azure Functions Core Tools not found"
    echo "Install with: brew install azure-functions-core-tools@4"
    exit 1
fi

# Navigate to AppHost directory and run
cd "$PROJECT_ROOT/DocumentQA.AppHost"

echo "Starting Aspire application from: $PROJECT_ROOT/DocumentQA.AppHost"
echo "Press Ctrl+C to stop"
echo ""

/usr/local/share/dotnet/dotnet run
