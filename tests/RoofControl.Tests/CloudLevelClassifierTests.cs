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
