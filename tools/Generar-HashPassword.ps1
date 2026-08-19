<#
.SYNOPSIS
  Genera un hash de contraseña compatible con PasswordHasher (Auth.cs), sin depender de Node.

.DESCRIPTION
  Mismo algoritmo que la aplicación: PBKDF2-SHA256, 100,000 iteraciones, sal de 16 bytes,
  clave de 32 bytes, almacenado como "base64(sal).base64(hash)".

  ÚLTIMO RECURSO. El camino normal para recuperar acceso es que el admin de plataforma
  resetee la contraseña desde la app (Usuarios -> Resetear). Esto es para cuando también
  se perdió la contraseña del admin y hay que escribir el hash directo en la base del
  CATÁLOGO (no en la del tenant).

.EXAMPLE
  .\Generar-HashPassword.ps1 -Password "TempAprendor2026!" -Email admin@local

.EXAMPLE
  .\Generar-HashPassword.ps1 -Password "TempAprendor2026!" -Verify "sal.hash"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Password,
    [string]$Email = 'CORREO_DEL_USUARIO',
    [string]$Verify
)

$ErrorActionPreference = 'Stop'

$Iterations = 100000
$SaltBytes  = 16
$KeyBytes   = 32

function Get-Pbkdf2 {
    param([string]$Text, [byte[]]$Salt)
    $sha256 = [System.Security.Cryptography.HashAlgorithmName]::SHA256
    $kdf = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($Text, $Salt, $Iterations, $sha256)
    try { return $kdf.GetBytes($KeyBytes) } finally { $kdf.Dispose() }
}

if ($Verify) {
    $parts = $Verify.Split('.')
    if ($parts.Count -ne 2) { Write-Error 'El hash debe tener el formato "sal.hash".'; exit 1 }
    $salt     = [Convert]::FromBase64String($parts[0])
    $expected = [Convert]::FromBase64String($parts[1])
    $actual   = Get-Pbkdf2 -Text $Password -Salt $salt
    if ([Convert]::ToBase64String($actual) -eq [Convert]::ToBase64String($expected)) {
        Write-Output 'La contrasena SI corresponde a ese hash.'
    } else {
        Write-Output 'La contrasena NO corresponde a ese hash.'
    }
    exit 0
}

$salt = New-Object byte[] $SaltBytes
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($salt) } finally { $rng.Dispose() }

$hash  = Get-Pbkdf2 -Text $Password -Salt $salt
$value = '{0}.{1}' -f [Convert]::ToBase64String($salt), [Convert]::ToBase64String($hash)

# Comprobacion de ida y vuelta: si esto fallara, el hash no serviria para entrar.
$check = Get-Pbkdf2 -Text $Password -Salt $salt
if ([Convert]::ToBase64String($check) -ne [Convert]::ToBase64String($hash)) {
    Write-Error 'Error interno: el hash generado no se verifica.'; exit 1
}

Write-Output ''
Write-Output ("Hash (PBKDF2-SHA256, {0:N0} iteraciones):" -f $Iterations)
Write-Output $value
Write-Output ''
Write-Output 'SQL para la base del CATALOGO (no la del tenant):'
Write-Output ''
Write-Output 'UPDATE [User]'
Write-Output ("   SET PasswordHash = '{0}'," -f $value)
Write-Output '       MustChangePassword = 1'
Write-Output (" WHERE Email = '{0}';" -f $Email)
Write-Output ''
Write-Output 'Despues entra con esa contrasena y cambiala desde la app (te la va a pedir).'
