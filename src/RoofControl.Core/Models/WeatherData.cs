// <copyright file="WeatherData.cs" company="">
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

namespace RoofControl.Core.Models;

public record WeatherData(
    double? SkyTemperatureC,
    int? CloudSafetyStatus,
    double? AmbientTemperatureC,
    double? WindSpeed,
    int? WindSafetyStatus,
    int? RainRawAdc,
    int? RainSafetyStatus,
    double? SkyBrightnessMpsas,
    int? LightSafetyStatus,
    int? SwitchState,
    int? OverallSafe,
    double? HumidityPercent,
    int? HumiditySafetyStatus,
    DateTime? DataTimestamp,
    string? FirmwareInfo,
    double? DewPointC,
    int? RawIrValue
)
{
    public bool IsStale(DateTime now, TimeSpan maxAge)
        => !DataTimestamp.HasValue || (now - DataTimestamp.Value) > maxAge;
}
