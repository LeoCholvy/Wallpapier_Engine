using System.Text.Json.Serialization;

namespace Wallpapier.WinClient.DTOs;

public class PhotoMetadataDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("filename")]
    public required string Filename { get; set; }

    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("capture_date")]
    public DateTime? CaptureDate { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("upload_date")]
    public DateTime UploadDate { get; set; }
}