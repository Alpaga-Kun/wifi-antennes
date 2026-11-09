# Wi-Fi Antennes - build script (Windows 10/11, no dependency)
# Uses the C# compiler shipped with .NET Framework 4.x.
#
#   .\build.ps1            -> builds dist\WifiAntennes.exe
#   .\build.ps1 -Install   -> builds, installs to %LOCALAPPDATA%\Programs\Wi-Fi Antennes
#                             and creates Desktop + Start menu shortcuts
param([switch]$Install)

$ErrorActionPreference = 'Stop'
$root   = $PSScriptRoot
$src    = Join-Path $root 'src'
$assets = Join-Path $root 'assets'
$dist   = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $assets, $dist | Out-Null

# ---- 1. Icon (drawn in code, saved as PNG + ICO) ----
Add-Type -AssemblyName System.Drawing
function New-IconPng([int]$size) {
    $s   = $size / 256.0
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $o = 8 * $s; $w = 240 * $s; $d = 112 * $s
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($o, $o, $d, $d, 180, 90)
    $path.AddArc($o + $w - $d, $o, $d, $d, 270, 90)
    $path.AddArc($o + $w - $d, $o + $w - $d, $d, $d, 0, 90)
    $path.AddArc($o, $o + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28, 28, 34))), $path)
    $cx = 128 * $s; $cy = 182 * $s
    $cols  = @([System.Drawing.Color]::FromArgb(46, 204, 113), [System.Drawing.Color]::FromArgb(163, 217, 72), [System.Drawing.Color]::FromArgb(241, 196, 15))
    $radii = @(42, 78, 114)
    for ($i = 0; $i -lt 3; $i++) {
        $r = $radii[$i] * $s
        $pen = New-Object System.Drawing.Pen $cols[$i], ([float](22 * $s))
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $g.DrawArc($pen, [float]($cx - $r), [float]($cy - $r), [float](2 * $r), [float](2 * $r), 225, 90)
    }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $cols[0]), [float]($cx - 16 * $s), [float]($cy - 16 * $s), [float](32 * $s), [float](32 * $s))
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs  = @{}
foreach ($sz in $sizes) { $pngs[$sz] = New-IconPng $sz }
[IO.File]::WriteAllBytes((Join-Path $assets 'icon.png'), $pngs[256])

# ICO container with PNG-compressed images (Windows Vista+)
$ico = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter $ico
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$sz].Length); $bw.Write([UInt32]$offset)
    $offset += $pngs[$sz].Length
}
foreach ($sz in $sizes) { $bw.Write($pngs[$sz]) }
$bw.Flush()
$icoPath = Join-Path $assets 'icon.ico'
[IO.File]::WriteAllBytes($icoPath, $ico.ToArray())

# ---- 2. Compile ----
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '.NET Framework 4.x compiler (csc.exe) not found.' }

$exe = Join-Path $dist 'WifiAntennes.exe'
$cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu',
    "/out:$exe", "/win32icon:$icoPath", "/win32manifest:$(Join-Path $src 'app.manifest')",
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
    (Join-Path $src 'WifiAntennes.cs'))
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed (code $LASTEXITCODE)." }
Write-Host "Built: $exe" -ForegroundColor Green

# ---- 3. Optional install ----
if ($Install) {
    $target = Join-Path $env:LOCALAPPDATA 'Programs\Wi-Fi Antennes'
    New-Item -ItemType Directory -Force $target | Out-Null
    Get-Process WifiAntennes -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 300
    $installed = Join-Path $target 'WifiAntennes.exe'
    Copy-Item -Force $exe $installed
    $ws = New-Object -ComObject WScript.Shell
    $links = @(
        (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Wi-Fi Antennes.lnk'),
        (Join-Path ([Environment]::GetFolderPath('Programs')) 'Wi-Fi Antennes.lnk')
    )
    foreach ($l in $links) {
        $s = $ws.CreateShortcut($l)
        $s.TargetPath = $installed
        $s.WorkingDirectory = $target
        $s.IconLocation = "$installed,0"
        $s.Description = 'Wi-Fi Antennes - placement des antennes Wi-Fi'
        $s.Save()
    }
    Write-Host "Installed: $installed (+ Desktop and Start menu shortcuts)" -ForegroundColor Green
}
