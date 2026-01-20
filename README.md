# dotnet-scripts

A collection of .NET-based command-line utilities implemented as single-file C# scripts.

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
