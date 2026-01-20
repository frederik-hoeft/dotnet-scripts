# dotnet-scripts

A collection of .NET-based command-line utilities implemented as single-file C# scripts.

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
