using Microsoft.Data.SqlClient;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Linq;

namespace nuell
{
    public class Captcha
    {
        public string Src { get; private set; }
        public long Code { get; private set; }
        public static int Digits { get; set; } = 5;
        public static Color ForeColor { get; set; } = Color.FromRgb(60, 60, 60);
        public static Color BackColor { get; set; } = Color.White;
        public static int Width { get; set; } = 200;
        public static int Height { get; set; } = 60;
        public static string Path { get; set; }
        public static string FontPath { get; set; }
        public static string RootPath { get; set; }
        public static string ConnString { get; set; }

        public Captcha()
        {
            CleanUp();

            var rnd = new Random();
            int randNumber = rnd.Next((int)Math.Pow(10, Digits));

            using var img = new Image<Rgba32>(Width, Height);
            var font = new FontCollection()
                .Install(System.IO.Path.Combine(RootPath, FontPath))
                .CreateFont(30, FontStyle.Regular);

            string txt = randNumber.ToString("D" + Digits);
            var renderOptions = new RendererOptions(font);

            var charSize = new FontRectangle[txt.Length];
            for (int i = 0; i < charSize.Length; i++)
                charSize[i] = TextMeasurer.Measure(txt.Substring(i, 1), renderOptions);

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
                        TextBuilder.GenerateGlyphs(txt.Substring(i, 1), renderOptions)
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

            using (var cnnct = new SqlConnection(ConnString))
            {
                using var cmnd = new SqlCommand($@"insert into CaptchaCodes (Id, Captcha, CreationDate) 
                    values (@id, @captcha, getdate())", cnnct);
                cmnd.Parameters.Add(new SqlParameter("@id", Code));
                cmnd.Parameters.Add(new SqlParameter("@captcha", randNumber));
                cmnd.Parameters.Add(new SqlParameter("@date", DateTime.Now));
                cnnct.Open();
                cmnd.ExecuteNonQuery();
            }

            img.Save(System.IO.Path.Combine(RootPath, Path, $"{Code}.png"));
            Src = System.IO.Path.Combine("/", Path, $"{Code}.png").Replace('\\', '/');
        }

        public static bool IsValid(string userInput, string captchaCode)
        {
            CleanUp();

            long.TryParse(captchaCode, out long code);
            int.TryParse(userInput, out int input);

            string imgFile = System.IO.Path.Combine(RootPath, Path, $"{code}.png");
            if (!File.Exists(imgFile))
                return false;
            File.Delete(imgFile);

            using var cnnct = new SqlConnection(ConnString);
            using var cmnd = new SqlCommand($"select 1 from CaptchaCodes where Id={code} and Captcha={input}", cnnct);
            cnnct.Open();
            return Convert.ToBoolean(cmnd.ExecuteScalar());
        }

        private static void CleanUp()
        {
            DateTime expiry = DateTime.Now.AddMinutes(-10);

            foreach (string f in Directory.GetFiles(System.IO.Path.Combine(RootPath, Path), "*.png"))
                if (File.GetCreationTime(f) < expiry)
                    File.Delete(f);

            string cmdTxt =
                @"if not exists(select 1 from INFORMATION_SCHEMA.TABLES where TABLE_NAME = 'CaptchaCodes')
                    create table CaptchaCodes(
                        Id bigint not null,
                        Captcha int not null,
                        CreationDate datetime2 not null,
                        constraint PK_CaptchaCodes primary key(Id));
                delete from CaptchaCodes where CreationDate < @date;";
            using var cnnct = new SqlConnection(ConnString);
            using var cmnd = new SqlCommand(cmdTxt, cnnct);
            cmnd.Parameters.Add(new SqlParameter("@date", expiry));
            cnnct.Open();
            cmnd.ExecuteNonQuery();
        }
    }
}
