$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Build.ps1')
foreach($mode in @('--diagnostics-test','--log-test','--self-test','--smoke-test')){
 $p=Start-Process -FilePath (Join-Path $PSScriptRoot 'PhoneDeskBluetooth.exe') -ArgumentList $mode -WindowStyle Hidden -PassThru
 if(!$p.WaitForExit(15000)){throw ('Test timeout: '+$mode)}
 if($p.ExitCode){throw ('Test failed: '+$mode+' exit='+$p.ExitCode)}
 Write-Output ('PASS '+$mode)
}
Write-Output 'Switch 2 pairing, audio reception and latency still require physical hardware testing.'
