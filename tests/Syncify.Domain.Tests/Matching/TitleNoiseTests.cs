using Syncify.Domain.Matching;

namespace Syncify.Domain.Tests.Matching;

public sealed class TitleNoiseTests
{
    [Theory]
    [InlineData("Bilionera (official video)", "Bilionera")]
    [InlineData("Minim doi [Official video HD]", "Minim doi")]
    [InlineData("Undeva-n Balkani (Videoclip Oficial HQ)", "Undeva-n Balkani")]
    [InlineData("Vita Bella OFFICIAL VIDEO", "Vita Bella")]
    [InlineData("Pana vara viitoare (Official Video) by Famous Production", "Pana vara viitoare")]
    [InlineData("In bucati (by KAZIBO) official video", "In bucati")]
    [InlineData("Strazile (Prod. Tata Vlad) (Videoclip)", "Strazile")]
    [InlineData("That's My Name (Ultra Music)", "That's My Name")]
    [InlineData("Rezervat (Summer Sensation 2015)", "Rezervat")]
    [InlineData("Taxi (Official Video) #uASAP", "Taxi")]
    [InlineData("CÂND ÎNGERII PLEACĂ 😈 (video oficial)", "CÂND ÎNGERII PLEACĂ")]
    [InlineData("Allegro Ventigo * official video 2018", "Allegro Ventigo")]
    [InlineData("Pacatul Meu Official Video Fara Reclame", "Pacatul Meu")]
    [InlineData("Tic-Tac Xsession Version", "Tic-Tac")]
    [InlineData("Am incredere in tine HIT", "Am incredere in tine")]
    [InlineData("Vino, Vino - HIT", "Vino, Vino")]
    [InlineData("Memories ( official video )", "Memories")]
    [InlineData("De Ce Plang Chitarele [Official Video] HD", "De Ce Plang Chitarele")]
    [InlineData("Tu [Videoclip Oficial (HD)]", "Tu")]
    public void Clean_PromotionalDecoration_IsRemoved(string input, string expected) =>
        TitleNoise.Clean(input).ShouldBe(expected);

    [Theory]
    [InlineData("Didaia (TraLaLa) (Videoclip Oficial)", "Didaia (TraLaLa)")]
    [InlineData("Indragostit (desi n-am vrut) [Official Video]", "Indragostit (desi n-am vrut)")]
    [InlineData("Sub Pielea Mea (#eroina)", "Sub Pielea Mea (#eroina)")]
    [InlineData("Tu (Remix)", "Tu (Remix)")]
    [InlineData("Ce are ea (Official Video Live)", "Ce are ea (Live)")]
    [InlineData("Hit the Road", "Hit the Road")]
    [InlineData("Video", "Video")]
    [InlineData("Official Video", "Official Video")]
    public void Clean_MeaningfulParts_AreKept(string input, string expected) =>
        TitleNoise.Clean(input).ShouldBe(expected);

    [Theory]
    [InlineData("Cola Song (feat. J Balvin)", "cola song")]
    [InlineData("Tu - Remix", "tu")]
    [InlineData("Bună, ce mai zici - Radio Edit", "buna ce mai zici")]
    [InlineData("Strazile feat. Mario V", "strazile")]
    [InlineData("Mamma Mia (He's Italiano)", "mamma mia")]
    [InlineData("(Intro)", "intro")]
    public void CoreTitle_DecoratedTitle_ReturnsBareNormalizedName(string input, string expected) =>
        TitleNoise.CoreTitle(input).ShouldBe(expected);

    [Fact]
    public void RemoveSymbols_Emojis_AreDroppedAndLettersKept() =>
        TitleNoise.RemoveSymbols("LeLe 👸🏻🎷 Lăutarul ❤️!").ShouldBe("LeLe  Lăutarul !");
}
