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
