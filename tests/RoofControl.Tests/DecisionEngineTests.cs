// <copyright file="DecisionEngineTests.cs" company="">
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

using Microsoft.Extensions.Logging.Abstractions;
using RoofControl.Core.Models;
using RoofControl.Decision;

namespace RoofControl.Tests;

public class DecisionEngineTests
{
    private static DecisionEngine CreateEngine(
        string timezone = "UTC",
        string dayStart = "06:00",
        string dayEnd = "20:00",
        double targetOpenPct = 20.0,
        int nightfallHys = 0,
        double minTemp = 5.0,
        double maxTemp = 40.0,
        double skyTempMin = -50.0,
        double skyTempMax = -1.0,
        double maxHumidity = 75.0,
        int rainThreshold = 400,
        int rainSafety = 0,
        double windThreshold = 10.0,
        int closeDelay = 0)
    {
        return new DecisionEngine(
            NullLogger<DecisionEngine>.Instance,
            timezone, dayStart, dayEnd,
            targetOpenPct, nightfallHys,
            minTemp, maxTemp, skyTempMin, skyTempMax,
            maxHumidity, rainThreshold, rainSafety, windThreshold,
            closeDelay);
    }

    private static WeatherData MakeWeather(
        double? temp = 20.0,
        double? skyTemp = -10.0,
        double? humidity = 50.0,
        int? rain = 100,
        double? wind = 2.0,
        int? safe = 1,
        DateTime? ts = null) => new(
        SkyTemperatureC: skyTemp, CloudSafetyStatus: 1,
        AmbientTemperatureC: temp, WindSpeed: wind,
        WindSafetyStatus: 1, RainRawAdc: rain,
        RainSafetyStatus: 0, SkyBrightnessMpsas: null,
        LightSafetyStatus: 1, SwitchState: 0,
        OverallSafe: safe, HumidityPercent: humidity,
        HumiditySafetyStatus: 1,
        DataTimestamp: ts ?? DateTime.UtcNow,
        FirmwareInfo: null, DewPointC: null, RawIrValue: null
    );

    private static RoofStatus MakeRoof(RoofState state = RoofState.Closed, int ticks = 0) => new(
        State: state,
        PositionTicks: ticks,
        PositionPercent: ticks / 500.0, // 50000 max
        PowerSupplyVoltage: 12.0,
        CloudWatcherRelayClosed: false,
        RoofTotallyOpen: state == RoofState.Open && ticks >= 50000,
        RoofTotallyClosed: state == RoofState.Closed,
        LastActionCode: 0,
        LastActionDescription: null
    );

    [Fact]
    public void Decide_NightTime_ClosesOpenRoof()
    {
        var engine = CreateEngine();
        var weather = MakeWeather();
        var roof = MakeRoof(RoofState.Open, 10000);
        // Night time: 03:00 UTC
        var night = new DateTime(2026, 6, 12, 3, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, night);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_DaytimeGoodConditions_OpensToTarget()
    {
        var engine = CreateEngine(closeDelay: 0);
        var weather = MakeWeather();
        var roof = MakeRoof(RoofState.Closed);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.OpenToTarget, result.Action);
        Assert.Equal(20.0, result.TargetPositionPercent);
    }

    [Fact]
    public void Decide_DaytimeAlreadyAtTarget_NoAction()
    {
        var engine = CreateEngine(closeDelay: 0);
        var weather = MakeWeather();
        var roof = MakeRoof(RoofState.Open, 10000); // 20% at 50000 max
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.None, result.Action);
    }

    [Fact]
    public void Decide_HumidityTooHigh_ClosesRoof()
    {
        var engine = CreateEngine(closeDelay: 0);
        var weather = MakeWeather(humidity: 85.0);
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_TempBelowMin_ClosesRoof()
    {
        var engine = CreateEngine(minTemp: 5.0, closeDelay: 0);
        var weather = MakeWeather(temp: 2.0);
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_TempAboveMax_ClosesRoof()
    {
        var engine = CreateEngine(maxTemp: 40.0, closeDelay: 0);
        var weather = MakeWeather(temp: 42.0);
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_RainAboveThreshold_ClosesRoof()
    {
        var engine = CreateEngine(rainThreshold: 400, closeDelay: 0);
        var weather = MakeWeather(rain: 500);
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_WindAboveThreshold_ClosesRoof()
    {
        var engine = CreateEngine(windThreshold: 10.0, closeDelay: 0);
        var weather = MakeWeather(wind: 15.0);
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_SkyTempAboveMax_ClosesRoof()
    {
        var engine = CreateEngine(skyTempMax: -1.0, closeDelay: 0);
        var weather = MakeWeather(skyTemp: 5.0); // too warm = cloudy
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_OverallUnsafe_ClosesRoof()
    {
        var engine = CreateEngine(closeDelay: 0);
        var weather = MakeWeather(safe: 0);
        var roof = MakeRoof(RoofState.Open, 10000);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.Close, result.Action);
    }

    [Fact]
    public void Decide_RoofAlreadyClosed_BadConditions_NoAction()
    {
        var engine = CreateEngine(closeDelay: 0);
        var weather = MakeWeather(wind: 20.0);
        var roof = MakeRoof(RoofState.Closed);
        var day = new DateTime(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);

        var result = engine.Decide(weather, roof, day);

        Assert.Equal(DecisionAction.None, result.Action);
    }

    [Fact]
    public void Decide_NightfallHysteresis_DelaysClose()
    {
        var engine = CreateEngine(nightfallHys: 300); // 5 min hysteresis
        var weather = MakeWeather();
        var roof = MakeRoof(RoofState.Open, 10000);
        // Start of night
        var night = new DateTime(2026, 6, 12, 20, 0, 1, DateTimeKind.Utc);

        var result1 = engine.Decide(weather, roof, night);
        Assert.Equal(DecisionAction.None, result1.Action); // still in hysteresis
    }

    [Fact]
    public void Decide_DaytimeCrossMidnight_Works()
    {
        // Night schedule: 22:00 to 06:00
        var engine = CreateEngine(dayStart: "06:00", dayEnd: "22:00");
        var weather = MakeWeather();
        var roof = MakeRoof(RoofState.Open, 10000);

        // 23:00 = nighttime
        var night = new DateTime(2026, 6, 12, 23, 0, 0, DateTimeKind.Utc);
        var result = engine.Decide(weather, roof, night);
        Assert.Equal(DecisionAction.Close, result.Action);
    }
}
