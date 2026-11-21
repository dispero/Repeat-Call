param([Parameter(Mandatory = $true)][string]$ManifestPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class LaboratorySpriteExport
{
    public static void Export(string source, string destination, int size, bool fitHeight)
    {
        using (var input = new Bitmap(source))
        {
            int minX = input.Width, minY = input.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < input.Height; y++)
            for (int x = 0; x < input.Width; x++)
            {
                if (input.GetPixel(x, y).A < 96) continue;
                minX = Math.Min(x, minX);
                minY = Math.Min(y, minY);
                maxX = Math.Max(x, maxX);
                maxY = Math.Max(y, maxY);
            }

            if (maxX < minX || maxY < minY || (minX == 0 && minY == 0 &&
                maxX == input.Width - 1 && maxY == input.Height - 1))
                throw new InvalidOperationException("Invalid alpha bounds: " + source);

            int sourceWidth = maxX - minX + 1, sourceHeight = maxY - minY + 1;
            double scale = size / (double)(fitHeight ? sourceHeight : sourceWidth);
            int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            using (var sprite = new Bitmap(width + 2, height + 2, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(sprite))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    graphics.DrawImage(input, new Rectangle(1, 1, width, height),
                        minX, minY, sourceWidth, sourceHeight, GraphicsUnit.Pixel);
                }

                sprite.Save(destination, ImageFormat.Png);
            }
        }
    }
}
'@
$outputFolder = Join-Path $PWD.Path 'work/laboratory-props-export'
New-Item -Path $outputFolder -ItemType Directory -Force | Out-Null
foreach ($item in (Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json)) {
    $spritePath = Join-Path $outputFolder ($item.name + '.png')
    [LaboratorySpriteExport]::Export($item.source, $spritePath, $item.size, $item.fitHeight)
    Get-Item -LiteralPath $spritePath | Select-Object Name,Length
}
