// <copyright file="ConfigValidationTests.cs" company="">
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
using RoofControl.Core.Configuration;

namespace RoofControl.Tests;

public class ConfigValidationTests
{
    private static bool TryValidateAll(object obj, List<ValidationResult> results)
    {
        var context = new ValidationContext(obj);
        var valid = Validator.TryValidateObject(obj, context, results, validateAllProperties: true);

        // Recursively validate nested objects
        foreach (var prop in obj.GetType().GetProperties())
        {
            if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
            {
                var nested = prop.GetValue(obj);
                if (nested is not null)
                {
                    var nestedResults = new List<ValidationResult>();
                    if (!TryValidateAll(nested, nestedResults))
                    {
                        results.AddRange(nestedResults);
                        valid = false;
                    }
                }
            }
        }

        return valid;
    }

    [Fact]
    public void DefaultConfig_Valid()
    {
        var config = new RoofControlConfig();
        config.WeatherSource.Url = "http://192.168.1.100/cgi-bin/cgiLastData"; // placeholder URL
        var results = new List<ValidationResult>();

        var valid = TryValidateAll(config, results);

        Assert.True(valid, string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    [Fact]
    public void Config_EmptyTimezone_Invalid()
    {
        var config = new RoofControlConfig { Timezone = "" };
        var results = new List<ValidationResult>();

        var valid = TryValidateAll(config, results);

        Assert.False(valid);
    }

    [Fact]
    public void Config_InvalidTalon6PortName_Invalid()
    {
        var config = new RoofControlConfig();
        config.Talon6.PortName = "";
        var results = new List<ValidationResult>();

        var valid = TryValidateAll(config, results);

        Assert.False(valid);
    }

    [Fact]
    public void Config_InvalidTargetOpenPercent_Invalid()
    {
        var config = new RoofControlConfig();
        config.RoofRules.Daytime.TargetOpenPercent = 150;
        var results = new List<ValidationResult>();

        var valid = TryValidateAll(config, results);

        Assert.False(valid);
    }

    [Fact]
    public void Config_InvalidEncoderTicksMax_Invalid()
    {
        var config = new RoofControlConfig();
        config.Talon6.EncoderTicksMax = 50; // below 1000
        var results = new List<ValidationResult>();

        var valid = TryValidateAll(config, results);

        Assert.False(valid);
    }

    [Fact]
    public void Config_InvalidWeatherUrl_Invalid()
    {
        var config = new RoofControlConfig();
        config.WeatherSource.Url = "not-a-url";
        var results = new List<ValidationResult>();

        var valid = TryValidateAll(config, results);

        Assert.False(valid);
    }
}
