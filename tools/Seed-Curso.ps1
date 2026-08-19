<#
.SYNOPSIS
  Carga un curso completo (course.json) en Aprendor a través de la API. No requiere Node.

.DESCRIPTION
  Hace lo mismo que content/hipaa-advance-logistics/seed.mjs pero en PowerShell, para
  servidores donde no hay Node instalado.

  La cuenta que uses tiene que tener rol Admin, Author o Moderator DENTRO del tenant
  destino: el curso se crea en la base del tenant que resuelve su token. Un admin de
  plataforma (sin tenant) NO sirve.

.EXAMPLE
  .\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@cliente.com -Password "clave" -CoursePath .\course.json

.EXAMPLE
  .\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@cliente.com -Password "clave" -CoursePath .\course.json -Publish
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [Parameter(Mandatory = $true)][string]$CoursePath,
    [switch]$Publish,
    [switch]$SoloProbarConexion
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$base = $Url.TrimEnd('/')
$script:token = $null

# PS 5.1 manda el body en la codificación equivocada si se le pasa un string:
# los acentos llegan rotos. Por eso se envía siempre como bytes UTF-8.
function Invoke-Api {
    param([string]$Path, [string]$Method = 'GET', $Body = $null)
    $headers = @{}
    if ($script:token) { $headers['Authorization'] = "Bearer $($script:token)" }
    $args = @{ Uri = "$base$Path"; Method = $Method; Headers = $headers; ContentType = 'application/json; charset=utf-8' }
    if ($null -ne $Body) {
        $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 30 -Compress }
        $args['Body'] = [System.Text.Encoding]::UTF8.GetBytes($json)
    }
    try {
        return Invoke-RestMethod @args
    } catch {
        $status = $null
        $detalle = ''
        if ($_.Exception.Response) {
            $status = $_.Exception.Response.StatusCode.value__
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $detalle = $reader.ReadToEnd()
                $reader.Close()
            } catch { }
        }
        throw "$Method $Path -> $(if ($status) { "HTTP $status" } else { $_.Exception.Message }) $detalle".Trim()
    }
}

# ---- Contenido ----
if (-not (Test-Path $CoursePath)) { Write-Error "No encuentro $CoursePath"; exit 1 }
$course = Get-Content -Path $CoursePath -Raw -Encoding UTF8 | ConvertFrom-Json
$items = @($course.items)
$preguntas = @($items | Where-Object { $_.points -gt 0 })
$puntos = ($preguntas | Measure-Object -Property points -Sum).Sum

Write-Output ''
Write-Output "Curso:     $($course.training.title)"
Write-Output "Servidor:  $base"
Write-Output "Items:     $($items.Count)  ($(@($items | Where-Object { $_.type -eq 'Info' }).Count) pantallas, $($preguntas.Count) preguntas, $puntos puntos)"
Write-Output ''

# ---- Login ----
try {
    $login = Invoke-Api -Path '/auth/login' -Method 'POST' -Body @{ email = $Email; password = $Password }
} catch {
    if ("$_" -match 'HTTP 401') {
        Write-Error 'Correo o contrasena incorrectos (o la URL no es la de este Aprendor).'
    } else {
        Write-Error "No pude conectar: $_"
    }
    exit 1
}
$script:token = $login.token
Write-Output "Autenticado como $($login.user.email) - rol $($login.user.role)"
if (-not $login.user.tenantId) {
    Write-Error 'Esa cuenta no pertenece a ningun tenant (es admin de plataforma). Usa un usuario Author del tenant destino.'
    exit 1
}
if ($SoloProbarConexion) { Write-Output 'Conexion y credenciales OK. No se creo nada (-SoloProbarConexion).'; exit 0 }

# ---- Categoria ----
$categorias = Invoke-Api -Path '/categories'
$cat = $categorias | Where-Object { $_.name -eq $course.training.category } | Select-Object -First 1
if (-not $cat) {
    $cat = Invoke-Api -Path '/categories' -Method 'POST' -Body @{ name = $course.training.category; parentId = $null }
    Write-Output "Categoria creada: $($course.training.category)"
} else {
    Write-Output "Categoria existente: $($course.training.category)"
}

# ---- Curso ----
$training = Invoke-Api -Path '/trainings' -Method 'POST' -Body @{
    title = $course.training.title; description = $course.training.description; categoryId = $cat.id
}
Write-Output "Curso creado: $($training.id)"

# ---- Recurrencia y certificado ----
Invoke-Api -Path "/trainings/$($training.id)/recurrence" -Method 'POST' -Body @{
    recurrenceMonths = $course.training.recurrenceMonths; renewLeadDays = $course.training.renewLeadDays
} | Out-Null
Invoke-Api -Path "/trainings/$($training.id)/certificate-config" -Method 'PUT' -Body $course.training.certificate | Out-Null
Write-Output "Recurrencia y certificado configurados."

# ---- Items, en orden ----
$n = 0
foreach ($it in $items) {
    $payloadJson = $it.payload | ConvertTo-Json -Depth 30 -Compress
    Invoke-Api -Path "/trainings/$($training.id)/items" -Method 'POST' -Body @{
        type = $it.type; payloadJson = $payloadJson; points = [int]$it.points; required = $true; active = $true
    } | Out-Null
    $n++
    Write-Progress -Activity 'Subiendo contenido' -Status "$n de $($items.Count)" -PercentComplete ($n * 100 / $items.Count)
}
Write-Progress -Activity 'Subiendo contenido' -Completed
Write-Output "$n items creados en el borrador."

# ---- Publicar ----
if ($Publish) {
    $v = Invoke-Api -Path "/trainings/$($training.id)/publish" -Method 'POST'
    Write-Output "Publicado: version $($v.versionNumber)."
} else {
    Write-Output 'Queda en BORRADOR. Revisalo en la app y publicalo desde ahi.'
}
