using System;
using System.IO;
using System.Text;

// A single file, shared by UI and WinRT callbacks, retaining the current local day.
public sealed class DailyLog {
 readonly object gate=new object();
 readonly Func<DateTime> clock;
 readonly int limit;
 public readonly string FilePath;
 public string LastError { get; private set; }
 public DailyLog(string folder,Func<DateTime> now=null,int maxBytes=2097152){
  FilePath=Path.Combine(folder,"current.log");clock=now??(()=>DateTime.Now);limit=maxBytes;
 }
 public void Write(string name,string fields=""){
  lock(gate){try{
   Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
   DateTime now=clock();string header="date="+now.ToString("yyyy-MM-dd");
   string existing=null;
   if(File.Exists(FilePath))using(var reader=new StreamReader(FilePath,Encoding.UTF8)){existing=reader.ReadLine();}
   bool rollover=existing!=header;
   string entry=now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")+" event="+Clean(name)+(fields.Length>0?" "+Clean(fields):"")+Environment.NewLine;
   bool capped=!rollover && new FileInfo(FilePath).Length+Encoding.UTF8.GetByteCount(entry)>limit;
   using(var writer=new StreamWriter(FilePath,rollover||capped?false:true,new UTF8Encoding(false))){
    if(rollover||capped){writer.WriteLine(header);writer.WriteLine("reset="+(capped?"size_limit":"new_day"));}
    writer.Write(entry);
   }
   LastError=null;
  }catch(IOException e){LastError=e.GetType().Name;}catch(UnauthorizedAccessException e){LastError=e.GetType().Name;}}
 }
 static string Clean(string s){return s.Replace('\r',' ').Replace('\n',' ');}
 public void Error(string phase,Exception e){Write("error","phase="+phase+" type="+e.GetType().FullName+" hresult=0x"+e.HResult.ToString("X8"));}
 public static void SelfTest(string folder){
  DateTime day=new DateTime(2026,1,1,23,59,59);
  var log=new DailyLog(folder,()=>day);
  log.Write("first");log.Write("same_day");
  var restart=new DailyLog(folder,()=>day);restart.Write("restart");
  string s=File.ReadAllText(log.FilePath);
  if(!s.Contains("event=first")||!s.Contains("event=restart"))throw new Exception("Same-day append failed");
  day=day.AddDays(1);log.Write("next_day");s=File.ReadAllText(log.FilePath);
  if(s.Contains("event=first")||!s.Contains("event=next_day")||!s.StartsWith("date=2026-01-02"))throw new Exception("Daily replacement failed");
  System.Threading.Tasks.Parallel.For(0,100,i=>log.Write("parallel","index="+i));
  if(File.ReadAllLines(log.FilePath).Length!=103)throw new Exception("Concurrent entries lost");
  var capped=new DailyLog(Path.Combine(folder,"size"),()=>day,200);
  for(int i=0;i<30;i++)capped.Write("bounded","index="+i);
  s=File.ReadAllText(capped.FilePath);
  if(new FileInfo(capped.FilePath).Length>200||!s.Contains("index=29"))throw new Exception("Size limit failed");
  string blocked=Path.Combine(folder,"blocked");File.WriteAllText(blocked,"not a directory");
  var failing=new DailyLog(blocked,()=>day);failing.Write("failure");
  if(failing.LastError==null)throw new Exception("Write failure was not exposed");
 }
}
