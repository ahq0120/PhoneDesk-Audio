param([switch]$NoPrompt)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
$started=Get-Date
$since=$started.AddMinutes(-15)
$report=[ordered]@{Version='1.1';Started=$started.ToString('o');Since=$since.ToString('o');Sections=[ordered]@{};Errors=@()}
function Token([string]$s){
 if(!$s){return ''};$sha=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($s.ToUpperInvariant()))).Replace('-','').Substring(0,16)}finally{$sha.Dispose()}
}
function Redact([string]$s){
 foreach($v in @($env:USERNAME,$env:COMPUTERNAME)){if($v -and $v.Length -gt 2){$s=[regex]::Replace($s,[regex]::Escape($v),'[REDACTED]','IgnoreCase')}}
 $s=[regex]::Replace($s,'(?i)(DEV_|BLUETOOTHDEVICE_)[0-9a-f]{12}','$1[ADDRESS]')
 $s=[regex]::Replace($s,'(?i)\b(?:[0-9a-f]{2}[:-]){5}[0-9a-f]{2}\b','[ADDRESS]')
 $s=[regex]::Replace($s,'S-1-5-21-(?:\d+-){2,}\d+','[SID]')
 return $s
}
function Capture([string]$name,[scriptblock]$action){
 Write-Host ('Reading '+$name+' ...')
 try{$report.Sections[$name]=@(& $action)}catch{$report.Errors+=@{Section=$name;Type=$_.Exception.GetType().Name;Message=(Redact $_.Exception.Message);HResult=$_.Exception.HResult}}
}
function Devices {
 Get-CimInstance Win32_PnPEntity -Filter "PNPClass='Bluetooth' OR PNPClass='MEDIA'" -OperationTimeoutSec 20 | ForEach-Object {
  [pscustomobject]@{Token=(Token $_.PNPDeviceID);Switch=($_.Name -match 'Nintendo Switch');Name=$_.Name;Class=$_.PNPClass;Service=$_.Service;Status=$_.Status;Problem=$_.ConfigManagerErrorCode;HardwareIDs=@($_.HardwareID);Instance=$_.PNPDeviceID}
 }
}
Write-Host 'PhoneDesk Switch 2 connection capture (read only)' -ForegroundColor Cyan
Write-Host 'No installs, settings changes, pairing changes, packet capture or uploads.'
Capture 'Before' { Devices }
if(!$NoPrompt){
 Write-Host 'Stop phone reception. On Switch 2, attempt Bluetooth audio connection to the PC.' -ForegroundColor Yellow
 [void](Read-Host 'When Switch 2 succeeds or shows the error, return here and press Enter')
 $report.SwitchMessage=Read-Host 'Optional: type the error shown on Switch 2 (or leave blank)'
}
$report.AfterTime=(Get-Date).ToString('o')
Capture 'After' { Devices }
Capture 'System' {Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 20 | Select-Object Caption,Version,BuildNumber}
Capture 'Drivers' {
 Get-CimInstance Win32_PnPSignedDriver -Filter "DeviceClass='BLUETOOTH' OR DeviceClass='MEDIA'" -OperationTimeoutSec 20 | ForEach-Object {
  [pscustomobject]@{Token=(Token $_.DeviceID);Inf=$_.InfName;Version=$_.DriverVersion;Provider=$_.DriverProviderName;Name=$_.DeviceName;Signed=$_.IsSigned}
 }
}
Capture 'Channels' {
 Get-WinEvent -ListLog '*Bluetooth*','*BTH*','*Kernel-PnP*','*UserPnp*','*DeviceSetupManager*' -ErrorAction SilentlyContinue | Select-Object LogName,IsEnabled,RecordCount
}
Capture 'Events' {
 $logs=@('System')+@($report.Sections.Channels | Where-Object {$_.IsEnabled} | ForEach-Object {$_.LogName})
 foreach($log in @($logs | Select-Object -Unique)){
  try{
   # Filter by provider at the event query, including informational install events.
   $filter=@{LogName=$log;StartTime=$since}
   if($log -eq 'System'){$filter.ProviderName=@('Microsoft-Windows-Kernel-PnP','Microsoft-Windows-UserPnp','BTHUSB','BTHMINI','Microsoft-Windows-DeviceSetupManager')}
   Get-WinEvent -FilterHashtable $filter -MaxEvents 250 -ErrorAction Stop | ForEach-Object {
    [pscustomobject]@{Log=$log;Time=$_.TimeCreated.ToString('o');Id=$_.Id;Provider=$_.ProviderName;Level=$_.Level;Message=$_.Message}
   }
  }catch{[pscustomobject]@{Log=$log;ReadStatus='No matching events or unavailable';HResult=$_.Exception.HResult}}
 }
}
Capture 'SetupAPI' {
 # Keep complete bounded sections containing Bluetooth installation IDs, not the whole log.
 $path=Join-Path $env:WINDIR 'INF/setupapi.dev.log'
 $tail=@(Get-Content -LiteralPath $path -Tail 30000)
 $blocks=New-Object 'Collections.Generic.List[string]'
 $current=New-Object 'Collections.Generic.List[string]'
 foreach($line in $tail){
  if($line -match '^>>>\s+\['){
   if($current.Count){$blocks.Add(($current -join "`n"));$current.Clear()}
  }
  $current.Add($line)
 }
 if($current.Count){$blocks.Add(($current -join "`n"))}
 $selected=@($blocks | Where-Object {$_ -match '(?i)BTHENUM\\|BTHA2DP|bluetooth_a2dp|Nintendo Switch'})
 [pscustomobject]@{Path='Windows/INF/setupapi.dev.log';TailLines=$tail.Count;MatchingSections=$selected.Count;Note='Historical sections may predate this attempt; inspect Section start timestamps.';Sections=@($selected | Select-Object -Last 30 | ForEach-Object {$_.Substring(0,[Math]::Min($_.Length,24000))})}
}
Capture 'ReceiverLog' {
 foreach($p in @(Get-CimInstance Win32_Process -Filter "Name='PhoneDeskBluetooth.exe'" -OperationTimeoutSec 10)){
  if(!$p.ExecutablePath){continue}
  $path=Join-Path (Split-Path $p.ExecutablePath -Parent) 'logs/current.log'
  if((Test-Path -LiteralPath $path) -and (Get-Item -LiteralPath $path).Length -le 2097152){
   # Read plain strings without Get-Content provider metadata.
   [IO.File]::ReadAllLines($path) | Select-Object -Last 1200
   break
  }
 }
}
function Plain($value,[int]$depth=0){
 if($null -eq $value){return $null}
 if($value -is [string]){return [string]::Copy($value)}
 if($value -is [datetime]){return $value.ToString('o')}
 if($value -is [enum] -or $value -is [version]){return $value.ToString()}
 if($value -is [ValueType]){return [Convert]::ChangeType($value,$value.GetType())}
 if($depth -ge 9){return '[depth limit]'}
 if($value -is [Collections.IDictionary]){
  $copy=[ordered]@{};foreach($key in $value.Keys){$copy[[string]$key]=Plain $value[$key] ($depth+1)};return $copy
 }
 if($value -is [Collections.IEnumerable]){return ,@(foreach($item in $value){Plain $item ($depth+1)})}
 $copy=[ordered]@{}
 foreach($property in $value.PSObject.Properties){if($property.IsGettable -and $property.Name -notmatch '^PS'){$copy[$property.Name]=Plain $property.Value ($depth+1)}}
 return $copy
}
Write-Host 'Normalizing report ...'
$plain=Plain $report
$folder=Join-Path $PSScriptRoot ('reports/'+$started.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
# Strip PowerShell provider/ETS metadata before JSON serialization.
Write-Host 'Writing connection.json ...'
$json=Redact ($plain | ConvertTo-Json -Depth 12)
[IO.File]::WriteAllText((Join-Path $folder 'connection.json'),$json,[Text.UTF8Encoding]::new($true))
$summary="Read-only Switch 2 connection report.`r`nEvents start: $($since.ToString('o'))`r`nIncomplete sections: $($report.Errors.Count)`r`nNo events does not prove no Bluetooth negotiation occurred. Disabled channels remain disabled.`r`nSetupAPI is a bounded historical tail, not a complete packet trace.`r`nReview before sharing: device names, file paths and event messages may contain personal data.`r`n"
[IO.File]::WriteAllText((Join-Path $folder 'README.txt'),$summary,[Text.UTF8Encoding]::new($true))
Write-Host 'Compressing report ...'
Compress-Archive -LiteralPath $folder -DestinationPath ($folder+'.zip') -Force
Write-Host ('Completed. Send this ZIP: '+$folder+'.zip') -ForegroundColor Green
