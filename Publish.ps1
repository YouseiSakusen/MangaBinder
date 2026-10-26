# MangaBinder - Release publish for local verification (no ZIP)
# Example: .\Publish.ps1 -OutputDirectory 'H:\MangaBinder-Test'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = $PSScriptRoot
$uiProject = Join-Path $repoRoot 'MangaBinder\MangaBinder.csproj'
$workerProject = Join-Path $repoRoot 'MangaBinder.Worker\MangaBinder.Worker.csproj'
$templateDb = Join-Path $repoRoot 'MangaBinder\template.db'

foreach ($file in @($uiProject, $workerProject, $templateDb)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Required file not found: $file"
    }
}

$target = [System.IO.Path]::GetFullPath($OutputDirectory)
$sourceRoot = [System.IO.Path]::GetFullPath($repoRoot)
if ($target.TrimEnd('\', '/') -eq $sourceRoot.TrimEnd('\', '/')) {
    throw 'OutputDirectory must not be the repository root.'
}
if (Test-Path -LiteralPath $target) {
    $existing = @(Get-ChildItem -LiteralPath $target -Force)
    if ($existing.Count -ne 0) {
        throw "OutputDirectory is not empty (existing files are protected): $target"
    }
} else {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

$workerTarget = Join-Path $target 'worker'
New-Item -ItemType Directory -Path $workerTarget -Force | Out-Null

function Invoke-Publish([string]$project, [string]$destination) {
    Write-Host "Publishing $project -> $destination"
    & dotnet publish $project -c Release -r win-x64 --self-contained false -o $destination /p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed (exit code $LASTEXITCODE): $project"
    }
}

Invoke-Publish $uiProject $target
Invoke-Publish $workerProject $workerTarget

# Copy the locally maintained database template. It is intentionally gitignored.
Copy-Item -LiteralPath $templateDb -Destination (Join-Path $target 'template.db') -Force

# Do not ship developer-specific settings and API keys.
foreach ($relative in @(
    'google-books-settings.json',
    'appsettings.Debug.json',
    'appsettings.Development.json',
    'worker\appsettings.Debug.json',
    'worker\appsettings.Development.json',
    'worker\google-books-settings.json'
)) {
    $path = Join-Path $target $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
    }
}

Write-Host "Publish completed: $target"
Write-Host 'No ZIP was created.'
