param([switch]$Integration)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$outRoot = Join-Path $repoRoot 'dist/PhoneDesk'
$adapter = Join-Path $outRoot 'resources/app/receiver/AirPlay/UxPlay.exe'
if (-not (Test-Path -LiteralPath $adapter)) { throw 'Run scripts/Build.ps1 first.' }
& $adapter --phonedesk-self-test
if ($LASTEXITCODE -ne 0) { throw 'Audio argument/quoting tests failed.' }
& (Join-Path $outRoot 'resources/app/receiver/AirPlay/UxPlay-original.exe') --phonedesk-audio-self-test
if ($LASTEXITCODE -ne 0) { throw 'Native queue/flush/timestamp tests failed.' }
if ($Integration) {
    $process = Start-Process -FilePath (Join-Path $outRoot 'PhoneDesk.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw ('Receiver integration test failed: ' + $process.ExitCode) }
    Get-Content -LiteralPath (Join-Path $outRoot 'validation.txt')
}
