[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TagName,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $AssetDirectory,

    [switch] $ValidateOnly
)

$ErrorActionPreference = "Stop"

if ($TagName -ne "v$Version") {
    throw "Release tag '$TagName' does not match package version '$Version'."
}

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Unsupported release version: $Version"
}

$assets = (Resolve-Path -LiteralPath $AssetDirectory).Path
$archiveName = "Frelon-$Version-win-x64.zip"
$archive = Join-Path $assets $archiveName
$checksum = "$archive.sha256"
foreach ($path in @($archive, $checksum)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required release asset is missing: $path"
    }
}

$checksumLine = (Get-Content -LiteralPath $checksum -Raw).Trim()
$checksumParts = $checksumLine -split '\s+', 2
$checksumIsInvalid = $checksumParts.Count -ne 2 -or
    $checksumParts[0] -notmatch '^[0-9a-fA-F]{64}$' -or
    $checksumParts[1] -ne $archiveName
if ($checksumIsInvalid) {
    throw "The SHA-256 file has an invalid format or filename."
}

$actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $checksumParts[0].ToLowerInvariant()) {
    throw "The release archive does not match its SHA-256 file."
}

if ($ValidateOnly) {
    Write-Output "Release assets validated for $TagName."
    return
}

if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    throw "GH_TOKEN is required to create the draft release."
}

if (Test-Path variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$existingDraft = & gh release view $TagName `
    --json isDraft `
    --jq '.isDraft' 2>$null
$viewExitCode = $LASTEXITCODE
if ($viewExitCode -eq 0) {
    if ($existingDraft.Trim() -ne "true") {
        throw "Release $TagName is already published and will not be modified."
    }

    & gh release upload $TagName $archive $checksum --clobber
    if ($LASTEXITCODE -ne 0) {
        throw "The assets of draft release $TagName could not be updated."
    }

    Write-Output "Draft release $TagName updated."
    return
}

$ghArguments = @(
    "release",
    "create",
    $TagName,
    $archive,
    $checksum,
    "--draft",
    "--verify-tag",
    "--generate-notes",
    "--title",
    "Frelon $Version"
)
if ($Version.Contains("-")) {
    $ghArguments += "--prerelease"
}

& gh @ghArguments
if ($LASTEXITCODE -ne 0) {
    throw "Draft release $TagName could not be created."
}

Write-Output "Draft release $TagName created."
