using Microsoft.Extensions.Options;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Drawing.Text;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using IOPath = System.IO.Path;

namespace nuel;

public class CaptchaRenderer : ICaptchaRenderer
{
	private readonly CaptchaOptions _options;
	private readonly Font _font;

	public CaptchaRenderer(IOptions<CaptchaOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_options = options.Value;
		_options.Validate();

		_font = LoadAndCacheFont(_options);
	}

	public CaptchaRenderer(CaptchaOptions options)
		: this(Options.Create(options))
	{
	}

	private static Font LoadAndCacheFont(CaptchaOptions options)
	{
		if (options.FontFamily.HasValue)
		{
			return options.FontFamily.Value.CreateFont(options.FontSize, FontStyle.Regular);
		}

		if (!string.IsNullOrWhiteSpace(options.FontPath))
		{
			string fullPath = IOPath.IsPathRooted(options.FontPath)
				? options.FontPath
				: IOPath.Combine(AppContext.BaseDirectory, options.FontPath);

			if (!File.Exists(fullPath))
			{
				if (File.Exists(options.FontPath))
				{
					fullPath = IOPath.GetFullPath(options.FontPath);
				}
				else
				{
					throw new FileNotFoundException($"Captcha font file not found: '{options.FontPath}' (checked '{fullPath}').", options.FontPath);
				}
			}

			var collection = new FontCollection();
			var family = collection.Add(fullPath);
			return family.CreateFont(options.FontSize, FontStyle.Regular);
		}

		if (SystemFonts.TryGet("Arial", out var arialFamily))
		{
			return arialFamily.CreateFont(options.FontSize, FontStyle.Regular);
		}

		var fallbackFamily = SystemFonts.Families.FirstOrDefault();
		if (fallbackFamily.Name != null)
		{
			return fallbackFamily.CreateFont(options.FontSize, FontStyle.Regular);
		}

		throw new InvalidOperationException("No font specified in CaptchaOptions.FontPath, and no system fonts could be found.");
	}

	public byte[] RenderBytes(string text)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(text);

		using var img = new Image<Rgba32>(_options.Width, _options.Height);
		var textOptions = new TextOptions(_font);

		var charSize = new FontRectangle[text.Length];
		for (int i = 0; i < charSize.Length; i++)
		{
			charSize[i] = TextMeasurer.MeasureBounds(text.AsSpan(i, 1), textOptions);
		}

		float startX = ((float)Random.Shared.NextDouble() + 1f) * _options.Width * 0.1f;
		float x = startX;
		float totalCharWidth = charSize.Sum(s => s.Width);
		float spaceSum = Math.Max(0f, _options.Width - (2f * startX) - totalCharWidth);

		img.Mutate(ctx =>
		{
			ctx.Paint(canvas =>
			{
				canvas.Clear(Brushes.Solid(_options.BackColor));

				var textBrush = Brushes.Solid(_options.ForeColor);
				for (int i = 0; i < text.Length; i++)
				{
					float maxAvailableY = Math.Max(0f, _options.Height - charSize[i].Height);
					float y = (float)Random.Shared.NextDouble() * maxAvailableY;

					var glyphs = TextBuilder.GeneratePaths(text.Substring(i, 1), textOptions)
						.Rotate((float)Random.Shared.NextDouble() * 0.6f - 0.3f)
						.Translate(x, y);

					canvas.Fill(textBrush, glyphs);

					if (i + 1 < text.Length)
					{
						float space = (float)Random.Shared.NextDouble() * spaceSum / (text.Length - i - 1f);
						spaceSum -= space;
						x += charSize[i].Width + space;
					}
				}

				int curveCount = 5;
				float strokeWidth = Math.Min(_options.Width, _options.Height) / (float)text.Length / 7f;
				var curvePen = Pens.Solid(_options.ForeColor, strokeWidth);
				for (int i = 0; i < curveCount; i++)
				{
					canvas.DrawBezier(
						curvePen,
						new PointF((float)Random.Shared.NextDouble() * _options.Width * 0.2f, _options.Height / 4f * (1f + 2f * (float)Random.Shared.NextDouble())),
						new PointF(_options.Width / 3f, (float)Random.Shared.NextDouble() * _options.Height),
						new PointF(_options.Width / 3f * 2f, (float)Random.Shared.NextDouble() * _options.Height),
						new PointF(_options.Width * (1f - (float)Random.Shared.NextDouble() * 0.2f), _options.Height / 4f * (1f + 2f * (float)Random.Shared.NextDouble())));
				}
			});
		});

		var encoder = new PngEncoder { ColorType = PngColorType.Palette };
		using var mem = new MemoryStream();
		img.SaveAsPng(mem, encoder);
		return mem.ToArray();
	}

	public string Render(string text)
	{
		byte[] bytes = RenderBytes(text);
		return "data:image/png;base64," + Convert.ToBase64String(bytes);
	}
}
