#!/usr/bin/env dotnet

#:property TargetFramework=net10.0
#:property PublishAot=true
#:property PublishTrimmed=true
#:property OptimizationPreference=speed
#:property AllowUnsafeBlocks=true
#:package ConsoleAppFramework@5.7.13

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
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
    /// Gets the current clipboard text and writes it to stdout.
    /// </summary>
    [Command("get")]
    public async Task GetAsync(CancellationToken cancellationToken = default)
    {
        IClipboard clipboard = CreateClipboard();
        string? text = await clipboard.GetTextAsync(cancellationToken);
        if (text is not null)
        {
            await Console.Out.WriteAsync(text.AsMemory(), cancellationToken);
        }
    }

    /// <summary>
    /// Sets the clipboard text to the specified value or reads from stdin if no value is provided.
    /// </summary>
    /// <param name="value">The text to set to the clipboard. If omitted, reads all stdin.</param>
    [Command("set")]
    public async Task SetAsync([Argument] string? value = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(value))
        {
            value = await Console.In.ReadToEndAsync(cancellationToken);
        }
        IClipboard clipboard = CreateClipboard();
        await clipboard.SetTextAsync(value, cancellationToken);
    }

    private static IClipboard CreateClipboard()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Win32Clipboard();
        }
        if (OperatingSystem.IsLinux())
        {
            if (Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null)
            {
                return new WaylandClipboard();
            }
            else
            {
                return new X11Clipboard();
            }
        }
        throw new PlatformNotSupportedException("Clipboard operations are only supported on Windows and Linux.");
    }
}

internal interface IClipboard
{
    Task<string?> GetTextAsync(CancellationToken cancellationToken);

    Task SetTextAsync(string text, CancellationToken cancellationToken);
}

internal abstract class DelegatingClipboard : IClipboard
{
    protected abstract void ConfigureGetProcess(ProcessStartInfo startInfo);

    protected abstract void ConfigureSetProcess(ProcessStartInfo startInfo);

    public async Task<string?> GetTextAsync(CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        ConfigureGetProcess(startInfo);
        using Process process = Process.Start(startInfo)!;
        return await process.StandardOutput.ReadToEndAsync(cancellationToken);
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        ConfigureSetProcess(startInfo);
        using Process process = Process.Start(startInfo)!;
        await process.StandardInput.WriteAsync(text.AsMemory(), cancellationToken);
    }
}

[SupportedOSPlatform("linux")]
internal sealed class WaylandClipboard : DelegatingClipboard
{
    protected override void ConfigureGetProcess(ProcessStartInfo startInfo)
    {
        startInfo.FileName = "wl-paste";
        startInfo.ArgumentList.Add("--no-newline");
    }

    protected override void ConfigureSetProcess(ProcessStartInfo startInfo)
    {
        startInfo.FileName = "wl-copy";
    }
}

[SupportedOSPlatform("linux")]
internal sealed class X11Clipboard : DelegatingClipboard
{
    protected override void ConfigureGetProcess(ProcessStartInfo startInfo)
    {
        startInfo.FileName = "xclip";
        startInfo.ArgumentList.Add("-selection");
        startInfo.ArgumentList.Add("clipboard");
        startInfo.ArgumentList.Add("-out");
    }

    protected override void ConfigureSetProcess(ProcessStartInfo startInfo)
    {
        startInfo.FileName = "xclip";
        startInfo.ArgumentList.Add("-selection");
        startInfo.ArgumentList.Add("clipboard");
        startInfo.ArgumentList.Add("-in");
    }
}

[SupportedOSPlatform("windows")]
internal sealed partial class Win32Clipboard : IClipboard
{
    private const uint cfUnicodeText = 13;

    public Task<string?> GetTextAsync(CancellationToken cancellationToken)
    {
        if (!IsClipboardFormatAvailable(cfUnicodeText))
        {
            return Task.FromResult<string?>(null);
        }
        using OpenWin32Clipboard _ = new();
        IntPtr handle = GetClipboardData(cfUnicodeText);
        using LockedMemory lockedMemory = new(handle);
        int size = GlobalSize(lockedMemory.Pointer);
        byte[] buff = new byte[size];
        Marshal.Copy(lockedMemory.Pointer, buff, 0, size);
        string text = Encoding.Unicode.GetString(buff).TrimEnd('\0');
        return Task.FromResult<string?>(text);
    }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        using OpenWin32Clipboard _ = new();
        EmptyClipboard();
        int bytes = Encoding.Unicode.GetByteCount(text) + Encoding.Unicode.GetByteCount("\0");
        using UnmanagedMemory unmanagedMemory = new(bytes);
        using LockedMemory lockedMemory = unmanagedMemory.Lock();
        Marshal.Copy(text.ToCharArray(), 0, lockedMemory.Pointer, text.Length);
        if (SetClipboardData(cfUnicodeText, lockedMemory.Pointer) == default)
        {
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
        }
        return Task.CompletedTask;
    }

    [LibraryImport("User32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("User32.dll", SetLastError = true)]
    private static partial IntPtr GetClipboardData(uint uFormat);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr SetClipboardData(uint uFormat, IntPtr data);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyClipboard();

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    private static partial int GlobalSize(IntPtr hMem);

    private ref partial struct OpenWin32Clipboard : IDisposable
    {
        private bool _isOpen;

        public OpenWin32Clipboard()
        {
            _isOpen = OpenClipboard(default);
            if (!_isOpen)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        public void Dispose()
        {
            if (_isOpen)
            {
                CloseClipboard();
                _isOpen = false;
            }
        }

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool OpenClipboard(IntPtr hWndNewOwner);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseClipboard();
    }

    private ref partial struct LockedMemory : IDisposable
    {
        private IntPtr _ptr;

        public LockedMemory(IntPtr hMem)
        {
            _ptr = GlobalLock(hMem);
            if (_ptr == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
            }
        }

        public readonly IntPtr Pointer => _ptr;

        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                GlobalUnlock(_ptr);
                _ptr = IntPtr.Zero;
            }
        }

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial IntPtr GlobalLock(IntPtr hMem);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GlobalUnlock(IntPtr hMem);
    }

    private ref struct UnmanagedMemory : IDisposable
    {
        private IntPtr _ptr;

        public UnmanagedMemory(int size)
        {
            _ptr = Marshal.AllocHGlobal(size);
            if (_ptr == IntPtr.Zero)
            {
                throw new OutOfMemoryException();
            }
        }

        public readonly IntPtr Pointer => _ptr;

        public readonly LockedMemory Lock() => new(_ptr);

        public void Dispose()
        {
            if (_ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_ptr);
                _ptr = IntPtr.Zero;
            }
        }
    }
}