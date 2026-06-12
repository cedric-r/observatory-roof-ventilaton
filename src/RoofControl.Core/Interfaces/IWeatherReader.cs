using RoofControl.Core.Models;

namespace RoofControl.Core.Interfaces;

public interface IWeatherReader
{
    Task<WeatherData> ReadAsync(CancellationToken ct);
}
