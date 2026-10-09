using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using System.Security.Cryptography;

namespace JarvisStudio;

public sealed partial class StudioService : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web){WriteIndented=true};
    private readonly string root;
    private readonly string statePath;
    private readonly object sync=new();
    private readonly SemaphoreSlim saveLock=new(1,1);
    private readonly SemaphoreSlim libraryLock=new(1,1);
    private readonly Channel<string> queue=Channel.CreateUnbounded<string>(new(){SingleReader=true});
    private readonly CancellationTokenSource shutdown=new();
    private readonly IVoiceEngine engine;
    public RuntimeFiles Owned {get;}
    private readonly List<Job> jobs=[];
    private readonly Task worker;
    private Settings settings;
    private bool paused;
    private TaskCompletionSource gate=OpenGate();
    private CancellationTokenSource? current;
    private string engineState="Engine idle · loads when needed";
    private string? notice;
    public event Action<Snapshot>? Changed;
    public StudioService(string root,IVoiceEngine? engine=null)
    {
        this.root=root;Owned=new RuntimeFiles(root);statePath=Path.Combine(root,"user-data","state.json");
        this.engine=engine??new NativeEngine(root);
        var bundledReference=Path.Combine(root,"references","reference.mp3");
        settings=new(){Folder=Path.Combine(root,"recordings"),Reference=File.Exists(bundledReference)?bundledReference:""};
        var transcript=Path.Combine(root,"references","reference.txt");if(File.Exists(transcript))settings.ReferenceTranscript=File.ReadAllText(transcript).Trim();
        if(File.Exists(statePath))try{
            var saved=JsonSerializer.Deserialize<DiskState>(File.ReadAllText(statePath),Json);
            if(saved!=null){settings=saved.Settings;jobs=saved.Jobs;Rebase(saved.Root);}
            foreach(var job in jobs){job.FileBusy=false;if(job.Status=="processing"){job.Status="pending";job.Phase="Recovered after restart";}}
        }catch(Exception e){notice="Could not restore the previous session: "+e.Message;}
        if(jobs.Any(j=>j.Status=="pending")){paused=true;gate=new(TaskCreationOptions.RunContinuationsAsynchronously);}
        foreach(var job in jobs.Where(j=>j.Status=="pending"))queue.Writer.TryWrite(job.Id);
        this.engine.Status+=message=>{lock(sync)engineState=message;Publish();};
        CleanOrphanWork();worker=Task.Run(RunAsync);
    }
    private static TaskCompletionSource OpenGate(){var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);t.SetResult();return t;}
    private static Settings Copy(Settings s)=>s with{Lexicon=s.Lexicon.ToList()};
    public Snapshot Snapshot(){lock(sync)return new(Copy(settings),jobs.Select(j=>j with{Options=Copy(j.Options)}).ToList(),paused,engineState,notice);}
    private void Publish()=>Changed?.Invoke(Snapshot());
    private async Task SaveAsync()
    {
        await saveLock.WaitAsync();
        try{var state=Snapshot();Owned.CreateDirectory(Path.GetDirectoryName(statePath)!);
            var temp=statePath+".tmp";await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(new DiskState(state.Settings,state.Jobs,root),Json));File.Move(temp,statePath,true);Owned.Track(statePath);
        }catch(Exception e){lock(sync)notice="Session could not be saved: "+e.Message;Publish();}
        finally{saveLock.Release();}
    }
    public static Settings Validate(Settings value)
    {
        if(!new[]{"flac","wav","mp3"}.Contains(value.Format))throw new ArgumentException("Select FLAC, WAV, or MP3.");
        if(value.Speed<.5||value.Speed>2||!double.IsFinite(value.Speed))throw new ArgumentException("Speed must be between 0.5× and 2×.");
        if(string.IsNullOrWhiteSpace(value.Folder)||!Path.IsPathFullyQualified(value.Folder))throw new ArgumentException("Choose an absolute output folder.");
        if(value.MaxRetries<0||value.MaxRetries>3)throw new ArgumentException("Select between zero and three retries.");
        if(value.Backend is not ("auto" or "cpu"))throw new ArgumentException("Choose automatic GPU selection or CPU.");
        if(value.Lexicon.Count>100||value.Lexicon.Any(r=>string.IsNullOrWhiteSpace(r.Word)||string.IsNullOrWhiteSpace(r.SayAs)||r.Word.Length>100||r.SayAs.Length>300))throw new ArgumentException("Use up to 100 pronunciation rules with nonempty words and replacements.");
        if(value.Lexicon.Select(r=>r.Word).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=value.Lexicon.Count)throw new ArgumentException("Pronunciation words must be unique.");
        if(value.ReferenceTranscript.Length>5000)throw new ArgumentException("Reference transcript is too long.");
        return value;
    }
    public async Task UpdateSettingsAsync(Settings value){Validate(value);value=Copy(value) with{Speed=1,Tone="natural"};
        if(string.IsNullOrWhiteSpace(value.ReferenceTranscript))value.ReferenceTranscript=await DraftReferenceAsync(value.Reference);
        value.ReferenceHash=await ReferenceHashAsync(value.Reference);
        lock(sync)settings=value;await SaveAsync();Publish();}
    private static async Task<string> ReferenceHashAsync(string path){await using var file=File.OpenRead(path);if(file.Length>25*1024*1024)throw new ArgumentException("Use a short reference recording under 25 MB.");return Convert.ToHexString(await SHA256.HashDataAsync(file));}
    public async Task EnqueueAsync(InputPrompt[] prompts)
    {
        if(prompts.Length==0||prompts.Length>500)throw new ArgumentException("Stage between 1 and 500 prompts per batch.");
        var batch=Guid.NewGuid().ToString("N");var config=Snapshot().Settings;
        // Validate the whole batch before accepting any task.
        var incoming=prompts.Select(p=>{
            var text=TextRules.Clean(p.Text);if(text.Length==0)throw new ArgumentException("A prompt is empty after sanitizing.");
            if(SpeechValidation.Tokens(text).Length==0)throw new ArgumentException("Each prompt must contain words or numbers to speak; punctuation is preserved.");
            if(text.Length>1500)throw new ArgumentException("For reliable verification, keep each line under 1,500 characters. Split longer scripts across lines.");
            var options=Copy(Validate(p.Options??config)) with{Speed=1,Tone="natural"};
            var spoken=Pronunciation.Apply(text,options.Lexicon);if(spoken.Length>4000)throw new ArgumentException("Pronunciation replacements made a line too long. Split it into shorter lines.");
            return new Job{BatchId=batch,Text=text,SpokenText=spoken,Notes=p.Notes??"",Options=options};
        }).ToArray();
        foreach(var group in incoming.GroupBy(x=>(x.Options.Reference,x.Options.ReferenceTranscript,x.Options.ReferenceHash))){
            var options=group.First().Options;var hash=await ReferenceHashAsync(options.Reference);var transcript=options.ReferenceTranscript;
            if(string.IsNullOrWhiteSpace(transcript)||(!string.IsNullOrEmpty(options.ReferenceHash)&&hash!=options.ReferenceHash))transcript=await DraftReferenceAsync(options.Reference);
            foreach(var job in group){job.Options.ReferenceHash=hash;job.Options.ReferenceTranscript=transcript;}
        }
        lock(sync)jobs.AddRange(incoming);
        await SaveAsync();foreach(var job in incoming)queue.Writer.TryWrite(job.Id);Publish();
    }
    public async Task SetPausedAsync(bool value)
    {
        lock(sync){if(paused!=value){paused=value;if(value)gate=new(TaskCreationOptions.RunContinuationsAsynchronously);else gate.TrySetResult();}}
        await SaveAsync();Publish();
    }
    public void CancelCurrent(){lock(sync)current?.Cancel();}
    public async Task RemovePendingAsync(string id)
    {
        lock(sync){var job=jobs.Find(j=>j.Id==id);if(job is {Status:"pending"})jobs.Remove(job);else throw new ArgumentException("Only waiting tasks can be removed.");}
        await SaveAsync();Publish();
    }
    public async Task ClearFinishedAsync(){lock(sync)foreach(var job in jobs.Where(j=>j.Status is "completed" or "failed" or "review"))job.Archived=true;await SaveAsync();Publish();}
    public void Unload(){lock(sync){if(current!=null)throw new InvalidOperationException("Pause the queue and wait for processing to finish before releasing the model.");engine.Stop();}}
    private async Task RunAsync()
    {
        try{await foreach(var id in queue.Reader.ReadAllAsync(shutdown.Token))
        {
            Job? job;
            while(true){Task wait;lock(sync)wait=gate.Task;await wait.WaitAsync(shutdown.Token);
                lock(sync){if(paused)continue;job=jobs.Find(j=>j.Id==id&&j.Status=="pending");if(job!=null){job.Status="processing";job.Phase="Starting";current=CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);}break;}}
            if(job==null)continue;
            Publish();await SaveAsync();
            var clock=Stopwatch.StartNew();var work=Path.Combine(root,"user-data","work",job.Id);string? staging=null;
            try{
                Owned.CreateDirectory(work);Owned.CreateDirectory(job.Options.Folder);
                if(await ReferenceHashAsync(job.Options.Reference)!=job.Options.ReferenceHash)throw new Exception("The reference changed after this task was queued. Regenerate to use the updated reference.");
                var name=$"jarvis-{job.Created:yyyyMMdd-HHmmss}-{job.Id[..8]}.{job.Options.Format}";
                var output=Path.Combine(job.Options.Folder,name);staging=output+".partial";
                SpeechCheck? best=null;int bestSeed=job.Options.Seed;var bestFile=Path.Combine(work,"best."+job.Options.Format);
                for(int attempt=1;attempt<=job.Options.MaxRetries+1;attempt++){
                    lock(sync)job.Attempts=attempt;
                    await engine.GenerateAsync(job,work,message=>{lock(sync)job.Phase=message;Publish();},current!.Token);
                    lock(sync)job.Phase="Exporting at original speed";Publish();
                    await engine.EncodeAsync(ExportArgs(Path.Combine(work,"speech.wav"),staging,job.Options.Format,job.Options.Normalize?"loudnorm=I=-18:TP=-1.5:LRA=11":"anull"),current.Token);
                    lock(sync)job.Phase=$"Checking spoken words · attempt {attempt}";Publish();
                    var heard=await engine.TranscribeAsync(staging,work,current.Token);
                    var check=SpeechValidation.Compare(job.SpokenText,heard,job.Options.Lexicon);
                    if(best==null||check.Errors<best.Errors){best=check;bestSeed=unchecked(job.Options.Seed+attempt-1);File.Copy(staging,bestFile,true);}
                    lock(sync)job.Verification=check;Publish();
                    if(check.Passed)break;
                }
                current!.Token.ThrowIfCancellationRequested();
                File.Copy(bestFile,staging,true);lock(sync){job.Verification=best;job.GenerationSeed=bestSeed;}
                if(!File.Exists(staging)||new FileInfo(staging).Length<64)throw new Exception("Audio encoder did not produce a valid file.");
                File.Move(staging,output,false);staging=null;Owned.Track(output,"audio");
                lock(sync){job.Path=output;job.Status=job.Verification!.Passed?"completed":"review";job.Phase=job.Verification.Passed?"Words checked · listen to assess voice":"Word mismatch · listening review needed";job.Seconds=clock.Elapsed.TotalSeconds;}
            }catch(OperationCanceledException){engine.Stop();lock(sync){job.Status=shutdown.IsCancellationRequested?"pending":"failed";job.Error=shutdown.IsCancellationRequested?null:"Cancelled by you. Regenerate to try again.";job.Phase="Stopped";}}
            catch(Exception e){lock(sync){job.Status="failed";job.Error=e.Message;job.Phase="Needs attention";}engine.Stop();}
            finally{
                try{Directory.CreateDirectory(Path.Combine(root,"user-data"));await File.WriteAllTextAsync(Path.Combine(root,"user-data","native-engine.log"),engine.LastLog);Owned.Track(Path.Combine(root,"user-data","native-engine.log"));}catch(IOException){}
                try{if(staging!=null)File.Delete(staging);CleanWork(work);}catch(IOException){}
                lock(sync){current?.Dispose();current=null;}await SaveAsync();Publish();
            }
        }}catch(OperationCanceledException){}
    }
    public async Task StopAsync(){shutdown.Cancel();try{await worker;}catch(OperationCanceledException){}await libraryLock.WaitAsync();try{engine.Dispose();await SaveAsync();}finally{libraryLock.Release();}}
    public void Dispose(){shutdown.Cancel();engine.Dispose();}
}

