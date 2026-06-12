using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;
using RoofControl.Core.Interfaces;
using RoofControl.Core.Models;

namespace RoofControl.Talon6;

public sealed class Talon6Controller : IRoofController, IDisposable
{
    private readonly ILogger<Talon6Controller> _logger;
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _readTimeoutMs;
    private readonly SemaphoreSlim _commandQueue = new(1, 1);
    private SerialPort? _serialPort;
    private bool _disposed;

    public Talon6Controller(
        ILogger<Talon6Controller> logger,
        string portName,
        int baudRate,
        int encoderTicksMax,
        bool safetyEnabled,
        int readTimeoutSeconds,
        int writeTimeoutSeconds)
    {
        _logger = logger;
        _portName = portName;
        _baudRate = baudRate;
        _readTimeoutMs = readTimeoutSeconds * 1000;
        Talon6Protocol.EncoderTicksMax = encoderTicksMax;
    }

    public async Task<RoofStatus> GetStatusAsync(CancellationToken ct)
        => await DispatchCommandAsync(async () =>
        {
            await EnsureConnectedAsync(ct);
            var resp = await SendFrameAsync(Talon6Protocol.QueryStatus, ct);
            return Talon6Protocol.ParseStatusResponse(resp);
        }, ct);

    public async Task GoToPositionAsync(double percent, CancellationToken ct)
    {
        var clamped = Math.Clamp(percent, 0.0, 100.0);
        var ticks = (int)(clamped / 100.0 * Talon6Protocol.EncoderTicksMax);
        var cmd = Talon6Protocol.BuildGoToCommand(ticks);
        await DispatchCommandAsync(async () =>
        {
            await EnsureConnectedAsync(ct);
            _logger.LogInformation("GoTo {Percent}% ({Ticks} ticks)", clamped, ticks);
            await SendFrameAsync(cmd, ct);
        }, ct);
    }

    public async Task StopAsync(CancellationToken ct)
        => await DispatchCommandAsync(async () =>
        {
            await EnsureConnectedAsync(ct);
            _logger.LogInformation("Stop motion");
            await SendFrameAsync(Talon6Protocol.StopMotion, ct);
        }, ct);

    public async Task OpenFullyAsync(CancellationToken ct)
        => await DispatchCommandAsync(async () =>
        {
            await EnsureConnectedAsync(ct);
            _logger.LogInformation("Open fully");
            await SendFrameAsync(Talon6Protocol.OpenFully, ct);
        }, ct);

    public async Task CloseAsync(CancellationToken ct)
        => await DispatchCommandAsync(async () =>
        {
            await EnsureConnectedAsync(ct);
            _logger.LogInformation("Closing (Park)");
            await SendFrameAsync(Talon6Protocol.Close, ct);
        }, ct);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _commandQueue.Dispose();
        try { _serialPort?.Close(); } catch { }
        _serialPort?.Dispose();
    }

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_serialPort?.IsOpen == true) return;

        _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = _readTimeoutMs,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = true
        };

        _serialPort.Open();
        _logger.LogInformation("Connected to Talon6 on {Port}", _portName);
    }

    /// <summary>
    /// Send a command (e.g. &amp;G%#) and read the binary response frame.
    /// Response format: &lt;header&gt; + data-bytes + # (0x23) terminator.
    /// </summary>
    private async Task<byte[]> SendFrameAsync(string command, CancellationToken ct)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
            throw new InvalidOperationException("Serial port not connected");

        var cmdBytes = Encoding.ASCII.GetBytes(command);
        await _serialPort.BaseStream.WriteAsync(cmdBytes, ct);
        await _serialPort.BaseStream.FlushAsync(ct);

        // Read bytes until we find # (0x23) terminator
        using var ms = new MemoryStream(64);
        var buf = new byte[1];
        var start = Environment.TickCount;

        while ((Environment.TickCount - start) < _readTimeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            if (_serialPort.BytesToRead > 0)
            {
                var read = await _serialPort.BaseStream.ReadAsync(buf, ct);
                if (read == 0) break;

                ms.WriteByte(buf[0]);
                if (buf[0] == 0x23) // # terminator
                    break;
            }
            else
            {
                await Task.Delay(5, ct);
            }
        }

        var result = ms.ToArray();
        _logger.LogDebug("Talon6 response ({Len} bytes): {Hex}",
            result.Length, BitConverter.ToString(result));

        // Auto-retry once if we got the startup text ("\notro reset\r\n")
        // instead of a binary frame. The Talon6 sends this line once on
        // serial connect; the real response follows on the next query.
        if (result.Length > 0 && result[0] == 0x0A && result.Length < 20)
        {
            _logger.LogInformation("Got startup message, retrying query once");
            return await SendFrameAsync(command, ct);
        }

        return result;
    }

    private async Task<T> DispatchCommandAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await _commandQueue.WaitAsync(ct);
        try { return await action(); }
        finally { _commandQueue.Release(); }
    }

    private async Task DispatchCommandAsync(Func<Task> action, CancellationToken ct)
    {
        await _commandQueue.WaitAsync(ct);
        try { await action(); }
        finally { _commandQueue.Release(); }
    }
}
