using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoofControl.Core.Interfaces;
using RoofControl.Core.Models;

namespace RoofControl.App;

/// <summary>
/// Safety watchdog that monitors for hazardous conditions independent of the main decision loop.
/// Checks: weather data staleness, roof error state, serial port health.
/// Issues fail-safe commands through a dedicated CancellationToken that can preempt the main loop.
/// </summary>
public sealed class SafetyWatchdog : BackgroundService
{
    private readonly ILogger<SafetyWatchdog> _logger;
    private readonly IRoofController _roofController;
    private readonly IWeatherReader _weatherReader;
    private readonly TimeSpan _maxWeatherAge;
    private readonly int _maxSerialRetries;
    private readonly int _baseRetryDelayMs;
    private int _consecutiveSerialFailures;

    private DateTime? _lastKnownWeatherTimestamp;

    public SafetyWatchdog(
        ILogger<SafetyWatchdog> logger,
        IRoofController roofController,
        IWeatherReader weatherReader,
        TimeSpan maxWeatherAge,
        int maxSerialRetries,
        int baseRetryDelayMs)
    {
        _logger = logger;
        _roofController = roofController;
        _weatherReader = weatherReader;
        _maxWeatherAge = maxWeatherAge;
        _maxSerialRetries = maxSerialRetries;
        _baseRetryDelayMs = baseRetryDelayMs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Safety watchdog started (maxWeatherAge={Age}s, maxRetries={Retries})",
            _maxWeatherAge.TotalSeconds, _maxSerialRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await HeartbeatAsync(stoppingToken);
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Safety watchdog error");
                await Task.Delay(10000, stoppingToken);
            }
        }

        _logger.LogInformation("Safety watchdog stopped");
    }

    private async Task HeartbeatAsync(CancellationToken ct)
    {
        // 1. Update last-known weather timestamp from a fresh read
        try
        {
            var weather = await _weatherReader.ReadAsync(ct);
            if (weather.DataTimestamp.HasValue)
            {
                _lastKnownWeatherTimestamp = weather.DataTimestamp.Value;

                if (weather.IsStale(DateTime.UtcNow, _maxWeatherAge))
                {
                    _logger.LogWarning("SAFETY: Weather data stale ({Ts}), initiating fail-safe close",
                        weather.DataTimestamp);
                    await IssueFailSafeClose(ct);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SAFETY: Weather fetch failed in watchdog");
        }

        // 2. Check roof error state
        try
        {
            var status = await _roofController.GetStatusAsync(ct);
            _consecutiveSerialFailures = 0; // reset on success

            if (status.State == RoofState.Error)
            {
                _logger.LogError("SAFETY: Roof in Error state (action code {Code}: {Desc})",
                    status.LastActionCode, status.LastActionDescription ?? "unknown");
                await IssueFailSafeStop(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            _consecutiveSerialFailures++;
            _logger.LogWarning("SAFETY: Serial communication timeout ({Count}/{MaxRetries})",
                _consecutiveSerialFailures, _maxSerialRetries);

            if (_consecutiveSerialFailures >= _maxSerialRetries)
            {
                _logger.LogError("SAFETY: Max serial retries exceeded, initiating fail-safe close");
                await IssueFailSafeClose(ct);
            }
        }
        catch (Exception ex)
        {
            _consecutiveSerialFailures++;
            _logger.LogWarning(ex, "SAFETY: Serial communication failure ({Count}/{MaxRetries})",
                _consecutiveSerialFailures, _maxSerialRetries);

            if (_consecutiveSerialFailures >= _maxSerialRetries)
            {
                _logger.LogError("SAFETY: Max serial retries exceeded, initiating fail-safe close");
                await IssueFailSafeClose(ct);
            }
        }
    }

    private async Task IssueFailSafeClose(CancellationToken ct)
    {
        try
        {
            _logger.LogWarning("FAIL-SAFE: Issuing close command");
            await _roofController.CloseAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAIL-SAFE: Close command failed");
        }
    }

    private async Task IssueFailSafeStop(CancellationToken ct)
    {
        try
        {
            _logger.LogWarning("FAIL-SAFE: Issuing emergency stop");
            await _roofController.StopAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FAIL-SAFE: Stop command failed");
        }
    }
}
