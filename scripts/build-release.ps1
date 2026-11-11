# ---------------------------------------------------------------------
# Compila y empaqueta un release de Orthanc Mc.
#
# Uso:
#   pwsh ./scripts/build-release.ps1                  # version del csproj
#   pwsh ./scripts/build-release.ps1 -Version 1.1.0   # fuerza una version
#   pwsh ./scripts/build-release.ps1 -FrameworkDependent
#
# Genera: dist/OrthancMC-v<version>-win-x64.zip
# ---------------------------------------------------------------------
param(
    [string]$Version = "",
    [switch]$FrameworkDependent,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "McPanel.csproj"

if (-not (Test-Path $project)) {
    throw "No se encontro McPanel.csproj en $root"
}

# Version: parametro, o la del csproj.
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$xml = Get-Content $project
    $Version = $xml.Project.PropertyGroup.Version
}
$Version = $Version.Trim()
if ($Version.StartsWith("v") -or $Version.StartsWith("V")) {
    $Version = $Version.Substring(1)
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "No se pudo determinar la version."
}

$name = "OrthancMC-v$Version-$Runtime"
$outDir = Join-Path $root "dist\$name"
$zipPath = Join-Path $root "dist\$name.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force $outDir | Out-Null

$selfContained = "true"
if ($FrameworkDependent) { $selfContained = "false" }

Write-Host "Compilando Orthanc Mc v$Version ($Runtime, self-contained=$selfContained)..."

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained $selfContained `
    -o $outDir `
    --nologo `
    -p:Version=$Version `
    -p:FileVersion="$Version.0" `
    -p:AssemblyVersion="$Version.0"

if ($LASTEXITCODE -ne 0) { throw "dotnet publish fallo con codigo $LASTEXITCODE" }

# Los .pdb no hacen falta en el paquete.
Get-ChildItem $outDir -Filter *.pdb -File -ErrorAction SilentlyContinue | Remove-Item -Force

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ""
Write-Host "Release listo: $zipPath"
Write-Host ("Tamano: {0:N1} MB" -f ((Get-Item $zipPath).Length / 1MB))
