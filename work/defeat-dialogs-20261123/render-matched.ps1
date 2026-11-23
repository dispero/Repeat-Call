$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$folder = $PSScriptRoot
$template = [Drawing.Bitmap]::new((Join-Path $folder 'template.png'))
foreach ($item in @(@{Key='approach'; Index='01'}, @{Key='accomplices'; Index='02'})) {
    $canvas = $template.Clone()
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $textLayer = [Drawing.Bitmap]::new((Join-Path $folder ("text-" + $item.Key + '.png')))
    $graphics.DrawImageUnscaled($textLayer, 0, 702)
    $graphics.Dispose()
    $textLayer.Dispose()
    $filename = "defeat-" + $item.Index + "-" + $item.Key + ".png"
    $canvas.Save((Join-Path $folder $filename), [Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
}
$template.Dispose()
