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
