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
 public static ulong[] PairedPhoneSources(){
  var result=new System.Collections.Generic.List<ulong>();
  var search=new Search{size=(uint)Marshal.SizeOf(typeof(Search)),authenticated=1};
  var device=new Device{size=(uint)Marshal.SizeOf(typeof(Device))};IntPtr handle=BluetoothFindFirstDevice(ref search,ref device);
  if(handle==IntPtr.Zero)return result.ToArray();
  try{int seen=0;do{
   if(device.authenticated!=0&&((device.deviceClass>>8)&31)==2){
    uint count=128;var services=new Guid[count];uint status=BluetoothEnumerateInstalledServices(IntPtr.Zero,ref device,ref count,services);
    if(status==0&&count<=services.Length){for(int i=0;i<count;i++)if(services[i]==new Guid("0000110a-0000-1000-8000-00805f9b34fb")&&!result.Contains(device.address))result.Add(device.address);}
   }
   device.size=(uint)Marshal.SizeOf(typeof(Device));
  }while(++seen<256&&BluetoothFindNextDevice(handle,ref device));
  }finally{BluetoothFindDeviceClose(handle);}return result.ToArray();
 }
 [StructLayout(LayoutKind.Sequential)] struct RadioSearch {public uint size;}
 [DllImport("bthprops.cpl",SetLastError=true)] static extern IntPtr BluetoothFindFirstRadio(ref RadioSearch search,out IntPtr radio);
 [DllImport("bthprops.cpl",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothFindNextRadio(IntPtr find,out IntPtr radio);
 [DllImport("bthprops.cpl")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothFindRadioClose(IntPtr find);
 [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool CloseHandle(IntPtr handle);
 [DllImport("bthprops.cpl")] static extern uint BluetoothGetDeviceInfo(IntPtr radio,ref Device device);
 [DllImport("bthprops.cpl")] static extern uint BluetoothSetServiceState(IntPtr radio,ref Device device,ref Guid service,uint flags);
 public static bool EnableSwitchAudio(ulong address,DailyLog log){
  var search=new RadioSearch{size=4};IntPtr radio;
  IntPtr find=BluetoothFindFirstRadio(ref search,out radio);
  if(find==IntPtr.Zero){log.Write("enable_no_radio","win32="+Marshal.GetLastWin32Error());return false;}
  try{do{
   try{
    var device=new Device{size=(uint)Marshal.SizeOf(typeof(Device)),address=address};
    uint read=BluetoothGetDeviceInfo(radio,ref device);
    if(read!=0||device.authenticated==0||(device.name??"").IndexOf("Nintendo Switch",StringComparison.OrdinalIgnoreCase)<0)continue;
    var service=new Guid("0000110a-0000-1000-8000-00805f9b34fb");
    log.Write("enable_audio_source_requested");
    uint result=BluetoothSetServiceState(radio,ref device,ref service,1);
    log.Write("enable_audio_source_result","win32="+result+" hex=0x"+result.ToString("X8"));
    uint count=128;var services=new Guid[count];uint status=BluetoothEnumerateInstalledServices(radio,ref device,ref count,services);
    bool enabled=false;
    if(status==0&&count<=services.Length){for(int i=0;i<count;i++)enabled|=services[i]==service;}
    log.Write("enable_audio_source_check","enumeration_code="+status+" enabled="+enabled);
    Console.WriteLine("Windows result: "+result+"; cached enabled state: "+enabled);
    return enabled;
   }finally{CloseHandle(radio);}
  }while(BluetoothFindNextRadio(find,out radio));
  }finally{BluetoothFindRadioClose(find);}
  log.Write("enable_no_matching_paired_device");return false;
 }
}
