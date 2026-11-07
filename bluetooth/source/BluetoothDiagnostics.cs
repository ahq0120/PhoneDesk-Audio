using System;
using System.Runtime.InteropServices;

// Read cached Windows pairing/service information only. No inquiry or pairing changes.
public static class BluetoothDiagnostics {
 [StructLayout(LayoutKind.Sequential)] struct Time {public ushort a,b,c,d,e,f,g,h;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Device {
  public uint size; public ulong address; public uint deviceClass;
  public int connected,remembered,authenticated;
  public Time lastSeen,lastUsed;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)] public string name;
 }
 [StructLayout(LayoutKind.Sequential)] struct Search {
  public uint size;public int authenticated,remembered,unknown,connected,inquiry;
  public byte timeout; public IntPtr radio;
 }
 [DllImport("bthprops.cpl",SetLastError=true)] static extern IntPtr BluetoothFindFirstDevice(ref Search search,ref Device device);
 [DllImport("bthprops.cpl",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothFindNextDevice(IntPtr find,ref Device device);
 [DllImport("bthprops.cpl")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothFindDeviceClose(IntPtr find);
 [DllImport("bthprops.cpl")] static extern uint BluetoothEnumerateInstalledServices(IntPtr radio,ref Device device,ref uint count,[Out] Guid[] services);
 public static string Flags(Guid[] services,int count){
  bool source=false,sink=false,hid=false;
  for(int i=0;i<count;i++){
   source|=services[i]==new Guid("0000110a-0000-1000-8000-00805f9b34fb");
   sink|=services[i]==new Guid("0000110b-0000-1000-8000-00805f9b34fb");
   hid|=services[i]==new Guid("00001124-0000-1000-8000-00805f9b34fb");
  }
  return "enabled_audio_source="+source+" enabled_audio_sink="+sink+" enabled_hid="+hid;
 }
 public static void Run(DailyLog log){
  log.Write("cached_diagnostics_started");
  var search=new Search{size=(uint)Marshal.SizeOf(typeof(Search)),authenticated=1,remembered=1,connected=1};
  var device=new Device{size=(uint)Marshal.SizeOf(typeof(Device))};
  IntPtr handle=BluetoothFindFirstDevice(ref search,ref device);
  if(handle==IntPtr.Zero){log.Write("cached_devices_unavailable","win32="+Marshal.GetLastWin32Error());return;}
  int index=0;
  try{do{
   bool knownSwitch=(device.name??"").IndexOf("Nintendo Switch",StringComparison.OrdinalIgnoreCase)>=0;
   log.Write("cached_device","index="+index+" switch_name_match="+knownSwitch+" paired="+(device.authenticated!=0)+" connected="+(device.connected!=0)+" class=0x"+device.deviceClass.ToString("X6"));
   uint count=128;var services=new Guid[count];
   uint result=BluetoothEnumerateInstalledServices(IntPtr.Zero,ref device,ref count,services);
   if(result==234 && count<=4096){services=new Guid[count];result=BluetoothEnumerateInstalledServices(IntPtr.Zero,ref device,ref count,services);}
   if(result==0 && count<=services.Length)log.Write("cached_services","index="+index+" count="+count+" "+Flags(services,(int)count));
   else log.Write("cached_services_failed","index="+index+" win32="+result);
   index++;device.size=(uint)Marshal.SizeOf(typeof(Device));
  }while(index<256&&BluetoothFindNextDevice(handle,ref device));
  if(index<256){int error=Marshal.GetLastWin32Error();if(error!=259)log.Write("cached_enumeration_end","win32="+error);}
  }finally{BluetoothFindDeviceClose(handle);}
  log.Write("cached_diagnostics_completed","count="+index);
 }
 public static void SelfTest(){
  if(Marshal.SizeOf(typeof(Device))!=560||Marshal.SizeOf(typeof(Search))!=40)throw new Exception("Invalid x64 Bluetooth ABI layout");
  var flags=Flags(new[]{new Guid("0000110a-0000-1000-8000-00805f9b34fb")},1);
  if(!flags.Contains("enabled_audio_source=True")||!flags.Contains("enabled_audio_sink=False"))throw new Exception("Service classification failed");
 }
 public static ulong[] SwitchAddresses(){
  var result=new System.Collections.Generic.List<ulong>();
  var search=new Search{size=(uint)Marshal.SizeOf(typeof(Search)),authenticated=1,remembered=1};
  var device=new Device{size=(uint)Marshal.SizeOf(typeof(Device))};
  IntPtr handle=BluetoothFindFirstDevice(ref search,ref device);
  if(handle==IntPtr.Zero)return result.ToArray();
  try{int seen=0;do{
   if((device.name??"").IndexOf("Nintendo Switch",StringComparison.OrdinalIgnoreCase)>=0&&!result.Contains(device.address))result.Add(device.address);
   device.size=(uint)Marshal.SizeOf(typeof(Device));
  }while(++seen<256&&BluetoothFindNextDevice(handle,ref device));
  }finally{BluetoothFindDeviceClose(handle);}
  return result.ToArray();
 }
}
