using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

public sealed class RecoveryTest : Form {
 static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
 static readonly DailyLog Log=new DailyLog(Path.Combine(Root,"logs"));
 readonly Label state=new Label();
 readonly Button crash=new Button(), hang=new Button();
 readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 string session;
 Stopwatch elapsed;
 public RecoveryTest(){
  Text="PhoneDesk 看門狗測試 1.0";ClientSize=new Size(710,355);
  StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft JhengHei",10);
  Controls.Add(new Label{Text="程序故障復原測試",Font=new Font(Font.FontFamily,19),Location=new Point(24,20),Size=new Size(660,40)});
  Controls.Add(new Label{Text="不更換驅動、不連藍牙、不攔截鍵鼠，也不會真的按住任何按鍵。\n測試的是：獨立看門狗能否辨識閃退，或終止停止回應的測試程序。",Location=new Point(26,72),Size=new Size(660,60)});
  Setup(crash,"測試閃退",26,150,165);Setup(hang,"測試無回應",211,150,165);
  crash.Click+=(s,e)=>StartTest("crash");hang.Click+=(s,e)=>StartTest("hang");
  var logs=new Button();Setup(logs,"開啟 Log 資料夾",396,150,270);
  logs.Click+=(s,e)=>{try{Directory.CreateDirectory(Path.Combine(Root,"logs"));Process.Start(new ProcessStartInfo("explorer.exe","\""+Path.Combine(Root,"logs")+"\""){UseShellExecute=true});}catch{state.Text="無法開啟資料夾，請自行查看程式旁的 logs。";}};
  state.SetBounds(26,208,650,62);state.Text="請先按「測試閃退」，完成後再按「測試無回應」。";Controls.Add(state);
  Controls.Add(new Label{Text="限制：通過不代表 WinUSB 驅動、鍵鼠配對或唯一滑鼠已能自動還原。\n目前請保留原有 Windows 藍牙設定；此工具不接管 USB 藍牙棒。",ForeColor=Color.DarkRed,Location=new Point(26,278),Size=new Size(660,62)});
  timer.Interval=200;timer.Tick+=(s,e)=>Poll();
  FormClosed+=(s,e)=>{timer.Stop();timer.Dispose();};
 }
 void Setup(Button b,string text,int x,int y,int w){b.Text=text;b.SetBounds(x,y,w,39);Controls.Add(b);}
 static string Folder(string id){Guid parsed;if(!Guid.TryParseExact(id,"N",out parsed))throw new ArgumentException("Invalid session");return Path.Combine(Root,"reports",id);}
 static Process Launch(string mode,string id,string kind){
  var info=new ProcessStartInfo(Application.ExecutablePath,"--"+mode+" "+id+" "+kind){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
  var p=new Process{StartInfo=info};p.Start();return p;
 }
 void StartTest(string kind){
  try{session=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Folder(session));
   using(var p=Launch("watchdog",session,kind)){}
   elapsed=Stopwatch.StartNew();crash.Enabled=hang.Enabled=false;state.Text="獨立看門狗正在測試，約 3～10 秒。\n原本的滑鼠與鍵盤仍由 Windows 管理。";timer.Start();
  }catch(Exception ex){Log.Error("start_test",ex);state.Text="無法啟動測試，請傳回 logs/current.log。";}
 }
 void Poll(){
  string file=Path.Combine(Folder(session),"result.txt");
  try{if(File.Exists(file)){
   string result=File.ReadAllText(file);timer.Stop();crash.Enabled=hang.Enabled=true;
   state.Text=result.StartsWith("PASS")?"通過：看門狗已處理測試程序。\n請確認目前滑鼠仍能移動與點擊；藍牙設定沒有變更。":"測試未通過，請傳回 logs/current.log 與 reports 裡的 result.txt。";return;
  }}catch(IOException){}
  if(elapsed.Elapsed.TotalSeconds>30){timer.Stop();crash.Enabled=hang.Enabled=true;state.Text="超過 30 秒仍無結果，不能視為通過。\n請傳回 logs/current.log；不要進行 USB 接管測試。";}
 }
 static int Worker(string id,string kind){
  var watch=Stopwatch.StartNew();int count=0;
  while(watch.ElapsedMilliseconds<900){File.WriteAllText(Path.Combine(Folder(id),"heartbeat.txt"),(++count).ToString());Thread.Sleep(100);}
  if(kind=="crash")return 42;
  Thread.Sleep(Timeout.Infinite);return 99;
 }
 static int Watchdog(string id,string kind){
  Directory.CreateDirectory(Folder(id));Log.Write("watchdog_started","hang_mode="+(kind=="hang"));
  bool passed=false;string reason="unknown";Process worker=null;
  var total=Stopwatch.StartNew();var changed=Stopwatch.StartNew();string last=null;
  try{
   worker=Launch("worker",id,kind);
   while(total.Elapsed.TotalSeconds<12){
    if(worker.HasExited){passed=kind=="crash"&&worker.ExitCode==42;reason=passed?"expected_worker_exit":"unexpected_worker_exit";break;}
    string heartbeat=null;try{heartbeat=File.ReadAllText(Path.Combine(Folder(id),"heartbeat.txt"));}catch(IOException){}
    int value;
    if(int.TryParse(heartbeat,out value)&&heartbeat!=last){last=heartbeat;changed.Restart();}
    if(last!=null&&changed.Elapsed.TotalSeconds>2){
     // Only terminate the child created by this watchdog. No process-name matching.
     worker.Kill();bool stopped=worker.WaitForExit(2000);
     passed=kind=="hang"&&stopped;reason=passed?"hung_child_terminated":"child_stop_failed";break;
    }
    Thread.Sleep(80);
   }
  }catch(Exception ex){reason="watchdog_error";Log.Error(reason,ex);}
  finally{
   if(worker!=null){try{if(!worker.HasExited){worker.Kill();worker.WaitForExit(2000);}}catch(Exception ex){passed=false;Log.Error("child_cleanup",ex);}worker.Dispose();}
  }
  Log.Write(reason,"passed="+passed+" elapsed_ms="+total.ElapsedMilliseconds);
  string file=Path.Combine(Folder(id),"result.txt");
  File.WriteAllText(file+".tmp",(passed?"PASS":"FAIL")+" "+kind+" "+reason+Environment.NewLine+"Bluetooth drivers, pairing and real input were not changed."+Environment.NewLine);
  File.Move(file+".tmp",file);return passed?0:1;
 }
 static int SelfTest(){
  foreach(string kind in new[]{"crash","hang"}){
   string id=Guid.NewGuid().ToString("N");Directory.CreateDirectory(Folder(id));
   using(var p=Launch("watchdog",id,kind)){
    if(!p.WaitForExit(20000)){p.Kill();return 3;}
    if(p.ExitCode!=0)return 4;
   }
   string result=File.ReadAllText(Path.Combine(Folder(id),"result.txt"));if(!result.StartsWith("PASS"))return 5;
  }
  File.WriteAllText(Path.Combine(Root,"self-test.txt"),"PASS: real independent watchdog recognized child exit and terminated hung child. No Bluetooth, driver or input changes.");return 0;
 }
 [STAThread] public static int Main(string[] args){
  try{
   if(args.Length==3&&(args[0]=="--watchdog"||args[0]=="--worker")){
    Folder(args[1]);if(args[2]!="crash"&&args[2]!="hang")return 2;
    return args[0]=="--worker"?Worker(args[1],args[2]):Watchdog(args[1],args[2]);
   }
   if(args.Length==1&&args[0]=="--self-test")return SelfTest();
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length==1&&args[0]=="--preview"){
    using(var f=new RecoveryTest()){f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.Combine(Root,"preview.png"));}}return 0;
   }
   Application.Run(new RecoveryTest());return 0;
  }catch(Exception ex){Log.Error("fatal",ex);return 1;}
 }
}
