// <copyright file="CloudLevelClassifierTests.cs" company="">
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

using RoofControl.Decision;

namespace RoofControl.Tests;

public class CloudLevelClassifierTests
{
    [Fact]
    public void Classify_NullSkyTemp_ReturnsUnknown()
    {
        Assert.Equal(CloudLevel.Unknown, CloudLevelClassifier.Classify(null));
    }

    [Fact]
    public void Classify_ColdSky_Clear()
    {
        Assert.Equal(CloudLevel.Clear, CloudLevelClassifier.Classify(-20.0));
    }

    [Fact]
    public void Classify_WarmSky_VeryCloudy()
    {
        Assert.Equal(CloudLevel.VeryCloudy, CloudLevelClassifier.Classify(0.0));
    }

    [Fact]
    public void Classify_MidRange_Cloudy()
    {
        Assert.Equal(CloudLevel.Cloudy, CloudLevelClassifier.Classify(-10.0));
    }

    [Fact]
    public void Classify_CustomThresholds()
    {
        // Custom thresholds: clear below -5, cloudy below 0
        Assert.Equal(CloudLevel.Clear, CloudLevelClassifier.Classify(-6.0, -5.0, 0.0));
        Assert.Equal(CloudLevel.Cloudy, CloudLevelClassifier.Classify(-2.0, -5.0, 0.0));
        Assert.Equal(CloudLevel.VeryCloudy, CloudLevelClassifier.Classify(5.0, -5.0, 0.0));
    }
}
