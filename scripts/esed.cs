#!/usr/bin/env dotnet

#:property TargetFramework=net10.0
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed
#:package ConsoleAppFramework@5.7.13

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using ConsoleAppFramework;

// Force UTF-8 for redirected stdin/stdout (pipes/files)
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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
    /// <param name="inputFileName">-f|--file: The input file name, if not specified, reads from standard input</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Command("")]
    public async Task ExecuteAsync([Argument][StringSyntax(StringSyntaxAttribute.Regex)] string regex, [Argument] string? inputFileName = null, [HideDefaultValue] bool inplace = false, CancellationToken cancellationToken = default)
    {
        SedProcessor processor = new(regex);
        string content = inputFileName switch
        {
            null => await ReadStdinAsync(cancellationToken),
            _ => await ReadFileAsync(inputFileName, cancellationToken)
        };
        string result = processor.Process(content);
        if (inplace)
        {
            if (inputFileName is null)
            {
                throw new ArgumentException("In-place editing requires an input file name.", nameof(inputFileName));
            }
            string tempFileName = $"{Guid.CreateVersion7()}.tmp";
            await File.WriteAllTextAsync(tempFileName, result, cancellationToken);
            File.Replace(tempFileName, inputFileName, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            await Console.Out.WriteLineAsync(result);
        }
    }

    private static async Task<string> ReadFileAsync(string fileName, CancellationToken cancellationToken)
    {
        using FileStream fs = File.OpenRead(fileName);
        using StreamReader reader = new(fs, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<string> ReadStdinAsync(CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Console.OpenStandardInput(), Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
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