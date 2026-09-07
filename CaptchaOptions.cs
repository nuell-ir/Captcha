using SixLabors.Fonts;
using SixLabors.ImageSharp;

namespace nuel;

public class CaptchaOptions
{
    public int Digits { get; set; } = 5;
    public Color ForeColor { get; set; } = Color.FromRgb(60, 60, 60);
    public Color BackColor { get; set; } = Color.White;
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 60;
    public float FontSize { get; set; } = 30f;

    public string? FontPath { get; set; }
    public FontFamily? FontFamily { get; set; }
    public string? ConnectionString { get; set; }

    public TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CleanUpInterval { get; set; } = TimeSpan.FromMinutes(10);
    public bool AutoCreateDatabaseTable { get; set; } = true;

    public void Validate()
    {
        if (Digits is < 1 or > 9)
            throw new ArgumentOutOfRangeException(nameof(Digits), Digits, "Digits must be between 1 and 9.");

        if (Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), Width, "Width must be greater than zero.");

        if (Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Height), Height, "Height must be greater than zero.");

        if (FontSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(FontSize), FontSize, "FontSize must be greater than zero.");

        if (Expiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Expiration), Expiration, "Expiration must be greater than zero.");

        if (CleanUpInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(CleanUpInterval), CleanUpInterval, "CleanUpInterval must be greater than zero.");
    }
}

