<#
.SYNOPSIS
    Script oficial de publicacion y empaquetado seguro de versiones de ParkFlow Desktop (WPF).
.DESCRIPTION
    1. Compila Parking y ParkFlow.Updater en modo Release (win-x64).
    2. Empaqueta los binarios en un archivo ZIP de distribucion (excluyendo bases de datos locales y configuraciones de desarrollo).
    3. Calcula el Checksum SHA-256 criptografico inmutable.
    4. Genera un manifiesto JSON listo para el backend ParkingApi.
.PARAMETER Version
    Numero de version semantica (ej: 1.1.0).
.PARAMETER IsMandatory
    Indica si la version es obligatoria para las terminales fisicas.
.PARAMETER ReleaseNotes
    Descripcion de los cambios o mejoras de la version.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    $IsMandatory = $true,

    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "Actualizacion y optimizaciones de estabilidad de ParkFlow Desktop."
)

$ErrorActionPreference = "Stop"

$isMandatoryBool = if ($IsMandatory -is [bool]) { 
    $IsMandatory 
} elseif ($IsMandatory -is [System.Management.Automation.SwitchParameter]) { 
    $IsMandatory.IsPresent 
} else { 
    $valStr = $IsMandatory.ToString().Trim('$', ' ', '"', "'")
    if ($valStr -eq "1" -or $valStr -eq "true" -or $valStr -eq "True") { $true } else { $false }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path "$scriptDir\..").Path
$outputDir = "$rootDir\Releases\v$Version"
$stagingDir = "$outputDir\Staging"
$zipFileName = "ParkFlow_v$Version.zip"
$zipFilePath = "$outputDir\$zipFileName"
$manifestFilePath = "$outputDir\release_manifest.json"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  EMPAQUETADOR OFICIAL PARKFLOW DESKTOP v$Version" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan

# 1. Limpieza de staging previo
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# 2. Compilar aplicacion principal WPF
Write-Host "`n[1/5] Compilando Parking WPF (Release win-x64)..." -ForegroundColor Yellow
dotnet publish "$rootDir\Parking\Parking.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:Version=$Version `
    /p:AssemblyVersion=$Version `
    /p:FileVersion=$Version `
    -o "$stagingDir"

# 3. Compilar Micro-Updater
Write-Host "`n[2/5] Compilando ParkFlow.Updater (Release win-x64)..." -ForegroundColor Yellow
dotnet publish "$rootDir\ParkFlow.Updater\ParkFlow.Updater.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    /p:Version=$Version `
    /p:AssemblyVersion=$Version `
    /p:FileVersion=$Version `
    -o "$stagingDir"

# 4. Limpiar archivos no deseados en la distribucion (PDBs pesados de desarrollo, configs locales, bases de datos)
Write-Host "`n[3/5] Depurando paquete para distribucion..." -ForegroundColor Yellow
Get-ChildItem -Path $stagingDir -Filter "*.pdb" | Remove-Item -Force
Get-ChildItem -Path $stagingDir -Filter "*.xml" | Remove-Item -Force
Get-ChildItem -Path $stagingDir -Filter "*.db*" | Remove-Item -Force
Get-ChildItem -Path $stagingDir -Filter "license.dat" | Remove-Item -Force
if (Test-Path "$stagingDir\appsettings.Development.json") {
    Remove-Item "$stagingDir\appsettings.Development.json" -Force
}

# Depurar carpetas satélites de idiomas de EF Core / .NET no utilizadas (Ahorra ~20 MB de peso muerto)
$satelliteCultures = @('cs','de','es','fr','it','ja','ko','pl','pt-BR','ru','tr','zh-Hans','zh-Hant')
foreach ($culture in $satelliteCultures) {
    $culturePath = "$stagingDir\$culture"
    if (Test-Path $culturePath) {
        Remove-Item $culturePath -Recurse -Force
    }
}

# 5. Generar archivo comprimido ZIP
Write-Host "`n[4/5] Comprimiendo paquete ZIP oficial..." -ForegroundColor Yellow
if (Test-Path $zipFilePath) {
    Remove-Item $zipFilePath -Force
}
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipFilePath -CompressionLevel Optimal

# 6. Calcular Checksum SHA-256
Write-Host "`n[5/5] Calculando firma criptografica SHA-256..." -ForegroundColor Yellow
$hashResult = Get-FileHash -Path $zipFilePath -Algorithm SHA256
$sha256 = $hashResult.Hash.ToLowerInvariant()
$packageSize = (Get-Item $zipFilePath).Length
$mbSize = [math]::Round($packageSize / 1MB, 2)

# 7. Crear manifiesto de publicacion para el API Central
$manifest = [PSCustomObject]@{
    version = $Version
    minSupportedVersion = "1.0.0"
    isMandatory = $isMandatoryBool
    packageFileName = $zipFileName
    packageSha256 = $sha256
    packageSizeBytes = $packageSize
    releaseNotes = $ReleaseNotes
    releaseDateUtc = (Get-Date).ToUniversalTime().ToString("o")
}

$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestFilePath -Encoding UTF8

# 8. Compilar instalador oficial de Windows con Inno Setup si está disponible
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$setupExePath = "$outputDir\ParkFlow_Setup_v$Version.exe"

if ($isccPath) {
    Write-Host "`n[6/6] Compilando instalador oficial de Windows (Setup Wizard)..." -ForegroundColor Yellow
    $installerIssPath = "$scriptDir\installer.iss"
    & "$isccPath" /Q /DMyAppVersion="$Version" /O"$outputDir" "$installerIssPath"
    if (Test-Path $setupExePath) {
        $setupSize = [math]::Round((Get-Item $setupExePath).Length / 1MB, 2)
        Write-Host "  -> Instalador Windows generado: $setupExePath ($setupSize MB)" -ForegroundColor Green
    }
} else {
    Write-Host "`n[Aviso] Inno Setup 6 no encontrado en el sistema. Se omite la generacion del Setup .exe." -ForegroundColor Gray
}

Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host "  VERSION v$Version PUBLICADA EXITOSAMENTE" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
if (Test-Path $setupExePath) {
    Write-Host "  - Instalador Setup: $setupExePath" -ForegroundColor Green
}
Write-Host "  - Paquete ZIP (OTA):$zipFilePath" -ForegroundColor White
Write-Host "  - Tamano ZIP:       $mbSize MB ($packageSize bytes)" -ForegroundColor White
Write-Host "  - Hash SHA-256:     $sha256" -ForegroundColor Cyan
Write-Host "  - Manifiesto JSON:  $manifestFilePath" -ForegroundColor White
Write-Host "================================================================================" -ForegroundColor Cyan

