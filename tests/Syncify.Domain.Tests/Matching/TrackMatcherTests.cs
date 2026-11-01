using Syncify.Domain.Matching;

namespace Syncify.Domain.Tests.Matching;

public sealed class TrackMatcherTests
{
    [Fact]
    public void Match_NoCandidates_ReturnsNotFound()
    {
        var result = TrackMatcher.Match(Track("Bilionera", "Otilia"), []);

        result.Status.ShouldBe(MatchStatus.NotFound);
        result.Candidate.ShouldBeNull();
        result.Reasons.ShouldBe([MatchReason.NoCandidates]);
    }

    [Fact]
    public void Match_ExactTitleAndArtist_ReturnsMatched()
    {
        var result = TrackMatcher.Match(Track("Bilionera", "Otilia"), [Candidate("1", "Bilionera", "Otilia")]);

        result.Status.ShouldBe(MatchStatus.Matched);
        result.Candidate!.Id.ShouldBe("1");
        result.Confidence.ShouldBe(1);
        result.Reasons.ShouldBe([MatchReason.ArtistMatch, MatchReason.TitleMatch]);
    }

    [Fact]
    public void Match_SeveralSongsBySameArtist_PicksTheRightOne()
    {
        var result = TrackMatcher.Match(
            Track("Mi-e dor de noi", "DJ Project", "Giulia"),
            [Candidate("1", "Nu", "DJ Project"), Candidate("2", "Mi-e dor de noi", "DJ Project"), Candidate("3", "Slabiciuni", "DJ Project")]);

        result.Status.ShouldBe(MatchStatus.Matched);
        result.Candidate!.Id.ShouldBe("2");
    }

    [Fact]
    public void Match_DiacriticsAndFeaturedCreditInCandidate_ReturnsMatched()
    {
        var result = TrackMatcher.Match(
            Track("Buna, ce mai zici?", "Ruby", "Dorian Popa"),
            [Candidate("1", "Bună, ce mai zici (feat. Dorian Popa)", "Ruby")]);

        result.Status.ShouldBe(MatchStatus.Matched);
    }

    [Fact]
    public void Match_SameTitleByAnotherArtist_ReturnsNotFound()
    {
        var result = TrackMatcher.Match(Track("Tu", "JO"), [Candidate("1", "Tu", "Lidia Buble")]);

        result.Status.ShouldBe(MatchStatus.NotFound);
        result.Candidate!.Id.ShouldBe("1");
        result.Reasons.ShouldContain(MatchReason.ArtistMismatch);
    }

    [Fact]
    public void Match_SimilarButDifferentArtist_IsNeverMatched()
    {
        var duration = TimeSpan.FromSeconds(200);

        var result = TrackMatcher.Match(
            new TrackInfo("Tu", ["Florin Salam"], duration),
            [Candidate("1", "Tu", ["Florin Cercel"], duration)]);

        result.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Reasons.ShouldContain(MatchReason.ArtistMismatch);
    }

    [Fact]
    public void Match_DifferentTitle_ReturnsNotFound()
    {
        var result = TrackMatcher.Match(Track("Dalinda", "Alex Mica"), [Candidate("1", "Panama", "Alex Mica")]);

        result.Status.ShouldBe(MatchStatus.NotFound);
        result.Reasons.ShouldContain(MatchReason.TitleMismatch);
    }

    [Fact]
    public void Match_OriginalAndRemixAvailable_PrefersOriginal()
    {
        var result = TrackMatcher.Match(
            Track("Dale", "Delia"),
            [Candidate("remix", "Dale - Remix", "Delia"), Candidate("original", "Dale", "Delia")]);

        result.Status.ShouldBe(MatchStatus.Matched);
        result.Candidate!.Id.ShouldBe("original");
    }

    [Fact]
    public void Match_OnlyAnotherVersionAvailable_ReturnsAmbiguous()
    {
        var result = TrackMatcher.Match(Track("Dale", "Delia"), [Candidate("1", "Dale (Live)", "Delia")]);

        result.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Reasons.ShouldContain(MatchReason.VersionMismatch);
    }

    [Fact]
    public void Match_SourceIsLiveAndCandidateIsLive_ReturnsMatched()
    {
        var result = TrackMatcher.Match(Track("Ce are ea (Live)", "Delia"), [Candidate("1", "Ce are ea - Live", "Delia")]);

        result.Status.ShouldBe(MatchStatus.Matched);
    }

    [Fact]
    public void Match_UnknownArtistNamedInTitle_ReturnsMatched()
    {
        var result = TrackMatcher.Match(
            Track("Theo Rose x Valentin Dinu Pacatul Meu"),
            [Candidate("1", "Pacatul Meu", "Theo Rose", "Valentin Dinu")]);

        result.Status.ShouldBe(MatchStatus.Matched);
        result.Reasons.ShouldContain(MatchReason.ArtistFoundInTitle);
    }

