function Resolve-RequiredFile {
    param([string]$Path, [string]$Label)

    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Label was not found: $resolved"
    }
    return $resolved
}

function Copy-Utf8JsonContract {
    param([string]$Source, [string]$Destination, [string]$Label)

    $bytes = [IO.File]::ReadAllBytes($Source)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $stripped = New-Object byte[] ($bytes.Length - 3)
        [Array]::Copy($bytes, 3, $stripped, 0, $stripped.Length)
        $bytes = $stripped
    }
    if ($bytes.Length -eq 0 -or $bytes[0] -ne 0x7B) {
        throw "$Label must be a UTF-8 JSON object."
    }
    [IO.File]::WriteAllBytes($Destination, $bytes)
}

function Assert-ChildPath {
    param([string]$Parent, [string]$Child)

    $prefix = [IO.Path]::GetFullPath($Parent).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    $candidate = [IO.Path]::GetFullPath($Child)
    if (-not $candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to operate outside the installer staging root: $candidate"
    }
}

function Assert-ExactProperties {
    param([object]$Object, [string[]]$Names, [string]$Label)

    $actual = @($Object.PSObject.Properties.Name)
    if ($actual.Count -ne $Names.Count -or
        @($actual | Where-Object { $_ -cnotin $Names }).Count -ne 0 -or
        @($Names | Where-Object { $_ -cnotin $actual }).Count -ne 0) {
        throw "$Label does not have the closed release schema."
    }
}

function Read-DesktopRuntimeContract {
    param([string]$Path)

    $contract = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    Assert-ExactProperties $contract @(
        "schema", "framework", "architecture", "minimum_version",
        "file_name", "download_url", "sha256", "sha512"
    ) "Desktop runtime contract"
    if ([string]$contract.schema -cne "malco.dotnet-desktop-prerequisite.v1" -or
        [string]$contract.framework -cne "Microsoft.WindowsDesktop.App" -or
        [string]$contract.architecture -cne "x64" -or
        [string]$contract.minimum_version -cnotmatch '^10\.0\.\d+$' -or
        [string]$contract.file_name -cnotmatch '^windowsdesktop-runtime-10\.0\.\d+-win-x64\.exe$' -or
        [string]$contract.sha256 -cnotmatch '^[0-9a-f]{64}$' -or
        [string]$contract.sha512 -cnotmatch '^[0-9a-f]{128}$') {
        throw "The Desktop runtime contract contains an unsupported identity."
    }
    $downloadUri = [Uri]::new([string]$contract.download_url, [UriKind]::Absolute)
    $expectedUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/{0}/{1}" -f
        [string]$contract.minimum_version, [string]$contract.file_name
    if ($downloadUri.AbsoluteUri -cne $expectedUrl) {
        throw "The Desktop runtime contract download URL is not the exact Microsoft build URL."
    }
    return $contract
}

function Assert-DesktopRuntimeInstaller {
    param([string]$Path, [object]$Contract)

    if ([IO.Path]::GetFileName($Path) -cne [string]$Contract.file_name -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -cne [string]$Contract.sha256 -or
        (Get-FileHash -LiteralPath $Path -Algorithm SHA512).Hash.ToLowerInvariant() -cne [string]$Contract.sha512) {
        throw "The Desktop runtime installer does not match its approved identity."
    }
}

function Resolve-OfficialNetCoreCheck {
    param([string]$StagingRoot)

    $nupkgPath = Join-Path $StagingRoot "Microsoft.NET.Tools.NETCoreCheck.x64.7.0.0.nupkg"
    Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.net.tools.netcorecheck.x64/7.0.0/microsoft.net.tools.netcorecheck.x64.7.0.0.nupkg" -OutFile $nupkgPath
    $destination = Join-Path $StagingRoot "NetCoreCheck.exe"
    $archiveStream = [IO.File]::OpenRead($nupkgPath)
    $zip = [IO.Compression.ZipArchive]::new($archiveStream, [IO.Compression.ZipArchiveMode]::Read, $false)
    try {
        $entry = $null
        foreach ($candidate in $zip.Entries) {
            if (($candidate.FullName -replace '\\', '/') -ceq "win-x64/NetCoreCheck.exe") {
                $entry = $candidate
                break
            }
        }
        if ($null -eq $entry) {
            throw "The Microsoft NETCoreCheck package did not contain win-x64/NetCoreCheck.exe."
        }
        $input = $entry.Open()
        try {
            $output = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $input.CopyTo($output); $output.Flush($true) } finally { $output.Dispose() }
        }
        finally { $input.Dispose() }
    }
    finally {
        $zip.Dispose()
        $archiveStream.Dispose()
    }
    return $destination
}

function Read-ReleaseManifest {
    param([string]$Path)

    $envelope = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string]$envelope.schema -cne "malco.signed-release-envelope.v1" -or
        [string]::IsNullOrWhiteSpace([string]$envelope.signed)) {
        throw "The signed release envelope has an unsupported shape."
    }
    try {
        return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([string]$envelope.signed)) | ConvertFrom-Json
    }
    catch {
        throw "The signed release envelope contains invalid manifest data."
    }
}

function Get-ReleaseManifestSha256 {
    param([string]$Path)

    $envelope = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ([string]$envelope.schema -cne "malco.signed-release-envelope.v1" -or
        [string]::IsNullOrWhiteSpace([string]$envelope.signed)) {
        throw "The signed release envelope has an unsupported shape."
    }
    try {
        $signedBytes = [Convert]::FromBase64String([string]$envelope.signed)
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha256.ComputeHash($signedBytes))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    catch {
        throw "The signed release envelope contains invalid manifest data."
    }
}
