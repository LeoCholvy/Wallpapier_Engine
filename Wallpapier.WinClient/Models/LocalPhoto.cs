namespace Wallpapier.WinClient.Models;

public class LocalPhoto
{
    public required string Id { get; set; }
    public required string Filepath { get; set; }
    public bool IsFavorite { get; set; }
    public bool HasBeenShown { get; set; }
    public DateTime? CaptureDate { get; set; }
    public string? Location { get; set; }
}