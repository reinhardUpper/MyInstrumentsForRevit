using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MyInstrumentsForRevit.RebarSketch
{
    internal static class SketchImageRenderer
    {
        private const float FontSize = 55.0f;
        private const float DefaultWidthScale = 0.85f;

        public static void Render(
            SketchTemplate template,
            IReadOnlyDictionary<string, string> parameterValues,
            string outputPath)
        {
            using (var source = new Bitmap(template.ImagePath))
            using (var bitmap = CreateWorkingBitmap(source))
            using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap))
            using (Font font = CreateFont())
            using (var format = new StringFormat(StringFormat.GenericTypographic)
                   {
                       Alignment = StringAlignment.Center,
                       LineAlignment = StringAlignment.Center
                   })
            {
                graphics.DrawImageUnscaled(source, 0, 0);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                foreach (SketchParameterPlacement placement in template.Parameters)
                {
                    if (!parameterValues.TryGetValue(placement.ParameterName, out string value)
                        || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    GraphicsState state = graphics.Save();
                    graphics.TranslateTransform(placement.X, placement.Y);
                    graphics.RotateTransform(-placement.Angle);
                    graphics.ScaleTransform(GetWidthScale(value, placement.IsNarrow), 1.0f);
                    graphics.DrawString(value, font, Brushes.Black, 0, 0, format);
                    graphics.Restore(state);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? Path.GetTempPath());
                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }

        private static Bitmap CreateWorkingBitmap(Bitmap source)
        {
            var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            if (source.HorizontalResolution > 0 && source.VerticalResolution > 0)
            {
                bitmap.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            }

            return bitmap;
        }

        private static Font CreateFont()
        {
            try
            {
                return new Font("Isocpeur", FontSize, FontStyle.Regular);
            }
            catch (ArgumentException)
            {
                return new Font("Arial", FontSize, FontStyle.Regular);
            }
        }

        private static float GetWidthScale(string value, bool isNarrow)
        {
            if (!isNarrow)
            {
                return DefaultWidthScale;
            }

            if (value.Length > 10)
            {
                return 0.5f;
            }

            if (value.Length > 6)
            {
                return 0.6f;
            }

            return value.Length > 3 ? 0.7f : DefaultWidthScale;
        }
    }
}
