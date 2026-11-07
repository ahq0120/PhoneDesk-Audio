using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public class PhoneDeskLite : Form {
 Process audio,screen;
 Label audioStatus,screenStatus,notice;
 Button audioButton,screenButton,restartButton;
 CheckBox movieSync;
 EventWaitHandle syncEvent;
 readonly string syncEventName="Local\\PhoneDeskAudioSync-"+Guid.NewGuid().ToString("N");
 readonly object healthLock=new object();
 readonly string healthFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhoneDeskAudio","audio-health.log");
 bool closing;
 readonly string receiver=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","app","receiver");
 readonly Color bg=Color.FromArgb(11,17,24), card=Color.FromArgb(17,27,38), muted=Color.FromArgb(160,180,195), accent=Color.FromArgb(113,229,194);
 public PhoneDeskLite(bool autoStart=true){
  syncEvent=new EventWaitHandle(false,EventResetMode.ManualReset,syncEventName);
  Text="PhoneDesk · 手機音訊";Size=new Size(790,700);MinimumSize=new Size(790,700);MaximumSize=new Size(790,700);StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=Color.White;Font=new Font("Microsoft JhengHei",10);AutoScaleMode=AutoScaleMode.Dpi;
  Label title=LabelAt(this,"PhoneDesk",30,22,700,40,24,Color.White);title.Font=new Font(Font.FontFamily,24,FontStyle.Bold);
  Panel a=Card(30,116,714,235);LabelAt(a,"手機音訊",20,15,300,30,16,Color.White);
  audioStatus=LabelAt(a,"準備啟動",440,20,245,28,10,accent);
  LabelAt(a,"與電腦聲音一起播放，使用 Windows 目前的預設輸出裝置。",20,56,670,28,10,muted);
  LabelAt(a,"1. 手機與電腦連到相同區域網路。\n2. 手機控制中心 → 螢幕鏡像輸出 → PhoneDesk Audio。\n3. 電腦不顯示畫面；純音訊也可從播放控制的 AirPlay 連線。",20,91,670,80,10,Color.White);
  audioButton=ButtonAt(a,"停止音訊",20,184,142);audioButton.Click+=(s,e)=>{if(audio!=null)StopAudio();else StartAudio();};
  restartButton=ButtonAt(a,"重啟音訊",180,184,132);restartButton.Click+=(s,e)=>{StopAudio();StartAudio();notice.Text="音訊已重啟；若手機未自動恢復，請重新選 PhoneDesk Audio。";};
  movieSync=new CheckBox{Text="動畫／影片同步",Location=new Point(330,190),Size=new Size(330,28),ForeColor=accent,BackColor=card};a.Controls.Add(movieSync);
  movieSync.CheckedChanged+=(s,e)=>{if(movieSync.Checked)syncEvent.Set();else syncEvent.Reset();notice.Text=movieSync.Checked?"影音同步已開啟：聲音會配合影片時間，操作音效可能稍慢。":"已恢復遊戲快速回應，不需要重新連線。";};
  Panel v=Card(30,368,714,205);LabelAt(v,"手機畫面",20,15,300,30,16,Color.White);screenStatus=LabelAt(v,"已關閉",560,20,120,28,10,muted);
  LabelAt(v,"平常保持關閉，需要看畫面時再開啟。",20,56,670,26,10,muted);
  screenButton=ButtonAt(v,"開啟手機畫面",20,94,160);screenButton.Click+=(s,e)=>{if(screen!=null)StopScreen();else StartScreen();};
  LabelAt(v,"開啟後在投影視窗按 Connect，手機螢幕鏡像輸出選 PhoneDesk Screen。\n關閉畫面後，若要繼續聽聲音，從 AirPlay 音訊選回 PhoneDesk Audio。",20,145,670,50,9,muted);
  notice=LabelAt(this,"",32,585,710,40,9,Color.FromArgb(255,188,172));
  LabelAt(this,"只接收音訊與選用畫面 · 不需要 ESP32 或手機 USB 線",32,625,710,25,9,muted);
  Shown+=(s,e)=>{if(autoStart)StartAudio();};FormClosing+=(s,e)=>{closing=true;StopScreen();StopAudio();};
 }
 Panel Card(int x,int y,int w,int h){var p=new Panel{Location=new Point(x,y),Size=new Size(w,h),BackColor=card};Controls.Add(p);return p;}
 Label LabelAt(Control c,string t,int x,int y,int w,int h,int size,Color color){var l=new Label{Text=t,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Microsoft JhengHei",size),ForeColor=color};c.Controls.Add(l);return l;}
 Button ButtonAt(Control c,string t,int x,int y,int w){var b=new Button{Text=t,Location=new Point(x,y),Size=new Size(w,36),FlatStyle=FlatStyle.Flat,BackColor=accent,ForeColor=bg};b.FlatAppearance.BorderSize=0;c.Controls.Add(b);return b;}
 void UI(Action action){if(closing||IsDisposed)return;try{BeginInvoke(action);}catch(InvalidOperationException){}}
 void RefreshState(){audioButton.Text=audio!=null?"停止音訊":"啟動音訊";restartButton.Enabled=audio!=null;audioStatus.Text=audio!=null?"已啟動 · 可從手機選取":"已停止";screenButton.Text=screen!=null?"關閉手機畫面":"開啟手機畫面";screenStatus.Text=screen!=null?"視窗已開啟":"已關閉";}
 public static string SelectLAN(){
  string fallback=null;
  foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
   if(adapter.OperationalStatus!=OperationalStatus.Up || !adapter.SupportsMulticast ||
      (adapter.NetworkInterfaceType!=NetworkInterfaceType.Ethernet && adapter.NetworkInterfaceType!=NetworkInterfaceType.Wireless80211)) continue;
   string name=(adapter.Name+" "+adapter.Description).ToLowerInvariant();
   if(name.Contains("vpn")||name.Contains("virtual")||name.Contains("hyper-v")||name.Contains("vethernet")||name.Contains("wireguard")||name.Contains("tunnel"))continue;
   var props=adapter.GetIPProperties();if(props.GatewayAddresses.Count==0)continue;
   foreach(var address in props.UnicastAddresses) {
    if(address.Address.AddressFamily!=AddressFamily.InterNetwork)continue;
    var b=address.Address.GetAddressBytes();if(b[0]==127||b[0]==169&&b[1]==254)continue;
    var ip=address.Address.ToString();
    if(b[0]==10 || b[0]==192&&b[1]==168 || b[0]==172&&b[1]>=16&&b[1]<=31)return ip;
    if(fallback==null)fallback=ip;
   }
  }
  return fallback;
 }
 static void ApplyLAN(ProcessStartInfo start){
  string ip=SelectLAN();if(ip==null)throw new Exception("找不到可用的家用區域網路，請確認乙太網路或 Wi-Fi 已連線。");
  start.EnvironmentVariables["PHONEDESK_INTERFACE_IP"]=ip;
 }
 ProcessStartInfo AudioStart(){
  string dir=Path.Combine(receiver,"AirPlay"),exe=Path.Combine(dir,"UxPlay-original.exe");
  var hash=(Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(receiver,"phonedesk-audio-manifest.json")));
  string actual;using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(exe))).Replace("-","").ToLowerInvariant();
  if(actual!=(string)hash["AirPlay/UxPlay-original.exe"])throw new Exception("音訊接收器校驗失敗。");
  var p=new ProcessStartInfo(exe,"-n \"PhoneDesk Audio\" -nh -vs 0 -as \"wasapisink exclusive=false low-latency=false buffer-time=80000 latency-time=10000 slave-method=resample\" -async no -vsync no -nofreeze");p.WorkingDirectory=dir;p.UseShellExecute=false;p.CreateNoWindow=true;p.RedirectStandardOutput=true;p.RedirectStandardError=true;
  p.EnvironmentVariables["PATH"]=dir+";"+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32");
  foreach(string key in new[]{"GST_PLUGIN_PATH","GST_PLUGIN_PATH_1_0"})p.EnvironmentVariables[key]=Path.Combine(dir,"lib","gstreamer-1.0");
  foreach(string key in new[]{"GST_PLUGIN_SYSTEM_PATH","GST_PLUGIN_SYSTEM_PATH_1_0"})p.EnvironmentVariables[key]="";
  p.EnvironmentVariables["GST_REGISTRY_FORK"]="no";
  string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PhoneDeskAudio");Directory.CreateDirectory(data);p.EnvironmentVariables["GST_REGISTRY_1_0"]=Path.Combine(data,"registry.bin");
  p.EnvironmentVariables["PHONEDESK_AUDIO_SYNC_EVENT"]=syncEventName;ApplyLAN(p);p.EnvironmentVariables.Remove("IMIRROR_STOP_EVENT");p.EnvironmentVariables.Remove("IMIRROR_DISCONNECT_EVENT");return p;
 }
 void Health(string line){
  if(line==null || !line.StartsWith("PhoneDesk audio:",StringComparison.Ordinal))return;
  try{lock(healthLock){Directory.CreateDirectory(Path.GetDirectoryName(healthFile));
   if(File.Exists(healthFile)&&new FileInfo(healthFile).Length>256*1024)File.WriteAllText(healthFile,"");
   File.AppendAllText(healthFile,DateTime.UtcNow.ToString("o")+" "+line+Environment.NewLine,new UTF8Encoding(false));}}
  catch(IOException){}catch(UnauthorizedAccessException){}
 }
 void StartAudio(){try{if(audio!=null)return;notice.Text="";var child=new Process{StartInfo=AudioStart(),EnableRaisingEvents=true};audio=child;
  child.OutputDataReceived+=(s,e)=>Health(e.Data);child.ErrorDataReceived+=(s,e)=>Health(e.Data);
  child.Exited+=(s,e)=>UI(()=>{if(audio==child){audio=null;notice.Text="音訊接收器已停止，請按「啟動音訊」重試。";RefreshState();}child.Dispose();});
  child.Start();child.BeginOutputReadLine();child.BeginErrorReadLine();Health("PhoneDesk audio: receiver_started stable_output_80ms input_limit_60_frames pcm_limit_24_frames");RefreshState();
 }catch(Exception e){audio=null;notice.Text=e.Message;RefreshState();}}

 void StopAudio(){var child=audio;audio=null;if(child!=null){try{if(!child.HasExited){child.Kill();child.WaitForExit(2000);}}catch{} }RefreshState();}
 void StartScreen(){try{if(screen!=null)return;notice.Text="";string exe=Path.Combine(receiver,"iMirror.exe");
  string configDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"iMirror");Directory.CreateDirectory(configDir);string configFile=Path.Combine(configDir,"settings.json");var serializer=new JavaScriptSerializer();var config=new Dictionary<string,object>();try{config=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(configFile));}catch{}
  config["version"]=3;config["connection"]="Wireless";config["receiver_name"]="PhoneDesk Screen";config["control_enabled"]=false;config["quality"]="LowLatency";File.WriteAllText(configFile,serializer.Serialize(config),new UTF8Encoding(false));
  var child=new Process{StartInfo=new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=receiver},EnableRaisingEvents=true};ApplyLAN(child.StartInfo);screen=child;child.Exited+=(s,e)=>UI(()=>{if(screen==child){screen=null;RefreshState();}child.Dispose();});child.Start();RefreshState();
 }catch(Exception e){screen=null;notice.Text=e.Message;RefreshState();}}
 void StopScreen(){var child=screen;screen=null;if(child!=null){try{if(!child.HasExited){child.Kill();child.WaitForExit(2000);}}catch{} }RefreshState();}
 protected override void Dispose(bool disposing){if(disposing&&syncEvent!=null){syncEvent.Dispose();syncEvent=null;}base.Dispose(disposing);}
 [STAThread]public static int Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0&&args[0]=="--sync-control-test"){
   using(var f=new PhoneDeskLite(false))using(var fastSeen=new ManualResetEvent(false))using(var syncSeen=new ManualResetEvent(false)){
    var start=f.AudioStart();start.Arguments=start.Arguments.Replace("PhoneDesk Audio","PhoneDesk Sync Test")+" -m 02:50:44:53:59:4e";
    using(var child=new Process{StartInfo=start}){
     child.OutputDataReceived+=(s,e)=>{};child.ErrorDataReceived+=(s,e)=>{if(e.Data!=null&&e.Data.Contains("sync_mode=0"))fastSeen.Set();if(e.Data!=null&&e.Data.Contains("sync_mode=1"))syncSeen.Set();};
     try{child.Start();child.BeginOutputReadLine();child.BeginErrorReadLine();if(!fastSeen.WaitOne(8000))return 21;f.syncEvent.Set();if(!syncSeen.WaitOne(3000))return 22;fastSeen.Reset();f.syncEvent.Reset();if(!fastSeen.WaitOne(3000))return 23;if(child.HasExited)return 24;}
     finally{try{if(!child.HasExited){child.Kill();child.WaitForExit(3000);}}catch{}}
    }
   }
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sync-validation.txt"),"PASS: same receiver process switches fast -> video-sync -> fast via named event without disconnect/restart.");return 0;
  }
  if(args.Length>0&&args[0]=="--self-test") {using(var f=new PhoneDeskLite()){var start=f.AudioStart();using(var p=Process.Start(start)){p.OutputDataReceived+=(s,e)=>{};p.ErrorDataReceived+=(s,e)=>{};p.BeginOutputReadLine();p.BeginErrorReadLine();Thread.Sleep(2500);if(p.HasExited)return 2;f.StartScreen();Thread.Sleep(1500);if(f.screen==null||f.screen.HasExited)return 3;f.StopScreen();if(p.HasExited)return 4;p.Kill();p.WaitForExit();}File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"validation.txt"),"PASS: audio-only receiver (-vs 0) stayed alive; screen off by default; optional screen starts/stops while audio stays alive; clean stop. Native UI has no USB/BLE/keyboard/WDA features.");return 0;}}
  if(args.Length>0&&args[0]=="--preview"){using(var f=new PhoneDeskLite(false)){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2000,-2000);f.Show();Application.DoEvents();using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}f.Close();return 0;}}
  bool fresh;using(var mutex=new Mutex(true,"Local\\PhoneDeskAudioLite",out fresh)){if(!fresh){MessageBox.Show("PhoneDesk 已經開啟。","PhoneDesk");return 0;}Application.Run(new PhoneDeskLite());}return 0;
 }
}
