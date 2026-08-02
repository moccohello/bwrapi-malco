[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [string]$BwrApiPackageDirectory,

    [Parameter(Mandatory)]
    [ValidateLength(1, 128)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [string]$OutputArchive
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Resolve-RequiredFile {
    param([string]$Path, [string]$Label)

    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Label was not found: $resolved"
    }
    return $resolved
}

function Get-Sha256 {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RelativePath {
    param([string]$Root, [string]$Path)

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]'\/')
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The path is outside the payload root: $fullPath"
    }
    return $fullPath.Substring($rootPath.Length).TrimStart([char[]]'\/').Replace('\', '/')
}

function Assert-ExactProperties {
    param([object]$Object, [string[]]$Names, [string]$Label)

    $actual = @($Object.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count -or
        @($actual | Where-Object { $_ -cnotin $Names }).Count -ne 0 -or
        @($Names | Where-Object { $_ -cnotin $actual }).Count -ne 0) {
        throw "$Label does not have the closed schema."
    }
}

function Assert-SameFile {
    param([string]$Source, [string]$Destination, [string]$Label)

    if (-not (Test-Path -LiteralPath $Destination -PathType Leaf) -or
        (Get-Item -LiteralPath $Source).Length -ne (Get-Item -LiteralPath $Destination).Length -or
        (Get-Sha256 $Source) -cne (Get-Sha256 $Destination)) {
        throw "$Label does not match the publish output."
    }
}

function Read-BwrApiPackageIdentity {
    param([string]$PackageRoot)

    $nuspec = Get-ChildItem -LiteralPath $PackageRoot -File -Filter "*.nuspec" |
        Select-Object -First 1
    if ($null -eq $nuspec) {
        throw "The BWRAPI package metadata was not found: $PackageRoot"
    }
    [xml]$document = Get-Content -LiteralPath $nuspec.FullName -Raw
    $metadata = $document.package.metadata
    if ($null -eq $metadata -or [string]$metadata.id -cne "BwrApi.Client" -or
        [string]::IsNullOrWhiteSpace([string]$metadata.version)) {
        throw "The BWRAPI package metadata has an unsupported identity."
    }
    return [ordered]@{
        id = "BwrApi.Client"
        version = [string]$metadata.version
    }
}

function Get-SourceRevision {
    param([string]$Version)

    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $git) {
        $revision = & $git.Source -C $PSScriptRoot rev-parse HEAD 2>$null
        $gitExit = $LASTEXITCODE
        if ($gitExit -eq 0 -and -not [string]::IsNullOrWhiteSpace([string]$revision)) {
            return ([string]$revision).Trim()
        }
    }
    return "local-$Version"
}

function Write-GeneratedBom {
    param(
        [string]$StagingRoot,
        [string]$Version,
        [string]$BwrApiVersion,
        [string]$RuntimeConfigPath,
        [string]$MalcoAssemblyPath
    )

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) {
        throw ".NET SDK was not found while creating the package BOM."
    }
    $dotnetSdk = & $dotnet.Source --version 2>$null
    $dotnetExit = $LASTEXITCODE
    if ($dotnetExit -ne 0 -or [string]::IsNullOrWhiteSpace([string]$dotnetSdk)) {
        throw "The .NET SDK version could not be read while creating the package BOM."
    }

    $runtimeConfig = Get-Content -LiteralPath $RuntimeConfigPath -Raw | ConvertFrom-Json
    $frameworks = @($runtimeConfig.runtimeOptions.frameworks | ForEach-Object {
        [ordered]@{
            name = [string]$_.name
            minimum_version = [string]$_.version
        }
    })
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($MalcoAssemblyPath).Version.ToString()
    $payload = @(Get-ChildItem -LiteralPath $StagingRoot -File -Recurse |
        ForEach-Object {
            $path = Get-RelativePath $StagingRoot $_.FullName
            if ($path -ne "MALCO-PACKAGE-BOM.json" -and $path -ne "SHA256SUMS.txt") {
                [ordered]@{
                    path = $path
                    length = [long]$_.Length
                    sha256 = Get-Sha256 $_.FullName
                }
            }
        } | Sort-Object path)
    $bom = [ordered]@{
        schema_version = 2
        package_label = $Version
        assembly_version = $assemblyVersion
        informational_version = $Version
        runtime_identifier = "win-x64"
        dotnet_sdk = ([string]$dotnetSdk).Trim()
        deployment = [ordered]@{
            mode = "framework-dependent"
            roll_forward = "LatestPatch"
            frameworks = $frameworks
        }
        bwrapi_package = "BwrApi.Client/$BwrApiVersion"
        source_revision = Get-SourceRevision $Version
        payload = $payload
        integrity_note = "SHA-256 values are generated from this candidate payload."
    }
    $bom | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $StagingRoot "MALCO-PACKAGE-BOM.json") -Encoding UTF8
}

