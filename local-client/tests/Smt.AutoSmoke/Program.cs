using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Smt.Core;
using Smt.Desktop;
using System.Text;

// Isolated CI runner only: exercise the actual default location without saved configuration.
var folder=LogFolderLocator.Resolve();
if(Directory.Exists(folder)||File.Exists(SettingsStore.PathName)) throw new Exception("Auto smoke requires a clean runner profile");
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
var window=new MainWindow();window.Show();
void PumpUntil(Func<bool> condition,string label)
{
 var wait=System.Diagnostics.Stopwatch.StartNew();
 while(!condition()&&wait.Elapsed<TimeSpan.FromSeconds(12)) { Dispatcher.UIThread.RunJobs();Thread.Sleep(25); }
 if(!condition())throw new Exception("FAIL: "+label);
 Console.WriteLine("PASS: "+label);
}
string Count()=>window.FindControl<TextBlock>("IntelCount")!.Text!;
string Line(string text)=>$"[ {DateTimeOffset.UtcNow:yyyy.MM.dd HH:mm:ss} ] Scout > {text}\r\n";
try
{
 PumpUntil(()=>window.FindControl<TextBlock>("IntelState")!.Text!.Contains("Waiting"),"Launch automatically waits for the default folder");
 Directory.CreateDirectory(folder);var file=Path.Combine(folder,"TestIntel.txt");
 File.WriteAllText(file,Line("1DQ1-A 3 hostiles"),Encoding.Unicode);
 PumpUntil(()=>Count()=="1 REPORTS","New default folder and UTF16 log ingested without configuration");
 File.AppendAllText(file,Line("T5ZI-S clear"),Encoding.Unicode);
 PumpUntil(()=>Count()=="2 REPORTS","Appended live report appears automatically");
 File.WriteAllText(Path.Combine(folder,"NewSession.txt"),Line("Jita hostile"));
 PumpUntil(()=>Count()=="3 REPORTS","New channel/session file appears automatically");
 window.ToggleDemo();window.ToggleDemo();
 PumpUntil(()=>Count()=="3 REPORTS","Monitoring resumes after demo with no duplicated history");
 Console.WriteLine("Automatic desktop ingestion smoke checks passed on "+System.Runtime.InteropServices.RuntimeInformation.OSDescription);
}
finally { window.Close();if(Directory.Exists(folder))Directory.Delete(folder,true); }
