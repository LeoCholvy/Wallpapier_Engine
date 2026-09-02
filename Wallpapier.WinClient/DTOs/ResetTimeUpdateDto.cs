using System.Text.Json.Serialization;

namespace Wallpapier.WinClient.DTOs;

public class ResetTimeUpdateDto
{
    [JsonPropertyName("reset_time")]
    public required string ResetTime { get; set; }
}