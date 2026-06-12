// <copyright file="CloudLevelClassifier.cs" company="">
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

namespace RoofControl.Decision;

public enum CloudLevel
{
    Unknown,
    Clear,
    Cloudy,
    VeryCloudy
}

/// <summary>
/// Classifies cloud cover based on sky (IR) temperature.
/// Clear skies have colder IR readings; clouds trap heat and read warmer.
/// </summary>
public static class CloudLevelClassifier
{
    /// <summary>
    /// Classify cloud level from sky temperature.
    /// </summary>
    /// <param name="skyTempC">Sky temperature from IR sensor. Null if unavailable.</param>
    /// <param name="thresholdClearMax">Max sky temp considered "clear" (e.g. -15°C).</param>
    /// <param name="thresholdCloudyMax">Max sky temp considered "cloudy" (e.g. -5°C).</param>
    public static CloudLevel Classify(
        double? skyTempC,
        double thresholdClearMax = -15.0,
        double thresholdCloudyMax = -5.0)
    {
        if (!skyTempC.HasValue)
            return CloudLevel.Unknown;

        if (skyTempC.Value <= thresholdClearMax)
            return CloudLevel.Clear;

        if (skyTempC.Value <= thresholdCloudyMax)
            return CloudLevel.Cloudy;

        return CloudLevel.VeryCloudy;
    }
}
