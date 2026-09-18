<#
.SYNOPSIS
    Script oficial de publicación y empaquetado seguro de versiones de ParkFlow Desktop (WPF).
.DESCRIPTION
    1. Compila Parking y ParkFlow.Updater en modo Release (win-x64).
    2. Empaqueta los binarios en un archivo ZIP de distribución (excluyendo bases de datos locales y configuraciones de desarrollo).
    3. Calcula el Checksum SHA-256 criptográfico inmutable.
    4. Genera un manifiesto JSON listo para el backend ParkingApi.
.PARAMETER Version
    Número de versión semántica (ej: 1.1.0).
.PARAMETER IsMandatory
    Indica si la versión es obligatoria para las terminales físicas.
.PARAMETER ReleaseNotes
    Descripción de los cambios o mejoras de la versión.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [bool]$IsMandatory = $false,

    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "Actualización y optimizaciones de estabilidad de ParkFlow Desktop."
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Resolve-Path "$scriptDir\.."
$outputDir = "$rootDir\Releases\v$Version"
$stagingDir = "$outputDir\Staging"
$zipFileName = "ParkFlow_v$Version.zip"
$zipFilePath = "$outputDir\$zipFileName"
$manifestFilePath = "$outputDir\release_manifest.json"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  📦 EMPAQUETADOR OFICIAL PARKFLOW DESKTOP v$Version" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Limpieza de staging previo
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# 2. Compilar aplicación principal WPF
Write-Host "`n[1/5] Compilando Parking WPF (Release win-x64)..." -ForegroundColor Yellow
dotnet publish "$rootDir\Parking\Parking.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o "$stagingDir"

# 3. Compilar Micro-Updater
Write-Host "`n[2/5] Compilando ParkFlow.Updater (Release win-x64)..." -ForegroundColor Yellow
dotnet publish "$rootDir\ParkFlow.Updater\ParkFlow.Updater.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -o "$stagingDir"

# 4. Limpiar archivos no deseados en la distribución (PDBs pesados de desarrollo, configs locales, bases de datos)
Write-Host "`n[3/5] Depurando paquete para distribución..." -ForegroundColor Yellow
Get-ChildItem -Path $stagingDir -Filter "*.pdb" | Remove-Item -Force
Get-ChildItem -Path $stagingDir -Filter "*.db*" | Remove-Item -Force
Get-ChildItem -Path $stagingDir -Filter "license.dat" | Remove-Item -Force
if (Test-Path "$stagingDir\appsettings.Development.json") {
    Remove-Item "$stagingDir\appsettings.Development.json" -Force
}

# 5. Generar archivo comprimido ZIP
Write-Host "`n[4/5] Comprimiendo paquete ZIP oficial..." -ForegroundColor Yellow
if (Test-Path $zipFilePath) {
    Remove-Item $zipFilePath -Force
}
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipFilePath -CompressionLevel Optimal

# 6. Calcular Checksum SHA-256
Write-Host "`n[5/5] Calculando firma criptográfica SHA-256..." -ForegroundColor Yellow
$hashResult = Get-FileHash -Path $zipFilePath -Algorithm SHA256
$sha256 = $hashResult.Hash.ToLowerInvariant()
$packageSize = (Get-Item $zipFilePath).Length

# 7. Crear manifiesto de publicación para el API Central
$manifest = [PSCustomObject]@{
    version = $Version
    minSupportedVersion = "1.0.0"
    isMandatory = $IsMandatory
    packageFileName = $zipFileName
    packageSha256 = $sha256
    packageSizeBytes = $packageSize
    releaseNotes = $ReleaseNotes
    releaseDateUtc = (Get-Date).ToUniversalTime().ToString("o")
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestFilePath -Encoding UTF8

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  ✅ PAQUETE GENERADO EXITOSAMENTE" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  - Archivo ZIP:    $zipFilePath" -ForegroundColor White
Write-Host "  - Tamaño:         $([math]::Round($packageSize / 1MB, 2)) MB ($packageSize bytes)" -ForegroundColor White
Write-Host "  - Hash SHA-256:   $sha256" -ForegroundColor Green
Write-Host "  - Manifiesto:     $manifestFilePath" -ForegroundColor White
Write-Host "================================================================================" -ForegroundColor Cyan
