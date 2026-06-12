using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoofControl.Core.Interfaces;

namespace RoofControl.App;

/// <summary>
/// Monitors for the ESC key press. When detected, closes the roof and
/// initiates a graceful application shutdown.
/// </summary>
public sealed class EscShutdownMonitor : BackgroundService
{
    private readonly IRoofController _controller;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<EscShutdownMonitor> _logger;

    public EscShutdownMonitor(
        IRoofController controller,
        IHostApplicationLifetime lifetime,
        ILogger<EscShutdownMonitor> logger)
    {
        _controller = controller;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("ESC shutdown monitor started — press ESC to close roof and exit");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Escape)
                    {
                        _logger.LogWarning("ESC pressed — closing roof and shutting down");
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        await _controller.CloseAsync(cts.Token);
                        _lifetime.StopApplication();
                        return;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // No console attached (e.g. running as Windows service) — stop polling
                _logger.LogDebug("No console available, ESC monitor disabled");
                return;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await Task.Delay(200, ct);
        }
    }
}
