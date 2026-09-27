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

.EXAMPLE
  # Cuenta con verificacion en dos pasos: el codigo de la app autenticadora (si no se
  # pasa, el script lo pregunta). Turnstile no se puede resolver desde un script: ejecutalo
  # en el propio servidor contra http://localhost:8086 con
  # APRENDOR_Turnstile__ExemptNetworks="127.0.0.1/32,::1/128" en el sitio.
  .\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@cliente.com -Password "clave" -CoursePath .\course.json -TotpCode 123456

.EXAMPLE
  # Actualiza el curso YA EXISTENTE (por título exacto) en vez de crear uno nuevo:
  # reemplaza todos sus items y refresca titulo, descripcion, recurrencia y certificado.
  # No crea un Training nuevo ni duplica el curso.
  .\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@cliente.com -Password "clave" -CoursePath .\course.json -Update

.EXAMPLE
  # Graba ademas la voz de cada lamina (Azure AI Speech, voz de Puerto Rico) antes de
  # publicar. El sitio necesita APRENDOR_Speech__Key y APRENDOR_Speech__Region. La voz
  # sale de training.voice en course.json (por defecto es-PR-KarinaNeural).
  .\Seed-Curso.ps1 -Url http://localhost:8086 -Email autor@cliente.com -Password "clave" -CoursePath .\course.json -Update -Voz -Publish
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [Parameter(Mandatory = $true)][string]$CoursePath,
    [switch]$Publish,
    [switch]$Update,
    [switch]$SoloProbarConexion,
    # Graba la voz de cada lamina con Azure antes de publicar.
    [switch]$Voz,
    # Codigo de la app autenticadora, si la cuenta lo pide (si no se pasa, se pregunta).
    [string]$TotpCode
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
# La pantalla de entrada (Info con layout 'intro') no cuenta como pantalla de contenido.
$esIntro = { param($it) $it.type -eq 'Info' -and $it.payload -and $it.payload.layout -eq 'intro' }
$pantallas = @($items | Where-Object { $_.type -eq 'Info' -and -not (& $esIntro $_) }).Count
$conIntro = @($items | Where-Object { & $esIntro $_ }).Count -gt 0

Write-Output ''
Write-Output "Curso:     $($course.training.title)"
Write-Output "Servidor:  $base"
Write-Output "Items:     $($items.Count)  ($pantallas pantallas, $($preguntas.Count) preguntas, $puntos puntos$(if ($conIntro) { ' + pantalla de entrada' }))"
Write-Output ''

# ---- Login ----
# Turnstile: un script no puede resolver el widget, asi que solo entra por una conexion
# DIRECTA (sin tunel ni proxy) desde una red de Turnstile:ExemptNetworks. En el servidor:
# -Url http://localhost:8086 y APRENDOR_Turnstile__ExemptNetworks="127.0.0.1/32,::1/128".
# Doble factor: si la cuenta lo pide, se usa -TotpCode o se pregunta en la consola.
try {
    $login = Invoke-Api -Path '/auth/login' -Method 'POST' -Body @{ email = $Email; password = $Password }
} catch {
    if ("$_" -match 'turnstileFailed') {
        Write-Error ('El servidor pidio la verificacion de Turnstile, que un script no puede resolver. ' +
            'Ejecuta el script en el propio servidor contra http://localhost:<puerto> (sin pasar por el tunel ni por un proxy) ' +
            'con APRENDOR_Turnstile__ExemptNetworks="127.0.0.1/32,::1/128" en la configuracion del sitio.')
    } elseif ("$_" -match 'HTTP 401') {
        Write-Error 'Correo o contrasena incorrectos (o la URL no es la de este Aprendor).'
    } elseif ("$_" -match 'HTTP 429') {
        Write-Error "Demasiados intentos: $_"
    } else {
        Write-Error "No pude conectar: $_"
    }
    exit 1
}
if ($login.requires2fa) {
    $codigo = if ($TotpCode) { $TotpCode } else { Read-Host 'Codigo de la app autenticadora' }
    if (-not $codigo) { Write-Error 'La cuenta pide el codigo de la app autenticadora: pasalo con -TotpCode.'; exit 1 }
    try {
        $login = Invoke-Api -Path '/auth/2fa/verify' -Method 'POST' -Body @{ challengeId = $login.challengeId; code = "$codigo".Trim() }
    } catch {
        Write-Error "El codigo de la app autenticadora no fue aceptado: $_"
        exit 1
    }
}
if ($login.scope -and $login.scope -ne 'full') {
    $motivo = switch ($login.scope) {
        'change-password' { 'la cuenta tiene que cambiar su contrasena' }
        'enroll-2fa' { 'la compania exige verificacion en dos pasos y la cuenta no tiene app autenticadora' }
        'verify-email' { 'la cuenta tiene que validar su correo' }
        default { "la sesion es restringida ($($login.scope))" }
    }
    Write-Error "No se puede sembrar: $motivo. Completalo en la app y vuelve a correr el script."
    exit 1
}
$script:token = $login.token
Write-Output "Autenticado como $($login.user.email) - rol $($login.user.role)"
if (-not $login.user.tenantId) {
    Write-Error 'Esa cuenta no pertenece a ningun tenant (es admin de plataforma). Usa un usuario Author del tenant destino.'
    exit 1
}
if ($SoloProbarConexion) { Write-Output 'Conexion y credenciales OK. No se creo nada (-SoloProbarConexion).'; exit 0 }

