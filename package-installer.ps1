[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$LatestEnvelope,

    [Parameter(Mandatory)]
    [string]$PayloadArchive,

    [Parameter(Mandatory)]
    [string]$Launcher,

    [Parameter(Mandatory)]
    [string]$LauncherPolicy,

    [Parameter(Mandatory)]
    [string]$DesktopRuntimeContract,

    [Parameter(Mandatory)]
    [string]$DesktopRuntimeInstaller,

    [Parameter(Mandatory)]
    [string]$InnoCompilerPath,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "artifacts\installer")
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

. (Join-Path $PSScriptRoot "packaging\installer\InstallerPackaging.Helpers.ps1")

$latestPath = Resolve-RequiredFile $LatestEnvelope "Signed latest envelope"
$archivePath = Resolve-RequiredFile $PayloadArchive "Payload archive"
$launcherPath = Resolve-RequiredFile $Launcher "Launcher"
$policyPath = Resolve-RequiredFile $LauncherPolicy "Launcher policy"
$runtimeContractPath = Resolve-RequiredFile $DesktopRuntimeContract "Desktop runtime contract"
$runtimeInstallerPath = Resolve-RequiredFile $DesktopRuntimeInstaller "Desktop runtime installer"
$isccPath = Resolve-RequiredFile $InnoCompilerPath "Inno Setup compiler"
$installerDefinition = Resolve-RequiredFile (Join-Path $PSScriptRoot "packaging\installer\Malco.iss") "Installer definition"
$englishLanguage = Resolve-RequiredFile (Join-Path $PSScriptRoot "packaging\installer\language-en-US.isl") "English installer language label"
$koreanLanguage = Resolve-RequiredFile (Join-Path $PSScriptRoot "packaging\installer\language-ko-KR.isl") "Korean installer language label"
$productIcon = Resolve-RequiredFile (Join-Path $PSScriptRoot "branding\malco.ico") "Malco product icon"

$manifest = Read-ReleaseManifest $latestPath
$releaseVersion = [string]$manifest.version
if ($releaseVersion -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$') {
    throw "The signed release version has an unsupported shape."
}
$desktopRuntime = Read-DesktopRuntimeContract $runtimeContractPath
Assert-DesktopRuntimeInstaller $runtimeInstallerPath $desktopRuntime
$archiveRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "mi"))
$workRoot = Join-Path $archiveRoot ([Guid]::NewGuid().ToString("N"))
$preparedRoot = Join-Path $workRoot "prepared"
$compiledRoot = Join-Path $workRoot "compiled"
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$expectedInstaller = Join-Path $outputRoot ("Malco-{0}-Setup.exe" -f $releaseVersion)
New-Item -ItemType Directory -Path $preparedRoot, $compiledRoot -Force | Out-Null
try {
    Copy-Item -LiteralPath $launcherPath -Destination (Join-Path $preparedRoot "Malco.Launcher.exe")
    Copy-Utf8JsonContract $policyPath (Join-Path $preparedRoot "launcher-policy.json") "Launcher policy"
    Copy-Item -LiteralPath $installerDefinition -Destination (Join-Path $workRoot "Malco.iss")
    Copy-Item -LiteralPath $englishLanguage -Destination (Join-Path $workRoot "language-en-US.isl")
    Copy-Item -LiteralPath $koreanLanguage -Destination (Join-Path $workRoot "language-ko-KR.isl")
    Copy-Item -LiteralPath $productIcon -Destination (Join-Path $workRoot "malco.ico")

    $stateRoot = Join-Path $preparedRoot "state"
    $manifestHash = Get-ReleaseManifestSha256 $latestPath
    $versionDirectoryName = ("{0:D20}-{1}" -f [long]$manifest.sequence, $manifestHash)
    $versionRoot = Join-Path (Join-Path $preparedRoot "versions") $versionDirectoryName
    $payloadRoot = Join-Path $versionRoot "payload"
    New-Item -ItemType Directory -Path $stateRoot, (Join-Path $preparedRoot "data"), (Join-Path $preparedRoot "cache"), (Join-Path $preparedRoot "staging"), $payloadRoot -Force | Out-Null
    Copy-Item -LiteralPath $latestPath -Destination (Join-Path $versionRoot "release-envelope.json")
    $reference = [ordered]@{ sequence = [long]$manifest.sequence; manifest_sha256 = $manifestHash }
    # The packaged release is the stable rollback target for any update offered
    # on first launch. LaunchStable still performs a full startup handshake.
    $state = [ordered]@{
        schema = "malco.install-state.v2"
        generation = 0
        highest_accepted_sequence = [long]$manifest.sequence
        current = $reference
        last_known_good = $null
        pending = $null
        last_rollback = $null
    }
    [IO.File]::WriteAllText(
        (Join-Path $stateRoot "install-state.json"),
        ($state | ConvertTo-Json -Depth 8),
        ([Text.UTF8Encoding]::new($false)))

    Add-Type -AssemblyName System.IO.Compression
    $archiveStream = [IO.File]::OpenRead($archivePath)
    $zip = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Read, $false)
    try {
        foreach ($entry in $zip.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) { continue }
            $relative = $entry.FullName.Replace('/', '\')
            if ($relative.Contains('..\') -or $relative.StartsWith('\') -or $relative.Contains(':')) {
                throw "The payload contains an unsafe path: $($entry.FullName)"
            }
            $destination = [IO.Path]::GetFullPath((Join-Path $payloadRoot $relative))
            Assert-ChildPath $payloadRoot $destination
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            $input = $entry.Open()
            try {
                $output = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try { $input.CopyTo($output); $output.Flush($true) } finally { $output.Dispose() }
            }
            finally { $input.Dispose() }
        }
    }
    finally {
        $zip.Dispose()
        $archiveStream.Dispose()
    }

    & $isccPath `
        "/DPreparedRoot=$preparedRoot" `
        "/DAppVersion=$releaseVersion" `
        "/DInstallerOutput=$compiledRoot" `
        "/DDotNetMinimumVersion=$([string]$desktopRuntime.minimum_version)" `
        "/DDotNetInstallerFileName=$([string]$desktopRuntime.file_name)" `
        "/DDotNetDownloadUrl=$([string]$desktopRuntime.download_url)" `
        "/DDotNetInstallerSha256=$([string]$desktopRuntime.sha256)" `
        (Join-Path $workRoot "Malco.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup did not create the installer." }
    $privateInstaller = Join-Path $compiledRoot ("Malco-{0}-Setup.exe" -f $releaseVersion)
    if (-not (Test-Path -LiteralPath $privateInstaller -PathType Leaf)) {
        throw "The expected installer executable was not created."
    }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    if (Test-Path -LiteralPath $expectedInstaller) {
        throw "The destination installer already exists: $expectedInstaller"
    }
    Move-Item -LiteralPath $privateInstaller -Destination $expectedInstaller
    Write-Host "Installer created: $expectedInstaller" -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
