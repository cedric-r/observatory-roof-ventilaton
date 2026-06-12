// <copyright file="EscShutdownMonitor.cs" company="">
// Copyright (c) 2026 Cedric Raguenaud
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
// </copyright>

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
