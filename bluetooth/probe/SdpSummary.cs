using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// Only standard numeric attributes are summarized. No raw record, text or device address is logged.
public static class SdpSummary {
 [UnmanagedFunctionPointer(CallingConvention.Winapi)] [return:MarshalAs(UnmanagedType.Bool)] delegate bool Callback(uint id,IntPtr data,uint size,IntPtr state);
 [DllImport("bthprops.cpl",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool BluetoothSdpEnumAttributes(byte[] data,uint size,Callback callback,IntPtr state);
 [StructLayout(LayoutKind.Sequential)] struct Blob {public uint size;public IntPtr data;}
 static string Numeric(byte[] b,ref int pos,int end,int depth){
  if(depth>6||pos>=end)throw new FormatException();
  int h=b[pos++],type=h>>3,index=h&7;long length;
  if(type==0)length=0;
  else if(index<=4)length=1L<<index;
  else {int bytes=index==5?1:index==6?2:4;if(pos+bytes>end)throw new FormatException();length=0;for(int i=0;i<bytes;i++)length=(length<<8)|b[pos++];}
  if(length>end-pos)throw new FormatException();int stop=pos+(int)length;
  if(type==6||type==7){var children=new List<string>();while(pos<stop){if(children.Count>=64)throw new FormatException();children.Add(Numeric(b,ref pos,stop,depth+1));}return "["+string.Join(",",children)+"]";}
  string result="type"+type+"_len"+length;
  if((type==1||type==2||type==5)&&length<=4){ulong n=0;for(int i=pos;i<stop;i++)n=(n<<8)|b[i];result="0x"+n.ToString("X");}
  if(type==3){
   if(length==2||length==4){uint n=0;for(int i=pos;i<stop;i++)n=(n<<8)|b[i];result="uuid0x"+n.ToString("X4");}
   else result="uuid128_present";
  }
  pos=stop;return result;
 }
 public static string[] Summarize(byte[] data){
  if(data==null||data.Length==0||data.Length>65536)return new[]{"record=unsupported_size"};
  var rows=new List<string>();bool handle=false;int count=0;
  Callback callback=(id,p,size,state)=>{
   try{
    if(++count>128)return false;
    if(id==0){handle=true;rows.Add("attribute=0x0000 present=1 value_masked=1");return true;}
    if(id!=1&&id!=4&&id!=5&&id!=9&&id!=0x311)return true;
    if(size>65536){rows.Add("attribute=0x"+id.ToString("X4")+" too_large=1");return true;}
    var value=new byte[size];Marshal.Copy(p,value,0,value.Length);int pos=0;
    rows.Add("attribute=0x"+id.ToString("X4")+" numeric="+Numeric(value,ref pos,value.Length,0));
    return true;
   }catch{rows.Add("attribute=0x"+id.ToString("X4")+" parse_error=1");return true;}
  };
  bool ok=BluetoothSdpEnumAttributes(data,(uint)data.Length,callback,IntPtr.Zero);int error=ok?0:Marshal.GetLastWin32Error();
  GC.KeepAlive(callback);rows.Add("parse_ok="+ok+" win32="+error+" handle_present="+handle+" attribute_count="+count);return rows.ToArray();
 }
 public static void LogBlob(IntPtr blob,IntPtr buffer,uint capacity,DailyLog log,string key){
  try{
   long start=buffer.ToInt64(),end=start+capacity,p=blob.ToInt64();
   if(blob==IntPtr.Zero||p<start||p>end-Marshal.SizeOf(typeof(Blob))){log.Write("sdp_record",key+" blob_valid=0");return;}
   var b=(Blob)Marshal.PtrToStructure(blob,typeof(Blob));long bytes=b.data.ToInt64();
   if(b.size==0||b.size>65536||bytes<start||bytes>end-b.size){log.Write("sdp_record",key+" payload_valid=0");return;}
   var data=new byte[b.size];Marshal.Copy(b.data,data,0,data.Length);
   foreach(var row in Summarize(data))log.Write("sdp_record",key+" "+row);
  }catch(Exception e){log.Error("sdp_summary",e);}
 }
 public static void SelfTest(){
  byte[] valid={0x35,0x10,0x09,0,0,0x0a,0,0,0,1,0x09,0,1,0x35,3,0x19,0x11,0x0a};
  string text=string.Join(" ",Summarize(valid));
  if(!text.Contains("handle_present=True")||!text.Contains("uuid0x110A")||!text.Contains("parse_ok=True"))throw new Exception("SDP summary test failed");
  byte[] missing={0x35,8,0x09,0,1,0x35,3,0x19,0x11,0x0a};
  if(!string.Join(" ",Summarize(missing)).Contains("handle_present=False"))throw new Exception("Missing handle test failed");
  if(!string.Join(" ",Summarize(new byte[]{0x35,0xff})).Contains("parse_ok=False"))throw new Exception("Malformed record accepted");
  byte[] privateText={0x35,9,0x09,1,0,0x25,4,0x54,0x45,0x53,0x54};
  if(string.Join(" ",Summarize(privateText)).Contains("TEST"))throw new Exception("Private text leaked");
 }
}
