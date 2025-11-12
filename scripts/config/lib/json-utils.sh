#!/usr/bin/env bash
# json-utils.sh - Safe JSON manipulation utilities using jq

# Source guard
[ -n "${_JSON_UTILS_SH_SOURCED:-}" ] && return 0
readonly _JSON_UTILS_SH_SOURCED=1

set -euo pipefail

# Source validation functions
_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./validation.sh
source "${_LIB_DIR}/validation.sh"

# Safely update a JSON value in a file
# Usage: json_update_value "file.json" ".path.to.key" "new value"
json_update_value() {
    local file_path="$1"
    local json_path="$2"
    local new_value="$3"

    if [ ! -f "$file_path" ]; then
        print_error "File does not exist: $file_path"
        return 1
    fi

    local temp_file
    temp_file=$(mktemp)

    if jq --arg val "$new_value" "${json_path} = \$val" "$file_path" > "$temp_file" 2>/dev/null; then
        if mv "$temp_file" "$file_path"; then
            return 0
        else
            print_error "Failed to update file: $file_path"
            rm -f "$temp_file"
            return 1
        fi
    else
        print_error "Failed to update JSON path: $json_path"
        rm -f "$temp_file"
        return 1
    fi
}

# Safely read a JSON value from a file
# Usage: value=$(json_read_value "file.json" ".path.to.key")
json_read_value() {
    local file_path="$1"
    local json_path="$2"

    if [ ! -f "$file_path" ]; then
        print_error "File does not exist: $file_path"
        return 1
    fi

    local value
    if value=$(jq -r "$json_path" "$file_path" 2>/dev/null); then
        echo "$value"
        return 0
    else
        print_error "Failed to read JSON path: $json_path"
        return 1
    fi
}

# Update multiple JSON values atomically
# Usage: json_update_multiple "file.json" "path1:value1" "path2:value2" ...
json_update_multiple() {
    local file_path="$1"
    shift
    local updates=("$@")

    if [ ! -f "$file_path" ]; then
        print_error "File does not exist: $file_path"
        return 1
    fi

    if [ ${#updates[@]} -eq 0 ]; then
        print_warning "No updates provided"
        return 0
    fi

    local temp_file
    temp_file=$(mktemp)

    # Build jq filter for all updates
    local jq_filter=""
    local jq_args=()

    for update in "${updates[@]}"; do
        local path="${update%%:*}"
        local value="${update#*:}"

        if [ -z "$jq_filter" ]; then
            jq_filter="${path} = \$val${#jq_args[@]}"
        else
            jq_filter="${jq_filter} | ${path} = \$val${#jq_args[@]}"
        fi

        jq_args+=(--arg "val${#jq_args[@]}" "$value")
    done

    if jq "${jq_args[@]}" "$jq_filter" "$file_path" > "$temp_file" 2>/dev/null; then
        if mv "$temp_file" "$file_path"; then
            return 0
        else
            print_error "Failed to update file: $file_path"
            rm -f "$temp_file"
            return 1
        fi
    else
        print_error "Failed to apply updates"
        rm -f "$temp_file"
        return 1
    fi
}

# Update appsettings.Development.json with Azure OpenAI configuration
# Usage: update_appsettings_openai "file.json" "endpoint" "api_key" "embedding_model" "chat_model"
update_appsettings_openai() {
    local file_path="$1"
    local endpoint="$2"
    local api_key="$3"
    local embedding_deployment="$4"
    local chat_deployment="$5"

    print_info "Updating Azure OpenAI configuration in $file_path..."

    # Validate inputs
    if ! validate_url "$endpoint"; then
        return 1
    fi

    if [ -z "$api_key" ]; then
        print_error "API key cannot be empty"
        return 1
    fi

    if [ -z "$embedding_deployment" ]; then
        print_error "Embedding deployment name cannot be empty"
        return 1
    fi

    if [ -z "$chat_deployment" ]; then
        print_error "Chat deployment name cannot be empty"
        return 1
    fi

    # Ensure endpoint has trailing slash
    if [[ ! "$endpoint" =~ /$ ]]; then
        endpoint="${endpoint}/"
    fi

    # Update all Azure OpenAI values atomically
    if json_update_multiple "$file_path" \
        ".Azure.OpenAI.Endpoint:$endpoint" \
        ".Azure.OpenAI.ApiKey:$api_key" \
        ".Azure.OpenAI.EmbeddingDeploymentName:$embedding_deployment" \
        ".Azure.OpenAI.ChatDeploymentName:$chat_deployment"; then
        print_success "Configuration updated successfully"
        return 0
    else
        print_error "Failed to update configuration"
        return 1
    fi
}

# Check if a JSON path exists in a file
# Usage: if json_path_exists "file.json" ".Azure.OpenAI"; then ...
json_path_exists() {
    local file_path="$1"
    local json_path="$2"

    if [ ! -f "$file_path" ]; then
        return 1
    fi

    local value
    value=$(jq -r "${json_path} // empty" "$file_path" 2>/dev/null) || return 1

    if [ -n "$value" ] && [ "$value" != "null" ]; then
        return 0
    else
        return 1
    fi
}

# Pretty print a section of JSON
# Usage: json_print_section "file.json" ".Azure.OpenAI"
json_print_section() {
    local file_path="$1"
    local json_path="$2"

    if [ ! -f "$file_path" ]; then
        print_error "File does not exist: $file_path"
        return 1
    fi

    local section
    if section=$(jq -C "${json_path}" "$file_path" 2>/dev/null); then
        echo "$section"
        return 0
    else
        print_error "Failed to read JSON section: $json_path"
        return 1
    fi
}

# Initialize a JSON file with default structure if it doesn't exist
# Usage: json_initialize "file.json" '{"Azure": {"OpenAI": {}}}'
json_initialize() {
    local file_path="$1"
    local default_content="$2"

    if [ -f "$file_path" ]; then
        print_info "File already exists: $file_path"
        return 0
    fi

    local dir_path
    dir_path=$(dirname "$file_path")

    if [ ! -d "$dir_path" ]; then
        print_error "Directory does not exist: $dir_path"
        return 1
    fi

    if echo "$default_content" | jq . > "$file_path" 2>/dev/null; then
        print_success "Initialized JSON file: $file_path"
        return 0
    else
        print_error "Failed to initialize JSON file: $file_path"
        return 1
    fi
}

# Merge two JSON objects
# Usage: merged=$(json_merge "$json1" "$json2")
json_merge() {
    local json1="$1"
    local json2="$2"

    local merged
    if merged=$(jq -s '.[0] * .[1]' <(echo "$json1") <(echo "$json2") 2>/dev/null); then
        echo "$merged"
        return 0
    else
        print_error "Failed to merge JSON objects"
        return 1
    fi
}

# Validate JSON structure matches expected schema
# Usage: json_validate_schema "file.json" ".Azure.OpenAI | keys" '["Endpoint", "ApiKey"]'
json_validate_schema() {
    local file_path="$1"
    local check_query="$2"
    local expected="$3"

    if [ ! -f "$file_path" ]; then
        print_error "File does not exist: $file_path"
        return 1
    fi

    local actual
    if actual=$(jq -r "$check_query" "$file_path" 2>/dev/null); then
        if [ "$actual" = "$expected" ]; then
            return 0
        else
            print_error "Schema validation failed. Expected: $expected, Got: $actual"
            return 1
        fi
    else
        print_error "Failed to validate schema"
        return 1
    fi
}
