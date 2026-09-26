<#
.SYNOPSIS
  Regenera los iconos de la pestaña del navegador a partir de wwwroot/favicon.svg.

.DESCRIPTION
  Rasteriza el logo de la marca (el mismo SVG del login de index.html) con Chrome o
  Edge en modo headless y escribe:
    - wwwroot/favicon.ico              PNG de 32 y 48 px dentro de un ICO
    - wwwroot/img/apple-touch-icon.png 180 px, a sangre (iOS redondea las esquinas)
  Solo hace falta volver a correrlo si cambia el logo. Compatible con PowerShell 5.1.

.EXAMPLE
  .\tools\Generar-Favicon.ps1
#>
[CmdletBinding()]
param([string]$Navegador)

$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$www = Join-Path $raiz 'wwwroot'
$svg = Join-Path $www 'favicon.svg'
if (-not (Test-Path $svg)) { throw "No se encontró $svg" }

if (-not $Navegador) {
    $candidatos = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe")
    $Navegador = $candidatos | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $Navegador) { throw 'No se encontró Chrome ni Edge; pásalo con -Navegador.' }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('aprendor-favicon-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp | Out-Null
try {
    # Versión a sangre para iOS: fondo con el degradado de la marca y la flecha un poco más chica.
    $svgApple = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="180" height="180" style="display:block">
  <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#6366f1"/><stop offset="1" stop-color="#0ea5e9"/></linearGradient></defs>
  <rect x="0" y="0" width="32" height="32" fill="url(#g)"/>
  <g transform="translate(16 16.75) scale(.8) translate(-16 -16.75)">
    <path d="M9 19 L16 11 L23 19" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M11 22.5 L21 22.5" stroke="#fff" stroke-width="2.6" stroke-linecap="round" opacity=".75"/>
  </g>
</svg>
'@

    function Rasterizar([string]$marcado, [int]$tam, [string]$salida) {
        # Se captura una ventana del tamaño pedido con la imagen ocupándola entera.
        $html = Join-Path $tmp "i$tam.html"
        Set-Content -Path $html -Encoding UTF8 -Value ('<!doctype html><html><body style="margin:0;background:transparent">' + $marcado + '</body></html>')
        $url = 'file:///' + ($html -replace '\\', '/')
        # El navegador escribe su progreso en stderr; en PowerShell 5.1 eso cuenta como error.
        $ErrorActionPreference = 'Continue'
        & $Navegador --headless=new --disable-gpu --hide-scrollbars --default-background-color=00000000 `
            --force-device-scale-factor=1 "--user-data-dir=$tmp\perfil" "--window-size=$tam,$tam" "--screenshot=$salida" $url 2>$null | Out-Null
        $t = 0
        while (-not (Test-Path $salida) -and $t -lt 50) { Start-Sleep -Milliseconds 200; $t++ }
        if (-not (Test-Path $salida)) { throw "El navegador no generó $salida" }
    }

    $p32 = Join-Path $tmp 'p32.png'; $p48 = Join-Path $tmp 'p48.png'
    $urlSvg = 'file:///' + ($svg -replace '\\', '/' -replace ' ', '%20')
    Rasterizar "<img src=`"$urlSvg`" width=`"32`" height=`"32`" style=`"display:block`">" 32 $p32
    Rasterizar "<img src=`"$urlSvg`" width=`"48`" height=`"48`" style=`"display:block`">" 48 $p48
    New-Item -ItemType Directory -Force (Join-Path $www 'img') | Out-Null
    Rasterizar $svgApple 180 (Join-Path $www 'img\apple-touch-icon.png')

    # ICO con imágenes PNG dentro (válido desde Windows Vista y en todos los navegadores):
    # cabecera de 6 bytes, una entrada de 16 bytes por imagen y los PNG tal cual.
    $imagenes = @(@{ Tam = 32; Datos = [IO.File]::ReadAllBytes($p32) }, @{ Tam = 48; Datos = [IO.File]::ReadAllBytes($p48) })
    $ms = New-Object IO.MemoryStream
    $bw = New-Object IO.BinaryWriter($ms)
    $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$imagenes.Count)
    $offset = 6 + 16 * $imagenes.Count
    foreach ($i in $imagenes) {
        $bw.Write([byte]$i.Tam); $bw.Write([byte]$i.Tam); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([UInt16]1); $bw.Write([UInt16]32)
        $bw.Write([UInt32]$i.Datos.Length); $bw.Write([UInt32]$offset)
        $offset += $i.Datos.Length
    }
    foreach ($i in $imagenes) { $bw.Write($i.Datos) }
    $bw.Flush()
    [IO.File]::WriteAllBytes((Join-Path $www 'favicon.ico'), $ms.ToArray())
    Write-Host 'Listo: wwwroot/favicon.ico y wwwroot/img/apple-touch-icon.png'
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
