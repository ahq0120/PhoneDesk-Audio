using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

public static class AudioProbe {
 [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator {}
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEnum {
  [PreserveSig]int Endpoints(int flow,uint state,out ICollection list);
  [PreserveSig]int Default(int flow,int role,out IDevice device);
  [PreserveSig]int Device([MarshalAs(UnmanagedType.LPWStr)]string id,out IDevice device);
  [PreserveSig]int Register(IntPtr callback);[PreserveSig]int Unregister(IntPtr callback);
 }
 [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ICollection {
  [PreserveSig]int Count(out uint count);[PreserveSig]int Item(uint index,out IDevice device);
 }
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
  [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr param,[MarshalAs(UnmanagedType.IUnknown)]out object value);
  [PreserveSig]int Properties(uint access,out IntPtr store);
  [PreserveSig]int Id([MarshalAs(UnmanagedType.LPWStr)]out string value);
  [PreserveSig]int State(out uint value);
 }
 [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IVolume {
  [PreserveSig]int Register(IntPtr p);[PreserveSig]int Unregister(IntPtr p);[PreserveSig]int Channels(out uint count);
  [PreserveSig]int SetDb(float value,IntPtr context);[PreserveSig]int SetScalar(float value,IntPtr context);
  [PreserveSig]int GetDb(out float value);[PreserveSig]int Scalar(out float value);
  [PreserveSig]int SetChannelDb(uint c,float v,IntPtr p);[PreserveSig]int SetChannelScalar(uint c,float v,IntPtr p);
  [PreserveSig]int ChannelDb(uint c,out float v);[PreserveSig]int ChannelScalar(uint c,out float v);
  [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool value,IntPtr context);
  [PreserveSig]int Mute([MarshalAs(UnmanagedType.Bool)]out bool value);
 }
 [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMeter {
  [PreserveSig]int Peak(out float value);[PreserveSig]int Channels(out uint count);
  [PreserveSig]int Peaks(uint count,IntPtr values);[PreserveSig]int Support(out uint flags);
 }
 [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IManager {
  [PreserveSig]int Control(IntPtr id,uint flags,out IntPtr control);[PreserveSig]int Volume(IntPtr id,uint flags,out IntPtr volume);
  [PreserveSig]int Sessions(out ISessions sessions);
 }
 [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessions {
  [PreserveSig]int Count(out int count);[PreserveSig]int Item(int i,out IControl control);
 }
 [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IControl {
  [PreserveSig]int State(out int state);[PreserveSig]int Name([MarshalAs(UnmanagedType.LPWStr)]out string value);
  [PreserveSig]int SetName([MarshalAs(UnmanagedType.LPWStr)]string value,IntPtr context);
  [PreserveSig]int Icon([MarshalAs(UnmanagedType.LPWStr)]out string value);[PreserveSig]int SetIcon([MarshalAs(UnmanagedType.LPWStr)]string value,IntPtr context);
  [PreserveSig]int Group(out Guid id);[PreserveSig]int SetGroup(ref Guid id,IntPtr context);
  [PreserveSig]int Register(IntPtr p);[PreserveSig]int Unregister(IntPtr p);
  [PreserveSig]int SessionId(out IntPtr id);[PreserveSig]int InstanceId(out IntPtr id);
  [PreserveSig]int Pid(out uint pid);
 }
 [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISimple {
  [PreserveSig]int SetVolume(float v,IntPtr context);[PreserveSig]int Volume(out float v);
  [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,IntPtr context);[PreserveSig]int Mute([MarshalAs(UnmanagedType.Bool)]out bool mute);
 }
 public sealed class Session {public uint Pid;public string Process,Error;public int State;public float? Volume;public bool? Muted;}
 public sealed class Endpoint {
  public string Id,Flow,Error;public uint State;public bool? Muted;public float? Volume;
  public List<string> DefaultRoles=new List<string>();
  public List<Session> Sessions=new List<Session>();
  public float PeakMax;public int PeakSamples,ActiveSamples;
 }
 static string Failure(int hr){return "0x"+hr.ToString("X8");}
 static void Free(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
 public static Endpoint[] Collect(int seconds){
  var result=new List<Endpoint>();var meters=new Dictionary<Endpoint,object>();IEnum enumerator=null;
  try{
   enumerator=(IEnum)new Enumerator();
   for(int flow=0;flow<2;flow++){
    var defaults=new Dictionary<string,List<string>>();
    for(int role=0;role<3;role++){
     IDevice d=null;try{if(enumerator.Default(flow,role,out d)==0){string id;if(d.Id(out id)==0){if(!defaults.ContainsKey(id))defaults[id]=new List<string>();defaults[id].Add(new[]{"Console","Multimedia","Communications"}[role]);}}}finally{Free(d);}
    }
    ICollection list=null;
    try{
     int hr=enumerator.Endpoints(flow,15,out list);if(hr!=0)throw new COMException("Endpoint enumeration failed",hr);
     uint count;Marshal.ThrowExceptionForHR(list.Count(out count));
     for(uint i=0;i<count;i++){
      IDevice d=null;object v=null,m=null;
      try{
       Marshal.ThrowExceptionForHR(list.Item(i,out d));string id;uint state;Marshal.ThrowExceptionForHR(d.Id(out id));Marshal.ThrowExceptionForHR(d.State(out state));
       var e=new Endpoint{Id=id,Flow=flow==0?"Render":"Capture",State=state};if(defaults.ContainsKey(id))e.DefaultRoles=defaults[id];result.Add(e);
       if(state!=1)continue;
       if(flow==0){object manager=null;ISessions sessions=null;try{
        Guid iid=typeof(IManager).GUID;
        if(d.Activate(ref iid,23,IntPtr.Zero,out manager)==0&&((IManager)manager).Sessions(out sessions)==0){
         int total;Marshal.ThrowExceptionForHR(sessions.Count(out total));
         for(int k=0;k<total;k++){IControl control=null;try{
          if(sessions.Item(k,out control)!=0)continue;
          var entry=new Session();control.Pid(out entry.Pid);control.State(out entry.State);
          try{entry.Process=entry.Pid==0?"SystemSounds":System.Diagnostics.Process.GetProcessById((int)entry.Pid).ProcessName;}catch{entry.Process="unavailable";}
          try{var simple=(ISimple)control;float scalar;bool mute;if(simple.Volume(out scalar)==0)entry.Volume=scalar;if(simple.Mute(out mute)==0)entry.Muted=mute;}catch(Exception ex){entry.Error=Failure(ex.HResult);}
          e.Sessions.Add(entry);
         }finally{Free(control);}}
        }
       }catch(Exception ex){e.Error="sessions="+Failure(ex.HResult);}finally{Free(sessions);Free(manager);}}
       Guid volume=typeof(IVolume).GUID;hr=d.Activate(ref volume,23,IntPtr.Zero,out v);
       if(hr==0){float scalar;bool mute;var api=(IVolume)v;if(api.Scalar(out scalar)==0)e.Volume=scalar;if(api.Mute(out mute)==0)e.Muted=mute;}else e.Error="volume="+Failure(hr);
       Guid meter=typeof(IMeter).GUID;hr=d.Activate(ref meter,23,IntPtr.Zero,out m);
       if(hr==0){meters[e]=m;m=null;}else e.Error=(e.Error??"")+" meter="+Failure(hr);
      }catch(Exception ex){result.Add(new Endpoint{Flow=flow==0?"Render":"Capture",Error=ex.GetType().Name+" "+Failure(ex.HResult)});}
      finally{Free(v);Free(m);Free(d);}
     }
    }finally{Free(list);}
   }
   var timer=System.Diagnostics.Stopwatch.StartNew();
   do{
    foreach(var pair in meters){float peak;int hr=((IMeter)pair.Value).Peak(out peak);if(hr==0){pair.Key.PeakSamples++;pair.Key.PeakMax=Math.Max(pair.Key.PeakMax,peak);if(peak>0.0001f)pair.Key.ActiveSamples++;}}
    Thread.Sleep(100);
   }while(timer.Elapsed.TotalSeconds<seconds);
  }finally{foreach(var m in meters.Values)Free(m);Free(enumerator);}
  return result.ToArray();
 }
}
