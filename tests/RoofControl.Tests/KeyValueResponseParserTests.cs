using RoofControl.Weather;

namespace RoofControl.Tests;

public class KeyValueResponseParserTests
{
    [Fact]
    public void Parse_FullResponse_ReturnsAllFields()
    {
        // Actual CloudWatcher format: newline-separated key=value pairs
        var raw = """
            clouds=-12.5
            cloudsSafe=1
            temp=18.2
            wind=2.3
            windSafe=1
            gust=3.1
            rain=250.0
            rainSafe=0
            lightmpsas=19.5
            lightSafe=1
            switch=0
            safe=1
            hum=45
            humSafe=1
            dataGMTTime=2026-06-12 14:30:00
            cwinfo=ESP32_v2.1
            dewp=5.2
            rawir=1024
            """;

        var result = KeyValueResponseParser.Parse(raw);

        Assert.NotNull(result);
        Assert.Equal(-12.5, result.SkyTemperatureC);
        Assert.Equal(1, result.CloudSafetyStatus);
        Assert.Equal(18.2, result.AmbientTemperatureC);
        Assert.Equal(2.3, result.WindSpeed);
        Assert.Equal(1, result.WindSafetyStatus);
        Assert.Equal(250, result.RainRawAdc);
        Assert.Equal(0, result.RainSafetyStatus);
        Assert.Equal(19.5, result.SkyBrightnessMpsas);
        Assert.Equal(1, result.LightSafetyStatus);
        Assert.Equal(0, result.SwitchState);
        Assert.Equal(1, result.OverallSafe);
        Assert.Equal(45, result.HumidityPercent);
        Assert.Equal(1, result.HumiditySafetyStatus);
        Assert.NotNull(result.DataTimestamp);
        Assert.Equal("ESP32_v2.1", result.FirmwareInfo);
        Assert.Equal(5.2, result.DewPointC);
        Assert.Equal(1024, result.RawIrValue);
    }

    [Fact]
    public void Parse_MinimalResponse_Succeeds()
    {
        var raw = """
            clouds=-5.0
            temp=15.0
            hum=60
            safe=1
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.Equal(-5.0, result.SkyTemperatureC);
        Assert.Equal(15.0, result.AmbientTemperatureC);
        Assert.Equal(60, result.HumidityPercent);
        Assert.Equal(1, result.OverallSafe);
        Assert.Null(result.WindSpeed);
        Assert.Null(result.DewPointC);
    }

    [Fact]
    public void Parse_WithTimestampHeader_ParsesTimestamp()
    {
        // First line is a date header without '='
        var raw = """
            2026-Jun-12GMT14:59:04
            clouds=-9.37
            temp=37.39
            hum=22.0
            safe=1
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.Equal(-9.37, result.SkyTemperatureC);
        Assert.Equal(37.39, result.AmbientTemperatureC);
        Assert.Equal(22.0, result.HumidityPercent);
        Assert.Equal(1, result.OverallSafe);
        Assert.NotNull(result.DataTimestamp);
        // June 12, 2026 at 14:59:04 UTC
        Assert.Equal(new DateTime(2026, 6, 12, 14, 59, 04, DateTimeKind.Utc), result.DataTimestamp.Value);
    }

    [Fact]
    public void Parse_RainZero_ReturnsZero()
    {
        // rain=0 means no rain detected. rainsensor is ignored (varies per unit).
        var raw = """
            rain=0.0
            rainsensor=3712.000000
            clouds=-10.0
            temp=20.0
            hum=50
            safe=1
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.Equal(0, result.RainRawAdc);
    }

    [Fact]
    public void Parse_RainWithPositiveValue_UsesRain()
    {
        // When rain > 0, uses rain field directly (threshold is minimum)
        var raw = """
            rain=500.0
            clouds=-10.0
            temp=20.0
            hum=50
            safe=1
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.Equal(500, result.RainRawAdc);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        Assert.Null(KeyValueResponseParser.Parse(""));
        Assert.Null(KeyValueResponseParser.Parse("   "));
        Assert.Null(KeyValueResponseParser.Parse(null!));
    }

    [Fact]
    public void Parse_NoValidKeyValuePairs_ReturnsNull()
    {
        Assert.Null(KeyValueResponseParser.Parse("garbage no-equals here"));
    }

    [Fact]
    public void Parse_InvalidNumericDefaultsToNull()
    {
        var raw = """
            clouds=notanumber
            temp=15.0
            hum=60
            safe=1
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.Null(result.SkyTemperatureC);
        Assert.Equal(15.0, result.AmbientTemperatureC);
    }

    [Fact]
    public void Parse_MissingOptionalFields_DoesNotFail()
    {
        var raw = """
            clouds=-10.0
            temp=20.0
            hum=50
            safe=1
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.Null(result.DewPointC);
        Assert.Null(result.SkyBrightnessMpsas);
        Assert.Null(result.RawIrValue);
    }

    [Fact]
    public void Parse_TimestampAltFormat_Parses()
    {
        var raw = """
            clouds=0
            temp=0
            hum=0
            safe=1
            dataGMTTime=06/12/2026 14:30:00
            """;
        var result = KeyValueResponseParser.Parse(raw);
        Assert.NotNull(result);
        Assert.NotNull(result.DataTimestamp);
    }
}
