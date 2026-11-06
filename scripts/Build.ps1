$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Prepare-Runtime.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x x64 C# compiler is required.' }
$outRoot = Join-Path $repoRoot 'dist/PhoneDesk'
$receiver = Join-Path $outRoot 'resources/app/receiver'
$airplay = Join-Path $receiver 'AirPlay'
& $compiler /nologo /optimize+ /target:exe /platform:x64 ('/out:' + (Join-Path $airplay 'UxPlay.exe')) (Join-Path $repoRoot 'src/PhoneDeskAudioBridge.cs')
if ($LASTEXITCODE -ne 0) { throw 'Audio adapter build failed.' }
& $compiler /nologo /optimize+ /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/out:' + (Join-Path $outRoot 'PhoneDesk.exe')) (Join-Path $repoRoot 'src/PhoneDeskLite.cs')
if ($LASTEXITCODE -ne 0) { throw 'PhoneDesk build failed.' }
$manifest = [ordered]@{}
foreach ($relative in @('AirPlay/UxPlay.exe','AirPlay/UxPlay-original.exe','AirPlay/lib/gstreamer-1.0/libgstwasapi.dll','AirPlay/lib/gstreamer-1.0/libgstlevel.dll')) {
    $manifest[$relative] = (Get-FileHash -LiteralPath (Join-Path $receiver $relative) -Algorithm SHA256).Hash.ToLowerInvariant()
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $receiver 'phonedesk-audio-manifest.json'), ($manifest | ConvertTo-Json), $utf8)
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD-PARTY.md') -Destination (Join-Path $receiver 'PHONEDESK-AUDIO-NOTICE.md') -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'src') -Destination $outRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $outRoot -Force
Write-Output ('Build ready: ' + (Join-Path $outRoot 'PhoneDesk.exe'))
