using HomecrestWeatherAlert.Services;

namespace HomecrestWeatherAlert.Tests;

public class SmsOptOutKeywordsTests
{
    [Theory]
    [InlineData("STOP", true)]
    [InlineData("stop", true)]
    [InlineData("Stop", true)]
    [InlineData("STOPALL", true)]
    [InlineData("UNSUBSCRIBE", true)]
    [InlineData("CANCEL", true)]
    [InlineData("END", true)]
    [InlineData("QUIT", true)]
    [InlineData("STOP!", true)]
    [InlineData("  stop  ", true)]
    [InlineData("PLEASE STOP", false)]
    [InlineData("START", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOptOut_recognizes_common_keywords(string? body, bool expected)
    {
        Assert.Equal(expected, SmsOptOutKeywords.IsOptOut(body));
    }
}
