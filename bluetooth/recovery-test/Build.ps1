$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$logSource=Join-Path $PSScriptRoot 'source/DailyLog.cs'
if(!(Test-Path -LiteralPath $logSource)){$logSource=Join-Path $PSScriptRoot '../source/DailyLog.cs'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll (('/out:')+(Join-Path $PSScriptRoot 'PhoneDeskRecoveryTest.exe')) (Join-Path $PSScriptRoot 'source/RecoveryTest.cs') $logSource
if($LASTEXITCODE){throw 'Build failed'}
