Add-Type -AssemblyName System.Drawing

$outputDirectory = Join-Path $PSScriptRoot '..\Assets'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sourceLogo = [System.Drawing.Bitmap]::new((Join-Path $outputDirectory 'atreus-logo.png'))

function New-LogoPng([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(4, 10, 14))

    $sourceRectangle = [System.Drawing.Rectangle]::new(
        [int]($sourceLogo.Width * .27),
        [int]($sourceLogo.Height * .12),
        [int]($sourceLogo.Width * .46),
        [int]($sourceLogo.Height * .56)
    )
    $scale = [Math]::Min(($size * .94) / $sourceRectangle.Width, ($size * .84) / $sourceRectangle.Height)
    $drawWidth = [int]($sourceRectangle.Width * $scale)
    $drawHeight = [int]($sourceRectangle.Height * $scale)
    $destinationRectangle = [System.Drawing.Rectangle]::new(
        [int](($size - $drawWidth) / 2),
        [int](($size - $drawHeight) / 2),
        $drawWidth,
        $drawHeight
    )
    $graphics.DrawImage($sourceLogo, $destinationRectangle, $sourceRectangle, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return ,$stream.ToArray()
}

$sizes = @(16, 32, 48, 256)
$images = foreach ($size in $sizes) { [PSCustomObject]@{ Size = $size; Data = New-LogoPng $size } }
$header = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($header)
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
    $writer.Write([Byte]$dimension); $writer.Write([Byte]$dimension); $writer.Write([Byte]0); $writer.Write([Byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$image.Data.Length); $writer.Write([UInt32]$offset)
    $offset += $image.Data.Length
}
foreach ($image in $images) { $writer.Write($image.Data) }
[System.IO.File]::WriteAllBytes((Join-Path $outputDirectory 'atreus.ico'), $header.ToArray())
$writer.Dispose(); $header.Dispose(); $sourceLogo.Dispose()