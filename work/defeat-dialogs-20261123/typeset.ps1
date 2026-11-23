$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$folder = $PSScriptRoot
$fontPath = (Resolve-Path 'Assets/Golovomka/Resources/Menu/PixelifySans.ttf').Path
$fonts = [Drawing.Text.PrivateFontCollection]::new()
$fonts.AddFontFile($fontPath)
$face = $fonts.Families[0]
$font = [Drawing.Font]::new($face, 16, [Drawing.FontStyle]::Regular, [Drawing.GraphicsUnit]::Pixel)
$format = [Drawing.StringFormat]::new([Drawing.StringFormat]::GenericTypographic)
$format.Alignment = [Drawing.StringAlignment]::Center
$ink = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(249,242,221))
$template = [Drawing.Bitmap]::new((Join-Path $folder 'template.png'))
$messages = @(
    @{
        Name = 'defeat-01-approach.png'
        Lines = @('Бандит увидел ваше приближение', 'и застрелил вашего отца')
    }
    @{
        Name = 'defeat-02-accomplices.png'
        Lines = @('Бандит понял, что вы убили обоих его', 'напарников, и застрелил вашего отца')
    }
)
$scale = 4
$width = [int]($template.Width / $scale)
$layer = [Drawing.Bitmap]::new($width, 44)
$layerGraphics = [Drawing.Graphics]::FromImage($layer)
$layerGraphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
foreach ($message in $messages) {
    $layerGraphics.Clear([Drawing.Color]::Transparent)
    for ($i = 0; $i -lt $message.Lines.Count; $i++) {
        $origin = [Drawing.PointF]::new($width / 2, 1 + 20 * $i)
        $layerGraphics.DrawString($message.Lines[$i], $font, $ink, $origin, $format)
    }
    $canvas = $template.Clone()
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
    $graphics.DrawImage($layer, [Drawing.Rectangle]::new(0, 697, $canvas.Width, $layer.Height * $scale))
    $graphics.Dispose()
    $canvas.Save((Join-Path $folder $message.Name), [Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}
$layerGraphics.Dispose()
$layer.Dispose()
$template.Dispose()
$ink.Dispose()
$format.Dispose()
$font.Dispose()
$fonts.Dispose()

