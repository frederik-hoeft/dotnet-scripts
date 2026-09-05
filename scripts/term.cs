#!/usr/bin/env dotnet

#:property TargetFramework=net10.0-windows
#:property OutputType=WinExe
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed

using System.Diagnostics;

string homeDirectory = GetDirectoryEnvironmentVariable("TERM_HOME_DIRECTORY", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

string terminal = ExpandEnvironmentVariables(Environment.GetEnvironmentVariable("TERM_TERMINAL") ?? "wt.exe");

string currentDirectory = Path.GetFullPath(Environment.CurrentDirectory);
string launcherDirectory = Path.GetFullPath(AppContext.BaseDirectory);

HashSet<string> neutralDirectories = new(StringComparer.OrdinalIgnoreCase)
{
    NormalizeDirectory(launcherDirectory),
};

string? configuredNeutralDirectories = Environment.GetEnvironmentVariable("TERM_NEUTRAL_DIRECTORIES");

if (!string.IsNullOrWhiteSpace(configuredNeutralDirectories))
{
    foreach (string directory in configuredNeutralDirectories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        neutralDirectories.Add(NormalizeDirectory(ExpandEnvironmentVariables(directory)));
    }
}

string startDirectory = args switch
{
    [] when neutralDirectories.Contains(NormalizeDirectory(currentDirectory)) => homeDirectory,
    [] => currentDirectory,
    ["."] => currentDirectory,
    [string directory] => Path.GetFullPath(ExpandEnvironmentVariables(directory), currentDirectory),
    _ => throw new ArgumentException("Expected at most one starting directory."),
};

ProcessStartInfo startInfo = new(terminal)
{
    UseShellExecute = false,
    CreateNoWindow = true,
};

startInfo.ArgumentList.Add("-w");
startInfo.ArgumentList.Add("-1");
startInfo.ArgumentList.Add("-d");
startInfo.ArgumentList.Add(startDirectory);

_ = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Windows Terminal.");

static string GetDirectoryEnvironmentVariable(string variableName, string defaultValue)
{
    string value = Environment.GetEnvironmentVariable(variableName) ?? defaultValue;

    return Path.GetFullPath(ExpandEnvironmentVariables(value));
}

static string ExpandEnvironmentVariables(string value) => Environment.ExpandEnvironmentVariables(value);

static string NormalizeDirectory(string directory) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
