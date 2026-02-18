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
    /// <param name="literalReplacement">Treat escape sequences literally when true, otherwise unescape common sequences</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Command("")]
    public Task ImplicitAsync([Argument][StringSyntax(StringSyntaxAttribute.Regex)] string regex, [Argument] string? inputFileName = null, [HideDefaultValue] bool inplace = false, [HideDefaultValue] bool literalReplacement = false, CancellationToken cancellationToken = default)
    {
        ScriptedSedProcessor processor = new(regex, literalReplacement);
        return ExecuteCoreAsync(processor, inputFileName, inplace, cancellationToken);
    }

    /// <summary>
    /// A sed-like processor that treats the regex and replacement as literal strings, escaping any special characters.
    /// </summary>
    /// <param name="regex">The regex pattern to search for (treated as a literal string)</param>
    /// <param name="replacement">The replacement string (treated as a literal string)</param>
    /// <param name="inputFileName">-f|--file: The input file name, if not specified, reads from standard input</param>
    /// <param name="inplace">-i|--inplace: Edit files in place</param>
    /// <param name="literalReplacement">Treat escape sequences literally when true, otherwise unescape common sequences</param>
    /// <param name="cancellationToken"></param>
    [Command("explicit")]
    public Task ExplicitAsync([Argument][StringSyntax(StringSyntaxAttribute.Regex)] string regex, [Argument][StringSyntax(StringSyntaxAttribute.Regex)] string replacement, [Argument] string? inputFileName = null, [HideDefaultValue] bool inplace = false, [HideDefaultValue] bool literalReplacement = false, CancellationToken cancellationToken = default)
    {
        SedProcessor processor = new(regex, replacement, literalReplacement);
        return ExecuteCoreAsync(processor, inputFileName, inplace, cancellationToken);
    }

    private static async Task ExecuteCoreAsync(ISedProcessor sedProcessor, string? inputFileName = null, bool inplace = false, CancellationToken cancellationToken = default)
    {
        string content = inputFileName switch
        {
            null => await ReadStdinAsync(cancellationToken),
            _ => await ReadFileAsync(inputFileName, cancellationToken)
        };
        string result = sedProcessor.Process(content);
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

internal interface ISedProcessor
{
    string Process(string content);
}

internal abstract class SedProcessorBase(bool literalReplacement) : ISedProcessor
{
    private readonly bool _literalReplacement = literalReplacement;

    public abstract string Process(string content);

    protected string NormalizeEscapes(string value) => _literalReplacement ? value : UnescapeCommonEscapes(value);

    private static string UnescapeCommonEscapes(string value)
    {
        StringBuilder builder = new(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (current != '\\' || i == value.Length - 1)
            {
                builder.Append(current);
                continue;
            }

            char next = value[i + 1];
            switch (next)
            {
                case '\\':
                    builder.Append('\\');
                    i++;
                    break;
                case 'n':
                    builder.Append('\n');
                    i++;
                    break;
                case 'r':
                    builder.Append('\r');
                    i++;
                    break;
                case 't':
                    builder.Append('\t');
                    i++;
                    break;
                case '0':
                    builder.Append('\0');
                    i++;
                    break;
                case 'f':
                    builder.Append('\f');
                    i++;
                    break;
                case 'v':
                    builder.Append('\v');
                    i++;
                    break;
                case 'a':
                    builder.Append('\a');
                    i++;
                    break;
                case 'b':
                    builder.Append('\b');
                    i++;
                    break;
                default:
                    builder.Append('\\');
                    break;
            }
        }

        return builder.ToString();
    }
}

internal sealed partial class ScriptedSedProcessor : SedProcessorBase
{
    private readonly string _regex;
    private readonly string _replacement;

    [GeneratedRegex(@"^s(?<sep>.)(?<regex>.*?(?:(?<=[^\\])(?:\\\\)*))\k<sep>(?<replacement>.*?)\k<sep>$")]
    private static partial Regex ScriptRegex { get; }

    public ScriptedSedProcessor(string script, bool literalReplacement) : base(literalReplacement)
    {
        Match match = ScriptRegex.Match(script);
        if (!match.Success)
        {
            throw new ArgumentException("Invalid sed script format.", nameof(script));
        }
        _regex = match.Groups["regex"].Value;
        _replacement = NormalizeEscapes(match.Groups["replacement"].Value);
    }

    public override string Process(string content) =>
        Regex.Replace(content, _regex, _replacement, RegexOptions.Multiline | RegexOptions.CultureInvariant);
}

internal sealed class SedProcessor : SedProcessorBase
{
    private readonly string _regex;
    private readonly string _replacement;

    public SedProcessor(string regex, string replacement, bool literalReplacement) : base(literalReplacement)
    {
        _regex = regex;
        _replacement = NormalizeEscapes(replacement);
    }

    public override string Process(string content) =>
        Regex.Replace(content, _regex, _replacement, RegexOptions.Multiline | RegexOptions.CultureInvariant);
}