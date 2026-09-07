using System.Data;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Data.SqlClient;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace nuell;
public class Captcha
{
    public static int Digits { get; set; } = 5;
    public static Color ForeColor { get; set; } = Color.FromRgb(60, 60, 60);
    public static Color BackColor { get; set; } = Color.White;
    public static int Width { get; set; } = 200;
    public static int Height { get; set; } = 60;

    public static string FontPath { get; set; }
    public static string RootPath { get; set; }
    public static string ConnectionString { get; set; }

    public static TimeSpan Expiration { get; set; } = TimeSpan.FromMinutes(5);
    public static TimeSpan CleanUpInterval { get; set; } = TimeSpan.FromMinutes(10);

    private static long _lastCleanUpTicks = 0;
    private static readonly Lock _initLock = new();
    private static volatile bool _isInitialized = false;

    public string Src { get; private set; }
    public long Code { get; private set; }

    private static void EnsureDatabaseInitialized()
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            try
            {
                InitializeDatabase();
            }
            catch
            {
                // Ostrich strategy: Ignore concurrency collisions or pre-existing table errors
            }

            _isInitialized = true;
        }
    }

    private static void InitializeDatabase()
    {
        if (string.IsNullOrEmpty(ConnectionString))
            throw new InvalidOperationException("ConnectionString is not configured.");

        const string ddl = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CaptchaCodes' AND schema_id = SCHEMA_ID('dbo'))
            BEGIN
                CREATE TABLE dbo.CaptchaCodes (
                    Id bigint NOT NULL,
                    Captcha int NOT NULL,
                    CreationDate datetime2 NOT NULL,
                    CONSTRAINT PK_CaptchaCodes PRIMARY KEY CLUSTERED (Id)
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CaptchaCodes_CreationDate' AND object_id = OBJECT_ID('dbo.CaptchaCodes'))
            BEGIN
                CREATE NONCLUSTERED INDEX IX_CaptchaCodes_CreationDate ON dbo.CaptchaCodes (CreationDate);
            END;";

        using var cnnct = new SqlConnection(ConnectionString);
        using var cmnd = new SqlCommand(ddl, cnnct);
        cnnct.Open();
        cmnd.ExecuteNonQuery();
    }

    public Captcha()
    {
        EnsureDatabaseInitialized();
        TryCleanUp();

        int randNumber = RandomNumberGenerator.GetInt32((int)Math.Pow(10, Digits));

        using var img = new Image<Rgba32>(Width, Height);
        var font = new FontCollection()
            .Add(System.IO.Path.Combine(RootPath, FontPath))
            .CreateFont(30, FontStyle.Regular);

        string txt = randNumber.ToString("D" + Digits);
        var textOptions = new TextOptions(font);

        var charSize = new FontRectangle[txt.Length];
        for (int i = 0; i < charSize.Length; i++)
            charSize[i] = TextMeasurer.MeasureSize(txt.AsSpan(i, 1), textOptions);

        var rnd = new Random();
        float x = ((float)rnd.NextDouble() + 1f) * Width * 0.1f, y;
        float spaceSum = Width - 2 * x - charSize.Sum(s => s.Width);
        float space;

        img.Mutate(ctx =>
        {
            ctx.Fill(BackColor);

            for (int i = 0; i < txt.Length; i++)
            {
                y = (float)rnd.NextDouble() * (Height - charSize[i].Height);

                ctx.Fill(ForeColor,
                    TextBuilder.GenerateGlyphs(txt.Substring(i, 1), textOptions)
                    .Rotate((float)rnd.NextDouble() * .6f - .3f) //almost between -15 and 15
                    .Translate(x, y));

                if (i + 1 < txt.Length)
                {
                    space = (float)rnd.NextDouble() * spaceSum / (Digits - i - 1f);
                    spaceSum -= space;
                    x += charSize[i].Width + space;
                }
            }

            for (int i = 0; i < 5; i++)
                ctx.DrawBeziers(ForeColor, Math.Min(Width, Height) / Digits / 7f,
                    new PointF((float)rnd.NextDouble() * Width * 0.2f, Height / 4f * (1f + 2f * (float)rnd.NextDouble())),
                    new PointF(Width / 3f, (float)rnd.NextDouble() * Height),
                    new PointF(Width / 3f * 2f, (float)rnd.NextDouble() * Height),
                    new PointF(Width * (1f - (float)rnd.NextDouble() * 0.2f), Height / 4f * (1f + 2f * (float)rnd.NextDouble())));
        });

        Code = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 4);

        using (var cnnct = new SqlConnection(ConnectionString))
        {
            using var cmnd = new SqlCommand(@"insert into dbo.CaptchaCodes (Id, Captcha, CreationDate) 
                    values (@id, @captcha, @date)", cnnct);
            cmnd.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = Code });
            cmnd.Parameters.Add(new SqlParameter("@captcha", SqlDbType.Int) { Value = randNumber });
            cmnd.Parameters.Add(new SqlParameter("@date", SqlDbType.DateTime2) { Value = DateTime.UtcNow });
            cnnct.Open();
            cmnd.ExecuteNonQuery();
        }

        var encoder = new PngEncoder { ColorType = PngColorType.Palette };
        using var mem = new MemoryStream();
        img.SaveAsPng(mem, encoder);
        Src = "data:image/png;base64," + Convert.ToBase64String(mem.ToArray());
    }

    public static bool IsValid(string userInput, string captchaCode)
    {
        if (!long.TryParse(captchaCode, out long code) || !int.TryParse(userInput, out int input))
            return false;

        using var cnnct = new SqlConnection(ConnectionString);
        using var cmnd = new SqlCommand(
            "delete from dbo.CaptchaCodes where Id = @id and Captcha = @captcha and CreationDate >= @date", cnnct);
        cmnd.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = code });
        cmnd.Parameters.Add(new SqlParameter("@captcha", SqlDbType.Int) { Value = input });
        cmnd.Parameters.Add(new SqlParameter("@date", SqlDbType.DateTime2) { Value = DateTime.UtcNow.Subtract(Expiration) });
        cnnct.Open();
        return cmnd.ExecuteNonQuery() == 1;
    }

    private static void TryCleanUp()
    {
        long now = Environment.TickCount64;
        long last = Volatile.Read(ref _lastCleanUpTicks);

        if (now - last < CleanUpInterval.TotalMilliseconds)
            return;

        if (Interlocked.CompareExchange(ref _lastCleanUpTicks, now, last) == last)
        {
            try
            {
                CleanUp();
            }
            catch
            {
                // Suppress exceptions in opportunistic cleanup to avoid breaking captcha generation
            }
        }
    }

    public static int CleanUp(TimeSpan? olderThan = null)
    {
        if (string.IsNullOrEmpty(ConnectionString))
            throw new InvalidOperationException("ConnectionString is not configured.");

        DateTime cutoffDate = DateTime.UtcNow.Subtract(olderThan ?? Expiration);
        using var cnnct = new SqlConnection(ConnectionString);
        using var cmnd = new SqlCommand("delete from dbo.CaptchaCodes where CreationDate < @date;", cnnct);
        cmnd.Parameters.Add(new SqlParameter("@date", SqlDbType.DateTime2) { Value = cutoffDate });
        cnnct.Open();
        return cmnd.ExecuteNonQuery();
    }
}