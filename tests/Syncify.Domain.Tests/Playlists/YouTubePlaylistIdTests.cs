using Syncify.Domain.Playlists;

namespace Syncify.Domain.Tests.Playlists;

public sealed class YouTubePlaylistIdTests
{
    [Theory]
    [InlineData("PLWJCFxS01ji1GMkShOn-snOVu_3RaWWlm", "PLWJCFxS01ji1GMkShOn-snOVu_3RaWWlm")]
    [InlineData("PLOp8kZQdZGQs", "PLOp8kZQdZGQs")]
    [InlineData("  PLFw-8m7e1Pig \n", "PLFw-8m7e1Pig")]
    [InlineData("OLAK5uy_kVbbJ0dZsXvRk3cR4Zzq8qWfOdbXyZq1A", "OLAK5uy_kVbbJ0dZsXvRk3cR4Zzq8qWfOdbXyZq1A")]
    public void TryParse_RawId_ReturnsId(string input, string expected)
    {
        YouTubePlaylistId.TryParse(input, out var id).ShouldBeTrue();
        id.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("https://youtube.com/playlist?list=PLOp8kZQdZGQs&si=AbC123xyz")]
    [InlineData("https://m.youtube.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("https://music.youtube.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("https://www.youtube.com/watch?v=0An1_kHGhnk&list=PLOp8kZQdZGQs&index=3")]
    [InlineData("https://youtu.be/0An1_kHGhnk?list=PLOp8kZQdZGQs")]
    [InlineData("http://www.YouTube.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("www.youtube.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("youtube.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData(" https://www.youtube.com/playlist?list=PLOp8kZQdZGQs ")]
    public void TryParse_Link_ReturnsListParameter(string input)
    {
        YouTubePlaylistId.TryParse(input, out var id).ShouldBeTrue();
        id.Value.ShouldBe("PLOp8kZQdZGQs");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PLshort")]
    [InlineData("not a playlist id")]
    [InlineData("PLOp8kZQdZGQs!")]
    [InlineData("https://www.youtube.com/watch?v=0An1_kHGhnk")]
    [InlineData("https://www.youtube.com/playlist?list=")]
    [InlineData("https://www.youtube.com/playlist?list=PL%20Op8kZQdZGQs")]
    [InlineData("https://example.com/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("https://youtube.com.evil.example/playlist?list=PLOp8kZQdZGQs")]
    [InlineData("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M")]
    public void TryParse_NotAPlaylist_ReturnsFalse(string? input)
    {
        YouTubePlaylistId.TryParse(input, out var id).ShouldBeFalse();
        id.ShouldBeNull();
    }

    [Fact]
    public void Equals_SameValue_AreEqual()
    {
        YouTubePlaylistId.TryParse("PLOp8kZQdZGQs", out var raw);
        YouTubePlaylistId.TryParse("https://www.youtube.com/playlist?list=PLOp8kZQdZGQs", out var fromLink);

        raw.ShouldBe(fromLink);
        raw!.ToString().ShouldBe("PLOp8kZQdZGQs");
    }
}
