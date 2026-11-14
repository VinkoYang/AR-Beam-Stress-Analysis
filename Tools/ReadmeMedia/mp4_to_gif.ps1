# Converts the Unity Recorder MP4s in Temp/Recordings/ into the README GIFs in Docs/media/.
# Two-pass palette (palettegen + paletteuse), 15 fps, 960 px wide. Needs ffmpeg on PATH.
# Run from the repository root:  powershell -ExecutionPolicy Bypass -File Tools/ReadmeMedia/mp4_to_gif.ps1
param([int]$Fps = 15, [int]$Width = 960)
$names = 'holomech-workbench-live', 'holomech-inputs', 'holomech-stress-cut'
foreach ($n in $names) {
    $src = "Temp/Recordings/$n.mp4"
    if (-not (Test-Path $src)) { Write-Warning "missing $src"; continue }
    $pal = "Temp/Recordings/$n-palette.png"
    $f = "fps=$Fps,scale=${Width}:-1:flags=lanczos"
    ffmpeg -v error -y -i $src -vf "$f,palettegen=stats_mode=diff" $pal
    ffmpeg -v error -y -i $src -i $pal -lavfi "$f [x]; [x][1:v] paletteuse=dither=sierra2_4a" "Docs/media/$n.gif"
    $kb = [math]::Round((Get-Item "Docs/media/$n.gif").Length / 1KB)
    Write-Output "Docs/media/$n.gif  $kb KB"
}
