#!/usr/bin/env bash

# installs the scripts to the specified directory
# removes .cs extension and makes them executable

# sane bash behavior
set -euo pipefail

# optional flags
compile=false
dockerized=false
install_dir=''

usage() {
    echo "Usage: $0 [--compile] [--dockerized] <install-directory>"
    echo "Default behavior (no --compile): installs executable .cs scripts without precompilation."
}

for arg in "$@"; do
    case "$arg" in
        --compile)
            compile=true
            ;;
        --dockerized)
            dockerized=true
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        --*)
            echo "Error: Unknown option '$arg'"
            usage
            exit 1
            ;;
        *)
            if [[ -z "$install_dir" ]]; then
                install_dir="$arg"
            else
                echo "Error: Unexpected argument '$arg'"
                usage
                exit 1
            fi
            ;;
    esac
done

# get safe absolute path of the current script directory
script_dir="$(/usr/bin/realpath "$(/usr/bin/dirname "${BASH_SOURCE[0]}")")"
if [[ -z "$install_dir" ]]; then
    usage
    exit 1
fi
if [[ ! -d "$install_dir" ]]; then
    echo "Error: Install directory '$install_dir' does not exist."
    exit 1
fi

if [[ "$dockerized" == "true" ]]; then
    if ! command -v docker >/dev/null 2>&1; then
        echo "Error: docker is required for --dockerized builds."
        exit 1
    fi

    /usr/bin/docker build \
        --file "$script_dir/Dockerfile" \
        --target artifacts \
        --build-arg "COMPILE=$compile" \
        --output "type=local,dest=$install_dir" \
        "$script_dir"
    echo "Dockerized build completed and artifacts exported to '${install_dir}'"
    exit 0
fi

# install each script
for script in "$script_dir"/scripts/*.cs; do
    if [[ "$compile" == "true" ]]; then
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