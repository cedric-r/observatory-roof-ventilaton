// <copyright file="HttpWeatherReader.cs" company="">
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

using System.Net.Http;
using Microsoft.Extensions.Logging;
using RoofControl.Core.Interfaces;
using RoofControl.Core.Models;

namespace RoofControl.Weather;

public sealed class HttpWeatherReader : IWeatherReader, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWeatherReader> _logger;
    private readonly string _url;
    private readonly string _format;
    private bool _disposed;

    public HttpWeatherReader(
        HttpClient httpClient,
        ILogger<HttpWeatherReader> logger,
        string url,
        string format)
    {
        _httpClient = httpClient;
        _logger = logger;
        _url = url;
        _format = format;
    }

    public async Task<WeatherData> ReadAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Fetching weather data from {Url}", _url);

            var response = await _httpClient.GetStringAsync(_url, ct);

            if (string.IsNullOrWhiteSpace(response))
            {
                _logger.LogWarning("Empty response from weather source");
                return CreateStaleData();
            }

            _logger.LogDebug("Raw weather response ({Len} chars): {Preview}",
                response.Length,
                response.Length > 200 ? response[..200] + "..." : response);

            WeatherData? parsed;

            if (_format.Equals("Json", StringComparison.OrdinalIgnoreCase))
            {
                parsed = ParseJson(response);
            }
            else
            {
                parsed = KeyValueResponseParser.Parse(response);
            }

            if (parsed is null)
            {
                _logger.LogWarning("Weather parser returned null — unrecognized response format");
                return CreateStaleData();
            }

            // Ensure DataTimestamp is set; if the device didn't provide one, use now
            if (!parsed.DataTimestamp.HasValue)
            {
                parsed = parsed with { DataTimestamp = DateTime.UtcNow };
            }

            _logger.LogDebug(
                "Weather data: temp={Temp}°C, humidity={Hum}%, clouds={Clouds}°C, safe={Safe}",
                parsed.AmbientTemperatureC, parsed.HumidityPercent,
                parsed.SkyTemperatureC, parsed.OverallSafe);

            return parsed;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Weather fetch cancelled");
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching weather data");
            return CreateStaleData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching weather data");
            return CreateStaleData();
        }
    }

    private static WeatherData CreateStaleData()
        => new(
            SkyTemperatureC: null, CloudSafetyStatus: null,
            AmbientTemperatureC: null, WindSpeed: null,
            WindSafetyStatus: null, RainRawAdc: null,
            RainSafetyStatus: null, SkyBrightnessMpsas: null,
            LightSafetyStatus: null, SwitchState: null,
            OverallSafe: null, HumidityPercent: null,
            HumiditySafetyStatus: null,
            DataTimestamp: DateTime.UtcNow,
            FirmwareInfo: null, DewPointC: null,
            RawIrValue: null
        );

    private static WeatherData? ParseJson(string json)
    {
        // Future: support JSON format if CloudWatcher firmware supports it
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
