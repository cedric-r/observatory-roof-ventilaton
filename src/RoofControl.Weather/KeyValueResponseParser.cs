// <copyright file="KeyValueResponseParser.cs" company="">
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

using System.Globalization;
using RoofControl.Core.Models;

namespace RoofControl.Weather;

/// <summary>
/// Parses AAG CloudWatcher key=value response format.
/// Input is newline-separated key=value pairs (one per line).
/// The first line may contain a timestamp in formats like:
///   "2026-Jun-12GMT14:59:04" without an '=' sign.
/// </summary>
public static class KeyValueResponseParser
{
    public static WeatherData? Parse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return null;

        var lines = rawResponse.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
            return null;

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DateTime? dataTimestamp = null;

        foreach (var line in lines)
        {
            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                // key=value pair
                var key = line[..eqIndex];
                var value = line[(eqIndex + 1)..];
                dict[key] = value;
            }
            else if (dataTimestamp is null)
            {
                // Line without '=' — try parsing as timestamp
                // Format: "2026-Jun-12GMT14:59:04"
                if (DateTime.TryParseExact(line, "yyyy-MMM-dd'GMT'HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var ts))
                {
                    dataTimestamp = ts;
                }
            }
        }

        if (dict.Count == 0)
            return null;

        // Override timestamp if dataGMTTime key is present
        if (dict.TryGetValue("dataGMTTime", out var timeStr))
        {
            if (DateTime.TryParseExact(timeStr, ["yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy HH:mm:ss"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedTime))
            {
                dataTimestamp = parsedTime;
            }
        }

        // Rain: use CloudWatcher's processed rain field directly (rain=0 = dry, rain>0 = wet).
        // The rainsensor raw ADC varies per hardware unit and should not be used
        // with a fixed threshold — CloudWatcher's own detection is authoritative.
        int? rainAdc = null;
        var rainVal = ParseDouble(dict, "rain");
        if (rainVal.HasValue)
            rainAdc = Convert.ToInt32(rainVal.Value);

        return new WeatherData(
            SkyTemperatureC: ParseDouble(dict, "clouds"),
            CloudSafetyStatus: ParseInt(dict, "cloudsSafe"),
            AmbientTemperatureC: ParseDouble(dict, "temp"),
            WindSpeed: ParseDouble(dict, "wind"),
            WindSafetyStatus: ParseInt(dict, "windSafe"),
            RainRawAdc: rainAdc,
            RainSafetyStatus: ParseInt(dict, "rainSafe"),
            SkyBrightnessMpsas: ParseDouble(dict, "lightmpsas"),
            LightSafetyStatus: ParseInt(dict, "lightSafe"),
            SwitchState: ParseInt(dict, "switch"),
            OverallSafe: ParseInt(dict, "safe"),
            HumidityPercent: ParseDouble(dict, "hum"),
            HumiditySafetyStatus: ParseInt(dict, "humSafe"),
            DataTimestamp: dataTimestamp,
            FirmwareInfo: dict.GetValueOrDefault("cwinfo"),
            DewPointC: ParseDouble(dict, "dewp"),
            RawIrValue: ParseInt(dict, "rawir")
        );
    }

    private static double? ParseDouble(Dictionary<string, string> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) &&
            double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static int? ParseInt(Dictionary<string, string> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) &&
            int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }
}
