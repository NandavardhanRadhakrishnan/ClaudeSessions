<#
.SYNOPSIS
    Builds the plugin and installs it into PowerToys Run for the current user.
.DESCRIPTION
    Build Release -> stop PowerToys -> copy to
    %LOCALAPPDATA%\Microsoft\PowerToys\PowerToys Run\Plugins\ClaudeSessions -> start PowerToys.
.EXAMPLE
    .\scripts\deploy.ps1
    .\scripts\deploy.ps1 -Platform ARM64
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'ARM64')]
    [string] $Platform = 'x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\Community.PowerToys.Run.Plugin.ClaudeSessions\Community.PowerToys.Run.Plugin.ClaudeSessions.csproj'
$output = Join-Path $root "artifacts\$Platform"
$destination = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins\ClaudeSessions'

Write-Host "Building ($Platform)..."
if (Test-Path $output) { Remove-Item $output -Recurse -Force }
dotnet build $project -c Release "-p:Platform=$Platform" -o $output --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

# Remember where PowerToys runs from so it can be restarted from the same place.
$powerToys = Get-Process -Name PowerToys -ErrorAction SilentlyContinue | Select-Object -First 1
$powerToysExe = $null
if ($powerToys) { $powerToysExe = $powerToys.Path }
if (-not $powerToysExe) {
    $powerToysExe = @(
        (Join-Path $env:ProgramFiles 'PowerToys\PowerToys.exe'),
        (Join-Path $env:LOCALAPPDATA 'PowerToys\PowerToys.exe')
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

Write-Host 'Stopping PowerToys...'
Get-Process -Name 'PowerToys', 'PowerToys.*' -ErrorAction SilentlyContinue | Stop-Process -Force
$deadline = (Get-Date).AddSeconds(15)
while ((Get-Process -Name 'PowerToys', 'PowerToys.PowerLauncher' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 250
}

Write-Host "Installing to $destination"
if (Test-Path $destination) { Remove-Item $destination -Recurse -Force }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item (Join-Path $output '*') $destination -Recurse -Force

if ($powerToysExe) {
    Write-Host 'Starting PowerToys...'
    Start-Process $powerToysExe
} else {
    Write-Warning 'PowerToys.exe not found; start PowerToys manually.'
}

Write-Host 'Done. Open PowerToys Run and type "cc".'
