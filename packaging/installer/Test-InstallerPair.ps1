[CmdletBinding()]
param()
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "InstallerPackaging.Helpers.ps1")

function Expect-Rejection([scriptblock]$Action, [string]$Label) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw "Expected rejection: $Label" }
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("malco-installer-pair-" + [Guid]::NewGuid().ToString("N"))
$privateRoot = Join-Path $fixtureRoot "private"
$outputRoot = Join-Path $fixtureRoot "output"
$reservation = $null
New-Item -ItemType Directory -Path $privateRoot, $outputRoot | Out-Null
try {
    $privateInstaller = Join-Path $privateRoot "Setup.exe"
    $privateBinding = Join-Path $privateRoot "Setup.binding.json"
    $installer = Join-Path $outputRoot "Setup.exe"
    $binding = Join-Path $outputRoot "Setup.binding.json"
    [IO.File]::WriteAllText($privateInstaller, "this-attempt-installer")
    $identity = Get-InstallerFileIdentity $privateInstaller

    # A missing binding causes the second real File.Move to fail after the
    # installer move succeeds. Rollback must permit the exact same pair retry.
    Expect-Rejection { Publish-InstallerPair $privateInstaller $privateBinding $installer $binding } "second-file publication failure"
    if ((Test-Path $installer) -or (Test-Path $binding) -or (Test-Path ($installer + ".publish.lock"))) {
        throw "Failed pair publication left an output artifact or reservation."
    }
    $rolledBack = Get-InstallerFileIdentity $privateInstaller
    if ($rolledBack.length -ne $identity.length -or $rolledBack.sha256 -cne $identity.sha256) {
        throw "Rollback did not preserve this attempt's installer."
    }

    [IO.File]::WriteAllText($privateBinding, "this-attempt-binding")
    # An existing other writer's binding is never removed or overwritten.
    [IO.File]::WriteAllText($binding, "other-writer-binding")
    Expect-Rejection { Publish-InstallerPair $privateInstaller $privateBinding $installer $binding } "preexisting binding"
    if ([IO.File]::ReadAllText($binding) -cne "other-writer-binding" -or (Test-Path $installer)) {
        throw "Pair rejection changed another writer's output."
    }
    [IO.File]::Delete($binding)

    $reservation = [IO.File]::Open($installer + ".publish.lock", [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    Expect-Rejection { Publish-InstallerPair $privateInstaller $privateBinding $installer $binding } "another active writer"
    if (-not (Test-Path ($installer + ".publish.lock")) -or (Test-Path $installer)) {
        throw "A competing writer changed the reserved pair."
    }
    $reservation.Dispose()
    $reservation = $null
    [IO.File]::Delete($installer + ".publish.lock")

    Publish-InstallerPair $privateInstaller $privateBinding $installer $binding
    if ([IO.File]::ReadAllText($installer) -cne "this-attempt-installer" -or
        [IO.File]::ReadAllText($binding) -cne "this-attempt-binding") {
        throw "Retry did not publish the exact pair."
    }
    [IO.File]::WriteAllText($privateInstaller, "different-installer")
    [IO.File]::WriteAllText($privateBinding, "different-binding")
    Expect-Rejection { Publish-InstallerPair $privateInstaller $privateBinding $installer $binding } "preexisting complete pair"
    if ([IO.File]::ReadAllText($installer) -cne "this-attempt-installer" -or
        [IO.File]::ReadAllText($binding) -cne "this-attempt-binding") {
        throw "An existing complete output pair was changed."
    }
    Write-Output "PASS: second-file failure rollback and retry, existing outputs preserved, competing writer excluded"
}
finally {
    if ($null -ne $reservation) { $reservation.Dispose() }
    Assert-ChildPath ([IO.Path]::GetTempPath()) $fixtureRoot
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
}