$publishRoot = [IO.Path]::GetFullPath($PublishDirectory)
if (-not (Test-Path -LiteralPath $publishRoot -PathType Container)) {
    throw "dotnet publish output was not found: $publishRoot"
}
$packageInput = [IO.Path]::GetFullPath($BwrApiPackageDirectory)
if (-not (Test-Path -LiteralPath $packageInput)) {
    throw "BWRAPI package was not found: $packageInput"
}
$outputPath = [IO.Path]::GetFullPath($OutputArchive)
if (Test-Path -LiteralPath $outputPath) {
    throw "The candidate payload already exists: $outputPath"
}
$outputParent = Split-Path -Parent $outputPath
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null

$requiredFiles = @(
    "Malco.exe",
    "Malco.dll",
    "Malco.runtimeconfig.json",
    "Malco.Telemetry.dll",
    "telemetry-policy.json",
    "BwrApi.Client.dll",
    "MALCO-PACKAGE-BOM.json",
    "RUNTIME-CONTRACT.json",
    "LICENSE.txt"
)
$runtimeFiles = @(
    "bwrapi/runtime/win-x64/bwrapi_runtime.dll",
    "bwrapi/runtime/win-x64/LICENSE.runtime.txt",
    "bwrapi/runtime/win-x64/THIRD_PARTY_NOTICES.md"
)

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("MalcoPayload-{0}" -f [Guid]::NewGuid().ToString("N"))
$stagingRoot = Join-Path $tempRoot "payload"
New-Item -ItemType Directory -Path $tempRoot, $stagingRoot -Force | Out-Null
try {
    $packageRoot = $packageInput
    if (Test-Path -LiteralPath $packageInput -PathType Leaf) {
        $packageRoot = Join-Path $tempRoot "bwrapi-package"
        New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($packageInput, $packageRoot)
    }
    elseif (-not (Test-Path -LiteralPath $packageInput -PathType Container)) {
        throw "BWRAPI package input must be a directory or .nupkg file."
    }
    $packageIdentity = Read-BwrApiPackageIdentity $packageRoot
    $managedPackageFiles = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse -Filter "BwrApi.Client.dll")
    if ($managedPackageFiles.Count -ne 1) {
        throw "The BWRAPI package must contain exactly one managed BwrApi.Client.dll."
    }
    $managedPackageFile = $managedPackageFiles[0].FullName

    foreach ($item in @(Get-ChildItem -LiteralPath $publishRoot -Force -Recurse)) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "The publish output contains a reparse point: $($item.FullName)"
        }
        if ($item.PSIsContainer) { continue }
        $relative = Get-RelativePath $publishRoot $item.FullName
        $destination = Join-Path $stagingRoot ($relative.Replace('/', '\'))
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $item.FullName -Destination $destination
    }

    $runtimeSourceCandidates = @(
        (Join-Path $packageRoot "bwrapi\runtime\win-x64"),
        (Join-Path $packageRoot "runtime\win-x64"),
        (Join-Path $packageRoot "contentFiles\any\any\bwrapi\runtime\win-x64")
    )
    $runtimeSource = $runtimeSourceCandidates | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ "bwrapi_runtime.dll") -PathType Leaf
    } | Select-Object -First 1
    if ($null -eq $runtimeSource) {
        throw "The BWRAPI package does not contain the win-x64 runtime."
    }
    $runtimeDestination = Join-Path $stagingRoot "bwrapi\runtime\win-x64"
    New-Item -ItemType Directory -Path $runtimeDestination -Force | Out-Null
    foreach ($name in @("bwrapi_runtime.dll", "LICENSE.runtime.txt", "THIRD_PARTY_NOTICES.md")) {
        $source = Resolve-RequiredFile (Join-Path $runtimeSource $name) "BWRAPI runtime file"
        $destination = Join-Path $runtimeDestination $name
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            Assert-SameFile $source $destination "BWRAPI runtime file"
        }
        else {
            Copy-Item -LiteralPath $source -Destination $destination
        }
    }

    $runtimeContractSource = Resolve-RequiredFile (Join-Path $PSScriptRoot "packaging\runtime-contract.json") "Runtime contract"
    $licenseSource = Resolve-RequiredFile (Join-Path $PSScriptRoot "LICENSE") "Malco license"
    $runtimeContractDestination = Join-Path $stagingRoot "RUNTIME-CONTRACT.json"
    if (-not (Test-Path -LiteralPath $runtimeContractDestination -PathType Leaf)) {
        Copy-Item -LiteralPath $runtimeContractSource -Destination $runtimeContractDestination
    }
    $licenseDestination = Join-Path $stagingRoot "LICENSE.txt"
    if (-not (Test-Path -LiteralPath $licenseDestination -PathType Leaf)) {
        Copy-Item -LiteralPath $licenseSource -Destination $licenseDestination
    }
    $bomDestination = Join-Path $stagingRoot "MALCO-PACKAGE-BOM.json"
    if (-not (Test-Path -LiteralPath $bomDestination -PathType Leaf)) {
        Write-GeneratedBom `
            $stagingRoot `
            $Version `
            ([string]$packageIdentity.version) `
            (Join-Path $stagingRoot "Malco.runtimeconfig.json") `
            (Join-Path $stagingRoot "Malco.dll")
    }

    foreach ($path in $requiredFiles + $runtimeFiles) {
        $null = Resolve-RequiredFile (Join-Path $stagingRoot ($path.Replace('/', '\'))) "Payload file"
    }

    $bom = Get-Content -LiteralPath (Join-Path $stagingRoot "MALCO-PACKAGE-BOM.json") -Raw | ConvertFrom-Json
    $expectedBwrApiPackage = "BwrApi.Client/$([string]$packageIdentity.version)"
    Assert-ExactProperties $bom @(
        "schema_version", "package_label", "assembly_version", "informational_version",
        "runtime_identifier", "dotnet_sdk", "deployment", "bwrapi_package",
        "source_revision", "payload", "integrity_note"
    ) "Malco package BOM"
    if ([int]$bom.schema_version -ne 2 -or
        [string]$bom.package_label -cne $Version -or
        [string]$bom.informational_version -cne $Version -or
        [string]$bom.runtime_identifier -cne "win-x64" -or
        [string]$bom.bwrapi_package -cne $expectedBwrApiPackage -or
        [string]::IsNullOrWhiteSpace([string]$bom.source_revision)) {
        throw "The Malco package BOM does not match the candidate version."
    }
    Assert-ExactProperties $bom.deployment @("mode", "roll_forward", "frameworks") "BOM deployment"
    if ([string]$bom.deployment.mode -cne "framework-dependent" -or
        [string]$bom.deployment.roll_forward -cne "LatestPatch") {
        throw "The BOM deployment is not framework-dependent LatestPatch."
    }

    $runtimeContract = Get-Content -LiteralPath (Join-Path $stagingRoot "RUNTIME-CONTRACT.json") -Raw | ConvertFrom-Json
    Assert-ExactProperties $runtimeContract @(
        "schema_version", "package", "runtime_path", "process_access_policy", "redistribution_notices"
    ) "Runtime contract"
    if ([int]$runtimeContract.schema_version -ne 2 -or
        [string]$runtimeContract.package -cne "BwrApi.Client" -or
        [string]$runtimeContract.runtime_path -cne $runtimeFiles[0] -or
        [string]$runtimeContract.process_access_policy -cne "read-query-only") {
        throw "The runtime contract has an unsupported identity."
    }
    $noticeSet = @($runtimeContract.redistribution_notices | ForEach-Object { [string]$_ }) | Sort-Object
    $expectedNotices = @($runtimeFiles[1], $runtimeFiles[2]) | Sort-Object
    if (($noticeSet -join "|") -cne ($expectedNotices -join "|")) {
        throw "The runtime contract notice set is incomplete."
    }

    $runtimeConfig = Get-Content -LiteralPath (Join-Path $stagingRoot "Malco.runtimeconfig.json") -Raw | ConvertFrom-Json
    Assert-ExactProperties $runtimeConfig.runtimeOptions @("tfm", "rollForward", "frameworks", "configProperties") "Malco runtime configuration"
    if ([string]$runtimeConfig.runtimeOptions.tfm -cne "net10.0" -or
        [string]$runtimeConfig.runtimeOptions.rollForward -cne "LatestPatch") {
        throw "Malco.runtimeconfig.json is not the expected framework-dependent configuration."
    }

    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $stagingRoot "BwrApi.Client.dll"))
    if ($assemblyName.Name -cne "BwrApi.Client") {
        throw "The managed BWRAPI payload is not BwrApi.Client.dll."
    }
    Assert-SameFile $managedPackageFile (Join-Path $stagingRoot "BwrApi.Client.dll") "Managed BWRAPI package file"

    $expectedPaths = @(Get-ChildItem -LiteralPath $stagingRoot -File -Recurse | ForEach-Object {
        Get-RelativePath $stagingRoot $_.FullName
    } | Sort-Object)
    $declaredPaths = @($bom.payload | ForEach-Object { [string]$_.path } | Sort-Object)
    if (($declaredPaths -join "|") -ne (($expectedPaths | Where-Object { $_ -ne "MALCO-PACKAGE-BOM.json" -and $_ -ne "SHA256SUMS.txt" }) -join "|")) {
        throw "The BOM payload list does not match the candidate files."
    }
    foreach ($identity in @($bom.payload)) {
        $path = [string]$identity.path
        $file = Join-Path $stagingRoot ($path.Replace('/', '\'))
        if ((Get-Item -LiteralPath $file).Length -ne [long]$identity.length -or
            (Get-Sha256 $file) -cne [string]$identity.sha256) {
            throw "The BOM digest does not match: $path"
        }
    }

    $stream = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($file in @(Get-ChildItem -LiteralPath $stagingRoot -File -Recurse | Sort-Object FullName)) {
            $entryName = Get-RelativePath $stagingRoot $file.FullName
            $entry = $archive.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
            $input = [IO.File]::OpenRead($file.FullName)
            $output = $entry.Open()
            try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
        }
    }
    finally {
        $archive.Dispose()
        $stream.Dispose()
    }
    Write-Output $outputPath
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
