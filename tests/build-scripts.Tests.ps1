param([string]$DotnetHost = (Get-Command dotnet -ErrorAction Stop).Source)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $repo 'build-packages.ps1'

# Intercept mutation commands even if validation regresses; never exercise deletion on the repository.
foreach ($version in @('..', '..\..', '*', 'v1/../../', 'v1.2.3\child', '')) {
    $result = & {
        param($path, $candidate)
        function Remove-Item { throw 'MUTATION_REACHED' }
        function New-Item { throw 'MUTATION_REACHED' }
        try { & $path -Version $candidate; 'ACCEPTED' }
        catch { $_.Exception.Message }
    } $scriptPath $version
    if (($result -join '') -match 'MUTATION_REACHED|ACCEPTED') {
        throw "Unsafe version reached packaging: '$version'"
    }
}

$oldDotnetRoot = $env:DOTNET_ROOT
try {
    $env:DOTNET_ROOT = Split-Path -Parent $DotnetHost
    $wrapper = Join-Path $repo 'dotnet10.cmd'
    $missingDll = Join-Path $repo ('artifacts\missing-' + [guid]::NewGuid().ToString('N') + '.dll')
    & $DotnetHost exec $missingDll 2>&1 | Out-Null
    $expectedFailure = $LASTEXITCODE
    & $wrapper exec $missingDll 2>&1 | Out-Null
    if ($expectedFailure -eq 0 -or $LASTEXITCODE -ne $expectedFailure) {
        throw "Wrapper did not propagate failure: expected $expectedFailure, got $LASTEXITCODE"
    }
    & $wrapper --version | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Wrapper did not propagate success.' }

    # Check the PATH lookup branch independently of DOTNET_ROOT.
    $oldPath = $env:PATH
    try {
        $env:DOTNET_ROOT = $null
        $env:PATH = (Split-Path -Parent $DotnetHost) + ';' + $oldPath
        & $wrapper exec $missingDll 2>&1 | Out-Null
        if ($LASTEXITCODE -ne $expectedFailure) { throw 'PATH lookup lost the exit code.' }
    }
    finally { $env:PATH = $oldPath }
}
finally { $env:DOTNET_ROOT = $oldDotnetRoot }

$fixture = Join-Path $repo ('artifacts\packaging-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Copy-Item -LiteralPath $scriptPath -Destination (Join-Path $fixture 'build-packages.ps1')
Set-Content -LiteralPath (Join-Path $fixture 'dotnet10.cmd') -Value "@exit /b 37" -Encoding ascii
try {
    & (Join-Path $fixture 'build-packages.ps1') -Version v1.2.3
    throw 'Publishing failure was ignored.'
}
catch {
    if ($_.Exception.Message -notmatch 'Publishing win-x64 failed with exit code 37') { throw }
}
if (Test-Path -LiteralPath (Join-Path $fixture 'publish\v1.2.3\SHA256SUMS.txt')) {
    throw 'Failed publish produced a success manifest.'
}
Set-Content -LiteralPath (Join-Path $fixture 'dotnet10.cmd') -Encoding ascii -Value @'
@echo off
:parse
if "%~1"=="" exit /b 1
if "%~1"=="-o" goto output
shift
goto parse
:output
if not exist "%~2" mkdir "%~2"
echo simulated executable>"%~2\HorizonRadioOverlay.exe"
exit /b 0
'@
& (Join-Path $fixture 'build-packages.ps1') -Version v1.2.3-test
$successDir = Join-Path $fixture 'publish\v1.2.3-test'
foreach ($rid in @('win-x64', 'win-x86', 'win-arm64')) {
    foreach ($extension in @('exe', 'zip')) {
        if (!(Test-Path -LiteralPath (Join-Path $successDir "HorizonRadioOverlay_v1.2.3-test_${rid}.$extension"))) {
            throw "Missing packaged artifact: $rid.$extension"
        }
    }
}
if (@(Get-Content -LiteralPath (Join-Path $successDir 'SHA256SUMS.txt')).Count -ne 6) {
    throw 'Successful publish did not produce all six checksums.'
}
Write-Host 'PASS: version boundaries, both wrapper paths, exit codes, failed publish, and packaging all three architectures with simulated executables.'
