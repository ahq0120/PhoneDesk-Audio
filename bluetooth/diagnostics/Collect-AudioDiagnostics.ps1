param([ValidateRange(1,30)][int]$SampleSeconds=10,[switch]$NoPrompt)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$script:report=[ordered]@{Version='1.0';Created=(Get-Date).ToString('o');Sections=[ordered]@{};Errors=@()}
function Capture([string]$Name,[scriptblock]$Action){
 Write-Host ('Checking '+$Name+' ...')
 try{$script:report.Sections[$Name]=@(& $Action)}catch{$script:report.Errors+=@{Section=$Name;Type=$_.Exception.GetType().FullName;HResult=$_.Exception.HResult;Message=$_.Exception.Message}}
}
function Token([string]$Value){
 if(!$Value){return $null}
 $sha=[Security.Cryptography.SHA256]::Create()
 try{return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value.ToUpperInvariant()))).Replace('-','').Substring(0,16))}finally{$sha.Dispose()}
}
function Plain($Value,[int]$Depth=0){
 if($null -eq $Value){return $null}
 if($Value -is [datetime]){return $Value.ToString('o')}
 if($Value -is [version] -or $Value -is [enum]){return $Value.ToString()}
 if($Value -is [string]){return [string]::Copy($Value)}
 if($Value -is [ValueType]){return [Convert]::ChangeType($Value,$Value.GetType())}
 if($Depth -ge 8){return '[depth limit]'}
 if($Value -is [Collections.IDictionary]){
  $copy=[ordered]@{};foreach($key in $Value.Keys){$copy[[string]$key]=Plain $Value[$key] ($Depth+1)};return $copy
 }
 if($Value -is [Collections.IEnumerable]){
  $items=@(foreach($item in $Value){Plain $item ($Depth+1)});return ,$items
 }
 $copy=[ordered]@{}
 foreach($property in $Value.PSObject.Properties){if($property.IsGettable -and $property.Name -notmatch '^PS'){
  try{$copy[$property.Name]=Plain $property.Value ($Depth+1)}catch{$copy[$property.Name]='unavailable'}
 }}
 return $copy
}
function Sanitize([string]$Text){
 foreach($s in @($env:USERNAME,$env:COMPUTERNAME)){if($s -and $s.Length -gt 2){$Text=[regex]::Replace($Text,[regex]::Escape($s),'[REDACTED]','IgnoreCase')}}
 $Text=[regex]::Replace($Text,'(?i)\b(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b','[BT-ADDRESS]')
 $Text=[regex]::Replace($Text,'(?i)(DEV_|BLUETOOTHDEVICE_)[0-9a-f]{12}','$1[BT-ADDRESS]')
 $Text=[regex]::Replace($Text,'S-1-5-21-(?:\d+-){2,}\d+','[USER-SID]')
 return $Text
}
Write-Host 'PhoneDesk Bluetooth / Windows audio diagnostics (read only)' -ForegroundColor Cyan
Write-Host 'No settings changes, installs, pairing changes, network upload or audio recording.'
Write-Host 'Keep PhoneDesk open and play music on the phone, with audio output set to this PC.'
Write-Host 'For the meter test, pause other PC music/videos so they do not obscure the result.'
if(!$NoPrompt){[void](Read-Host 'Press Enter when ready')}
$reports=Join-Path $PSScriptRoot 'reports'
$folder=Join-Path $reports ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
Capture 'System' {
 $os=Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 20
 $cv=Get-ItemProperty 'HKLM:/SOFTWARE/Microsoft/Windows NT/CurrentVersion'
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
 $principal=New-Object Security.Principal.WindowsPrincipal($identity)
 [pscustomobject]@{OS=$os.Caption;Build=$os.BuildNumber;DisplayVersion=$cv.DisplayVersion;UBR=$cv.UBR;Architecture=$os.OSArchitecture;PowerShell=$PSVersionTable.PSVersion.ToString();Is64BitProcess=[Environment]::Is64BitProcess;Elevated=$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator);TimeZone=[TimeZoneInfo]::Local.Id}
}
Capture 'Services' {
 Get-CimInstance Win32_Service -OperationTimeoutSec 20 | Where-Object {$_.Name -match '^(Audiosrv|AudioEndpointBuilder|bthserv|BthAvctpSvc|BTAGService|BluetoothUserService.*)$'} | Select-Object Name,State,StartMode,ExitCode
}
Capture 'Devices' {
 Get-CimInstance Win32_PnPEntity -Filter "PNPClass='Bluetooth' OR PNPClass='MEDIA' OR PNPClass='AudioEndpoint'" -OperationTimeoutSec 20 | ForEach-Object {
  $endpointToken=$null;if($_.PNPDeviceID -like 'SWD\MMDEVAPI\*'){$endpointToken=Token ($_.PNPDeviceID -replace '^SWD\\MMDEVAPI\\','')}
  [pscustomobject]@{Class=$_.PNPClass;Name=$_.Name;Status=$_.Status;Present=$_.Present;InstanceToken=(Token $_.PNPDeviceID);EndpointToken=$endpointToken;ProblemCode=$_.ConfigManagerErrorCode;Service=$_.Service;Manufacturer=$_.Manufacturer}
 }
}
Capture 'Drivers' {
 Get-CimInstance Win32_PnPSignedDriver -Filter "DeviceClass='BLUETOOTH' OR DeviceClass='MEDIA'" -OperationTimeoutSec 20 | Select-Object @{n='InstanceToken';e={Token $_.DeviceID}},DeviceName,DriverProviderName,DriverVersion,DriverDate,InfName,IsSigned,Signer
}
Capture 'PhoneDeskProcesses' {
 Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -match '^(PhoneDesk.*|audiodg)$'} | Select-Object ProcessName,Id,Responding,StartTime
}
Capture 'EndpointsAndMeters' {
 if(!('AudioProbe' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'AudioProbe.cs')}
 Write-Host ('Observing endpoint meters for '+$SampleSeconds+' seconds ...') -ForegroundColor Cyan
 $endpoints=[AudioProbe]::Collect($SampleSeconds)
 foreach($e in $endpoints){
  $name='unknown';$properties=@{}
  if($e.Id -match '(\{[a-fA-F0-9-]+\})$'){
   $branch=if($e.Flow -eq 'Render'){'Render'}else{'Capture'}
   $path='HKLM:/SOFTWARE/Microsoft/Windows/CurrentVersion/MMDevices/Audio/'+$branch+'/'+$Matches[1]
   try{
    $p=Get-ItemProperty -LiteralPath ($path+'/Properties')
    foreach($key in @('{a45c254e-df1c-4efd-8020-67d146a850e0},14','{b3f8fa53-0004-438e-9003-51a46e139bfc},6')){if($p.PSObject.Properties[$key]){$properties[$key]=$p.$key}}
    if($p.PSObject.Properties['{a45c254e-df1c-4efd-8020-67d146a850e0},14']){$name=$p.'{a45c254e-df1c-4efd-8020-67d146a850e0},14'}
   }catch{}
  }
  $endpointToken=Token $e.Id
  if($name -eq 'unknown'){$match=@($script:report.Sections.Devices | Where-Object {$_.EndpointToken -eq $endpointToken -and $endpointToken});if($match.Count){$name=$match[0].Name}}
  [pscustomobject]@{Name=$name;EndpointToken=$endpointToken;Flow=$e.Flow;State=$e.State;StateMeaning='1=active 2=disabled 4=notpresent 8=unplugged';DefaultRoles=$e.DefaultRoles;Muted=$e.Muted;Volume=$e.Volume;PeakMax=$e.PeakMax;PeakSamples=$e.PeakSamples;ActiveSamples=$e.ActiveSamples;Sessions=$e.Sessions;Error=$e.Error;Properties=$properties}
 }
}
Capture 'PairedBluetoothServices' {
 if(!('BluetoothDiagnostics' -as [type])){Add-Type -Path @((Join-Path $PSScriptRoot 'DailyLog.cs'),(Join-Path $PSScriptRoot 'BluetoothDiagnostics.cs'))}
 $log=New-Object DailyLog($folder)
 [BluetoothDiagnostics]::SelfTest();[BluetoothDiagnostics]::Run($log)
 if($log.LastError){throw ('Native diagnostic log failed: '+$log.LastError)}
 $native=Join-Path $folder 'current.log'
 if(Test-Path -LiteralPath $native){Get-Content -LiteralPath $native}
}
Capture 'BluetoothUserPolicies' {
 foreach($path in @('HKLM:/SOFTWARE/Policies/Microsoft/Windows/Bluetooth','HKLM:/SOFTWARE/Microsoft/PolicyManager/current/device/Bluetooth','HKCU:/SOFTWARE/Microsoft/Multimedia/Audio')){
  if(Test-Path $path){$values=Get-ItemProperty $path;[pscustomobject]@{Key=$path;Values=@($values.PSObject.Properties | Where-Object {$_.Name -notmatch '^PS' -and $_.Value -is [ValueType]} | ForEach-Object {@{Name=$_.Name;Value=$_.Value}})}}
 }
}
Capture 'EventChannels' {
 Get-WinEvent -ListLog '*Bluetooth*','*Audio*' -ErrorAction SilentlyContinue | Select-Object LogName,IsEnabled,RecordCount,LastWriteTime
}
$since=(Get-Date).AddHours(-6)
Capture 'RecentSystemEvents' {
 Get-WinEvent -FilterHashtable @{LogName='System';StartTime=$since;Level=@(1,2,3)} -MaxEvents 800 -ErrorAction Stop | Where-Object {$_.ProviderName -match 'Bluetooth|BTH|Audio|Kernel-PnP|UserPnp|DriverFrameworks'} | Select-Object -First 100 TimeCreated,Id,LevelDisplayName,ProviderName,@{n='Message';e={if($_.Message){$_.Message.Substring(0,[Math]::Min(2000,$_.Message.Length))}}}
}
Capture 'RecentApplicationEvents' {
 Get-WinEvent -FilterHashtable @{LogName='Application';StartTime=$since;Level=@(1,2,3)} -MaxEvents 300 -ErrorAction Stop | Where-Object {$_.Message -match 'PhoneDesk|audiodg|Bluetooth'} | Select-Object -First 40 TimeCreated,Id,ProviderName,@{n='Message';e={if($_.Message){$_.Message.Substring(0,[Math]::Min(2000,$_.Message.Length))}}}
}
Capture 'BluetoothAudioEvents' {
 $channels=@($script:report.Sections.EventChannels | Where-Object {$_.IsEnabled} | Select-Object -First 6)
 foreach($channel in $channels){
  try{Get-WinEvent -FilterHashtable @{LogName=$channel.LogName;StartTime=$since} -MaxEvents 30 -ErrorAction Stop | Select-Object TimeCreated,Id,ProviderName,@{n='Message';e={if($_.Message){$_.Message.Substring(0,[Math]::Min(1500,$_.Message.Length))}}}}catch{[pscustomobject]@{Channel=$channel.LogName;ReadStatus='no events or unavailable';ErrorCode=$_.Exception.HResult}}
 }
}
Capture 'PhoneDeskLog' {
 $candidates=@((Join-Path $PSScriptRoot '../logs/current.log'),(Join-Path $PSScriptRoot '../../logs/current.log'))
 foreach($process in @(Get-Process -Name 'PhoneDeskBluetooth' -ErrorAction SilentlyContinue)){try{if($process.Path){$candidates+=Join-Path (Split-Path $process.Path -Parent) 'logs/current.log'}}catch{}}
 foreach($file in $candidates){if((Test-Path -LiteralPath $file) -and (Get-Item -LiteralPath $file).Length -le 2097152){Get-Content -LiteralPath $file -Tail 1500;break}}
}
Write-Host 'Writing report ...'
$plainReport=[ordered]@{Version=$script:report.Version;Created=$script:report.Created;Sections=[ordered]@{};Errors=(Plain $script:report.Errors)}
foreach($key in $script:report.Sections.Keys){$plainReport.Sections[$key]=Plain $script:report.Sections[$key]}
$xml=Sanitize ([Management.Automation.PSSerializer]::Serialize($plainReport,10))
[IO.File]::WriteAllText((Join-Path $folder 'diagnostics.clixml'),$xml,[Text.UTF8Encoding]::new($true))
$summary=New-Object Collections.Generic.List[string]
$summary.Add('PhoneDesk Bluetooth diagnostics — read-only snapshot')
$summary.Add('No settings were changed. No audio was recorded. No data was uploaded.')
$summary.Add('Endpoint meter activity may include ANY PC sound, not just phone audio.')
$summary.Add('Cached Bluetooth services are not a complete live SDP capability scan.')
$summary.Add('Audio sessions are a point-in-time snapshot; a missing session is not proof of failure.')
$summary.Add('Sections collected: '+($script:report.Sections.Keys -join ', '))
foreach($e in @($script:report.Sections.EndpointsAndMeters)){
 if($e.DefaultRoles.Count -gt 0){$summary.Add(('Default '+$e.Flow+': '+$e.Name+' muted='+$e.Muted+' volume='+$e.Volume+' peak='+$e.PeakMax))}
 foreach($session in $e.Sessions){if($session.Muted -or $session.Volume -eq 0){$summary.Add('Muted/zero session: '+$session.Process+' on '+$e.Name)}}
}
foreach($failure in $script:report.Errors){$summary.Add('Incomplete section: '+$failure.Section+' / '+$failure.Type+' / '+$failure.Message)}
$summary.Add('Review report contents before sharing: device/app display names and event text can still contain personal information.')
[IO.File]::WriteAllText((Join-Path $folder 'SUMMARY.txt'),(Sanitize ($summary -join [Environment]::NewLine)),[Text.UTF8Encoding]::new($true))
# The native diagnostic contains only flags/counts, but sanitize it consistently.
$native=Join-Path $folder 'current.log';if(Test-Path $native){[IO.File]::WriteAllText($native,(Sanitize ([IO.File]::ReadAllText($native))),[Text.UTF8Encoding]::new($true))}
$zip=$folder+'.zip'
Compress-Archive -LiteralPath $folder -DestinationPath $zip -Force
Write-Host ('Completed. Send this ZIP: '+$zip) -ForegroundColor Green
Write-Host ('Partial/unavailable sections: '+$script:report.Errors.Count)
