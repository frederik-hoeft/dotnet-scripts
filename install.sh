#!/usr/bin/env bash

# installs the scripts to the specified directory
# removes .cs extension and makes them executable

# sane bash behavior
set -euo pipefail

# get safe absolute path of the current script directory
script_dir="$(/usr/bin/realpath "$(/usr/bin/dirname "${BASH_SOURCE[0]}")")"
install_dir="${1:-}"
if [[ -z "$install_dir" ]]; then
    echo "Usage: $0 <install-directory>"
    exit 1
fi
if [[ ! -d "$install_dir" ]]; then
    echo "Error: Install directory '$install_dir' does not exist."
    exit 1
fi
# install each script
for script in "$script_dir"/scripts/*.cs; do
    script_name="$(/usr/bin/basename "$script" .cs)"
    install_path="$install_dir/$script_name"
    /usr/bin/cp "$script" "${install_path}.tmp"
    /usr/bin/chmod +x "${install_path}.tmp"
    # atomically move to final location
    /usr/bin/mv "${install_path}.tmp" "$install_path"
    echo "Installed '${script_name}' to '${install_dir}'"
done