using Syncify.Domain.Matching;
using Xunit.Abstractions;

namespace Syncify.Domain.Tests.Matching;

public sealed class YouTubeTitleParserTests(ITestOutputHelper output)
{
    private const double MinimumCorpusAccuracy = 0.95;

    [Theory]
    [InlineData("Otilia - Bilionera (official video)", "Otilia", "Bilionera")]
    [InlineData("LORA – Ramas Bun | Official Video", "LORA", "Ramas Bun")]
    [InlineData("Ruby- Stinge lumina [Official video HD]", "Ruby", "Stinge lumina")]
    [InlineData("INNA - Cola Song (feat. J Balvin) | Official Music Video", "INNA|J Balvin", "Cola Song")]
    [InlineData("B.U.G. Mafia - Strazile feat. Mario V (Prod. Tata Vlad) (Videoclip)", "B.U.G. Mafia|Mario V", "Strazile")]
    [InlineData("Shaggy - I Need Your Love (Official Video) ft. Mohombi, Faydee", "Shaggy|Mohombi|Faydee", "I Need Your Love")]
    [InlineData("Dj Sava feat. Raluka - Aroma (special guest Connect R) Official Video", "Dj Sava|Raluka|Connect R", "Aroma")]
    [InlineData("Grasu XXL | Maximilian | Spike - Didaia (TraLaLa) (Videoclip Oficial)", "Grasu XXL|Maximilian|Spike", "Didaia (TraLaLa)")]
    [InlineData("Larisa - Nu-mi pasa | Oficial Video | 2023", "Larisa", "Nu-mi pasa")]
    [InlineData("ADDA - Lupii (Nu plange, ADDA - 2016)", "ADDA", "Lupii")]
    [InlineData("EMILIA x FLORIN SALAM x COSTI • TURBULENCE | Турбуленция", "EMILIA|FLORIN SALAM|COSTI", "TURBULENCE")]
    [InlineData("Narcotic Sound & Christian D \"Dança Bonito\" (Official Video)", "Narcotic Sound|Christian D", "Dança Bonito")]
    [InlineData("MARKO GLASS - \"SAVANA\"", "MARKO GLASS", "SAVANA")]
    [InlineData("Copilul de Aur ❌ Laura Vass 💕 Cine e inima mea 💕 Original Video Hit", "Copilul de Aur|Laura Vass", "Cine e inima mea")]
    [InlineData("Tzanca Uraganu ❌ Florin Salam - Daca ai femeie frumoasa", "Tzanca Uraganu|Florin Salam", "Daca ai femeie frumoasa")]
    [InlineData("Puya - Striga (feat. Inna) | Videoclip Oficial", "Puya|Inna", "Striga")]
    [InlineData("INNA - Spre Mare (feat. INNA)", "INNA", "Spre Mare")]
    [InlineData("Smiley - Tu feat. Alex Velea (Live)", "Smiley|Alex Velea", "Tu (Live)")]
    [InlineData("Smiley - Tu feat. Alex Velea - Remix", "Smiley|Alex Velea", "Tu - Remix")]
    public void Parse_ArtistDashSongTitle_ReturnsArtistsAndSong(string title, string artists, string song)
    {
        var parsed = YouTubeTitleParser.Parse(title);

        parsed.Artists.ShouldBe(artists.Split('|'));
        parsed.Song.ShouldBe(song);
    }

    [Fact]
    public void Parse_TopicChannel_TakesArtistFromChannel()
    {
        var parsed = YouTubeTitleParser.Parse("Buzele Tale, Doua Petale", "Copilul De Aur - Topic");

        parsed.Artists.ShouldBe(["Copilul De Aur"]);
        parsed.Song.ShouldBe("Buzele Tale, Doua Petale");
    }

    [Fact]
    public void Parse_TopicChannelAndDashInTitle_StillTrustsChannel()
    {
        var parsed = YouTubeTitleParser.Parse("Tu - Remix", "JO - Topic");

        parsed.Artists.ShouldBe(["JO"]);
        parsed.Song.ShouldBe("Tu - Remix");
    }

    [Theory]
    [InlineData("Da vina pe VOLTAJ (Official Video)", "Cat Music", "Da vina pe VOLTAJ")]
    [InlineData("Weekend", null, "Weekend")]
    [InlineData("", null, "")]
    public void Parse_NoSeparator_ReturnsNoArtists(string title, string? channel, string song)
    {
        var parsed = YouTubeTitleParser.Parse(title, channel);

        parsed.Artists.ShouldBeEmpty();
        parsed.Song.ShouldBe(song);
    }

    [Fact]
    public void ToTrack_ParsedTitle_CarriesArtistsSongAndDuration()
    {
        var duration = TimeSpan.FromSeconds(201);

        var track = YouTubeTitleParser.Parse("Otilia - Bilionera (official video)").ToTrack(duration);

        track.ShouldBe(new TrackInfo("Bilionera", track.Artists, duration));
        track.Artists.ShouldBe(["Otilia"]);
    }

    [Fact]
    public void Parse_RealPlaylistCorpus_MeetsAccuracyTarget()
    {
        var rows = LoadCorpus();
        var failures = new List<string>();

        foreach (var row in rows)
        {
            var parsed = YouTubeTitleParser.Parse(row.Title, row.Channel);
            var artistsMatch = Normalized(parsed.Artists).SetEquals(Normalized(row.Artists));
            var songMatches = TextNormalizer.Normalize(parsed.Song) == TextNormalizer.Normalize(row.Song);

            if (!artistsMatch || !songMatches)
            {
                failures.Add($"{row.Title}\n    got:      [{string.Join("; ", parsed.Artists)}] {parsed.Song}\n    expected: [{string.Join("; ", row.Artists)}] {row.Song}");
            }
        }

        var accuracy = 1 - (double)failures.Count / rows.Count;
        output.WriteLine($"Parsed {rows.Count - failures.Count}/{rows.Count} titles correctly ({accuracy:P1}).");
        failures.ForEach(output.WriteLine);

        accuracy.ShouldBeGreaterThanOrEqualTo(MinimumCorpusAccuracy, string.Join('\n', failures));
    }

    private static HashSet<string> Normalized(IEnumerable<string> artists) => artists.Select(TextNormalizer.Normalize).ToHashSet();

    private static List<CorpusRow> LoadCorpus() =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Matching", "Fixtures", "youtube-titles.tsv"))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(columns => new CorpusRow(columns[0], columns[1], columns[2].Split(';'), columns[3]))
            .ToList();

    private sealed record CorpusRow(string Title, string Channel, string[] Artists, string Song);
}
