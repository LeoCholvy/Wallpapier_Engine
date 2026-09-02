using System.Text.Json.Serialization;

namespace Wallpapier.WinClient.DTOs;

public class FavoriteUpdateDto
{
    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }
}