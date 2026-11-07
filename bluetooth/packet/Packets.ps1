param([ValidateSet('Start','Stop')][string]$Mode='Start',[ValidateRange(3,90)][int]$Seconds=60)
$ErrorActionPreference='Stop'
$session='PhoneDeskSwitch2Packets'
$admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if(!$admin){Write-Host 'Right-click Run-Packets.cmd and choose Run as administrator.' -ForegroundColor Yellow;exit 2}
if($Mode -eq 'Stop'){
 & logman.exe stop $session
 & logman.exe delete $session
 Write-Host 'Stopped/deleted only PhoneDeskSwitch2Packets. Other trace sessions were not touched.'
 exit
}
$folder=Join-Path $PSScriptRoot ('reports/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
function Native([string[]]$Arguments,[switch]$AllowFailure){
 $info=New-Object Diagnostics.ProcessStartInfo
 $info.FileName="$env:WINDIR/System32/logman.exe"
 $info.Arguments=(@($Arguments | ForEach-Object {'"'+$_+'"'}) -join ' ')
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true
 $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $encoding=[Text.Encoding]::GetEncoding([Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage)
 $info.StandardOutputEncoding=$encoding;$info.StandardErrorEncoding=$encoding
 $p=New-Object Diagnostics.Process
 $p.StartInfo=$info
 try{
  [void]$p.Start()
  $stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
  if(!$p.WaitForExit(20000)){$p.Kill();$p.WaitForExit();throw 'logman timed out after 20 seconds.'}
  $code=$p.ExitCode
  $text=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
  Add-Content -LiteralPath (Join-Path $folder 'commands.txt') -Value (($Arguments -join ' ')+' => '+$code+"`r`n"+$text) -Encoding UTF8
  if($code -ne 0 -and !$AllowFailure){throw ('logman failed: '+$code+' '+$text)}
  return [int]$code
 }finally{$p.Dispose()}
}
$created=$false;$running=$false
try{
 if((Native @('query',$session) -AllowFailure) -eq 0){throw 'PhoneDesk trace already exists. Run Stop-Packets.cmd as administrator first; other sessions are left alone.'}
 [void](Native @('query','providers','Microsoft-Windows-BTH-BTHPORT'))
 $providers=Join-Path $PSScriptRoot 'providers.txt'
 $etl=Join-Path $folder 'bluetooth-packets.etl'
 # Managed collector duration continues to apply if this console closes unexpectedly.
 $duration=[TimeSpan]::FromSeconds($Seconds).ToString('hh\:mm\:ss')
 [void](Native @('create','trace',$session,'-pf',$providers,'-o',$etl,'-f','bincirc','-max','32','-rf',$duration,'-ow'))
 $created=$true
 [void](Native @('start',$session));$running=$true
 Write-Host 'PACKET TRACE STARTED. Stop phone audio reception, then try Switch 2 Bluetooth audio connection NOW.' -ForegroundColor Green
 Write-Host ('Recording for '+$Seconds+' seconds. Leave this window open; no key press is required.')
 $watch=[Diagnostics.Stopwatch]::StartNew();$last=-1
 while($watch.Elapsed.TotalSeconds -lt $Seconds){
  $left=[Math]::Ceiling($Seconds-$watch.Elapsed.TotalSeconds)
  if($left -ne $last -and $left%10 -eq 0){Write-Host ($left.ToString()+' seconds remaining ...');$last=$left}
  Start-Sleep -Milliseconds 200
 }
}catch{
 $_.Exception.Message | Set-Content -LiteralPath (Join-Path $folder 'ERROR.txt') -Encoding UTF8
 Write-Host $_.Exception.Message -ForegroundColor Red
}finally{
 if($running){try{[void](Native @('stop',$session) -AllowFailure)}catch{Write-Host ('Stop failed: '+$_.Exception.Message) -ForegroundColor Red}}
 if($created){try{[void](Native @('delete',$session) -AllowFailure)}catch{Write-Host ('Cleanup failed: '+$_.Exception.Message) -ForegroundColor Red}}
}
$files=@(Get-ChildItem -LiteralPath $folder -Filter '*.etl')
if(!$files.Count){Write-Host ('No ETL produced. Send ERROR.txt / commands.txt from '+$folder);exit 1}
@('HCI/L2CAP protocol capture via Microsoft-Windows-BTH-BTHPORT.',
 'Provider 8A1F9517-3A8C-4A9E-A018-4F17A200F277; keyword mask 0x6000000000000000; level 4.',
 'This differs from the earlier WPP/service-only trace. Convert with Microsoft BTETLParse, not network etl2pcapng.',
 'HCI RAW keyword 0x8000000000000000 is excluded. No SSP debug or Full Packet Logging setting is enabled.',
 'Default packet redaction/truncation may omit useful data; a nonempty ETL is not proof it contains the failed negotiation.',
 'Raw ETL is NOT anonymized and can contain addresses, device names, protocol traffic and other private data.',
 'Do not publish on GitHub. Disconnect unrelated phone/headphone audio and do not type passwords during capture.',
 ('Requested duration: '+$Seconds+' seconds; circular file limit 32 MB.'),
 'Sources: https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/testing-btp-tools-btetlparse',
 'https://github.com/microsoft/busiotools/blob/master/bluetooth/tracing/BluetoothStack.wprp') | Set-Content -LiteralPath (Join-Path $folder 'README.txt') -Encoding UTF8
Write-Host 'Compressing report ...'
Compress-Archive -LiteralPath $folder -DestinationPath ($folder+'.zip') -Force
Write-Host ('Completed. Send this ZIP privately: '+$folder+'.zip') -ForegroundColor Green
