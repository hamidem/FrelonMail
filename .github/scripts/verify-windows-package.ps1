[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string] $SamplePath,

    [Parameter(Mandatory = $true)]
    [string] $ResultsDirectory
)

$ErrorActionPreference = "Stop"

$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$sample = (Resolve-Path -LiteralPath $SamplePath).Path
$executable = Join-Path $package "Frelon.Web.exe"
foreach ($requiredFile in @(
    "Frelon.Web.exe",
    "LICENSE.txt",
    "LISEZ-MOI.txt",
    "THIRD-PARTY-NOTICES.txt",
    "DOTNET-THIRD-PARTY-NOTICES.txt"
)) {
    $requiredPath = Join-Path $package $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "$requiredFile is missing from the package."
    }
}

New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$results = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$dataDirectory = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    "frelon-package-smoke-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $dataDirectory | Out-Null

$listener = [System.Net.Sockets.TcpListener]::new(
    [System.Net.IPAddress]::Loopback,
    0)
$listener.Start()
$port = ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
$listener.Stop()
$baseUri = "http://localhost:$port"

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $executable
$startInfo.WorkingDirectory = $package
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.Arguments = (@(
    "--Frelon:DataDirectory=$dataDirectory"
    "--Frelon:Port=$port"
    "--Frelon:OpenBrowser=false"
) | ForEach-Object {
    '"' + $_.Replace('"', '\"') + '"'
}) -join " "

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $startInfo
$started = $false
$standardOutput = $null
$standardError = $null

try {
    if (-not $process.Start()) {
        throw "The packaged Frelon process did not start."
    }

    $started = $true
    $standardOutput = $process.StandardOutput.ReadToEndAsync()
    $standardError = $process.StandardError.ReadToEndAsync()

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    $ready = $false
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            throw "Packaged Frelon exited before its local API became ready (code $($process.ExitCode))."
        }

        try {
            $null = Invoke-RestMethod `
                -Uri "$baseUri/api/application/info" `
                -TimeoutSec 2
            $ready = $true
            break
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    if (-not $ready) {
        throw "Packaged Frelon did not expose its local API within 45 seconds."
    }

    $incident = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUri/api/incidents/analyze" `
        -Headers @{ "X-Frelon-Filename" = [System.IO.Path]::GetFileName($sample) } `
        -ContentType "message/rfc822" `
        -InFile $sample `
        -TimeoutSec 40
    if ([string]::IsNullOrWhiteSpace([string] $incident.incidentId)) {
        throw "The packaged analysis did not return an incident identifier."
    }

    $stored = Invoke-RestMethod `
        -Uri "$baseUri/api/incidents/$($incident.incidentId)" `
        -TimeoutSec 5
    if ([string] $stored.incidentId -ne [string] $incident.incidentId) {
        throw "The packaged application did not persist the analyzed incident."
    }

    $session = Invoke-RestMethod `
        -Uri "$baseUri/api/application/session" `
        -TimeoutSec 5
    $null = Invoke-RestMethod `
        -Method Post `
        -Uri "$baseUri/api/application/shutdown" `
        -Headers @{ "X-Frelon-Shutdown-Token" = $session.shutdownToken } `
        -TimeoutSec 5

    if (-not $process.WaitForExit(15000)) {
        throw "Packaged Frelon did not stop within 15 seconds."
    }

    if ($process.ExitCode -ne 0) {
        throw "Packaged Frelon stopped with exit code $($process.ExitCode)."
    }

    Write-Output "Packaged analysis succeeded for incident $($incident.incidentId)."
}
finally {
    if ($started -and -not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit()
    }

    if ($null -ne $standardOutput) {
        $standardOutput.GetAwaiter().GetResult() |
            Set-Content -LiteralPath (Join-Path $results "stdout.log") -Encoding UTF8
    }

    if ($null -ne $standardError) {
        $standardError.GetAwaiter().GetResult() |
            Set-Content -LiteralPath (Join-Path $results "stderr.log") -Encoding UTF8
    }

    $process.Dispose()

    $unlockDeadline = [DateTimeOffset]::UtcNow.AddSeconds(10)
    while ($true) {
        try {
            $probe = [System.IO.File]::Open(
                $executable,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::None)
            $probe.Dispose()
            break
        }
        catch [System.IO.IOException] {
            if ([DateTimeOffset]::UtcNow -ge $unlockDeadline) {
                throw "Frelon.Web.exe remained locked after packaged shutdown."
            }

            Start-Sleep -Milliseconds 250
        }
    }

    if (Test-Path -LiteralPath $dataDirectory) {
        Remove-Item -LiteralPath $dataDirectory -Recurse -Force
    }
}
