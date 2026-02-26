#!/usr/bin/env dotnet

#:property TargetFramework=net10.0
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed
#:package ConsoleAppFramework@5.7.13

using System.Diagnostics;
using System.Text;
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
    /// Execute a program periodically, showing output fullscreen.
    /// Runs command repeatedly, displaying its output. This allows you to watch the program output change over time.
    /// </summary>
    /// <param name="command">The command to execute periodically.</param>
    /// <param name="interval">-n|--interval: Seconds between updates (default: 2).</param>
    /// <param name="differences">-d|--differences: Highlight differences between successive updates.</param>
    /// <param name="cumulative">--cumulative: Make highlighting sticky, showing all positions that have ever changed.</param>
    /// <param name="noTitle">-t|--no-title: Turn off the header showing interval, command, and current time.</param>
    /// <param name="shell">-s|--shell: Shell to use for executing commands (e.g., bash, pwsh, cmd). Defaults to $SHELL or cmd on Windows and /bin/sh on Unix.</param>
    /// <param name="cancellationToken"></param>
    [Command("")]
    public async Task WatchAsync(
        [Argument] string command,
        double interval = 2.0,
        bool differences = false,
        bool cumulative = false,
        bool noTitle = false,
        string? shell = null,
        CancellationToken cancellationToken = default)
    {
        if (interval <= 0)
        {
            interval = 2.0;
        }

        string? previousOutput = null;
        HashSet<(int line, int col)>? cumulativeChanges = cumulative ? [] : null;

        // Hide cursor for cleaner display
        Console.CursorVisible = false;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string output = await RunCommandAsync(command, shell, cancellationToken);

                Console.Clear();

                int headerLines = 0;
                if (!noTitle)
                {
                    headerLines = WriteHeader(command, interval);
                }

                int availableLines = Console.WindowHeight - headerLines;
                WriteOutput(output, previousOutput, differences, cumulativeChanges, availableLines);

                if (differences)
                {
                    previousOutput = output;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            Console.CursorVisible = true;
        }
    }

    private static int WriteHeader(string command, double interval)
    {
        string timeStr = DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy");
        string leftPart = $"Every {interval:0.#}s: {command}";

        int consoleWidth = Console.WindowWidth;
        int padding = Math.Max(0, consoleWidth - leftPart.Length - timeStr.Length);

        // Truncate left part if necessary
        if (leftPart.Length + timeStr.Length + 1 > consoleWidth)
        {
            int maxLeft = consoleWidth - timeStr.Length - 4;
            if (maxLeft > 0)
            {
                leftPart = leftPart[..maxLeft] + "...";
                padding = 0;
            }
        }

        Console.WriteLine($"{leftPart}{new string(' ', padding)}{timeStr}");
        Console.WriteLine();

        return 2; // Header takes 2 lines
    }

    private static void WriteOutput(
        string currentOutput,
        string? previousOutput,
        bool highlightDifferences,
        HashSet<(int line, int col)>? cumulativeChanges,
        int maxLines)
    {
        string[] currentLines = currentOutput.Split('\n');
        string[]? previousLines = previousOutput?.Split('\n');

        int linesToShow = Math.Min(currentLines.Length, maxLines);

        for (int lineIdx = 0; lineIdx < linesToShow; lineIdx++)
        {
            string currentLine = currentLines[lineIdx].TrimEnd('\r');
            string? previousLine = previousLines is not null && lineIdx < previousLines.Length
                ? previousLines[lineIdx].TrimEnd('\r')
                : null;

            if (highlightDifferences && (previousLine is not null || cumulativeChanges is not null))
            {
                WriteLineWithDifferences(currentLine, previousLine, lineIdx, cumulativeChanges);
            }
            else
            {
                Console.WriteLine(currentLine);
            }
        }
    }

    private static void WriteLineWithDifferences(
        string currentLine,
        string? previousLine,
        int lineIdx,
        HashSet<(int line, int col)>? cumulativeChanges)
    {
        for (int col = 0; col < currentLine.Length; col++)
        {
            char currentChar = currentLine[col];
            char? previousChar = previousLine is not null && col < previousLine.Length
                ? previousLine[col]
                : null;

            bool isDifferent = previousChar.HasValue && currentChar != previousChar.Value;
            bool isCumulativeChange = cumulativeChanges?.Contains((lineIdx, col)) == true;

            if (isDifferent)
            {
                cumulativeChanges?.Add((lineIdx, col));
            }

            if (isDifferent || isCumulativeChange)
            {
                Console.BackgroundColor = ConsoleColor.DarkYellow;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.Write(currentChar);
                Console.ResetColor();
            }
            else
            {
                Console.Write(currentChar);
            }
        }

        Console.WriteLine();
    }

    private static async Task<string> RunCommandAsync(string command, string? shellOverride, CancellationToken cancellationToken)
    {
        (string shell, string shellArg) = GetShell(shellOverride);

        ProcessStartInfo startInfo = new()
        {
            FileName = shell,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add(shellArg);
        startInfo.ArgumentList.Add(command);

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        // Read stdout and stderr concurrently
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        // Combine stdout and stderr (stderr after stdout, like typical terminal behavior)
        if (!string.IsNullOrEmpty(stderr))
        {
            return stdout + stderr;
        }

        return stdout;
    }

    private static (string shell, string arg) GetShell(string? shellOverride)
    {
        string? shell = shellOverride ?? Environment.GetEnvironmentVariable("SHELL");

        if (!string.IsNullOrEmpty(shell))
        {
            string name = Path.GetFileNameWithoutExtension(shell).ToLowerInvariant();
            string arg = name switch
            {
                "pwsh" or "powershell" => "-Command",
                "cmd" => "/c",
                _ => "-c" // bash, sh, zsh, fish, etc.
            };
            return (shell, arg);
        }

        return OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c")
            : ("/bin/sh", "-c");
    }
}
