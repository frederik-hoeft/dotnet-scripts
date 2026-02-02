#!/usr/bin/env bash

# installs the scripts to the specified directory
# removes .cs extension and makes them executable

# sane bash behavior
set -euo pipefail

# optional --compile flag to precompile scripts
compile_flag="${1:-}"

# get safe absolute path of the current script directory
script_dir="$(/usr/bin/realpath "$(/usr/bin/dirname "${BASH_SOURCE[0]}")")"
install_dir=''
if [[ "$compile_flag" == "--compile" ]]; then
    install_dir="${2:-}"
else
    install_dir="${1:-}"
fi
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
    if [[ "$compile_flag" == "--compile" ]]; then
        # precompile the script to a single-file executable
        compiled_path="${script%.cs}"
        dotnet publish "$script" -c Release -o "$install_dir"
        /usr/bin/chmod +x "$install_dir/$(/usr/bin/basename "$compiled_path")"
        # remove pdb files if any
        if [[ -f "$install_dir/$(/usr/bin/basename "$compiled_path").pdb" ]]; then
            /usr/bin/rm "$install_dir/$(/usr/bin/basename "$compiled_path").pdb"
        fi
        echo "Compiled and installed '$(/usr/bin/basename "$compiled_path")' to '${install_dir}'"
    else
        script_name="$(/usr/bin/basename "$script" .cs)"
        install_path="$install_dir/$script_name"
        /usr/bin/cp "$script" "${install_path}.tmp"
        /usr/bin/chmod +x "${install_path}.tmp"
        # atomically move to final location
        /usr/bin/mv "${install_path}.tmp" "$install_path"
        echo "Installed '${script_name}' to '${install_dir}'"
    fi
done