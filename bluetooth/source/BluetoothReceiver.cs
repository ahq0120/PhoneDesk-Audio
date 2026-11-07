using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation.Metadata;
using Windows.Media.Audio;

public sealed class BluetoothReceiver : Form {
 static readonly DailyLog Log=new DailyLog(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"logs"));
 readonly System.Windows.Forms.Timer healthTimer=new System.Windows.Forms.Timer();

 readonly ListBox devices=new ListBox();
 readonly Label status=new Label();
 readonly Label detail=new Label();
 readonly Button refresh=new Button(), connect=new Button(), disconnect=new Button();
 AudioPlaybackConnection connection;
 CancellationTokenSource pending;
 bool closing,busy,radioAvailable;
 int generation;
 sealed class DeviceItem {
  public string Id,Name;
  public override string ToString(){return string.IsNullOrWhiteSpace(Name)?"未命名藍牙音訊來源":Name;}
 }
 public BluetoothReceiver(bool start) {
  Text="PhoneDesk Bluetooth · 藍牙音訊接收";
  ClientSize=new Size(760,620);MinimumSize=Size;MaximumSize=Size;
  StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;
  BackColor=Color.FromArgb(11,17,24);ForeColor=Color.White;
  Font=new Font("Microsoft JhengHei",10);
  AddLabel("PhoneDesk Bluetooth",28,22,700,42,23,Color.White);
  AddLabel("Switch／手機 → 電腦耳機，與電腦聲音一起播放",30,72,700,28,11,Color.LightGray);
  AddLabel("1. 先在 Windows 藍牙設定完成配對。\n2. 按重新搜尋，選擇音訊來源，再按開始接收。\n3. Switch：設定 → 藍牙音訊 → 選擇這台電腦連線。",30,116,700,83,10,Color.White);
  var settings=AddButton("開啟藍牙設定",30,210,165);
  settings.Click+=(s,e)=>OpenSettings("ms-settings:bluetooth");
  var output=AddButton("選擇電腦輸出",210,210,165);
  output.Click+=(s,e)=>OpenSettings("ms-settings:sound");
  AddLabel("已配對、Windows 可接收的藍牙音訊來源",30,266,700,28,11,Color.White);
  devices.SetBounds(30,300,700,130);devices.BackColor=Color.FromArgb(20,33,44);
  devices.ForeColor=Color.White;devices.BorderStyle=BorderStyle.FixedSingle;
  devices.SelectedIndexChanged+=(s,e)=>UpdateButtons();Controls.Add(devices);
  ConfigureButton(refresh,"重新搜尋",30,450,140);refresh.Click+=async(s,e)=>await Scan();
  ConfigureButton(connect,"開始接收",190,450,140);connect.Click+=async(s,e)=>await Connect();
  ConfigureButton(disconnect,"停止接收",350,450,140);disconnect.Click+=(s,e)=>Stop("已停止接收");
  status.SetBounds(30,505,700,34);status.ForeColor=Color.FromArgb(113,229,194);status.Text="尚未開始搜尋";Controls.Add(status);
  detail.SetBounds(30,542,700,65);detail.ForeColor=Color.LightGray;
  detail.Text="使用 Windows 預設輸出裝置。程式需保持開啟；不錄音、不上傳資料。\n藍牙音訊有延遲，Switch 實際配對與相容性需在你的電腦確認。";Controls.Add(detail);
  var logs=AddButton("開啟 Log 資料夾",510,450,220);
  logs.Click+=(s,e)=>{try{Directory.CreateDirectory(Path.GetDirectoryName(Log.FilePath));Process.Start(new ProcessStartInfo("explorer.exe", "\""+Path.GetDirectoryName(Log.FilePath)+"\""){UseShellExecute=true});}catch(Exception ex){Log.Error("open_logs",ex);status.Text="無法開啟 Log 資料夾，請確認資料夾權限。";}};
  healthTimer.Interval=30000;
  healthTimer.Tick+=(s,e)=>{Log.Write("heartbeat","busy="+busy+" channel="+(connection==null?"none":connection.State.ToString()));if(Log.LastError!=null)detail.Text="Log 無法寫入（"+Log.LastError+"），請把程式解壓到可寫入的資料夾。";};
  if(start)healthTimer.Start();
  UpdateButtons();
  if(start)Shown+=async(s,e)=>await Scan();
  FormClosing+=(s,e)=>{closing=true;healthTimer.Stop();healthTimer.Dispose();Stop(null);Log.Write("app_closed");};
 }
 void AddLabel(string text,int x,int y,int w,int h,int size,Color color){Controls.Add(new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font(Font.FontFamily,size),ForeColor=color});}
 Button AddButton(string text,int x,int y,int width){var b=new Button();ConfigureButton(b,text,x,y,width);return b;}
 void ConfigureButton(Button b,string text,int x,int y,int width){b.Text=text;b.SetBounds(x,y,width,37);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=0;b.BackColor=Color.FromArgb(113,229,194);b.ForeColor=Color.FromArgb(11,17,24);Controls.Add(b);}
 void OpenSettings(string uri){try{Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});}catch(Exception ex){Log.Error("open_settings",ex);status.Text="無法開啟設定，請自行開啟 Windows 設定。";}}
 void UI(Action action){if(closing||IsDisposed||!IsHandleCreated)return;try{BeginInvoke(action);}catch(InvalidOperationException){}}
 void UpdateButtons(){refresh.Enabled=!busy&&connection==null;connect.Enabled=!busy&&radioAvailable&&devices.SelectedItem!=null&&connection==null;disconnect.Enabled=connection!=null||pending!=null;devices.Enabled=!busy&&connection==null;}
 static bool Supported(){return ApiInformation.IsTypePresent("Windows.Media.Audio.AudioPlaybackConnection");}
 async Task Scan(){
  if(busy||connection!=null||closing)return;
  Log.Write("scan_started");
  int ticket=++generation;busy=true;radioAvailable=false;devices.Items.Clear();status.Text="正在檢查藍牙與搜尋已配對來源…";UpdateButtons();
  try{
   if(!Supported()){Log.Write("api_unavailable");status.Text="需要 Windows 10 2004 或更新版本／Windows 11。";return;}
   var adapter=await BluetoothAdapter.GetDefaultAsync();
   if(closing||ticket!=generation)return;
   if(adapter==null){Log.Write("adapter_missing");status.Text="未偵測到藍牙介面，請確認硬體、驅動與藍牙開關。";return;}
   radioAvailable=true;Log.Write("adapter_available");
   var found=await DeviceInformation.FindAllAsync(AudioPlaybackConnection.GetDeviceSelector());
   if(closing||ticket!=generation)return;
   Log.Write("scan_completed","eligible_count="+found.Count);
   foreach(var d in found)devices.Items.Add(new DeviceItem{Id=d.Id,Name=d.Name});
   if(devices.Items.Count>0){devices.SelectedIndex=0;status.Text="找到音訊來源，選擇裝置後按開始接收。";}
   else status.Text="沒有可接收來源。先完成配對，再按重新搜尋。";
  }catch(Exception e){Log.Error("scan",e);if(!closing&&ticket==generation)status.Text="搜尋失敗："+Friendly(e);}
  finally{if(!closing&&ticket==generation){busy=false;UpdateButtons();}}
 }
 async Task Connect(){
  var item=devices.SelectedItem as DeviceItem;
  if(item==null||busy||connection!=null||closing)return;
  Log.Write("connect_requested","selection_index="+devices.SelectedIndex);
  int ticket=++generation;busy=true;
  var cancel=new CancellationTokenSource(TimeSpan.FromSeconds(25));pending=cancel;
  AudioPlaybackConnection child=null;
  try{
   status.Text="正在啟用藍牙音訊接收…";UpdateButtons();
   child=AudioPlaybackConnection.TryCreateFromId(item.Id);
   if(child==null)throw new InvalidOperationException("Windows 無法建立此裝置的音訊接收通道，請確認配對與驅動。");
   connection=child;
   child.StateChanged+=(s,e)=>UI(()=>{if(connection==s){Log.Write("channel_state","state="+s.State);status.Text=s.State==AudioPlaybackConnectionState.Opened?"通道已開啟，請在 Switch／手機播放聲音。":"通道未開啟；若無法恢復，請停止後重新接收。";}});
   await child.StartAsync().AsTask(cancel.Token);
   Log.Write("receiver_enabled");
   if(closing||ticket!=generation)return;
   status.Text="等待來源開啟音訊；必要時從 Switch 選擇電腦連線…";
   var result=await child.OpenAsync().AsTask(cancel.Token);
   if(closing||ticket!=generation)return;
   Log.Write("open_result","status="+result.Status);
   if(result.Status!=AudioPlaybackConnectionOpenResultStatus.Success)throw new InvalidOperationException("連線未成功（"+result.Status+"）。請在來源端連線後重試。");
   status.Text="通道已開啟，請播放聲音並調整 Windows 音量。";
  }catch(OperationCanceledException){Log.Write("connect_cancelled","timeout_or_stop=1");if(!closing&&ticket==generation){Release(child);status.Text="等待逾時。請確認來源已連線，再重新接收。";}}
  catch(Exception e){Log.Error("connect",e);if(!closing&&ticket==generation){Release(child);status.Text="無法接收："+Friendly(e);}}
  finally{
   if(closing||ticket!=generation){if(child!=null)try{child.Dispose();}catch{}}
   if(pending==cancel)pending=null;cancel.Dispose();
   if(!closing&&ticket==generation){busy=false;UpdateButtons();}
  }
 }
 static string Friendly(Exception e){return e is UnauthorizedAccessException?"存取遭拒，請檢查 Windows 藍牙權限與配對。":e.Message;}
 void Release(AudioPlaybackConnection child){if(connection==child)connection=null;if(child!=null)try{child.Dispose();}catch(Exception e){Log.Error("dispose_channel",e);}}
 void Stop(string message){Log.Write("stop_requested","closing="+closing);++generation;var cancel=pending;pending=null;if(cancel!=null)try{cancel.Cancel();}catch(ObjectDisposedException){}Release(connection);busy=false;if(!closing){status.Text=message;UpdateButtons();}}
 [STAThread] public static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   if(args.Length>0&&args[0]=="--log-test"){
    DailyLog.SelfTest(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".log-tests",Guid.NewGuid().ToString("N")));return 0;
   }
   Log.Write("app_started","version=0.2 os="+Environment.OSVersion.Version+" runtime="+Environment.Version+" x64="+Environment.Is64BitProcess);
   Application.ThreadException+=(s,e)=>{Log.Error("ui_unhandled",e.Exception);MessageBox.Show("程式發生錯誤，請查看 logs/current.log。","PhoneDesk Bluetooth");};
   AppDomain.CurrentDomain.UnhandledException+=(s,e)=>{var ex=e.ExceptionObject as Exception;if(ex!=null)Log.Error("unhandled",ex);};
   if(args.Length>0&&args[0]=="--smoke-test"){
    int code=7;
    using(var f=new BluetoothReceiver(false)){
     f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2000,-2000);
     f.Shown+=async(s,e)=>{try{await f.Scan();if(!f.busy&&f.status.Text.Length>0){code=0;File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-test.txt"),"PASS: live device scan completed, UI remains responsive; no Bluetooth audio streamed.");}}finally{f.Close();}};
     Application.Run(f);
    }return code;
   }
   if(args.Length>0&&args[0]=="--self-test")return SelfTest().GetAwaiter().GetResult();
   if(args.Length>0&&args[0]=="--preview"){
    using(var f=new BluetoothReceiver(false)){f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-2000,-2000);f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,f.Width,f.Height));b.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview.png"));}f.Close();}return 0;
   }
   bool fresh;using(var mutex=new Mutex(true,"Local\\PhoneDeskBluetoothReceiver",out fresh)){if(!fresh){MessageBox.Show("藍牙接收器已開啟。","PhoneDesk Bluetooth");return 0;}Application.Run(new BluetoothReceiver(true));}return 0;
  }catch(Exception e){Log.Error("startup",e);MessageBox.Show("無法啟動藍牙接收器："+e.Message,"PhoneDesk Bluetooth");return 1;}
 }
 static async Task<int> SelfTest(){
  bool supported=Supported();if(!supported)return 2;
  string selector=AudioPlaybackConnection.GetDeviceSelector();if(string.IsNullOrEmpty(selector))return 3;
  var adapter=await BluetoothAdapter.GetDefaultAsync();
  var found=await DeviceInformation.FindAllAsync(selector);
  using(var f=new BluetoothReceiver(false)){f.Stop("測試完成");}
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"API: available\r\nBluetooth adapter: "+(adapter==null?"not detected":"detected")+"\r\nEligible paired audio sources: "+found.Count+"\r\nNo live audio or Switch connection tested.\r\n");return 0;
 }
}