# Opciones del reproductor (allowBack, reviewAfterPass, immediateFeedback y el bloque
# `presentation` del modo 16:9). Solo se envian si course.json trae training.playerConfig;
# si no, el servidor conserva lo que ya tenga guardado.
function Enviar-PlayerConfig($trainingId) {
    $pc = $course.training.playerConfig
    if ($null -eq $pc) { return }
    Invoke-Api -Path "/trainings/$trainingId/player-config" -Method 'PUT' -Body $pc | Out-Null
    Write-Output "Opciones del reproductor enviadas (modo presentacion: $($pc.presentation.enabled))."
}

# Antes de publicar se relee el borrador: tiene que tener exactamente los items de
# course.json, en el mismo orden, y la pantalla de entrada (si la hay) de primera. Si
# quedo un item de mas (p. ej. uno agregado en la app mientras corria el script) o el
# orden no coincide, NO se publica: el reproductor mostraria el intro como una lamina.
function Verificar-Borrador($trainingId) {
    $d = Invoke-Api -Path "/trainings/$trainingId/draft"
    $got = @($d.items)
    $errores = @()
    if ($got.Count -ne $items.Count) { $errores += "el borrador tiene $($got.Count) items y course.json $($items.Count)" }
    $n = [Math]::Min($got.Count, $items.Count)
    for ($i = 0; $i -lt $n; $i++) {
        if ($got[$i].type -ne $items[$i].type) { $errores += "item $($i + 1): se esperaba $($items[$i].type) y hay $($got[$i].type)"; break }
    }
    if ($conIntro) {
        $primero = if ($got.Count) { try { $got[0].payloadJson | ConvertFrom-Json } catch { $null } } else { $null }
        if (-not $primero -or $primero.layout -ne 'intro') { $errores += 'la pantalla de entrada (layout intro) no quedo como primer item' }
    }
    if ($errores.Count) {
        Write-Error ("El borrador no quedo igual que course.json: " + ($errores -join '; ') + '. No se publico; revisa el curso en la app y vuelve a correr el script.')
        exit 1
    }
    Write-Output "Borrador verificado: $($got.Count) items en el orden de course.json$(if ($conIntro) { ', pantalla de entrada primero' })."
}

# Voz de las laminas (-Voz): el servidor graba unas pocas por llamada; se repite hasta que
# no quede ninguna. Si Azure pide esperar (nivel gratis), el servidor responde 'wait'.
function Grabar-Voz($trainingId) {
    if (-not $Voz) { return }
    $voice = if ($course.training.voice) { $course.training.voice } else { 'es-PR-KarinaNeural' }
    Write-Output "Grabando la voz ($voice)..."
    for ($vuelta = 0; $vuelta -lt 200; $vuelta++) {
        $r = Invoke-Api -Path "/trainings/$trainingId/narration" -Method 'POST' -Body @{ voice = $voice; max = 8 }
        Write-Progress -Activity 'Grabando la voz' -Status "$($r.done) de $($r.total) laminas" -PercentComplete ([Math]::Min(100, $r.done * 100 / [Math]::Max(1, $r.total)))
        if ($r.error) { Write-Error "No se pudo grabar la voz: $($r.error)"; exit 1 }
        if (-not $r.pending) { Write-Progress -Activity 'Grabando la voz' -Completed; Write-Output "Voz grabada: $($r.done) laminas."; return }
        if ($r.wait) { Start-Sleep -Seconds ([int]$r.wait) }
    }
    Write-Error 'La grabacion de la voz no termino; corre de nuevo con -Voz para seguir donde quedo.'
    exit 1
}

