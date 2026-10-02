using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace JarvisStudio;

public interface IVoiceEngine : IDisposable
{
    event Action<string>? Status;
    string LastLog {get;}
    Task GenerateAsync(Job job,string work,Action<string> phase,CancellationToken ct);
    Task<string> TranscribeAsync(string audio,string work,CancellationToken ct);
    Task EncodeAsync(IEnumerable<string> args,CancellationToken ct);
    void Stop();
}
public sealed class NativeEngine(string root) : IVoiceEngine
{
    private Process? process;
    private HttpClient? http;
    private string? referenceKey;
    private string? activeBackend;
    private readonly Queue<string> log=[];
    public event Action<string>? Status;
    public string LastLog {get {lock(log)return string.Join('\n',log);}}
    private string Qwen=>Path.Combine(root,"engines","qwen");
    private string Encoder=>Path.Combine(root,"engines","ffmpeg.exe");
    private void Log(string line){lock(log){log.Enqueue(line);while(log.Count>80)log.Dequeue();}}
    private async Task ReadLogAsync(StreamReader reader)
    {
        try{while(await reader.ReadLineAsync() is {} line){Log(line);if(line.Contains("Backend:",StringComparison.OrdinalIgnoreCase))Status?.Invoke("Qwen · "+line);}}
        catch(IOException){}catch(ObjectDisposedException){}
    }
    private async Task EnsureAsync(string backend,CancellationToken ct)
    {
        if(process is {HasExited:false}&&http!=null&&activeBackend==backend)return;
        Stop();activeBackend=backend;
        var cpu=backend=="cpu"||!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"vulkan-1.dll"));
        var exe=Path.Combine(Qwen,cpu?"tts-server-cpu.exe":"tts-server.exe");
        if(cpu&&!File.Exists(exe))exe=Path.Combine(Qwen,"tts-server.exe");
        if(!File.Exists(exe))throw new FileNotFoundException("Qwen engine is missing. Run scripts/setup-qwen.ps1.",exe);
        foreach(var file in new[]{"qwen-talker-1.7b-base-Q8_0.gguf","qwen-tokenizer-12hz-Q8_0.gguf"})
            if(!File.Exists(Path.Combine(Qwen,"models",file)))throw new FileNotFoundException("Missing Qwen model: "+file);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=Qwen};
        if(cpu)info.Environment["GGML_BACKEND"]="CPU";
        foreach(var arg in new[]{"--model",Path.Combine(Qwen,"models","qwen-talker-1.7b-base-Q8_0.gguf"),"--codec",Path.Combine(Qwen,"models","qwen-tokenizer-12hz-Q8_0.gguf"),"--host","127.0.0.1","--port",port.ToString(),"--lang","English"})info.ArgumentList.Add(arg);
        process=Process.Start(info)??throw new Exception("Could not start Qwen.");
        _=ReadLogAsync(process.StandardError);_=ReadLogAsync(process.StandardOutput);
        http=new(){BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromMinutes(30)};
        Status?.Invoke("Loading Qwen3-TTS 1.7B Base");
        for(int i=0;i<600;i++){
            ct.ThrowIfCancellationRequested();if(process.HasExited)throw new Exception("Qwen exited during startup: "+LastLog);
            try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(750);using var r=await http.GetAsync("/health",timeout.Token);if(r.IsSuccessStatusCode){Status?.Invoke("Qwen3-TTS 1.7B Base ready");return;}}
            catch(HttpRequestException){}catch(OperationCanceledException)when(!ct.IsCancellationRequested){}
            await Task.Delay(250,ct);
        }
        Stop();throw new TimeoutException("Qwen startup timed out.");
    }
    public async Task GenerateAsync(Job job,string work,Action<string> phase,CancellationToken ct)
    {
        phase("Loading Qwen3-TTS");await EnsureAsync(job.Options.Backend,ct);
        var refInfo=new FileInfo(job.Options.Reference);
        if(!refInfo.Exists)throw new Exception("Choose a reference recording in Settings.");
        var key=refInfo.FullName+refInfo.LastWriteTimeUtc.Ticks+job.Options.ReferenceTranscript;
        if(key!=referenceKey){
            phase("Preparing voice and reference transcript");
            var reference=Path.Combine(work,"reference.wav");await EncodeAsync(["-i",refInfo.FullName,"-ac","1","-ar","24000","-c:a","pcm_s16le",reference],ct);
            if(new FileInfo(reference).Length>24000*2*30+4096)throw new Exception("Use a reference no longer than 30 seconds for this prototype.");
            using var result=await http!.PostAsJsonAsync("/v1/audio/voices",new{name="reference",ref_text=job.Options.ReferenceTranscript,wav_b64=Convert.ToBase64String(await File.ReadAllBytesAsync(reference,ct))},ct);
            if(!result.IsSuccessStatusCode)throw new Exception("Qwen reference error: "+await result.Content.ReadAsStringAsync(ct));referenceKey=key;
        }
        phase($"Generating take {job.Attempts} with Qwen");
        using var request=new HttpRequestMessage(HttpMethod.Post,"/v1/audio/speech"){Content=JsonContent.Create(new{input=job.SpokenText,voice="reference",response_format="wav",language="English",seed=unchecked(job.Options.Seed+job.Attempts-1),max_new_tokens=2048,temperature=.8,top_p=.95,repetition_penalty=1.05})};
        using var audio=await http!.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!audio.IsSuccessStatusCode)throw new Exception("Qwen generation error: "+await audio.Content.ReadAsStringAsync(ct));
        var raw=Path.Combine(work,"speech.wav");await using(var file=File.Create(raw))await audio.Content.CopyToAsync(file,ct);
        if(new FileInfo(raw).Length<100)throw new Exception("Qwen returned empty audio.");
    }
    public async Task<string> TranscribeAsync(string audio,string work,CancellationToken ct)
    {
        var exe=Path.Combine(root,"engines","whisper","whisper-cli.exe");var model=Path.Combine(root,"engines","whisper","ggml-small.en.bin");
        if(!File.Exists(exe)||!File.Exists(model))throw new FileNotFoundException("Local Whisper verification files are missing. Run scripts/setup-qwen.ps1.");
        var wav=Path.Combine(work,"asr-input.wav");var output=Path.Combine(work,"asr-result");
        await EncodeAsync(["-i",audio,"-ar","16000","-ac","1","-c:a","pcm_s16le",wav],ct);
        // Do not supply target text: that would bias the independent recognition check.
        await RunAsync(exe,["-m",model,"-f",wav,"-l","en","-t","4","-ng","-nt","-oj","-of",output],ct,TimeSpan.FromMinutes(15));
        using var result=JsonDocument.Parse(await File.ReadAllTextAsync(output+".json",ct));
        return string.Join(" ",result.RootElement.GetProperty("transcription").EnumerateArray().Select(x=>x.GetProperty("text").GetString())).Trim();
    }
    public Task EncodeAsync(IEnumerable<string> args,CancellationToken ct)=>RunAsync(Encoder,new[]{"-hide_banner","-nostdin","-v","error","-y"}.Concat(args),ct,TimeSpan.FromMinutes(10));
    private async Task RunAsync(string exe,IEnumerable<string> args,CancellationToken ct,TimeSpan limit)
    {
        if(!File.Exists(exe))throw new FileNotFoundException("Required native tool is missing",exe);
        var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=Path.GetDirectoryName(exe)!};
        foreach(var arg in args)info.ArgumentList.Add(arg);
        using var tool=Process.Start(info)??throw new Exception("Could not start "+Path.GetFileName(exe));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(limit);
        using var cancel=timeout.Token.Register(()=>{try{if(!tool.HasExited)tool.Kill(true);}catch(InvalidOperationException){}});
        var error=tool.StandardError.ReadToEndAsync();var output=tool.StandardOutput.ReadToEndAsync();
        try{await tool.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new TimeoutException(Path.GetFileName(exe)+" timed out.");}
        var message=await error;await output;if(message.Length>0)Log(message.Length>4000?message[^4000..]:message);
        if(tool.ExitCode!=0)throw new Exception(Path.GetFileName(exe)+" failed: "+message);
    }
    public void Stop(){try{if(process is {HasExited:false})process.Kill(true);}catch(InvalidOperationException){}process?.Dispose();process=null;http?.Dispose();http=null;referenceKey=null;Status?.Invoke("Qwen idle · loads when needed");}
    public void Dispose()=>Stop();
}
