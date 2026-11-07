$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /platform:x64 /optimize+ ('/out:'+(Join-Path $PSScriptRoot 'SwitchServiceProbe.exe')) (Join-Path $PSScriptRoot 'SwitchServiceProbe.cs') (Join-Path $PSScriptRoot 'SdpSummary.cs') (Join-Path $root 'source/BluetoothDiagnostics.cs') (Join-Path $root 'source/DailyLog.cs')
if($LASTEXITCODE){throw 'Probe build failed.'}
