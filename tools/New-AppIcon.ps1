#Requires -Version 5.1
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repoRoot 'src\PomodoroFocus.App\Assets\AppIcon.ico'
$outputDir = Split-Path -Parent $outputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

function Render-LogoPngBytes {
    param([int]$Size)

    $dv = New-Object System.Windows.Media.DrawingVisual
    $dc = $dv.RenderOpen()

    $scale = [double]$Size / 256.0
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))

    # 1. Rounded square background with linear gradient #FF9F68 -> #FF5A5F
    $bgBrush = New-Object System.Windows.Media.LinearGradientBrush
    $bgBrush.StartPoint = New-Object System.Windows.Point(0, 0)
    $bgBrush.EndPoint = New-Object System.Windows.Point(1, 1)
    $bgBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0xFF, 0x9F, 0x68), 0.0)))
    $bgBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.Color]::FromRgb(0xFF, 0x5A, 0x5F), 1.0)))
    $bgRect = New-Object System.Windows.Rect(0, 0, 256, 256)
    $dc.DrawRoundedRectangle($bgBrush, $null, $bgRect, 60, 60)

    # 2. White progress arc: center (128, 142), radius 70, stroke 22, round cap, -60 deg clockwise 300 deg to 240 deg
    $cx = 128.0
    $cy = 142.0
    $r = 70.0
    $startRad = (-60.0) * [Math]::PI / 180.0
    $endRad = (240.0) * [Math]::PI / 180.0
    $startPt = New-Object System.Windows.Point(($cx + $r * [Math]::Cos($startRad)), ($cy + $r * [Math]::Sin($startRad)))
    $endPt = New-Object System.Windows.Point(($cx + $r * [Math]::Cos($endRad)), ($cy + $r * [Math]::Sin($endRad)))

    $arcFigure = New-Object System.Windows.Media.PathFigure
    $arcFigure.StartPoint = $startPt
    $arcFigure.IsClosed = $false
    $arcSeg = New-Object System.Windows.Media.ArcSegment(
        $endPt,
        (New-Object System.Windows.Size($r, $r)),
        0.0,
        $true,
        [System.Windows.Media.SweepDirection]::Clockwise,
        $true
    )
    $arcFigure.Segments.Add($arcSeg)
    $arcGeo = New-Object System.Windows.Media.PathGeometry
    $arcGeo.Figures.Add($arcFigure)

    $whitePen = New-Object System.Windows.Media.Pen([System.Windows.Media.Brushes]::White, 22.0)
    $whitePen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $whitePen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $dc.DrawGeometry($null, $whitePen, $arcGeo)

    # 3. Leaf + stem (#34D399)
    $leafColor = [System.Windows.Media.Color]::FromRgb(0x34, 0xD3, 0x99)
    $leafBrush = New-Object System.Windows.Media.SolidColorBrush($leafColor)
    $leafGeo = [System.Windows.Media.Geometry]::Parse('M128 82 C112 56 132 36 162 40 C158 64 144 78 128 82 Z')
    $dc.DrawGeometry($leafBrush, $null, $leafGeo)

    $stemGeo = [System.Windows.Media.Geometry]::Parse('M128 82 L128 60')
    $stemPen = New-Object System.Windows.Media.Pen($leafBrush, 8.0)
    $stemPen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $stemPen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $dc.DrawGeometry($null, $stemPen, $stemGeo)

    $dc.Pop()
    $dc.Close()

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($Size, $Size, 96.0, 96.0, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = New-Object System.IO.MemoryStream
    $encoder.Save($ms)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 256)
$frames = @()
foreach ($s in $sizes) {
    $png = Render-LogoPngBytes -Size $s
    $frames += [PSCustomObject]@{ Size = $s; Bytes = $png }
}

$fs = New-Object System.IO.FileStream($outputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICO header: reserved(2)=0, type(2)=1, count(2)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$frames.Count)

$offset = 6 + (16 * $frames.Count)
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { [byte]0 } else { [byte]$f.Size }
    $bw.Write($dim)              # Width
    $bw.Write($dim)              # Height
    $bw.Write([byte]0)           # ColorCount
    $bw.Write([byte]0)           # Reserved
    $bw.Write([UInt16]1)         # Planes
    $bw.Write([UInt16]32)        # BitCount
    $bw.Write([UInt32]$f.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}

foreach ($f in $frames) {
    $bw.Write($f.Bytes)
}

$bw.Flush()
$bw.Dispose()
$fs.Dispose()

Write-Output ("ICON OK: " + $outputPath + " (" + (Get-Item $outputPath).Length + " bytes)")
