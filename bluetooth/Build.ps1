$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$metadata=Join-Path $PSScriptRoot '.build/contracts/ref/netstandard2.0'
$lock=Get-Content (Join-Path $PSScriptRoot 'contracts.lock.json') -Raw | ConvertFrom-Json
if(!(Test-Path -LiteralPath $metadata)){
 $cache=Join-Path $PSScriptRoot '.build'
 New-Item -ItemType Directory -Path $cache -Force | Out-Null
 $archive=Join-Path $cache 'contracts.zip'
 Invoke-WebRequest -Uri $lock.url -OutFile $archive -UseBasicParsing
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.sha256){throw 'SDK contract checksum mismatch.'}
 Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $cache 'contracts') -Force
}
$argsList=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+(Join-Path $PSScriptRoot 'PhoneDeskBluetooth.exe')),'/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
foreach($dll in @('System.Runtime.dll','System.Runtime.WindowsRuntime.dll','System.Runtime.InteropServices.WindowsRuntime.dll')){$argsList+=('/r:'+(Join-Path $framework $dll))}
foreach($file in Get-ChildItem -LiteralPath $metadata -Filter '*.winmd'){$argsList+=('/r:'+$file.FullName)}
$argsList+=(Join-Path $PSScriptRoot 'source/BluetoothReceiver.cs')
$argsList+=(Join-Path $PSScriptRoot 'source/DailyLog.cs')
$argsList+=('/win32manifest:'+(Join-Path $PSScriptRoot 'source/app.manifest'))
& (Join-Path $framework 'csc.exe') @argsList
if($LASTEXITCODE){throw 'Build failed.'}
