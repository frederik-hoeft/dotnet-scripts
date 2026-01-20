#!/usr/bin/env dotnet

#:property TargetFramework=net10.0
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed
#:package ConsoleAppFramework@5.7.13

using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);

internal sealed class Commands
{
    /// <summary>
    /// Buffers redirected input and writes it to a file.
    /// </summary>
    /// <param name="file">-f|--file: The output file name, if not specified, writes to standard output</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [Command("")]
    public async Task ExecuteAsync([Argument][HideDefaultValue] string? file = null, CancellationToken cancellationToken = default)
    {
        string tempFile = $"{Guid.CreateVersion7()}.tmp";
        await using (FileStream fs = File.OpenWrite(tempFile))
        {
            await Console.OpenStandardInput().CopyToAsync(fs, cancellationToken);
        }
        if (file is not null)
        {
            const int RETRIES = 4;
            for (int i = 0; i < RETRIES; ++i)
            {
                try
                {
                    File.Replace(tempFile, file, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    break;
                }
                catch (IOException) when (i < RETRIES - 1)
                {
                    await Task.Delay((1 << i) * 100, cancellationToken);
                }
                catch
                {
                    File.Delete(tempFile);
                    throw;
                }
            }
        }
        else
        {
            await using (FileStream fs = File.OpenRead(tempFile))
            {
                await fs.CopyToAsync(Console.OpenStandardOutput(), cancellationToken);
            }
            File.Delete(tempFile);
        }
    }
}