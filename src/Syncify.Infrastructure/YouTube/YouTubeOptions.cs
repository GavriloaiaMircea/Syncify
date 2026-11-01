using System.ComponentModel.DataAnnotations;

namespace Syncify.Infrastructure.YouTube;

public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";

    [Required]
    public string ApiKey { get; set; } = "";

    [Required]
    public Uri BaseAddress { get; set; } = new("https://www.googleapis.com/youtube/v3/");
}
