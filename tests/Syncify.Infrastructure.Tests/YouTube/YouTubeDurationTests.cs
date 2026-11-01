using Syncify.Infrastructure.YouTube;

namespace Syncify.Infrastructure.Tests.YouTube;

public sealed class YouTubeDurationTests
{
    [Theory]
    [InlineData("PT4M8S", 248)]
    [InlineData("PT45S", 45)]
    [InlineData("PT3M", 180)]
    [InlineData("PT1H2M3S", 3723)]
    [InlineData("P1DT1S", 86401)]
    public void Parse_IsoDuration_ReturnsTimeSpan(string value, int expectedSeconds)
    {
        YouTubeDuration.Parse(value).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("P0D")]
    [InlineData("PT0S")]
    [InlineData("4:08")]
    public void Parse_LiveStreamOrUnreadable_ReturnsNull(string? value)
    {
        YouTubeDuration.Parse(value).ShouldBeNull();
    }
}
