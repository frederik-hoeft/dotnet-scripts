# dotnet-scripts

A collection of .NET-based command-line utilities implemented as single-file C# scripts.

## buffered-write

Buffers stdin and writes it to a temporary file and atomically renames it to the target, with retry logic for locked files.
If no output file is specified, writes to stdout.

Great for in-place file edits in pipelines, since the final write only occurs once all prior processing is complete (and previous streams are closed).

Requires .NET 10 or later.

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

## esed

A sed-like text processor with full .NET regex support, enabling advanced features like lookaheads, lookbehinds, named groups, and Unicode categories.

Requires .NET 10 or later.

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

## normalize

Converts text to ASCII by substituting Unicode characters (smart quotes, em-dashes, arrows, etc.) and stripping accents and combining marks.

Requires .NET 10 or later.

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
