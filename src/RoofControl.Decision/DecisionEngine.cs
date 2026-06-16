// <copyright file="DecisionEngine.cs" company="">
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

using Microsoft.Extensions.Logging;
using RoofControl.Core.Interfaces;
using RoofControl.Core.Models;

namespace RoofControl.Decision;

public sealed class DecisionEngine : IDecisionEngine
{
    private readonly ILogger<DecisionEngine> _logger;
    private readonly TimeZoneInfo _timezone;
    private readonly TimeSpan _dayStart;
    private readonly TimeSpan _dayEnd;
    private readonly double _targetOpenPercent;
    private readonly int _nightfallHysteresisSeconds;
    private readonly double _minAmbientTemp;
    private readonly double _maxAmbientTemp;
    private readonly double _skyTempMin;
    private readonly double _skyTempMax;
    private readonly double _maxHumidity;
    private readonly int _rainThreshold;
    private readonly int _rainSafetyThreshold;
    private readonly double _windThreshold;
    private readonly bool _ignoreCloudWatcherSafe;
    private readonly HysteresisTracker _closeHysteresis;
    private readonly HysteresisTracker _nightfallHysteresis;
    private DateTime _lastDecisionTime;

    public DecisionEngine(
        ILogger<DecisionEngine> logger,
        string timezone,
        string dayStart,
        string dayEnd,
        double targetOpenPercent,
        int nightfallHysteresisSeconds,
        double minAmbientTemp,
        double maxAmbientTemp,
        double skyTempMin,
        double skyTempMax,
        double maxHumidity,
        int rainThreshold,
        int rainSafetyThreshold,
        double windThreshold,
        int closeDelaySeconds,
        bool ignoreCloudWatcherSafe = false)
    {
        _logger = logger;
        _timezone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        _dayStart = TimeSpan.Parse(dayStart);
        _dayEnd = TimeSpan.Parse(dayEnd);
        _targetOpenPercent = targetOpenPercent;
        _nightfallHysteresisSeconds = nightfallHysteresisSeconds;
        _minAmbientTemp = minAmbientTemp;
        _maxAmbientTemp = maxAmbientTemp;
        _skyTempMin = skyTempMin;
        _skyTempMax = skyTempMax;
        _maxHumidity = maxHumidity;
        _rainThreshold = rainThreshold;
        _rainSafetyThreshold = rainSafetyThreshold;
        _windThreshold = windThreshold;
        _ignoreCloudWatcherSafe = ignoreCloudWatcherSafe;
        _closeHysteresis = new HysteresisTracker(logger, "CloseDelay", TimeSpan.FromSeconds(closeDelaySeconds));
        _nightfallHysteresis = new HysteresisTracker(logger, "Nightfall", TimeSpan.FromSeconds(nightfallHysteresisSeconds));

        if (_ignoreCloudWatcherSafe)
            _logger.LogWarning("CloudWatcher safe flag is configured to be IGNORED — using individual thresholds only");
    }

    public DecisionResult Decide(WeatherData weather, RoofStatus roof, DateTime now)
    {
        _lastDecisionTime = now;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, _timezone);
        var localTime = localNow.TimeOfDay;
        var isDaytime = IsDaytime(localTime);

        var localOffset = _timezone.GetUtcOffset(localNow);
        _logger.LogDebug("Decision: local={LocalTime:yyyy-MM-dd HH:mm:ss} offset={Offset:+HH:mm} tz={Timezone}, day={IsDaytime}",
            localNow, localOffset, _timezone.Id, isDaytime);

        // Night: close roof (with hysteresis to prevent rapid toggling at twilight edges)
        if (!isDaytime)
        {
            if (_nightfallHysteresis.Evaluate(true, now))
            {
                if (roof.State is RoofState.Open or RoofState.Opening && roof.PositionTicks > 0)
                {
                    _logger.LogInformation("Nightfall: closing roof");
                    return new DecisionResult(DecisionAction.Close, null, "Nightfall — closing roof");
                }
                return new DecisionResult(DecisionAction.None, null, "Nighttime — roof already closed");
            }

            _logger.LogDebug("Nightfall hysteresis not yet elapsed");
            return new DecisionResult(DecisionAction.None, null, "Nightfall hysteresis pending");
        }

        // Daytime: evaluate weather conditions
        // Reset nightfall hysteresis since we're back in daytime
        _nightfallHysteresis.Reset();

        // Evaluate each condition, collect pass/fail with readable summaries
        var results = new List<(string Name, bool Passed, string Detail)>();
        bool allPassed = true;

        // ── Ambient temperature ──
        if (weather.AmbientTemperatureC is null)
        { results.Add(("Temperature", false, "unavailable")); allPassed = false; }
        else if (weather.AmbientTemperatureC.Value < _minAmbientTemp)
        { results.Add(("Temperature", false, $"{weather.AmbientTemperatureC.Value:F1}°C below min {_minAmbientTemp}°C")); allPassed = false; }
        else if (weather.AmbientTemperatureC.Value > _maxAmbientTemp)
        { results.Add(("Temperature", false, $"{weather.AmbientTemperatureC.Value:F1}°C above max {_maxAmbientTemp}°C")); allPassed = false; }
        else
        { results.Add(("Temperature", true, $"{weather.AmbientTemperatureC.Value:F1}°C in [{_minAmbientTemp},{_maxAmbientTemp}]")); }

