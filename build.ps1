param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path $PSScriptRoot "src\Malco\Malco.csproj"
$launcherProjectPath = Join-Path $PSScriptRoot "src\Malco.Launcher\Malco.Launcher.csproj"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw ".NET SDK was not found."
}

& $dotnet.Source build $projectPath --configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Malco build failed."
}

& $dotnet.Source build $launcherProjectPath --configuration $Configuration --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw "Malco launcher build failed."
}

Write-Host "Build completed: Malco and Malco.Launcher." -ForegroundColor Green
