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

    [Parameter(Mandatory)]
    [string]$ReleaseToolPath,

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
$releaseTool = Resolve-RequiredFile $ReleaseToolPath "ReleaseTool"
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
$preparedRoot = Join-Path $workRoot "p"
$compiledRoot = Join-Path $workRoot "c"
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$expectedInstaller = Join-Path $outputRoot ("Malco-{0}-Setup.exe" -f $releaseVersion)
$expectedBinding = Join-Path $outputRoot ("Malco-{0}-Setup.binding.json" -f $releaseVersion)
New-Item -ItemType Directory -Path $preparedRoot, $compiledRoot -Force | Out-Null
try {
    # Verify the exact private inputs consumed below, not mutable caller paths.
    Copy-Item -LiteralPath $latestPath -Destination (Join-Path $workRoot "latest.json")
    Copy-Item -LiteralPath $archivePath -Destination (Join-Path $workRoot "payload.zip")
    Copy-Item -LiteralPath $policyPath -Destination (Join-Path $workRoot "launcher-policy.json")
    $latestPath = Join-Path $workRoot "latest.json"
    $archivePath = Join-Path $workRoot "payload.zip"
    $policyPath = Join-Path $workRoot "launcher-policy.json"
    $verification = & $releaseTool verify --json --latest $latestPath --payload $archivePath --launcher-policy $policyPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "ReleaseTool rejected the installer envelope/payload pair: $verification" }
    $verified = ($verification -join [Environment]::NewLine) | ConvertFrom-Json
    $manifest = Read-ReleaseManifest $latestPath
    if ([string]$verified.version -cne $releaseVersion -or [long]$verified.sequence -ne [long]$manifest.sequence) {
        throw "The installer inputs changed before verification."
    }
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
    # Reinstall keeps the existing selector; Launcher verifies and activates
    # this packaged candidate through its normal startup/rollback path.
    $stagedUpdate = [ordered]@{ schema = "malco.staged-update.v1"; candidate = $reference }
    [IO.File]::WriteAllText(
        (Join-Path $stateRoot "staged-update.json"),
        ($stagedUpdate | ConvertTo-Json -Depth 4),
        ([Text.UTF8Encoding]::new($false)))

    Add-Type -AssemblyName System.IO.Compression
    $netCoreCheckExe = Resolve-OfficialNetCoreCheck $workRoot
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
        "/DNetCoreCheckExe=$netCoreCheckExe" `
        (Join-Path $workRoot "Malco.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup did not create the installer." }
    $privateInstaller = Join-Path $compiledRoot ("Malco-{0}-Setup.exe" -f $releaseVersion)
    if (-not (Test-Path -LiteralPath $privateInstaller -PathType Leaf)) {
        throw "The expected installer executable was not created."
    }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $binding = [ordered]@{
        schema = "malco.installer-binding.v1"
        version = $releaseVersion
        sequence = [long]$manifest.sequence
        installer = Get-InstallerFileIdentity $privateInstaller
        envelope = Get-InstallerFileIdentity $latestPath
        payload = Get-InstallerFileIdentity $archivePath
    }
    $privateBinding = Join-Path $compiledRoot "installer-binding.json"
    [IO.File]::WriteAllText($privateBinding, ($binding | ConvertTo-Json -Depth 8), ([Text.UTF8Encoding]::new($false)))
    Publish-InstallerPair $privateInstaller $privateBinding $expectedInstaller $expectedBinding
    Write-Host "Installer created: $expectedInstaller" -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
