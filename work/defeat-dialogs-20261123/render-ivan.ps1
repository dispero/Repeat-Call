$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Drawing;

public static class DefeatArt
{
    public static void Overlay(Bitmap canvas, Bitmap art)
    {
        const int left = 178, right = 1175, top = 211, bottom = 748;
        const int feather = 30;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                int edge = Math.Min(Math.Min(x-left, right-1-x), bottom-1-y);
                double alpha = Math.Min(1, Math.Min(edge / (double)feather, (y-top) / 8.0));
                var source = art.GetPixel(x, y);
                if (alpha >= 1)
                {
                    canvas.SetPixel(x, y, source);
                    continue;
                }
                var target = canvas.GetPixel(x, y);
                canvas.SetPixel(x, y, Color.FromArgb(
                    Blend(source.R, target.R, alpha),
                    Blend(source.G, target.G, alpha),
                    Blend(source.B, target.B, alpha)));
            }
        }
    }

    private static int Blend(byte source, byte target, double alpha)
    {
        return (int)Math.Round(source * alpha + target * (1 - alpha));
    }
}
'@ -ReferencedAssemblies ([Drawing.Bitmap].Assembly.Location), ([Drawing.Color].Assembly.Location), ([Drawing.Point].Assembly.Location)
$folder = $PSScriptRoot
$raw = [Convert]::FromBase64String([IO.File]::ReadAllText((Join-Path $folder 'ivan-game-sprite-art.b64')))
$sourceStream = [IO.MemoryStream]::new($raw)
$art = [Drawing.Bitmap]::new($sourceStream)
$canvas = [Drawing.Bitmap]::new((Join-Path $folder 'template.png'))
if ($art.Size -ne $canvas.Size) { throw 'Source and template sizes differ.' }
[DefeatArt]::Overlay($canvas, $art)
$text = [Drawing.Bitmap]::new((Join-Path $folder 'text-ivan.png'))
$graphics = [Drawing.Graphics]::FromImage($canvas)
$graphics.DrawImageUnscaled($text, 0, 702)
$graphics.Dispose()
$canvas.Save((Join-Path $folder 'defeat-03-ivan.png'), [Drawing.Imaging.ImageFormat]::Png)
$text.Dispose()
$canvas.Dispose()
$art.Dispose()
$sourceStream.Dispose()
