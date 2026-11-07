# Offline comparison only. No radio access, pairing, driver changes or networking.
$ErrorActionPreference='Stop'
if(!('PhoneDeskNativeSdpComparison' -as [type])){
 Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PhoneDeskNativeSdpComparison {
 [UnmanagedFunctionPointer(CallingConvention.Winapi)]
 [return:MarshalAs(UnmanagedType.Bool)] delegate bool Callback(uint id,IntPtr value,uint size,IntPtr state);
 [DllImport("bthprops.cpl",SetLastError=true)]
 [return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothSdpEnumAttributes(byte[] data,uint size,Callback callback,IntPtr state);
 public static string Compare(byte[] data) {
  bool handle=false;int count=0;
  Callback callback=(id,value,size,state)=>{++count;if(id==0)handle=true;return true;};
  bool result=BluetoothSdpEnumAttributes(data,(uint)data.Length,callback,IntPtr.Zero);
  int error=result?0:Marshal.GetLastWin32Error();GC.KeepAlive(callback);
  return "parse_ok="+result+" win32="+error+" handle_present="+handle+" attributes="+count;
 }
}
'@
}
function Bytes([string]$hex){return [byte[]]@(for($i=0;$i -lt $hex.Length;$i+=2){[Convert]::ToByte($hex.Substring($i,2),16)})}
# Numeric service descriptors only; no name, address, serial or captured media.
$missing=Bytes '360030090001350319110a0900043510350619010009001935061900190901030900093508350619110d090104090311090000'
$body=[byte[]]$missing[3..($missing.Length-1)]
$withHandle=[byte[]](@(0x36,0,0x38)+@(9,0,0,10,0,1,0,1)+$body)
$a=[PhoneDeskNativeSdpComparison]::Compare($missing)
$b=[PhoneDeskNativeSdpComparison]::Compare($withHandle)
if($a -notmatch 'parse_ok=True.*handle_present=False' -or $b -notmatch 'parse_ok=True.*handle_present=True'){throw 'Native parser comparison differs from expected behavior; investigate before interpreting.'}
'Without handle: '+$a
'With handle:    '+$b
'Both records parse in the PUBLIC SDP attribute API. This does not test BthEnum PnP enumeration or prove why Windows refused AVDTP.'
