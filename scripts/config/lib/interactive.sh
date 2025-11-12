#!/usr/bin/env bash
# interactive.sh - Interactive selection utilities using fzf

# Source guard
[ -n "${_INTERACTIVE_SH_SOURCED:-}" ] && return 0
readonly _INTERACTIVE_SH_SOURCED=1

set -euo pipefail

# Source validation functions for colored output
_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=./validation.sh
source "${_LIB_DIR}/validation.sh"

# Display a header message
print_header() {
    echo "" >&2
    echo -e "${COLOR_BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${COLOR_RESET}" >&2
    echo -e "${COLOR_BLUE}  $1${COLOR_RESET}" >&2
    echo -e "${COLOR_BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${COLOR_RESET}" >&2
    echo "" >&2
}

# Display a section header
print_section() {
    echo "" >&2
    echo -e "${COLOR_BLUE}▸ $1${COLOR_RESET}" >&2
    echo "" >&2
}

# Interactive selection using fzf
# Usage: fzf_select "prompt" "items" "preview_command"
#   items: newline-separated list of items
#   preview_command: optional command to preview item (use {} as placeholder)
fzf_select() {
    local prompt="$1"
    local items="$2"
    local preview_cmd="${3:-}"

    if [ -z "$items" ]; then
        print_error "No items available for selection"
        return 1
    fi

    local fzf_opts=(
        --prompt="$prompt > "
        --height=~70%
        --layout=reverse
        --border=rounded
        --info=inline
        --margin=1
        --padding=1
        --color=prompt:#569cd6,pointer:#4ec9b0,marker:#4ec9b0,border:#3e4451
        --pointer="▶"
        --marker="✓"
        --header="Press ESC to cancel, Enter to select"
    )

    if [ -n "$preview_cmd" ]; then
        fzf_opts+=(--preview="$preview_cmd" --preview-window=right:50%:wrap)
    fi

    local selection
    if ! selection=$(echo "$items" | fzf "${fzf_opts[@]}" 2>/dev/tty); then
        print_warning "Selection cancelled"
        return 1
    fi

    echo "$selection"
    return 0
}

# Multi-select using fzf
# Usage: fzf_multiselect "prompt" "items"
fzf_multiselect() {
    local prompt="$1"
    local items="$2"

    if [ -z "$items" ]; then
        print_error "No items available for selection"
        return 1
    fi

    local fzf_opts=(
        --prompt="$prompt > "
        --height=~70%
        --layout=reverse
        --border=rounded
        --info=inline
        --margin=1
        --padding=1
        --multi
        --color=prompt:#569cd6,pointer:#4ec9b0,marker:#4ec9b0,border:#3e4451
        --pointer="▶"
        --marker="✓"
        --header="Press TAB to select multiple, ESC to cancel, Enter to confirm"
    )

    local selections
    if ! selections=$(echo "$items" | fzf "${fzf_opts[@]}" 2>/dev/tty); then
        print_warning "Selection cancelled"
        return 1
    fi

    echo "$selections"
    return 0
}

# Confirm action with yes/no prompt
confirm() {
    local prompt="$1"
    local default="${2:-n}" # Default to 'n' if not specified

    local yn
    if [ "$default" = "y" ]; then
        read -r -p "$(echo -e "${COLOR_YELLOW}? $prompt [Y/n]: ${COLOR_RESET}")" yn
        yn=${yn:-y}
    else
        read -r -p "$(echo -e "${COLOR_YELLOW}? $prompt [y/N]: ${COLOR_RESET}")" yn
        yn=${yn:-n}
    fi

    case "$yn" in
        [Yy]* ) return 0;;
        [Nn]* ) return 1;;
        * )
            print_warning "Invalid response. Please answer yes or no."
            confirm "$prompt" "$default"
            ;;
    esac
}

# Display a progress spinner
# Usage: long_running_command & spinner $! "Loading message"
spinner() {
    local pid=$1
    local message="${2:-Working...}"
    local spin_chars='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local delay=0.1

    # Hide cursor
    tput civis 2>/dev/null || true

    while kill -0 "$pid" 2>/dev/null; do
        for i in $(seq 0 9); do
            local char=${spin_chars:$i:1}
            echo -ne "\r${COLOR_BLUE}${char} ${message}${COLOR_RESET}"
            sleep $delay
        done
    done

    # Clear the line and show cursor
    echo -ne "\r\033[K"
    tput cnorm 2>/dev/null || true
}

# Display a key-value pair
print_key_value() {
    local key="$1"
    local value="$2"
    printf "  %-30s %s\n" "$key:" "$value" >&2
}

# Display a summary box
print_summary() {
    local title="$1"
    shift
    local items=("$@")

    echo "" >&2
    echo -e "${COLOR_GREEN}┌─────────────────────────────────────────────────────────────────┐${COLOR_RESET}" >&2
    echo -e "${COLOR_GREEN}│  ${title}$(printf '%*s' $((61 - ${#title})) '')│${COLOR_RESET}" >&2
    echo -e "${COLOR_GREEN}├─────────────────────────────────────────────────────────────────┤${COLOR_RESET}" >&2

    for item in "${items[@]}"; do
        # Split on first colon
        local key="${item%%:*}"
        local value="${item#*:}"
        printf "${COLOR_GREEN}│${COLOR_RESET}  %-30s %-30s ${COLOR_GREEN}│${COLOR_RESET}\n" "$key" "$value" >&2
    done

    echo -e "${COLOR_GREEN}└─────────────────────────────────────────────────────────────────┘${COLOR_RESET}" >&2
    echo "" >&2
}

# Show a loading message while executing a command
with_loading() {
    local message="$1"
    shift
    local cmd=("$@")

    echo -ne "${COLOR_BLUE}⟳ ${message}...${COLOR_RESET}" >&2

    local output
    local exit_code=0
    if output=$("${cmd[@]}" 2>&1); then
        echo -e "\r${COLOR_GREEN}✓ ${message}${COLOR_RESET}\033[K" >&2
    else
        exit_code=$?
        echo -e "\r${COLOR_RED}✗ ${message} (failed)${COLOR_RESET}\033[K" >&2
        if [ -n "$output" ]; then
            print_error "$output"
        fi
    fi

    return $exit_code
}

# Pretty-print JSON data
print_json() {
    local json="$1"
    echo "$json" | jq -C . 2>/dev/null >&2 || echo "$json" >&2
}

# Format a table from TSV data
# Usage: format_table "header1\theader2\theader3" "data in TSV format"
format_table() {
    local header="$1"
    local data="$2"

    {
        echo -e "$header"
        echo -e "$data"
    } | column -t -s $'\t'
}
