using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public static class SwitchServiceProbe {
 [StructLayout(LayoutKind.Sequential,Pack=1)] struct Address {public ushort family;public ulong address;public Guid service;public uint port;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Query {
  public uint size;public IntPtr name,service,version,comment;public uint ns;public IntPtr provider;
  [MarshalAs(UnmanagedType.LPWStr)]public string context;
  public uint protocolCount;public IntPtr protocols,query;public uint addressCount;public IntPtr addresses;public uint flags;public IntPtr blob;
 }
 [DllImport("ws2_32.dll")] static extern int WSAStartup(ushort version,IntPtr data);
 [DllImport("ws2_32.dll")] static extern int WSACleanup();
 [DllImport("ws2_32.dll")] static extern int WSAGetLastError();
 [DllImport("ws2_32.dll",CharSet=CharSet.Unicode)] static extern int WSAAddressToStringW(ref Address address,uint size,IntPtr protocol,StringBuilder text,ref uint length);
 [DllImport("ws2_32.dll",CharSet=CharSet.Unicode)] static extern int WSALookupServiceBeginW(ref Query query,uint flags,out IntPtr handle);
 [DllImport("ws2_32.dll",CharSet=CharSet.Unicode)] static extern int WSALookupServiceNextW(IntPtr handle,uint flags,ref uint length,IntPtr result);
 [DllImport("ws2_32.dll")] static extern int WSALookupServiceEnd(IntPtr handle);
 static bool Lookup(ulong address,ushort service,int index,DailyLog log){
  var native=new Address{family=32,address=address};var context=new StringBuilder(256);uint length=256;
  if(WSAAddressToStringW(ref native,(uint)Marshal.SizeOf(typeof(Address)),IntPtr.Zero,context,ref length)!=0){log.Write("sdp_address_error","target="+index+" wsa="+WSAGetLastError());return false;}
  IntPtr guid=Marshal.AllocHGlobal(16),handle=IntPtr.Zero,buffer=IntPtr.Zero;
  string key="target="+index+" uuid=0x"+service.ToString("X4");
  var timer=System.Diagnostics.Stopwatch.StartNew();
  try{
   Marshal.StructureToPtr(new Guid("0000"+service.ToString("x4")+"-0000-1000-8000-00805f9b34fb"),guid,false);
   var q=new Query{size=(uint)Marshal.SizeOf(typeof(Query)),ns=16,context=context.ToString(),service=guid};
   log.Write("sdp_begin",key+" force_remote=1");
   if(WSALookupServiceBeginW(ref q,0x1000,out handle)!=0){log.Write("sdp_begin_error",key+" wsa="+WSAGetLastError()+" elapsed_ms="+timer.ElapsedMilliseconds);return false;}
   uint capacity=4096;buffer=Marshal.AllocHGlobal((int)capacity);int records=0;
   while(records<32){
    Marshal.Copy(new byte[120],0,buffer,120);Marshal.WriteInt32(buffer,120);
    uint required=capacity;int rc=WSALookupServiceNextW(handle,0x200,ref required,buffer);
    if(rc==0){records++;continue;}
    int error=WSAGetLastError();
    if(error==10014&&required>capacity&&required<=1048576){Marshal.FreeHGlobal(buffer);buffer=IntPtr.Zero;capacity=required;buffer=Marshal.AllocHGlobal((int)capacity);continue;}
    log.Write("sdp_result",key+" records="+records+" terminal_wsa="+error+" elapsed_ms="+timer.ElapsedMilliseconds);return records>0;
   }
   log.Write("sdp_result",key+" records="+records+" capped=1");return records>0;
  }finally{if(handle!=IntPtr.Zero)WSALookupServiceEnd(handle);if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);Marshal.FreeHGlobal(guid);}
 }
 static int Run(DailyLog log,bool enable){
  IntPtr data=Marshal.AllocHGlobal(512);
  try{int rc=WSAStartup(0x202,data);if(rc!=0){log.Write("winsock_init_failed","code="+rc);return 1;}
   try{
    var addresses=BluetoothDiagnostics.SwitchAddresses();log.Write("switch_targets","count="+addresses.Length);
    if(addresses.Length==0){Console.WriteLine("No remembered Nintendo Switch found. Keep the existing Windows pairing.");return 2;}
    if(enable){
     if(addresses.Length!=1){log.Write("enable_refused","reason=multiple_targets");Console.WriteLine("More than one Switch remembered; no settings changed.");return 5;}
     if(!Lookup(addresses[0],0x110a,0,log)){log.Write("enable_refused","reason=no_live_source_record");return 6;}
     bool enabled=BluetoothDiagnostics.EnableSwitchAudio(addresses[0],log);
     Console.WriteLine("Return to PhoneDesk and refresh after a few seconds. Enable success is not audio-stream success.");
     return enabled?0:7;
    }
    for(int i=0;i<Math.Min(addresses.Length,2);i++)foreach(ushort service in new ushort[]{0x110a,0x110d,0x110c,0x110e})Lookup(addresses[i],service,i,log);
    log.Write("probe_completed");return 0;
   }finally{WSACleanup();}
  }finally{Marshal.FreeHGlobal(data);}
 }
 public static int Main(string[] args){
  bool fresh;using(var mutex=new System.Threading.Mutex(true,"Local\\PhoneDeskSwitchServiceProbe",out fresh)){
   if(!fresh){Console.WriteLine("Probe already running. Close the previous probe first.");return 4;}
   return Execute(args);
  }
 }
 static int Execute(string[] args){
  var log=new DailyLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"logs","switch2-service-probe"));
  try{
   BluetoothDiagnostics.SelfTest();
   if(Marshal.SizeOf(typeof(Query))!=120||Marshal.SizeOf(typeof(Address))!=30)throw new Exception("Invalid Winsock ABI");
   if(args.Length>0&&args[0]=="--self-test")return 0;
   bool enable=args.Length==1&&args[0]=="--enable-audio";
   if(args.Length>0&&!enable){Console.WriteLine("Unknown option.");return 1;}
   if(enable){
    Console.WriteLine("This will request Windows to ENABLE the paired Switch Audio Source service.");
    Console.WriteLine("Windows may install its mapped driver. No pairing deletion or third-party download by this tool.");
    Console.WriteLine("Only one remembered Switch and a successful live Audio Source query are allowed.");
    Console.Write("Type ENABLE to continue, or press Enter to cancel: ");
    if(Console.ReadLine()!="ENABLE")return 0;
   }else Console.WriteLine("Switch 2 live SDP service probe. No pairing/driver/settings changes.");
   Console.WriteLine("Keep Switch 2 awake on Bluetooth Audio. Close phone reception first.");
   Console.WriteLine("Querying may take up to 45 seconds. Addresses and names are not saved.");
   log.Write("probe_started","version=1.1 mode="+(enable?"enable":"read_only")+"");
   var task=Task.Run(()=>Run(log,enable));
   if(!task.Wait(45000)){log.Write("probe_timeout","limit_seconds=45");Console.WriteLine("Timed out. Send logs/switch2-service-probe/current.log");Environment.Exit(3);}
   Console.WriteLine("Done. Send logs/switch2-service-probe/current.log");return task.Result;
  }catch(Exception e){log.Error("probe",e);Console.WriteLine("Probe failed. Send logs/switch2-service-probe/current.log");return 1;}
 }
}
