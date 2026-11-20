param(
    [ValidatePattern('\Av?\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z')]
    [string]$Version = "v3.1.1"
)

$ErrorActionPreference = "Stop"

$workspaceRoot = $PSScriptRoot
$publishRoot = [IO.Path]::GetFullPath((Join-Path $workspaceRoot "publish"))
$publishDir = [IO.Path]::GetFullPath((Join-Path $publishRoot $Version))
if (![string]::Equals([IO.Path]::GetDirectoryName($publishDir), $publishRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish directory must be a version folder directly inside publish."
}
foreach ($directory in @($publishRoot, $publishDir)) {
    if ((Test-Path -LiteralPath $directory) -and
        ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to clean a linked publish directory: $directory"
    }
}

Write-Host "Starting build & packaging for version $Version..."
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$rids = @("win-x64", "win-x86", "win-arm64")

foreach ($rid in $rids) {
    Write-Host "`n>>> Publishing $rid..."
    $outDir = Join-Path $publishDir $rid
    
    & "$workspaceRoot\dotnet10.cmd" publish "$workspaceRoot\HorizonRadioOverlay.csproj" `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugSymbols=false `
        -p:DebugType=None `
        -o $outDir

    if ($LASTEXITCODE -ne 0) {
        throw "Publishing $rid failed with exit code $LASTEXITCODE."
    }

    $singleExe = Join-Path $outDir "HorizonRadioOverlay.exe"
    $targetExeName = "HorizonRadioOverlay_${Version}_${rid}.exe"
    $targetExePath = Join-Path $publishDir $targetExeName
    Copy-Item -LiteralPath $singleExe -Destination $targetExePath -Force
    Write-Host "Created single-file: $targetExeName"

    $zipName = "HorizonRadioOverlay_${Version}_${rid}.zip"
    $zipPath = Join-Path $publishDir $zipName
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force
    Write-Host "Created zip package: $zipName"
}

# 复制 Release Notes
$notesFile = Join-Path $workspaceRoot "docs\releases\RELEASE_NOTES_${Version}.md"
if (Test-Path $notesFile) {
    Copy-Item $notesFile (Join-Path $publishDir "RELEASE_NOTES_${Version}.md") -Force
}

# 计算 SHA256 哈希
Write-Host "`nGenerating SHA256 checksums..."
$shaFile = Join-Path $publishDir "SHA256SUMS.txt"
$filesToHash = Get-ChildItem -Path $publishDir -File -Filter "HorizonRadioOverlay_*" | Sort-Object Name
$hashEntries = foreach ($file in $filesToHash) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
$hashEntries | Set-Content -Path $shaFile -Encoding utf8
Write-Host "SHA256SUMS.txt generated successfully."

Write-Host "`nAll packages built successfully in: $publishDir"
Get-ChildItem -Path $publishDir -File | Format-Table Name, Length, LastWriteTime -AutoSize
