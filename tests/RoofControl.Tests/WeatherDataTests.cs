// <copyright file="WeatherDataTests.cs" company="">
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

using RoofControl.Core.Models;

namespace RoofControl.Tests;

public class WeatherDataTests
{
    [Fact]
    public void IsStale_NoTimestamp_ReturnsTrue()
    {
        var data = new WeatherData(
            SkyTemperatureC: null, CloudSafetyStatus: null,
            AmbientTemperatureC: null, WindSpeed: null,
            WindSafetyStatus: null, RainRawAdc: null,
            RainSafetyStatus: null, SkyBrightnessMpsas: null,
            LightSafetyStatus: null, SwitchState: null,
            OverallSafe: null, HumidityPercent: null,
            HumiditySafetyStatus: null,
            DataTimestamp: null,
            FirmwareInfo: null, DewPointC: null, RawIrValue: null);

        Assert.True(data.IsStale(DateTime.UtcNow, TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void IsStale_FreshData_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var data = MakeDataWithTimestamp(now);

        Assert.False(data.IsStale(now, TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void IsStale_OldData_ReturnsTrue()
    {
        var now = DateTime.UtcNow;
        var data = MakeDataWithTimestamp(now.AddSeconds(-100));

        Assert.True(data.IsStale(now, TimeSpan.FromSeconds(90)));
    }

    [Fact]
    public void IsStale_ExactlyAtBoundary_ReturnsFalse()
    {
        var now = DateTime.UtcNow;
        var data = MakeDataWithTimestamp(now.AddSeconds(-90));

        Assert.False(data.IsStale(now, TimeSpan.FromSeconds(90)));
    }

    private static WeatherData MakeDataWithTimestamp(DateTime ts) => new(
        SkyTemperatureC: null, CloudSafetyStatus: null,
        AmbientTemperatureC: null, WindSpeed: null,
        WindSafetyStatus: null, RainRawAdc: null,
        RainSafetyStatus: null, SkyBrightnessMpsas: null,
        LightSafetyStatus: null, SwitchState: null,
        OverallSafe: null, HumidityPercent: null,
        HumiditySafetyStatus: null,
        DataTimestamp: ts,
        FirmwareInfo: null, DewPointC: null, RawIrValue: null);
}
