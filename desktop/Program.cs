using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace JarvisStudio;

internal static class Program
{
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [STAThread]
    static void Main(string[] args)
    {
        if(args.Contains("--uninstall-cleanup")){Environment.ExitCode=RuntimeFiles.Cleanup(AppContext.BaseDirectory,args.Contains("--remove-recordings"));return;}
        ApplicationConfiguration.Initialize();
        SetCurrentProcessExplicitAppUserModelID("UltimateVoiceGenerator.Desktop");
        using var running=new Mutex(false,"Local\\UltimateVoiceGenerator.Running");
        using var single=new Mutex(true,"Local\\JarvisStudio-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(AppContext.BaseDirectory)))[..16],out var first);
        if(!first){MessageBox.Show("Ultimate Voice Generator is already running from this folder.","Ultimate Voice Generator");return;}
        Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"Ultimate Voice Generator");
        Application.Run(new StudioWindow(AppContext.BaseDirectory,args.Contains("--smoke-test"),args.Contains("--verify-app")));
    }
}

public sealed class StudioWindow : Form
{
    private readonly WebView2 browser=new(){Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(12,18,28)};
    private readonly string root;
    private StudioService? studio;
    private bool closing;
    private HashSet<string> preexistingCache=[];
    private readonly bool smoke;
    private readonly bool verify;
    private readonly Stopwatch startup=Stopwatch.StartNew();
    public StudioWindow(string root,bool smoke,bool verify=false)
    {
        this.root=root;this.smoke=smoke;this.verify=verify;Text="Ultimate Voice Generator";Width=1220;Height=880;MinimumSize=new(420,560);
        Icon=new Icon(Path.Combine(root,"app.ico"));ShowIcon=true;
        StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(12,18,28);Controls.Add(browser);
        Shown+=async(_,_)=>await InitializeAsync();FormClosing+=OnClosing;
    }
    private async Task InitializeAsync()
    {
        try{
            studio=await Task.Run(()=>new StudioService(root));
            preexistingCache=studio.Owned.ExistingUnownedCache();studio.Owned.CreateDirectory(Path.Combine(root,"user-data","webview"));
            var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"user-data","webview"),verify?new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"):null);
            await browser.EnsureCoreWebView2Async(env);
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
            browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
            browser.CoreWebView2.Settings.IsStatusBarEnabled=false;
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping("jarvis.local",Path.Combine(root,"ui"),CoreWebView2HostResourceAccessKind.DenyCors);
            browser.CoreWebView2.NavigationStarting+=(_,e)=>{if(!e.Uri.StartsWith("https://jarvis.local/",StringComparison.Ordinal))e.Cancel=true;};
            browser.CoreWebView2.NewWindowRequested+=(_,e)=>e.Handled=true;
            browser.CoreWebView2.PermissionRequested+=(_,e)=>e.State=CoreWebView2PermissionState.Deny;
            browser.CoreWebView2.WebMessageReceived+=OnMessage;
            browser.CoreWebView2.AddWebResourceRequestedFilter("https://audio.jarvis.local/*",CoreWebView2WebResourceContext.All);
            browser.CoreWebView2.WebResourceRequested+=OnAudio;
            studio.Changed+=snapshot=>{
                var serialized=JsonSerializer.Serialize(new{type="snapshot",data=snapshot},StudioService.Json);
                if(!IsDisposed&&IsHandleCreated)BeginInvoke(()=>{if(!closing)browser.CoreWebView2.PostWebMessageAsJson(serialized);});
            };
            browser.CoreWebView2.Navigate("https://jarvis.local/index.html");
        }catch(Exception e){MessageBox.Show("Could not start Ultimate Voice Generator.\n\n"+e.Message+"\n\nMake sure Microsoft Edge WebView2 Runtime is installed.","Startup error");Close();}
    }
    private void Post(object value)
    {
        if(!closing&&!IsDisposed&&browser.CoreWebView2!=null)browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(value,StudioService.Json));
    }
    private async void OnMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(studio==null||!e.Source.StartsWith("https://jarvis.local/",StringComparison.Ordinal))return;
        string? id=null;
        try{
            using var doc=JsonDocument.Parse(e.WebMessageAsJson);var message=doc.RootElement;
            id=message.GetProperty("id").GetString();var command=message.GetProperty("command").GetString();
            var payload=message.TryGetProperty("payload",out var p)?p:default;object? result=null;
            switch(command){
                case "bootstrap":result=studio.Snapshot();break;
                case "enqueue":await studio.EnqueueAsync(payload.Deserialize<InputPrompt[]>(StudioService.Json)??[]);result=studio.Snapshot();break;
                case "settings":await studio.UpdateSettingsAsync(payload.Deserialize<Settings>(StudioService.Json)??throw new Exception("Missing settings"));result=studio.Snapshot();break;
                case "pause":await studio.SetPausedAsync(payload.GetBoolean());result=studio.Snapshot();break;
                case "cancel":studio.CancelCurrent();break;
                case "remove":await studio.RemovePendingAsync(payload.GetString()!);result=studio.Snapshot();break;
                case "clear":await studio.ClearFinishedAsync();result=studio.Snapshot();break;
                case "deleteRecord":await studio.DeleteRecordAsync(payload.GetString()!);result=studio.Snapshot();break;
                case "updateSpeed":await studio.UpdateFileSpeedAsync(payload.GetProperty("id").GetString()!,payload.GetProperty("speed").GetDouble());result=studio.Snapshot();break;
                case "listeningReview":await studio.ReviewAsync(payload.GetProperty("id").GetString()!,payload.GetProperty("review").Deserialize<ListeningReview>(StudioService.Json)!);result=studio.Snapshot();break;
                case "draftReference":result=await studio.DraftReferenceAsync(payload.GetString()!);break;
                case "unload":await Task.Run(studio.Unload);result=studio.Snapshot();break;
                case "pickFolder":
                    using(var dialog=new FolderBrowserDialog{Description="Choose your audio export folder",UseDescriptionForTitle=true,SelectedPath=studio.Snapshot().Settings.Folder})
                        result=dialog.ShowDialog(this)==DialogResult.OK?dialog.SelectedPath:null;
                    break;
                case "pickReference":
                    using(var dialog=new OpenFileDialog{Title="Choose a clean reference voice",Filter="Audio|*.wav;*.mp3;*.flac",CheckFileExists=true})
                        result=dialog.ShowDialog(this)==DialogResult.OK?await studio.ImportReferenceAsync(dialog.FileName):null;
                    break;
                case "openFolder":
                    var folder=studio.Snapshot().Settings.Folder;
                    if(payload.ValueKind==JsonValueKind.String){var job=studio.Snapshot().Jobs.Find(j=>j.Id==payload.GetString());if(job?.Path!=null)folder=Path.GetDirectoryName(job.Path)!;}
                    if(!Directory.Exists(folder))throw new DirectoryNotFoundException("The export folder does not exist yet.");
                    var info=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};info.ArgumentList.Add(folder);Process.Start(info);break;
                case "uiReady":
                    if(verify){await VerifyAppAsync();break;}
                    if(smoke){
                        var elapsed=startup.Elapsed.TotalMilliseconds;
                        await Task.Delay(500);
                        await using(var screenshot=File.Create(Path.Combine(root,"desktop-preview.png")))await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
                        await File.WriteAllTextAsync(Path.Combine(root,"smoke-test.json"),JsonSerializer.Serialize(new{ready=true,python=false,title=Text,startupMs=elapsed,shellWorkingSetMB=Process.GetCurrentProcess().WorkingSet64/1048576.0,webViewWorkingSetSumMB=WebViewMemory(),engine=studio.Snapshot().Engine}));BeginInvoke(Close);
                    }break;
                default:throw new Exception("Unknown application command.");
            }
            Post(new{id,result});
        }catch(Exception error){Post(new{id,error=error.Message});}
    }
    private double WebViewMemory()
    {
        double bytes=0;
        foreach(var info in browser.CoreWebView2.Environment.GetProcessInfos())try{using var p=Process.GetProcessById(info.ProcessId);bytes+=p.WorkingSet64;}catch(ArgumentException){}
        return bytes/1048576;
    }
    private async Task VerifyAppAsync()
    {
        try{
            await browser.CoreWebView2.ExecuteScriptAsync("window.__frames=[];window.__frameDone=false;let last=performance.now();function frame(t){window.__frames.push(t-last);last=t;if(!window.__frameDone)requestAnimationFrame(frame)}requestAnimationFrame(frame);");
            var options=studio!.Snapshot().Settings with{Folder=Path.Combine(root,"recordings","verification"),Format="flac"};
            await using(var reference=File.OpenRead(options.Reference))options.ReferenceHash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(reference));
            await studio.EnqueueAsync([new InputPrompt("Good morning, sir. Systems awake. Driver status remains under evaluation.","Automated integration test with ASR reference draft; not a human voice-quality approval.",options)]);
            await studio.SetPausedAsync(false);
            await browser.CoreWebView2.ExecuteScriptAsync("Array.from(document.querySelectorAll('nav button')).find(x=>x.textContent.startsWith('Queue')).click();");
            var timeout=Stopwatch.StartNew();Job? job;
            do{await Task.Delay(150);job=studio.Snapshot().Jobs.Last();}while(job.Status is "pending" or "processing"&&timeout.Elapsed.TotalSeconds<180);
            if(job.Status!="completed")throw new Exception(job.Error??"Generation test timed out");
            await Task.Delay(250);
            await browser.CoreWebView2.ExecuteScriptAsync("window.__frameDone=true;window.__audioTest=null;const a=Array.from(document.querySelectorAll('audio')).at(-1);if(!a){window.__audioTest={error:'Audio element missing'}}else{a.addEventListener('error',()=>window.__audioTest={error:'Audio decoding failed '+a.error?.message},{once:true});a.addEventListener('loadedmetadata',async()=>{try{await a.play();a.currentTime=Math.min(.4,a.duration/2);a.addEventListener('seeked',()=>{a.pause();window.__audioTest={duration:a.duration,time:a.currentTime,paused:a.paused}},{once:true})}catch(e){window.__audioTest={error:e.message}}},{once:true});a.load();}");
            string audio="null";for(int i=0;i<100&&audio=="null";i++){await Task.Delay(100);audio=await browser.CoreWebView2.ExecuteScriptAsync("window.__audioTest");}
            var frames=await browser.CoreWebView2.ExecuteScriptAsync("JSON.stringify({count:__frames.length,averageMs:__frames.reduce((a,b)=>a+b,0)/__frames.length,maxMs:Math.max(...__frames)})");
            var report=new{job,audio=JsonSerializer.Deserialize<JsonElement>(audio),frames=JsonSerializer.Deserialize<string>(frames),shellWorkingSetMB=Process.GetCurrentProcess().WorkingSet64/1048576.0,webViewWorkingSetSumMB=WebViewMemory()};
            await File.WriteAllTextAsync(Path.Combine(root,"app-verification.json"),JsonSerializer.Serialize(report,StudioService.Json));
            await using(var screenshot=File.Create(Path.Combine(root,"queue-preview.png")))await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
        }catch(Exception error){await File.WriteAllTextAsync(Path.Combine(root,"app-verification-error.txt"),error.ToString());}
        BeginInvoke(Close);
    }
    private void OnAudio(object? sender,CoreWebView2WebResourceRequestedEventArgs e)
    {
        try{
            var id= new Uri(e.Request.Uri).AbsolutePath.Trim('/').Split('.')[0];
            var job=studio?.Snapshot().Jobs.Find(j=>j.Id==id&&j.Status is "completed" or "review");
            var audioPath=id=="reference"?studio?.Snapshot().Settings.Reference:job?.Path;
            var uriPath=new Uri(e.Request.Uri).AbsolutePath;
            if(uriPath.StartsWith("/references/",StringComparison.Ordinal)){
                var name=Uri.UnescapeDataString(uriPath[12..]);
                if(Path.GetFileName(name)!=name||!new[]{".mp3",".wav",".flac"}.Contains(Path.GetExtension(name).ToLowerInvariant()))throw new ArgumentException("Invalid reference name.");
                audioPath=Path.Combine(root,"references",name);
            }
            if(audioPath==null||job?.FileBusy==true||!File.Exists(audioPath)){e.Response=browser.CoreWebView2.Environment.CreateWebResourceResponse(null,404,"Missing audio","");return;}
            var file=new FileStream(audioPath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete,65536,FileOptions.Asynchronous);
            long start=0,end=file.Length-1;bool partial=false;
            if(e.Request.Headers.Contains("Range")){
                var range=e.Request.Headers.GetHeader("Range");
                if(range.StartsWith("bytes=")){
                    var parts=range[6..].Split('-');
                    if(parts.Length==2){
                        if(parts[0].Length==0&&long.TryParse(parts[1],out var suffix))start=Math.Max(0,file.Length-suffix);
                        else if(long.TryParse(parts[0],out var offset))start=offset;
                        if(parts[0].Length>0&&long.TryParse(parts[1],out var last))end=Math.Min(end,last);
                        partial=true;
                    }
                }
            }
            if(start<0||start>=file.Length||end<start){file.Dispose();e.Response=browser.CoreWebView2.Environment.CreateWebResourceResponse(null,416,"Invalid range","");return;}
            var type=Path.GetExtension(audioPath).ToLowerInvariant() switch{".mp3"=>"audio/mpeg",".flac"=>"audio/flac",_=>"audio/wav"};
            var headers=$"Content-Type: {type}\r\nAccept-Ranges: bytes\r\nContent-Length: {end-start+1}\r\nAccess-Control-Allow-Origin: https://jarvis.local\r\nCache-Control: no-store\r\n";
            if(partial)headers+=$"Content-Range: bytes {start}-{end}/{file.Length}\r\n";
            e.Response=browser.CoreWebView2.Environment.CreateWebResourceResponse(new AudioSlice(file,start,end-start+1),partial?206:200,partial?"Partial Content":"OK",headers);
        }catch(Exception){e.Response=browser.CoreWebView2.Environment.CreateWebResourceResponse(null,500,"Could not read recording","");}
    }
    private async void OnClosing(object? sender,FormClosingEventArgs e)
    {
        if(closing)return;
        if(studio?.Snapshot().Jobs.Any(j=>j.Status=="processing")==true&&MessageBox.Show("Stop generation and close? The current prompt will return to the waiting queue.","Close Ultimate Voice Generator",MessageBoxButtons.YesNo)!=DialogResult.Yes){e.Cancel=true;return;}
        e.Cancel=true;closing=true;Enabled=false;
        if(studio!=null)await studio.StopAsync();
        var exited=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var environment=browser.CoreWebView2?.Environment;
        if(environment!=null)environment.BrowserProcessExited+=(_,_)=>exited.TrySetResult();
        browser.Dispose();if(environment!=null)await Task.WhenAny(exited.Task,Task.Delay(10000));
        if(studio!=null)await Task.Run(()=>studio.Owned.CaptureCache(preexistingCache));Close();
    }
}

public sealed class AudioSlice(FileStream file,long start,long length) : Stream
{
    private long position;
    public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>false;
    public override long Length=>length;public override long Position{get=>position;set=>Seek(value,SeekOrigin.Begin);}
    public override int Read(byte[] buffer,int offset,int count){file.Position=start+position;var read=file.Read(buffer,offset,(int)Math.Min(count,length-position));position+=read;return read;}
    public override long Seek(long offset,SeekOrigin origin){position=Math.Clamp(origin switch{SeekOrigin.Begin=>offset,SeekOrigin.Current=>position+offset,_=>length+offset},0,length);return position;}
    public override void Flush(){}public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    protected override void Dispose(bool disposing){if(disposing)file.Dispose();base.Dispose(disposing);}
}

