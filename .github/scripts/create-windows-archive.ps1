[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = "Stop"

if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Unsupported package version: $Version"
}

$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$requiredFiles = @(
    "Frelon.Web.exe",
    "LICENSE.txt",
    "LISEZ-MOI.txt",
    "THIRD-PARTY-NOTICES.txt",
    "DOTNET-THIRD-PARTY-NOTICES.txt"
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $requiredFile) -PathType Leaf)) {
        throw "$requiredFile is missing from the package."
    }
}

if (Get-ChildItem -LiteralPath $package -Filter "*.pdb" -Recurse) {
    throw "Debug symbols must not be present in the distributed package."
}

$archiveName = "Frelon-$Version-win-x64.zip"
$archive = Join-Path $output $archiveName
$checksum = "$archive.sha256"
Remove-Item -LiteralPath $archive, $checksum -Force -ErrorAction SilentlyContinue

$created = $false
for ($attempt = 1; $attempt -le 10; $attempt++) {
    try {
        Compress-Archive `
            -Path (Join-Path $package "*") `
            -DestinationPath $archive `
            -CompressionLevel Optimal
        $created = $true
        break
    }
    catch {
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
        if ($attempt -eq 10) {
            throw
        }

        Start-Sleep -Milliseconds 500
    }
}

if (-not $created) {
    throw "The Windows archive could not be created."
}

Add-Type -AssemblyName System.IO.Compression
$zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
try {
    $entryNames = @($zip.Entries | ForEach-Object FullName)
    foreach ($requiredFile in $requiredFiles) {
        if ($entryNames -notcontains $requiredFile) {
            throw "$requiredFile is missing from the generated archive."
        }
    }
}
finally {
    $zip.Dispose()
}

$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $archiveName" |
    Set-Content -LiteralPath $checksum -Encoding ascii

Write-Output "Windows archive created: $archiveName"
