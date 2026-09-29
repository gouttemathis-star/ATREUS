Add-Type -AssemblyName System.Drawing

$outputDirectory = Join-Path $PSScriptRoot '..\Assets'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

function New-LogoPng([int]$size) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $shield = [System.Drawing.Point[]]@(
        [System.Drawing.Point]::new([int]($size * .18), [int]($size * .10)),
        [System.Drawing.Point]::new([int]($size * .82), [int]($size * .10)),
        [System.Drawing.Point]::new([int]($size * .82), [int]($size * .56)),
        [System.Drawing.Point]::new([int]($size * .50), [int]($size * .90)),
        [System.Drawing.Point]::new([int]($size * .18), [int]($size * .56))
    )
    $graphics.FillPolygon([System.Drawing.Brushes]::MidnightBlue, $shield)
    $graphics.DrawPolygon([System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180, 42, 91, 105), [Math]::Max(1, $size / 32)), $shield)

    $center = $size / 2
    $outer = $size * .22
    $inner = $size * .09
    $star = [System.Drawing.Point[]]::new(10)
    for ($index = 0; $index -lt 10; $index++) {
        $angle = -[Math]::PI / 2 + $index * [Math]::PI / 5
        $radius = if ($index % 2 -eq 0) { $outer } else { $inner }
        $star[$index] = [System.Drawing.Point]::new([int]($center + [Math]::Cos($angle) * $radius), [int]($center + [Math]::Sin($angle) * $radius))
    }
    $graphics.FillPolygon([System.Drawing.Brushes]::Gold, $star)
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
$writer.Dispose(); $header.Dispose()