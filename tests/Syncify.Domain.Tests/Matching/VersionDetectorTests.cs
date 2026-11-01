using Syncify.Domain.Matching;

namespace Syncify.Domain.Tests.Matching;

public sealed class VersionDetectorTests
{
    [Theory]
    [InlineData("Tu", TrackVersion.Original)]
    [InlineData("Tu - Radio Edit", TrackVersion.Original)]
    [InlineData("Tu - Remastered 2011", TrackVersion.Original)]
    [InlineData("Tu (Remix)", TrackVersion.Remix)]
    [InlineData("Tu - Dj Sava Rmx", TrackVersion.Remix)]
    [InlineData("Tu (Live)", TrackVersion.Live)]
    [InlineData("Tu - Live at Sala Palatului", TrackVersion.Live)]
    [InlineData("Tu (Acoustic Version)", TrackVersion.Acoustic)]
    [InlineData("Tu (Live Acoustic)", TrackVersion.Acoustic)]
    [InlineData("Tu [Unplugged]", TrackVersion.Acoustic)]
    [InlineData("Tu (Instrumental)", TrackVersion.Instrumental)]
    [InlineData("Tu (Karaoke Version)", TrackVersion.Karaoke)]
    [InlineData("Tu (Cover)", TrackVersion.Cover)]
    [InlineData("Tu (Sped Up)", TrackVersion.SpedUp)]
    [InlineData("Tu (Slowed + Reverb)", TrackVersion.Slowed)]
    [InlineData("Live Your Life", TrackVersion.Original)]
    [InlineData("Remix Of My Heart", TrackVersion.Original)]
    public void Detect_SongTitle_ReturnsVersion(string title, TrackVersion expected) =>
        VersionDetector.Detect(title).ShouldBe(expected);

    [Theory]
    [InlineData("Official Video Live", TrackVersion.Live)]
    [InlineData("Official Video", TrackVersion.Original)]
    public void Classify_Fragment_ReturnsVersion(string fragment, TrackVersion expected) =>
        VersionDetector.Classify(fragment).ShouldBe(expected);

    [Theory]
    [InlineData(TrackVersion.SpedUp, "Sped Up")]
    [InlineData(TrackVersion.Live, "Live")]
    public void Label_Version_ReturnsDisplayName(TrackVersion version, string expected) =>
        VersionDetector.Label(version).ShouldBe(expected);
}
