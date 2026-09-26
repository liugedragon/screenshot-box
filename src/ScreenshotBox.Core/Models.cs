namespace ScreenshotBox.Core;

public sealed class ScreenshotItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ImagePath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Tags { get; set; } = "";
    public bool IsFavorite { get; set; }
    public bool IsDeleted { get; set; }
    public string OcrStatus { get; set; } = "Pending";
    public string OcrText { get; set; } = "";
    public string OcrError { get; set; } = "";
    public string OcrModelVersion { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public int OcrGeneration { get; set; }
    public List<OcrBlock> OcrBlocks { get; set; } = [];
}

public sealed record OcrBlock(string Text, double X, double Y, double Width, double Height, double Confidence);
