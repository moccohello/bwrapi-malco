function Resolve-RequiredFile {
    param([string]$Path, [string]$Label)

    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "$Label was not found: $resolved"
    }
    return $resolved
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