function Publicar-SiCorresponde($trainingId) {
    Verificar-Borrador $trainingId
    Grabar-Voz $trainingId
    if ($Publish) {
        $v = Invoke-Api -Path "/trainings/$trainingId/publish" -Method 'POST'
        Write-Output "Publicado: version $($v.versionNumber)."
    } else {
        Write-Output 'Queda en BORRADOR. Revisalo en la app y publicalo desde ahi, o corre de nuevo con -Publish.'
    }
}

if ($Update) {
    # ---- Modo actualizar: reemplaza el contenido del curso YA EXISTENTE ----
    # No llama a POST /trainings ni POST /categories -- nunca crea un curso nuevo.
    $trainings = Invoke-Api -Path '/trainings'
    $existing = $trainings | Where-Object { $_.title -eq $course.training.title } | Select-Object -First 1
    if (-not $existing) {
        Write-Error "No hay ningun curso con el titulo exacto '$($course.training.title)' en este tenant. Corre sin -Update si quieres crearlo de nuevo."
        exit 1
    }
    Write-Output "Curso existente: $($existing.title) ($($existing.id)), status actual: $($existing.status)"

    Invoke-Api -Path "/trainings/$($existing.id)" -Method 'PUT' -Body @{
        title = $course.training.title; description = $course.training.description
    } | Out-Null
    Invoke-Api -Path "/trainings/$($existing.id)/recurrence" -Method 'POST' -Body @{
        recurrenceMonths = $course.training.recurrenceMonths; renewLeadDays = $course.training.renewLeadDays
    } | Out-Null
    Invoke-Api -Path "/trainings/$($existing.id)/certificate-config" -Method 'PUT' -Body $course.training.certificate | Out-Null
    Enviar-PlayerConfig $existing.id
    Write-Output "Titulo, descripcion, recurrencia y certificado actualizados."

    $draft = Invoke-Api -Path "/trainings/$($existing.id)/draft"
    Write-Output "Borrando $($draft.items.Count) items existentes del borrador..."
    foreach ($old in $draft.items) {
        Invoke-Api -Path "/items/$($old.id)" -Method 'DELETE' | Out-Null
    }

    $n = 0
    foreach ($it in $items) {
        $payloadJson = $it.payload | ConvertTo-Json -Depth 30 -Compress
        Invoke-Api -Path "/trainings/$($existing.id)/items" -Method 'POST' -Body @{
            type = $it.type; payloadJson = $payloadJson; points = [int]$it.points; required = $true; active = ($it.active -ne $false)
        } | Out-Null
        $n++
        Write-Progress -Activity 'Subiendo contenido actualizado' -Status "$n de $($items.Count)" -PercentComplete ($n * 100 / $items.Count)
    }
    Write-Progress -Activity 'Subiendo contenido actualizado' -Completed
    Write-Output "$n items nuevos creados (reemplazan a los anteriores)."

    Publicar-SiCorresponde $existing.id
    exit 0
}

# ---- Modo por defecto: crea un curso nuevo ----
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
Enviar-PlayerConfig $training.id
Write-Output "Recurrencia y certificado configurados."

# ---- Items, en orden ----
$n = 0
foreach ($it in $items) {
    $payloadJson = $it.payload | ConvertTo-Json -Depth 30 -Compress
    Invoke-Api -Path "/trainings/$($training.id)/items" -Method 'POST' -Body @{
        type = $it.type; payloadJson = $payloadJson; points = [int]$it.points; required = $true; active = ($it.active -ne $false)
    } | Out-Null
    $n++
    Write-Progress -Activity 'Subiendo contenido' -Status "$n de $($items.Count)" -PercentComplete ($n * 100 / $items.Count)
}
Write-Progress -Activity 'Subiendo contenido' -Completed
Write-Output "$n items creados en el borrador."

Publicar-SiCorresponde $training.id
