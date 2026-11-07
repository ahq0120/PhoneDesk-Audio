param([ValidateSet('All','Computer','Board','Bluetooth','Windows')][string]$Section='All',[string]$OutputFile)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
function Plain($v){
 if($null -eq $v){return $null}
 if($v -is [string]){return [string]::Copy($v)}
 if($v -is [datetime]){return $v.ToString('o')}
 if($v -is [ValueType]){return [Convert]::ChangeType($v,$v.GetType())}
 if($v -is [Collections.IDictionary]){$d=[ordered]@{};foreach($k in $v.Keys){$d[[string]$k]=Plain $v[$k]};return $d}
 if($v -is [Collections.IEnumerable]){return ,@(foreach($x in $v){Plain $x})}
 $d=[ordered]@{};foreach($p in $v.PSObject.Properties){if($p.Name -notmatch '^PS'){$d[$p.Name]=Plain $p.Value}};return $d
}
function SaveJson($path,$value){[IO.File]::WriteAllText($path,((Plain $value) | ConvertTo-Json -Depth 9),[Text.UTF8Encoding]::new($true))}
if($Section -ne 'All'){
 try{
  $data=switch($Section){
   'Computer' {Get-CimInstance Win32_ComputerSystem -OperationTimeoutSec 10 | ForEach-Object {[pscustomobject]@{Manufacturer=$_.Manufacturer;Model=$_.Model;SystemFamily=$_.SystemFamily}}}
   'Board' {Get-CimInstance Win32_BaseBoard -OperationTimeoutSec 10 | ForEach-Object {[pscustomobject]@{Manufacturer=$_.Manufacturer;Product=$_.Product;Version=$_.Version}}}
   'Windows' {Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 10 | ForEach-Object {[pscustomobject]@{Caption=$_.Caption;Version=$_.Version;BuildNumber=$_.BuildNumber;Architecture=$_.OSArchitecture}}}
   'Bluetooth' {
    $radios=@(Get-CimInstance Win32_PnPEntity -Filter "PNPClass='Bluetooth'" -OperationTimeoutSec 10 | Where-Object {$_.PNPDeviceID -match '^(USB|PCI)\\'})
    $drivers=@(Get-CimInstance Win32_PnPSignedDriver -Filter "DeviceClass='BLUETOOTH'" -OperationTimeoutSec 10)
    foreach($radio in $radios){
     $driver=$drivers | Where-Object {$_.DeviceID -eq $radio.PNPDeviceID} | Select-Object -First 1
     $ids=@($radio.HardwareID | ForEach-Object {
      # Model/revision identifiers only; never export the unique instance/serial suffix.
      if($_ -match '^(USB\\VID_[0-9A-F]{4}&PID_[0-9A-F]{4}(?:&REV_[0-9A-F]{4})?(?:&MI_[0-9A-F]{2})?)$'){$Matches[1].ToUpperInvariant()}
     } | Select-Object -Unique)
     $joined=$ids -join ' ';$vid=$pidValue=$revision=$null
     if($joined -match 'VID_([0-9A-F]{4})'){$vid=$Matches[1]}
     if($joined -match 'PID_([0-9A-F]{4})'){$pidValue=$Matches[1]}
     if($joined -match 'REV_([0-9A-F]{4})'){$revision=$Matches[1]}
     [pscustomobject]@{Name=$radio.Name;ReportedManufacturer=$radio.Manufacturer;Bus=($radio.PNPDeviceID -split '\\')[0];UsbVendorId=$vid;UsbProductId=$pidValue;UsbDeviceRevision=$revision;HardwareIds=$ids;Status=$radio.Status;ProblemCode=$radio.ConfigManagerErrorCode;Service=$radio.Service;DriverProvider=$driver.DriverProviderName;DriverVersion=$driver.DriverVersion;DriverDate=$driver.DriverDate;InfName=$driver.InfName;DriverSigned=$driver.IsSigned;RetailBrandModel='Not verified; chipset identifiers do not establish retail brand/model.'}
    }
   }
  }
  SaveJson $OutputFile ([ordered]@{Status='OK';Data=@($data)})
  exit 0
 }catch{SaveJson $OutputFile ([ordered]@{Status='Failed';ErrorType=$_.Exception.GetType().Name;HResult=$_.Exception.HResult});exit 1}
}
Write-Host 'PhoneDesk hardware information (read only)' -ForegroundColor Cyan
Write-Host 'No installs, driver changes, Bluetooth connections or uploads.'
Write-Host 'Computer/board serial numbers and paired-device lists are not collected.'
$folder=Join-Path $PSScriptRoot ('reports/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$report=[ordered]@{Version='1.0';Created=(Get-Date).ToString('o');Sections=[ordered]@{}}
foreach($name in @('Computer','Board','Bluetooth','Windows')){
 Write-Host ('Reading '+$name+' ...')
 $file=Join-Path $folder ($name+'.json')
 $info=New-Object Diagnostics.ProcessStartInfo
 $info.FileName="$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe"
 $info.Arguments='-NoLogo -NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -Section '+$name+' -OutputFile "'+$file+'"'
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $process=New-Object Diagnostics.Process;$process.StartInfo=$info
 try{
  [void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
  if(!$process.WaitForExit(18000)){$process.Kill();$process.WaitForExit();$report.Sections[$name]=@{Status='Timeout';Seconds=18};continue}
  if(Test-Path -LiteralPath $file){$report.Sections[$name]=Get-Content -LiteralPath $file -Raw -Encoding UTF8 | ConvertFrom-Json}
  else{$report.Sections[$name]=@{Status='Failed';ExitCode=$process.ExitCode}}
 }catch{$report.Sections[$name]=@{Status='Failed';ErrorType=$_.Exception.GetType().Name}}
 finally{$process.Dispose()}
}
SaveJson (Join-Path $folder 'hardware.json') $report
$summary=New-Object 'Collections.Generic.List[string]'
$summary.Add('PhoneDesk hardware information - read-only report')
foreach($name in @('Computer','Board','Bluetooth','Windows')){
 $s=$report.Sections[$name];$summary.Add('');$summary.Add('['+$name+'] '+$s.Status)
 if($s.Status -eq 'OK'){
  foreach($item in $s.Data){foreach($p in $item.PSObject.Properties){if($p.Name -notmatch '^PS'){$summary.Add($p.Name+': '+($p.Value -join ', '))}}}
 }
}
$summary.Add('');$summary.Add('Retail adapter brand/model may require a photo of the adapter/package.')
$summary.Add('USB REV is a device revision field, not a verified retail hardware/PCB revision.')
$summary.Add('No serial numbers, paired-device names, Bluetooth addresses or captured audio were requested.')
[IO.File]::WriteAllText((Join-Path $folder 'SUMMARY.txt'),($summary -join "`r`n"),[Text.UTF8Encoding]::new($true))
Write-Host 'Compressing report ...'
Compress-Archive -LiteralPath $folder -DestinationPath ($folder+'.zip') -Force
Write-Host ('Completed. Send this ZIP: '+$folder+'.zip') -ForegroundColor Green
