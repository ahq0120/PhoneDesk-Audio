using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

// Adapt only the explicit audio-off
// argument supplied by iMirror, preserving its video pipeline and stop events.
public static class PhoneDeskAudioBridge {
 public const string AudioSink = "wasapisink exclusive=false low-latency=false buffer-time=80000 latency-time=10000 slave-method=resample";
 public static string[] Transform(string[] args) {
  var result = (string[])args.Clone();
  for(int i=0;i+1<result.Length;i++) {
   if(result[i]=="-as" && result[i+1]=="0") { result[i+1]=AudioSink; i++; }
  }
  return result;
 }
 public static string Quote(string value) {
  var b=new StringBuilder("\"");int slashes=0;
  foreach(char c in value) {
   if(c=='\\'){slashes++;continue;}
   if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);slashes=0;continue;}
   b.Append('\\',slashes);slashes=0;b.Append(c);
  }
  b.Append('\\',slashes*2);b.Append('"');return b.ToString();
 }
 public static int Main(string[] args) {
  try {
   var converted=Transform(args);
   if(args.Length==1 && args[0]=="--phonedesk-self-test") {
    var a=Transform(new[]{"-vrtp","pt=96 ! udpsink host=127.0.0.1 port=1234 sync=false async=false","-as","0","-vs","fakesink"});
    if(a[3]!=AudioSink || a[1].IndexOf("port=1234")<0 || a[5]!="fakesink")return 2;
    if(Transform(new[]{"-as","custom"})[1]!="custom")return 3;
    if(Quote("abc\\")!="\"abc\\\\\"")return 4;
    Console.WriteLine("PASS: audio-off becomes shared buffered WASAPI; video and explicit sinks unchanged; Windows argument quoting.");return 0;
   }
   var dir=AppDomain.CurrentDomain.BaseDirectory;
   var exe=Path.Combine(dir,"UxPlay-original.exe");
   if(!File.Exists(exe))throw new FileNotFoundException("Original AirPlay receiver missing",exe);
   var cmd=new StringBuilder();foreach(var a in converted){if(cmd.Length>0)cmd.Append(' ');cmd.Append(Quote(a));}
   var start=new ProcessStartInfo(exe,cmd.ToString());
   start.WorkingDirectory=dir;start.UseShellExecute=false;start.CreateNoWindow=true;
   using(var process=Process.Start(start)){process.WaitForExit();return process.ExitCode;}
  }catch(Exception e){Console.Error.WriteLine("PhoneDesk audio bridge: "+e.Message);return 1;}
 }
}
