Add-Type -AssemblyName System.Drawing
$w = 256
$h = 512
$bmp = New-Object System.Drawing.Bitmap($w, $h)

for ($y = 0; $y -lt $h; $y++) {
    # v from -1 (bottom/stern) to +1 (top/bow)
    # Note: bitmap y=0 is top, y=h-1 is bottom. We want top to be bow (+v)
    $v = 1.0 - ($y / ($h - 1.0)) * 2.0
    for ($x = 0; $x -lt $w; $x++) {
        # u from -1 (left) to +1 (right)
        $u = ($x / ($w - 1.0)) * 2.0 - 1.0
        
        # Boat hull half-width at position v:
        # Near stern (v < 0), width is constant ~0.70 with rounded bottom corners.
        # Near bow (v > 0), width tapers down towards ~0.15 at the tip.
        $halfWidth = 0.70
        if ($v -gt 0.0) {
            $halfWidth = 0.70 * (1.0 - 0.70 * [Math]::Pow($v, 1.35))
        }
        
        # Longitudinal length limits: [-0.80, 0.85]
        $vNorm = 0.0
        if ($v -gt 0.0) {
            $vNorm = $v / 0.85
        } else {
            $vNorm = $v / -0.80
        }
        
        $uNorm = [Math]::Abs($u) / [Math]::Max(0.08, $halfWidth)
        
        # Super-ellipse metric for hull shape
        $d = [Math]::Pow($uNorm, 2.8) + [Math]::Pow($vNorm, 2.8)
        $dist = [Math]::Sqrt($d)
        
        $alpha = 0.0
        if ($dist -lt 0.55) {
            $alpha = 1.0
        } elseif ($dist -lt 1.25) {
            $t = ($dist - 0.55) / (1.25 - 0.55)
            # Smooth cubic hermite falloff
            $alpha = 1.0 - ($t * $t * (3.0 - 2.0 * $t))
            $alpha = [Math]::Pow($alpha, 1.3)
        }
        
        $aByte = [Math]::Min(255, [Math]::Max(0, [int]($alpha * 255.0)))
        $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($aByte, 255, 255, 255))
    }
}

$destPath = "Assets/Textures/Ship_FakeShadow.png"
$bmp.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "Successfully saved $destPath"
