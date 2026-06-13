// <copyright file="RoofWorker.cs" company="">
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

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoofControl.App;
using RoofControl.Core.Configuration;
using RoofControl.Core.Interfaces;
using RoofControl.Core.Models;

namespace RoofControl.App;

/// <summary>
/// Main orchestration loop: read weather → evaluate → act on roof.
/// Runs as a BackgroundService with configurable intervals.
/// </summary>
public sealed class RoofWorker : BackgroundService
{
    private readonly ILogger<RoofWorker> _logger;
    private readonly IWeatherReader _weatherReader;
    private readonly IRoofController _roofController;
    private readonly IDecisionEngine _decisionEngine;
    private readonly StatePersistence _statePersistence;
    private readonly OverrideMonitor _overrideMonitor;
    private readonly RoofControlConfig _config;
    private RoofStatus? _lastRoofStatus;

    public RoofWorker(
        ILogger<RoofWorker> logger,
        IWeatherReader weatherReader,
        IRoofController roofController,
        IDecisionEngine decisionEngine,
        StatePersistence statePersistence,
        OverrideMonitor overrideMonitor,
        RoofControlConfig config)
    {
        _logger = logger;
        _weatherReader = weatherReader;
        _roofController = roofController;
        _decisionEngine = decisionEngine;
        _statePersistence = statePersistence;
        _overrideMonitor = overrideMonitor;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoofWorker starting");

        // Restore last-known state on startup
        var savedState = await _statePersistence.LoadAsync(stoppingToken);
        _logger.LogInformation("Recovered state: {State} at {PosTicks} ticks",
            savedState.LastKnownState, savedState.LastKnownPositionTicks);

        // Query current roof position before issuing any commands (power-loss recovery)
        try
        {
            _lastRoofStatus = await _roofController.GetStatusAsync(stoppingToken);
            _logger.LogInformation("Current roof status: {State}, position={Pos}%",
                _lastRoofStatus.State, _lastRoofStatus.PositionPercent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not query initial roof status; will retry on first cycle");
        }

        var weatherTimer = TimeSpan.FromSeconds(_config.Polling.WeatherIntervalSeconds);
        var decisionTimer = TimeSpan.FromSeconds(_config.Polling.DecisionIntervalSeconds);
        var statusTimer = TimeSpan.FromSeconds(_config.Polling.RoofStatusIntervalSeconds);

        var lastWeatherFetch = DateTime.MinValue;
        var lastDecision = DateTime.MinValue;
        var lastStatusFetch = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // 1. Check operator override
                var overridden = _overrideMonitor.IsOverridden();

                // 2. Fetch weather on interval
                WeatherData? weather = null;
                if (now - lastWeatherFetch >= weatherTimer)
                {
                    try
                    {
                        weather = await _weatherReader.ReadAsync(stoppingToken);
                        lastWeatherFetch = now;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Weather fetch failed");
                    }
                }

                // 3. Fetch roof status on interval
                if (now - lastStatusFetch >= statusTimer)
                {
                    try
                    {
                        _lastRoofStatus = await _roofController.GetStatusAsync(stoppingToken);
                        lastStatusFetch = now;

                        // Persist current state
                        if (_lastRoofStatus is not null)
                        {
                            await _statePersistence.SaveAsync(new PersistedState
                            {
                                LastKnownState = _lastRoofStatus.State.ToString(),
                                LastKnownPositionTicks = _lastRoofStatus.PositionTicks
                            }, stoppingToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Roof status fetch failed");
                    }
                }

                // 4. Run decision on interval
                if (weather is not null && now - lastDecision >= decisionTimer)
                {
                    // Check weather staleness
                    if (weather.IsStale(now, TimeSpan.FromSeconds(_config.FailSafe.MaxWeatherAgeSeconds)))
                    {
                        _logger.LogWarning("Weather data is stale (timestamp={Ts}), initiating fail-safe close",
                            weather.DataTimestamp);

                        if (!overridden && _lastRoofStatus?.PositionTicks > 0)
                        {
                            await ExecuteSafeAsync(() => _roofController.CloseAsync(stoppingToken), stoppingToken);
                            if (_lastRoofStatus?.PositionTicks > 0)
                                await TriggerAutoShutdownAsync(stoppingToken);
                        }
                        lastDecision = now;
                        continue;
                    }

                    if (!overridden && _lastRoofStatus is not null)
                    {
                        var decision = _decisionEngine.Decide(weather, _lastRoofStatus, now);

                        switch (decision.Action)
                        {
                            case DecisionAction.OpenToTarget:
                                await ExecuteSafeAsync(
                                    () => _roofController.GoToPositionAsync(decision.TargetPositionPercent!.Value, stoppingToken),
                                    stoppingToken);
                                break;

                            case DecisionAction.Close:
                                await ExecuteSafeAsync(
                                    () => _roofController.CloseAsync(stoppingToken),
                                    stoppingToken);
                                await TriggerAutoShutdownAsync(stoppingToken);
                                break;

                            case DecisionAction.Stop:
                                await ExecuteSafeAsync(
                                    () => _roofController.StopAsync(stoppingToken),
                                    stoppingToken);
                                break;

                            case DecisionAction.None:
                                _logger.LogDebug("No action: {Reason}", decision.Reason);
                                break;
                        }
                    }
                    else if (overridden)
                    {
                        _logger.LogInformation("Operator override active — suppressing automated roof commands");
                    }

                    lastDecision = now;
                }

                await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in main loop");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("RoofWorker stopped");
    }

    private async Task TriggerAutoShutdownAsync(CancellationToken ct)
    {
        if (!_config.ShutdownTrigger.Enabled)
            return;

        _logger.LogWarning("SHUTDOWN TRIGGER: Roof closed automatically, waiting {Delay}s before system shutdown",
            _config.ShutdownTrigger.DelaySeconds);

        // Wait for roof to reach fully closed position
        try
        {
            var pollStart = DateTime.UtcNow;
            var maxPollTime = TimeSpan.FromSeconds(30);

            while (DateTime.UtcNow - pollStart < maxPollTime)
            {
                var status = await _roofController.GetStatusAsync(ct);
                if (status.State == RoofState.Closed || status.RoofTotallyClosed)
                {
                    _logger.LogInformation("SHUTDOWN TRIGGER: Roof confirmed fully closed");
                    break;
                }
                await Task.Delay(2000, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SHUTDOWN TRIGGER: Could not confirm roof closed, proceeding with shutdown anyway");
        }

        // Give operator a chance to abort via Ctrl+C during delay window
        _logger.LogWarning("SHUTDOWN TRIGGER: Initiating system shutdown in {DelaySeconds}s. Press Ctrl+C to abort.",
            _config.ShutdownTrigger.DelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(_config.ShutdownTrigger.DelaySeconds), ct);

        _logger.LogWarning("SHUTDOWN TRIGGER: Executing: {Command}", _config.ShutdownTrigger.Command);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows()
                    ? $"/c {_config.ShutdownTrigger.Command}"
                    : $"-c \"{_config.ShutdownTrigger.Command}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            _logger.LogInformation("SHUTDOWN TRIGGER: Shutdown command issued (pid={Pid})", proc?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SHUTDOWN TRIGGER: Failed to execute shutdown command");
        }
    }

    private async Task ExecuteSafeAsync(Func<Task> action, CancellationToken ct)
    {
        try
        {
            await action();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("safety"))
        {
            _logger.LogWarning("Command blocked by safety interlock: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed");
        }
    }
}
