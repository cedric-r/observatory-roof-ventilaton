// <copyright file="RoofControlConfig.cs" company="">
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

using System.ComponentModel.DataAnnotations;

namespace RoofControl.Core.Configuration;

public sealed class RoofControlConfig
{
    [Required, MinLength(1)]
    public string Timezone { get; set; } = "UTC";

    public Talon6Config Talon6 { get; set; } = new();
    public WeatherSourceConfig WeatherSource { get; set; } = new();
    public PollingConfig Polling { get; set; } = new();
    public RoofRulesConfig RoofRules { get; set; } = new();
    public HysteresisConfig Hysteresis { get; set; } = new();
    public FailSafeConfig FailSafe { get; set; } = new();
    public MaintenanceConfig MaintenanceMode { get; set; } = new();
    public RetryConfig Retry { get; set; } = new();
    public SerializationConfig Serialization { get; set; } = new();
    public OverrideConfig Override { get; set; } = new();
}

public sealed class Talon6Config
{
    [Required, MinLength(1)]
    public string PortName { get; set; } = "COM4";

    [Range(300, 115200)]
    public int BaudRate { get; set; } = 9600;

    [Range(1000, 100000)]
    public int EncoderTicksMax { get; set; } = 50000;

    public bool SafetyEnabled { get; set; } = true;

    [Range(0, 10)]
    public int Parity { get; set; } = 0;

    [Range(1, 10)]
    public int StopBits { get; set; } = 1;

    [Range(5, 30)]
    public int ReadTimeoutSeconds { get; set; } = 10;

    [Range(5, 30)]
    public int WriteTimeoutSeconds { get; set; } = 10;
}

public sealed class WeatherSourceConfig
{
    [Required, Url]
    public string Url { get; set; } = string.Empty;

    [RegularExpression("^(KeyValue|Json)$")]
    public string Format { get; set; } = "KeyValue";

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class PollingConfig
{
    [Range(5, 300)]
    public int WeatherIntervalSeconds { get; set; } = 30;

    [Range(1, 60)]
    public int RoofStatusIntervalSeconds { get; set; } = 5;

    [Range(10, 600)]
    public int DecisionIntervalSeconds { get; set; } = 60;
}

public sealed class RoofRulesConfig
{
    public DaytimeConfig Daytime { get; set; } = new();
}

public sealed class DaytimeConfig
{
    [Required, RegularExpression(@"^\d{2}:\d{2}$")]
    public string StartTime { get; set; } = "06:00";

    [Required, RegularExpression(@"^\d{2}:\d{2}$")]
    public string EndTime { get; set; } = "20:00";

    [Range(1, 100)]
    public double TargetOpenPercent { get; set; } = 20.0;

    [Range(10, 300)]
    public int NightfallHysteresisSeconds { get; set; } = 30;

    public OpenConditionsConfig OpenConditions { get; set; } = new();
}

public sealed class OpenConditionsConfig
{
    public double MinAmbientTemp { get; set; } = 5.0;
    public double MaxAmbientTemp { get; set; } = 40.0;
    public double SkyTempMin { get; set; } = -50.0;
    public double SkyTempMax { get; set; } = -1.0;
    public double MaxHumidity { get; set; } = 75.0;
    public int RainThreshold { get; set; } = 400;
    public int RainSafetyThreshold { get; set; } = 0;
    public double WindThreshold { get; set; } = 10.0;
    public bool IgnoreCloudWatcherSafe { get; set; } = false;
}

public sealed class HysteresisConfig
{
    [Range(0, 600)]
    public int CloseDelaySeconds { get; set; } = 120;
}

public sealed class FailSafeConfig
{
    [Range(10, 600)]
    public int MaxWeatherAgeSeconds { get; set; } = 90;

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;

    [Range(50, 5000)]
    public int BaseDelayMs { get; set; } = 500;
}

public sealed class MaintenanceConfig
{
    public bool Enabled { get; set; } = false;
}

public sealed class RetryConfig
{
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 2;

    [Range(50, 10000)]
    public int BaseDelayMs { get; set; } = 500;
}

public sealed class SerializationConfig
{
    [Required, MinLength(1)]
    public string StateFilePath { get; set; } = "/var/lib/roofcontrol/state.json";
}

public sealed class OverrideConfig
{
    [Required, MinLength(1)]
    public string FilePath { get; set; } = "/etc/roofcontrol/override.flag";
}
