$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$folder = $PSScriptRoot
$path = Join-Path $folder 'defeat-03-ivan.png'
$baseStream = [IO.MemoryStream]::new([IO.File]::ReadAllBytes($path))
$editStream = [IO.MemoryStream]::new([Convert]::FromBase64String([IO.File]::ReadAllText((Join-Path $folder 'ivan-closed-eyes.b64'))))
$canvas = [Drawing.Bitmap]::new($baseStream)
$edit = [Drawing.Bitmap]::new($editStream)
if ($edit.Size -ne $canvas.Size) { throw 'Source and window sizes differ.' }
$graphics = [Drawing.Graphics]::FromImage($canvas)
$graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
$eyeRegions = @(
    [Drawing.Rectangle]::new(526, 450, 50, 47)
    [Drawing.Rectangle]::new(576, 393, 48, 48)
)
foreach ($region in $eyeRegions) {
    $graphics.DrawImage($edit, $region, $region, [Drawing.GraphicsUnit]::Pixel)
}
$graphics.Dispose()
$canvas.Save($path, [Drawing.Imaging.ImageFormat]::Png)
$edit.Dispose()
$canvas.Dispose()
$editStream.Dispose()
$baseStream.Dispose()