        // ── Sky / cloud ──
        if (weather.SkyTemperatureC is null)
        { results.Add(("Sky temp", true, "unavailable (skipping)")); }
        else if (weather.SkyTemperatureC.Value < _skyTempMin)
        { results.Add(("Sky temp", false, $"{weather.SkyTemperatureC.Value:F1}°C below min {_skyTempMin}°C")); allPassed = false; }
        else if (weather.SkyTemperatureC.Value > _skyTempMax)
        { results.Add(("Sky temp", false, $"{weather.SkyTemperatureC.Value:F1}°C above max {_skyTempMax}°C (cloudy)")); allPassed = false; }
        else
        { results.Add(("Sky temp", true, $"{weather.SkyTemperatureC.Value:F1}°C in [{_skyTempMin},{_skyTempMax}]")); }

        // ── Humidity ──
        if (weather.HumidityPercent is null)
        { results.Add(("Humidity", false, "unavailable")); allPassed = false; }
        else if (weather.HumidityPercent.Value > _maxHumidity)
        { results.Add(("Humidity", false, $"{weather.HumidityPercent.Value:F0}% above max {_maxHumidity}%")); allPassed = false; }
        else
        { results.Add(("Humidity", true, $"{weather.HumidityPercent.Value:F0}% ≤ {_maxHumidity}%")); }

        // ── Rain sensor ──
        if (weather.RainRawAdc is null)
        { results.Add(("Rain", true, "unavailable (skipping)")); }
        else if (weather.RainRawAdc.Value >= _rainThreshold)
        { results.Add(("Rain", false, $"ADC {weather.RainRawAdc} ≥ {_rainThreshold}")); allPassed = false; }
        else
        { results.Add(("Rain", true, $"ADC {weather.RainRawAdc} < {_rainThreshold}")); }

        // ── Rain safety ──
        if (weather.RainSafetyStatus is null)
        { /* skip — not all units send this */ }
        else if (weather.RainSafetyStatus.Value > _rainSafetyThreshold)
        { results.Add(("Rain safe", false, $"status {weather.RainSafetyStatus} > {_rainSafetyThreshold}")); allPassed = false; }
        else
        { results.Add(("Rain safe", true, $"status {weather.RainSafetyStatus}")); }

        // ── Wind ──
        if (weather.WindSpeed is null)
        { results.Add(("Wind", false, "unavailable")); allPassed = false; }
        else if (weather.WindSpeed.Value > _windThreshold)
        { results.Add(("Wind", false, $"{weather.WindSpeed.Value:F1} m/s above {_windThreshold} m/s")); allPassed = false; }
        else
        { results.Add(("Wind", true, $"{weather.WindSpeed.Value:F1} m/s ≤ {_windThreshold} m/s")); }

        // ── CloudWatcher safe flag (optional) ──
        bool safePresent = weather.OverallSafe is not null;
        bool safeFailed = weather.OverallSafe is not null && weather.OverallSafe.Value == 0;
        if (!safePresent)
        { results.Add(("CW Safe", true, "unavailable (skipping)")); }
        else if (safeFailed && _ignoreCloudWatcherSafe)
        { results.Add(("CW Safe", true, $"flag=0 (IGNORED per config)")); }
        else if (safeFailed)
        { results.Add(("CW Safe", false, $"flag=0 (unsafe)")); allPassed = false; }
        else
        { results.Add(("CW Safe", true, $"flag={weather.OverallSafe}")); }

        // ── Log all checks at Information level ──
        foreach (var r in results)
            _logger.LogInformation("  {Mark} {Name,-13} {Detail}",
                r.Passed ? "✓" : "✗", r.Name, r.Detail);

        if (allPassed)
        {
            _logger.LogInformation("→ ALL CONDITIONS OK");

            if (roof.State == RoofState.Open &&
                Math.Abs(roof.PositionPercent - _targetOpenPercent) < 1.0)
                return new DecisionResult(DecisionAction.None, null, $"Already at target {_targetOpenPercent}%");

            if (_closeHysteresis.Evaluate(true, now))
            {
                _logger.LogInformation("→ OPENING roof to {Target}%", _targetOpenPercent);
                return new DecisionResult(DecisionAction.OpenToTarget, _targetOpenPercent,
                    $"Opening to {_targetOpenPercent}%");
            }

            _logger.LogInformation("→ Hysteresis pending, need conditions stable before opening");
            return new DecisionResult(DecisionAction.None, null, "Hysteresis pending before opening");
        }

        var failed = results.Where(r => !r.Passed).Select(r => $"{r.Name}: {r.Detail}");
        _logger.LogInformation("→ CONDITIONS BLOCKED: {Reasons}",
            string.Join("; ", failed));

        if (roof.State is RoofState.Open or RoofState.Opening && roof.PositionTicks > 0)
        {
            _closeHysteresis.Reset();
            _logger.LogInformation("→ CLOSING roof");
            return new DecisionResult(DecisionAction.Close, null,
                $"Closing: {string.Join("; ", failed)}");
        }

        _closeHysteresis.Reset();
        return new DecisionResult(DecisionAction.None, null, $"Blocked: {string.Join("; ", failed)}");
    }

    private bool IsDaytime(TimeSpan localTime)
    {
        if (_dayStart <= _dayEnd)
            return localTime >= _dayStart && localTime < _dayEnd;
        // Wraps around midnight (e.g. start=22:00, end=06:00)
        return localTime >= _dayStart || localTime < _dayEnd;
    }
}
