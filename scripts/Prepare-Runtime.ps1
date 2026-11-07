$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$cacheRoot = Join-Path $repoRoot '.cache'
$runtimeOut = Join-Path $repoRoot 'dist/PhoneDesk/resources/app/receiver'
$lock = Get-Content (Join-Path $repoRoot 'runtime.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
function Get-CheckedArtifact($entry) {
    $artifact = Join-Path $cacheRoot $entry.file
    if (-not (Test-Path -LiteralPath $artifact)) { Invoke-WebRequest -Uri $entry.url -OutFile $artifact -UseBasicParsing }
    if ((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw ('SHA-256 mismatch: ' + $entry.file) }
    return $artifact
}
$archive = Get-CheckedArtifact $lock.imirror
$unpacked = Join-Path $cacheRoot 'imirror'
Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
$receiverSource = Join-Path $unpacked 'iMirror'
if (-not (Test-Path -LiteralPath (Join-Path $receiverSource 'iMirror.exe'))) { throw 'Unexpected upstream release layout.' }
New-Item -ItemType Directory -Path $runtimeOut -Force | Out-Null
Copy-Item -Path (Join-Path $receiverSource '*') -Destination $runtimeOut -Recurse -Force
$airplay = Join-Path $runtimeOut 'AirPlay'
Copy-Item -LiteralPath (Join-Path $airplay 'UxPlay.exe') -Destination (Join-Path $airplay 'UxPlay-original.exe') -Force
foreach ($entry in $lock.plugins) {
    $archive = Get-CheckedArtifact $entry
    $packageDir = Join-Path $cacheRoot $entry.plugin
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    & tar.exe -xf $archive -C $packageDir
    if ($LASTEXITCODE -ne 0) { throw 'Could not extract pinned MSYS2 package.' }
    Copy-Item -LiteralPath (Join-Path $packageDir ('ucrt64/lib/gstreamer-1.0/' + $entry.plugin)) -Destination (Join-Path $airplay 'lib/gstreamer-1.0') -Force
}
Write-Output 'Pinned receiver and audio plugins prepared.'
