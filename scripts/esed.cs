#!/usr/bin/env dotnet

#:property TargetFramework=net10.0
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed
#:package ConsoleAppFramework@5.7.13

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);

internal sealed class Commands
{
    /// <summary>
    /// A simple sed-like processor that supports the full .NET regex feature set.
    /// </summary>
    /// <param name="inplace">-i|--inplace: Edit files in place</param>
    /// <param name="regex">The sed script in the form: s/regex/replacement/</param>
    /// <param name="inputFileName">-f|--file</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Command("")]
    public async Task ExecuteAsync([Argument][StringSyntax(StringSyntaxAttribute.Regex)] string regex, [Argument] string inputFileName, [HideDefaultValue] bool inplace = false, CancellationToken cancellationToken = default)
    {
        FileInfo fileInfo = new(inputFileName);
        if (!fileInfo.Exists)
        {
            await Console.Error.WriteLineAsync($"File not found: {inputFileName}");
            return;
        }
        SedProcessor processor = new(regex);
        string content = await File.ReadAllTextAsync(fileInfo.FullName, cancellationToken);
        string result = processor.Process(content);
        if (inplace)
        {
            string tempFileName = $"{Guid.CreateVersion7()}.tmp";
            await File.WriteAllTextAsync(tempFileName, result, cancellationToken);
            File.Replace(tempFileName, fileInfo.FullName, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            await Console.Out.WriteLineAsync(result);
        }
    }
}

internal sealed partial class SedProcessor
{
    private readonly string _regex;
    private readonly string _replacement;

    [GeneratedRegex(@"^s/(?<regex>.*?)(?<!\\)/(?<replacement>.*)/$")]
    private static partial Regex ScriptRegex { get; }

    public SedProcessor(string script)
    {
        Match match = ScriptRegex.Match(script);
        if (!match.Success)
        {
            throw new ArgumentException("Invalid sed script format.", nameof(script));
        }
        _regex = Regex.Unescape(match.Groups["regex"].Value);
        _replacement = Regex.Unescape(match.Groups["replacement"].Value);
    }

    internal string Process(string content) => 
        Regex.Replace(content, _regex, _replacement, RegexOptions.Multiline | RegexOptions.CultureInvariant);
}