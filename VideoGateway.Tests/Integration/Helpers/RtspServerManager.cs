using System.Diagnostics;

namespace VideoGateway.Tests.Integration.Helpers;

public class RtspServerManager : IDisposable
{
    private Process? _process;
    private readonly string _mediamtxPath;
    public int Port { get; } = 8554;

    public RtspServerManager()
    {
        _mediamtxPath = @"C:\tools\mediamtx\mediamtx.exe";
    }

    public void Start()
    {
        if (!File.Exists(_mediamtxPath))
            throw new FileNotFoundException($"MediaMTX not found at {_mediamtxPath}");

        var psi = new ProcessStartInfo
        {
            FileName = _mediamtxPath,
            Arguments = $"--rtspPort {Port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = Process.Start(psi);
        Thread.Sleep(2000);
    }

    public void Stop()
    {
        if (_process != null && !_process.HasExited)
        {
            try { _process.Kill(); } catch { }
            try { _process.Dispose(); } catch { }
            _process = null;
            Thread.Sleep(1000);
        }
    }

    public bool IsRunning => _process != null && !_process.HasExited;

    public void Dispose()
    {
        Stop();
    }
}
