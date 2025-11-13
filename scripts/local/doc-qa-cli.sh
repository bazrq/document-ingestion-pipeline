#!/bin/bash

################################################################################
# Document QA System - Interactive CLI
################################################################################
# This script provides an interactive menu-driven interface to interact with
# the Azure Functions API running locally via Azure Functions.
#
# Prerequisites:
#   - Azure Functions must be running (cd DocumentQA.AppHost && dotnet run)
#   - Functions API must be accessible at http://localhost:7071
#   - curl and jq must be installed
#
# Usage:
#   ./scripts/local/doc-qa-cli.sh
################################################################################

set -euo pipefail

# Configuration
API_URL="http://localhost:7071"
LAST_DOCUMENT_ID=""

################################################################################
# Utility Functions
################################################################################

# Check if required dependencies are installed
check_dependencies() {
    local missing_deps=()

    if ! command -v curl &> /dev/null; then
        missing_deps+=("curl")
    fi

    if ! command -v jq &> /dev/null; then
        missing_deps+=("jq")
    fi

    if [ ${#missing_deps[@]} -ne 0 ]; then
        echo "Error: Missing required dependencies: ${missing_deps[*]}"
        echo ""
        echo "To install missing dependencies on macOS:"
        for dep in "${missing_deps[@]}"; do
            if [ "$dep" = "jq" ]; then
                echo "  brew install jq"
            fi
        done
        exit 1
    fi
}

# Check if the API is reachable
check_health() {
    echo "Checking if Document QA API is running..."

    if curl -s -f -o /dev/null --connect-timeout 5 --max-time 10 "$API_URL/api/documents" 2>/dev/null; then
        echo "✓ API is reachable at $API_URL"
        echo ""
        return 0
    else
        echo "✗ API is not reachable at $API_URL"
        echo ""
        echo "Please ensure Azure Functions is running:"
        echo "  cd DocumentQA.AppHost && dotnet run"
        echo ""
        return 1
    fi
}

# Display a separator line
separator() {
    echo "========================================"
}

# Pause and wait for user to press Enter
pause() {
    echo ""
    read -p "Press Enter to continue..."
    echo ""
}

################################################################################
# Feature Functions
################################################################################

# Upload a PDF document
upload_document() {
    separator
    echo "Upload PDF Document"
    separator
    echo ""

    # Prompt for file path
    read -p "Enter path to PDF file: " filepath

    # Validate file
    echo "Validating file..."

    if [ ! -f "$filepath" ]; then
        echo "✗ Error: File does not exist: $filepath"
        pause
        return 1
    fi

    if [ ! -r "$filepath" ]; then
        echo "✗ Error: File is not readable: $filepath"
        pause
        return 1
    fi

    # Check if file is PDF (by extension)
    if [[ ! "$filepath" =~ \.pdf$ ]]; then
        echo "✗ Error: File must be a PDF (*.pdf extension)"
        pause
        return 1
    fi

    # Check file size (100MB = 104857600 bytes)
    local filesize
    filesize=$(stat -f%z "$filepath" 2>/dev/null || stat -c%s "$filepath" 2>/dev/null)
    local max_size=104857600

    if [ "$filesize" -gt "$max_size" ]; then
        local size_mb=$((filesize / 1024 / 1024))
        echo "✗ Error: File size ($size_mb MB) exceeds maximum (100 MB)"
        pause
        return 1
    fi

    local size_mb=$((filesize / 1024 / 1024))
    echo "✓ File exists"
    echo "✓ File is PDF"
    echo "✓ File size: $size_mb MB (within 100 MB limit)"
    echo ""

    # Upload the file
    echo "Uploading..."
    echo ""

    local response
    if response=$(curl -s -f --connect-timeout 5 --max-time 60 -X POST "$API_URL/api/upload" -F "file=@$filepath" 2>&1); then
        echo "Response:"
        echo "$response" | jq '.'
        echo ""

        # Save document ID for quick access
        LAST_DOCUMENT_ID=$(echo "$response" | jq -r '.documentId')
        if [ -n "$LAST_DOCUMENT_ID" ] && [ "$LAST_DOCUMENT_ID" != "null" ]; then
            echo "Document ID saved for quick access: $LAST_DOCUMENT_ID"
        fi
    else
        echo "✗ Error: Upload failed"
        echo "$response"
    fi

    pause
}

# Check document status
check_status() {
    separator
    echo "Check Document Status"
    separator
    echo ""

    # Prompt for document ID
    local default_id=""
    if [ -n "$LAST_DOCUMENT_ID" ]; then
        default_id=" (press Enter for: $LAST_DOCUMENT_ID)"
    fi

    read -p "Enter document ID$default_id: " doc_id

    # Use last document ID if user pressed Enter
    if [ -z "$doc_id" ] && [ -n "$LAST_DOCUMENT_ID" ]; then
        doc_id="$LAST_DOCUMENT_ID"
        echo "Using: $doc_id"
    fi

    if [ -z "$doc_id" ]; then
        echo "✗ Error: Document ID is required"
        pause
        return 1
    fi

    echo ""

    # Ask if user wants to poll
    read -p "Poll until complete? (y/n): " poll_choice
    echo ""

    if [[ "$poll_choice" =~ ^[Yy]$ ]]; then
        # Polling mode
        echo "Polling status (checking every 3 seconds, press Ctrl+C to stop)..."
        echo ""

        while true; do
            local response
            if response=$(curl -s -f --connect-timeout 5 --max-time 30 -X GET "$API_URL/api/status/$doc_id" 2>&1); then
                local status
                status=$(echo "$response" | jq -r '.status')

                echo "Status: $status ($(date '+%H:%M:%S'))"

                if [ "$status" = "completed" ] || [ "$status" = "failed" ]; then
                    echo ""
                    echo "Final response:"
                    echo "$response" | jq '.'
                    break
                fi

                sleep 3
            else
                echo "✗ Error: Failed to get status"
                echo "$response"
                break
            fi
        done
    else
        # Single check
        local response
        if response=$(curl -s -f --connect-timeout 5 --max-time 30 -X GET "$API_URL/api/status/$doc_id" 2>&1); then
            echo "Response:"
            echo "$response" | jq '.'
        else
            echo "✗ Error: Failed to get status"
            echo "$response"
        fi
    fi

    echo ""
    pause
}

# List all documents
list_documents() {
    separator
    echo "List All Documents"
    separator
    echo ""

    # Prompt for status filter
    echo "Filter by status?"
    echo "  Options: uploaded, processing, completed, failed, all"
    read -p "Enter status (or press Enter for 'all'): " status_filter

    if [ -z "$status_filter" ] || [ "$status_filter" = "all" ]; then
        status_filter=""
    fi

    # Prompt for limit
    read -p "Limit results? (enter number or press Enter for all): " limit_filter

    echo ""

    # Build query parameters
    local query_params=""
    if [ -n "$status_filter" ]; then
        query_params="?status=$status_filter"
    fi

    if [ -n "$limit_filter" ]; then
        if [ -n "$query_params" ]; then
            query_params="${query_params}&limit=$limit_filter"
        else
            query_params="?limit=$limit_filter"
        fi
    fi

    # Make request
    echo "Fetching documents..."
    echo ""

    local response
    if response=$(curl -s -f --connect-timeout 5 --max-time 30 -X GET "$API_URL/api/documents$query_params" 2>&1); then
        echo "Response:"
        echo "$response" | jq '.'
    else
        echo "✗ Error: Failed to list documents"
        echo "$response"
    fi

    echo ""
    pause
}

# Query documents
query_documents() {
    separator
    echo "Query Documents (Ask Question)"
    separator
    echo ""

    # Prompt for question
    read -p "Enter your question: " question

    if [ -z "$question" ]; then
        echo "✗ Error: Question is required"
        pause
        return 1
    fi

    echo ""

    # Prompt for document IDs
    local default_id=""
    if [ -n "$LAST_DOCUMENT_ID" ]; then
        default_id=" (press Enter for: $LAST_DOCUMENT_ID)"
    fi

    read -p "Enter document IDs (comma-separated)$default_id: " doc_ids

    # Use last document ID if user pressed Enter
    if [ -z "$doc_ids" ] && [ -n "$LAST_DOCUMENT_ID" ]; then
        doc_ids="$LAST_DOCUMENT_ID"
        echo "Using: $doc_ids"
    fi

    if [ -z "$doc_ids" ]; then
        echo "✗ Error: At least one document ID is required"
        pause
        return 1
    fi

    echo ""

    # Prompt for max chunks (optional)
    read -p "Max chunks to use (press Enter for default 7): " max_chunks

    if [ -z "$max_chunks" ]; then
        max_chunks=7
    fi

    echo ""

    # Build document IDs array for JSON
    IFS=',' read -ra doc_id_array <<< "$doc_ids"
    local doc_ids_json="["
    for i in "${!doc_id_array[@]}"; do
        local trimmed
        trimmed=$(echo "${doc_id_array[$i]}" | xargs)  # Trim whitespace
        doc_ids_json+="\"$trimmed\""
        if [ $i -lt $((${#doc_id_array[@]} - 1)) ]; then
            doc_ids_json+=","
        fi
    done
    doc_ids_json+="]"

    # Build JSON request
    local json_request
    json_request=$(jq -n \
        --arg q "$question" \
        --argjson ids "$doc_ids_json" \
        --argjson mc "$max_chunks" \
        '{question: $q, documentIds: $ids, maxChunks: $mc}')

    # Make request
    echo "Querying documents..."
    echo ""

    local response
    if response=$(curl -s -f --connect-timeout 5 --max-time 60 -X POST "$API_URL/api/query" \
        -H "Content-Type: application/json" \
        -d "$json_request" 2>&1); then
        echo "Response:"
        echo "$response" | jq '.'
    else
        echo "✗ Error: Failed to query documents"
        echo "$response"
    fi

    echo ""
    pause
}

# Manual health check from menu
manual_health_check() {
    separator
    echo "Check API Health"
    separator
    echo ""

    if curl -s -f -o /dev/null --connect-timeout 5 --max-time 10 "$API_URL/api/documents" 2>/dev/null; then
        echo "✓ API is healthy and reachable at $API_URL"
    else
        echo "✗ API is not reachable at $API_URL"
        echo ""
        echo "Please ensure Azure Functions is running:"
        echo "  cd DocumentQA.AppHost && dotnet run"
    fi

    echo ""
    pause
}

################################################################################
# Main Menu
################################################################################

show_menu() {
    clear
    separator
    echo "Document QA System - Local CLI"
    separator
    echo "1. Upload PDF Document"
    echo "2. Check Document Status"
    echo "3. List All Documents"
    echo "4. Query Documents (Ask Question)"
    echo "5. Check API Health"
    echo "6. Exit"
    separator
    echo ""
}

main() {
    # Check dependencies
    check_dependencies

    # Initial health check
    if ! check_health; then
        exit 1
    fi

    # Main loop
    while true; do
        show_menu
        read -p "Enter your choice (1-6): " choice
        echo ""

        case $choice in
            1)
                upload_document
                ;;
            2)
                check_status
                ;;
            3)
                list_documents
                ;;
            4)
                query_documents
                ;;
            5)
                manual_health_check
                ;;
            6)
                echo "Exiting. Goodbye!"
                exit 0
                ;;
            *)
                echo "Invalid choice. Please enter a number between 1 and 6."
                pause
                ;;
        esac
    done
}

# Run main function
main