    [Fact]
    public void Match_LiveSourceWithArtistInTitle_PrefersLiveCandidate()
    {
        var result = TrackMatcher.Match(
            Track("Theo Rose Pacatul Meu (Live)"),
            [Candidate("studio", "Pacatul Meu", "Theo Rose"), Candidate("live", "Pacatul Meu (Live)", "Theo Rose")]);

        result.Candidate!.Id.ShouldBe("live");
        result.Reasons.ShouldNotContain(MatchReason.VersionMismatch);
    }

    [Fact]
    public void Match_JoinerLeftAtEndOfTitle_IsIgnored()
    {
        var result = TrackMatcher.Match(Track("Pacatul Meu feat. Theo Rose"), [Candidate("1", "Pacatul Meu", "Theo Rose")]);

        result.Status.ShouldBe(MatchStatus.Matched);
    }

    [Theory]
    [InlineData("Mi-e dor de tine", "Tine")]
    [InlineData("Da vina pe VOLTAJ", "Voltaj")]
    public void Match_ArtistNameIsPartOfSongTitle_DoesNotCountAsArtistCredit(string title, string artist)
    {
        var result = TrackMatcher.Match(Track(title), [Candidate("1", title, artist)]);

        result.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Reasons.ShouldContain(MatchReason.ArtistUnknown);
    }

    [Fact]
    public void Match_UnknownArtistAndPerfectTitle_ReturnsAmbiguous()
    {
        var result = TrackMatcher.Match(Track("Weekend"), [Candidate("1", "Weekend", "The Motans")]);

        result.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Confidence.ShouldBeLessThan(TrackMatcher.MatchThreshold);
        result.Reasons.ShouldContain(MatchReason.ArtistUnknown);
    }

    [Fact]
    public void Match_TwoDifferentRecordingsScoreTheSame_ReturnsAmbiguous()
    {
        var result = TrackMatcher.Match(
            Track("Tu", "JO"),
            [Candidate("1", "Tu", ["JO"], TimeSpan.FromMinutes(3)), Candidate("2", "Tu", ["JO"], TimeSpan.FromMinutes(4))]);

        result.Status.ShouldBe(MatchStatus.Ambiguous);
        result.Reasons.ShouldContain(MatchReason.CloseRunnerUp);
    }

    [Fact]
    public void Match_SameRecordingOnSingleAndAlbum_ReturnsMatched()
    {
        var duration = TimeSpan.FromSeconds(187);

        var result = TrackMatcher.Match(
            Track("Tu", "JO"),
            [Candidate("single", "Tu", ["JO"], duration), Candidate("album", "Tu", ["JO"], duration + TimeSpan.FromSeconds(1))]);

        result.Status.ShouldBe(MatchStatus.Matched);
        result.Reasons.ShouldNotContain(MatchReason.CloseRunnerUp);
    }

    [Theory]
    [InlineData(200, MatchReason.DurationMatch)]
    [InlineData(208, MatchReason.DurationMatch)]
    [InlineData(260, MatchReason.DurationMismatch)]
    public void Match_KnownDurations_AddDurationReason(int candidateSeconds, MatchReason expected)
    {
        var source = new TrackInfo("Tu", ["JO"], TimeSpan.FromSeconds(200));

        var result = TrackMatcher.Match(source, [Candidate("1", "Tu", ["JO"], TimeSpan.FromSeconds(candidateSeconds))]);

        result.Reasons.ShouldContain(expected);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(215)]
    [InlineData(290)]
    public void Match_DurationFurtherOff_LowersConfidence(int candidateSeconds)
    {
        var source = new TrackInfo("Tu", ["JO"], TimeSpan.FromSeconds(200));
        var exact = TrackMatcher.Match(source, [Candidate("1", "Tu", ["JO"], TimeSpan.FromSeconds(200))]);

        var result = TrackMatcher.Match(source, [Candidate("1", "Tu", ["JO"], TimeSpan.FromSeconds(candidateSeconds))]);

        result.Confidence.ShouldBeLessThanOrEqualTo(exact.Confidence);
        if (candidateSeconds != 200)
        {
            result.Confidence.ShouldBeLessThan(exact.Confidence);
        }
    }

    [Fact]
    public void Match_ParsedYouTubeTitle_MatchesSpotifyStyleCandidate()
    {
        var source = YouTubeTitleParser.Parse("INNA - Cola Song (feat. J Balvin) | Official Music Video").ToTrack(TimeSpan.FromSeconds(215));

        var result = TrackMatcher.Match(
            source,
            [Candidate("1", "Cola Song (feat. J Balvin)", ["INNA", "J Balvin"], TimeSpan.FromSeconds(198)),
             Candidate("2", "Caliente", ["INNA"], TimeSpan.FromSeconds(190))]);

        result.Status.ShouldBe(MatchStatus.Matched);
        result.Candidate!.Id.ShouldBe("1");
    }

    private static TrackInfo Track(string title, params string[] artists) => new(title, artists);

    private static MatchCandidate Candidate(string id, string title, params string[] artists) => new(id, new TrackInfo(title, artists));

    private static MatchCandidate Candidate(string id, string title, string[] artists, TimeSpan duration) =>
        new(id, new TrackInfo(title, artists, duration));
}
