# dotnet-scripts

A collection of .NET-based command-line utilities implemented as single-file C# scripts. Requires .NET 10 or later.

## Installation

```bash
bash ./install.sh <install-directory>
bash ./install.sh --compile <install-directory>
bash ./install.sh --dockerized <install-directory> # implies --compile
```

## buffered-write

Buffers stdin and writes it to a temporary file and atomically renames it to the target, with retry logic for locked files.
If no output file is specified, writes to stdout.

Great for in-place file edits in pipelines, since the final write only occurs once all prior processing is complete (and previous streams are closed).

### Usage

```bash
chmod +x buffered-write.cs
cat input.txt | ./buffered-write.cs output.txt
```

### Options

- `-f|--file`: Output file (if omitted, writes to stdout)

### Examples

Atomic file write:
```bash
echo "data" | ./buffered-write.cs config.json
```

Pipelined in-place edit:
```bash
cat data.txt | ./normalize.cs | ./buffered-write.cs data.txt
```

## eclip

Manage clipboard text on Windows/Linux.

### Usage

```bash
chmod +x eclip.cs
./eclip.cs get                    # Output clipboard to stdout
echo 'text' | ./eclip.cs set      # Set clipboard from stdin
./eclip.cs set 'direct value'     # Set clipboard to literal text
```

### Commands

- `get`: Output clipboard text to stdout
- `set [value]`: Set clipboard text (from argument or stdin if omitted)

### Examples

Pipe clipboard to a file:
```bash
./eclip.cs get > clipboard.txt
```

Copy file to clipboard:
```bash
cat file.txt | ./eclip.cs set
```

## esed

A sed-like text processor with full .NET regex support, enabling advanced features like lookaheads, lookbehinds, named groups, and Unicode categories.

### Usage

```bash
chmod +x esed.cs
./esed.cs 's/old/new/' input.txt
```

### Options

- `-i|--inplace`: Edit file in place (otherwise prints to stdout)

### Examples

Basic replacement:
```bash
./esed.cs 's/foo/bar/' file.txt
```

Edit in place:
```bash
./esed.cs -i 's/\d+/NUMBER/' data.txt
```

Advanced regex (lookbehind):
```bash
./esed.cs 's/(?<=@)\w+/example/' emails.txt
```

Stream from stdin:
```bash
echo 'hello world' | ./esed.cs 's/world/universe/'
hello universe
```

## normalize

Converts text to ASCII by substituting Unicode characters (smart quotes, em-dashes, arrows, etc.) and stripping accents and combining marks.

### Usage

```bash
chmod +x normalize.cs
echo '“Café → voilà!”' | ./normalize.cs | tee output.txt
"Cafe -> voila!"
```

### Options

- `--collapse-whitespace`: Collapse runs of whitespace to single spaces
- `--trim`: Trim leading/trailing whitespace

Example:
```bash
echo '  Hello   world  ' | ./normalize.cs --trim --collapse-whitespace
Hello world
```

## term (windows only)

Launches Windows Terminal in a new window while preserving the caller's working directory. The launcher is built as a Windows GUI application so it can be invoked from hotkeys or launchers without first creating a console host.

### Configuration

`term` can be configured through environment variables:

* `TERM_HOME_DIRECTORY`: fallback directory used for neutral launch contexts. Defaults to `%USERPROFILE%`.
* `TERM_TERMINAL`: terminal executable to launch. Defaults to `wt.exe`.
* `TERM_NEUTRAL_DIRECTORIES`: `;`-separated list of directories that should fall back to `TERM_HOME_DIRECTORY` instead of being inherited as the starting directory.

The directory containing `term.exe` is always treated as neutral.

Example:

```powershell
setx TERM_NEUTRAL_DIRECTORIES "%ProgramFiles%\PowerToys"
```

Usage:

```text
term
term .
term C:\src
```

With no argument, the current directory is preserved unless it is neutral. `.` always uses the current directory, while an explicit path starts Terminal in that directory.

## watch

A port of the Unix `watch` utility. Executes a command periodically and displays the output fullscreen, allowing you to observe changes over time.

### Usage

```bash
chmod +x watch.cs
./watch.cs "ls -la"
./watch.cs -n 5 "df -h"
```

### Options

- `-n|--interval`: Seconds between updates (default: 2)
- `-d|--differences`: Highlight differences between successive updates
- `--cumulative`: Make highlighting sticky, showing all positions that have ever changed
- `-t|--no-title`: Turn off the header showing interval, command, and current time
- `-s|--shell`: Shell to use for executing commands (defaults to `$SHELL` or system default)

### Examples

Watch directory contents every 2 seconds:
```bash
./watch.cs "dir"
```

Monitor processes with difference highlighting:
```bash
./watch.cs -d "tasklist | findstr chrome"
```

Use a specific shell:
```bash
./watch.cs -s pwsh "Get-Process | Select-Object -First 10"
```

Cumulative highlighting without header:
```bash
./watch.cs -d --cumulative -t "netstat -an"
```
