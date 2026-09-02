using System.Text.Json.Serialization;

namespace Wallpapier.WinClient.DTOs;

public class ManifestResponseDto
{
    [JsonPropertyName("created")]
    public List<PhotoMetadataDto> Created { get; set; } = [];

    [JsonPropertyName("deleted")]
    public List<string> Deleted { get; set; } = [];

    [JsonPropertyName("favorites_changed")]
    public List<FavoriteChangeDto> FavoritesChanged { get; set; } = [];
}

public class FavoriteChangeDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }
}