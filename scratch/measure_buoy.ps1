Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile('c:\Users\ezgid\OneDrive\Masaüstü\PixelGame\scratch\gemi_gameplay_view.png')
# Center is x=540, let's scan around y=900 to 1000 for the red ring
$minX = 10000; $maxX = 0; $minY = 10000; $maxY = 0
for ($y = 850; $y -lt 1050; $y++) {
    for ($x = 450; $x -lt 630; $x++) {
        $c = $bmp.GetPixel($x, $y)
        # Red color of lifebuoy has high R and low B/G (R > 180, G < 80, B < 80)
        if ($c.R -gt 170 -and $c.G -lt 80 -and $c.B -lt 80) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
$w = $maxX - $minX + 1
$h = $maxY - $minY + 1
Write-Output "Center Buoy Bounds: X=[$minX, $maxX], Y=[$minY, $maxY], Width=$w, Height=$h, Ratio=$([math]::Round($h/$w, 3))"
$bmp.Dispose()
