using JarvisStudio;
using System.Diagnostics;
using System.Text.Json;

static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
static async Task Until(Func<bool> condition,int seconds=30){var start=Stopwatch.StartNew();while(!condition()){if(start.Elapsed.TotalSeconds>seconds)throw new TimeoutException("Test timed out");await Task.Delay(50);}}
var native=args.Contains("--native");
var root=Path.GetFullPath("test-artifacts/"+(native?"qwen-native-":"core-")+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
if(native){
    using var service=new StudioService(root,new NativeEngine(Path.GetFullPath(".")));
    string previous="";service.Changed+=s=>{var text=string.Join(", ",s.Jobs.Select(j=>j.Status+":"+j.Phase));if(text!=previous){Console.WriteLine(text);previous=text;}};
    var config=service.Snapshot().Settings with{Reference=Path.GetFullPath("references/reference.mp3"),ReferenceTranscript=File.ReadAllText("references/reference.txt").Trim(),MaxRetries=1};

    await service.UpdateSettingsAsync(config);
    var sentence=args.Contains("--short")?"Good morning, sir.":"Good morning, sir. Systems awake. Driver status remains under evaluation.";
    await service.EnqueueAsync([new(sentence,null,null)]);
    await Until(()=>service.Snapshot().Jobs.All(j=>j.Status is "completed" or "review" or "failed"),1800);
    var job=service.Snapshot().Jobs.Single();await File.WriteAllTextAsync("test-artifacts/qwen-native-results.json",JsonSerializer.Serialize(service.Snapshot(),StudioService.Json));
    Check(job.Path!=null&&job.Verification!=null,job.Error??"No generated/verified audio");
    if(args.Contains("--speed")){
        await service.UpdateFileSpeedAsync(job.Id,1.25);
        await File.WriteAllTextAsync("test-artifacts/qwen-speed-results.json",JsonSerializer.Serialize(service.Snapshot(),StudioService.Json));
        Check(service.Snapshot().Jobs.Single().FileRevision==1,"Speed revision missing");
    }
    await service.StopAsync();Console.WriteLine("PASS: actual Qwen generation, local ASR and audio export. Word check: "+job.Verification!.Passed);return;
}
const string punct="Don't strip [sir]: ‘hello’ — 2+2=4 / \"yes\" (now)… 🤖!";
Check(TextRules.Clean(punct)==punct,"Punctuation changed");
Check(TextRules.Clean("Cafe\u0301\t東京 ١٢٣ 😀")=="Café 東京 ١٢٣ 😀","Unicode mismatch");
Check(TextRules.Lines("one\r\n\n two\rthree").SequenceEqual(new[]{"one","two","three"}),"Line splitting mismatch");
var rules=new[]{new PronunciationRule("API","A P I"),new PronunciationRule("sir","sir"),new PronunciationRule("Dr. Smith","Doctor Smith")};
Check(Pronunciation.Apply("Dr. Smith's API, rapidly!",rules)=="Doctor Smith's A P I, rapidly!","Lexicon boundaries/longest match");
Check(Pronunciation.Apply("a b",[new("a","b"),new("b","c")])=="b c","Rules cascaded");
var dropped=SpeechValidation.Compare("Good morning, sir.","Good morning.");Check(!dropped.Passed&&dropped.Differences.Contains("Missing ‘sir’"),"Dropped sir passed");
Check(!SpeechValidation.Compare("Driver status ready.","Driving status ready.").Passed,"Substitution passed");
Check(SpeechValidation.Compare("Don't wait, sir!","don’t wait sir").Passed,"Punctuation caused false mismatch");
Check(SpeechValidation.Compare("A P I ready","API ready",rules).Passed,"Acronym lexicon validation failed");
Check(SpeechValidation.Compare("b c","b c",[new("a","b"),new("b","c")]).Passed,"Alias rules corrupted an exact transcript");
var fake=new FakeEngine();using var studio=new StudioService(root,fake);
var reference=Path.Combine(root,"reference.wav");await File.WriteAllBytesAsync(reference,new byte[128]);
await studio.UpdateSettingsAsync(studio.Snapshot().Settings with{Reference=reference,ReferenceTranscript="Test reference."});
await studio.SetPausedAsync(true);
await studio.EnqueueAsync([new("one [test]",null,null),new("retry sir",null,null),new("mismatch sir",null,null)]);
await Task.Delay(100);Check(studio.Snapshot().Jobs.All(j=>j.Status=="pending"),"Paused queue started");
await studio.SetPausedAsync(false);await Until(()=>studio.Snapshot().Jobs.All(j=>j.Status is "completed" or "review"));
var records=studio.Snapshot().Jobs;
Check(fake.Order.SequenceEqual(new[]{"one [test]","retry sir","retry sir","mismatch sir","mismatch sir"}),"FIFO/retry order wrong");
Check(records[1].Attempts==2&&records[1].Verification!.Passed,"Retry did not recover");Check(records[2].Status=="review","Bad speech was falsely completed");
Check(records.Select(j=>j.Path).Distinct().Count()==3,"Output collision");
Check(!Directory.EnumerateDirectories(Path.Combine(root,"user-data","work")).Any(),"Scratch files left behind");
await studio.ClearFinishedAsync();Check(studio.Snapshot().Jobs.Count==3&&studio.Snapshot().Jobs.All(j=>j.Archived),"History lost during clear");
var first=records[0];var before=await File.ReadAllBytesAsync(first.Path!);fake.FailEncoding=true;
try{await studio.UpdateFileSpeedAsync(first.Id,1.2);throw new Exception("Failed export accepted");}catch(IOException){}
Check((await File.ReadAllBytesAsync(first.Path!)).SequenceEqual(before),"Failed update destroyed original");Check(!studio.Snapshot().Jobs[0].FileBusy,"File lock stuck");
fake.FailEncoding=false;await studio.UpdateFileSpeedAsync(first.Id,1.2);Check(studio.Snapshot().Jobs[0].FileRevision==1,"Speed update missing");
await studio.ReviewAsync(first.Id,new(4,3,2,"test"));Check(studio.Snapshot().Jobs[0].Review.Voice==4,"Review not saved");
await studio.DeleteRecordAsync(first.Id);Check(!File.Exists(first.Path)&&studio.Snapshot().Jobs.Count==2,"Delete did not remove audio and history");
var count=studio.Snapshot().Jobs.Count;
try{await studio.EnqueueAsync([new("accepted",null,null),new("  ",null,null)]);throw new Exception("Empty batch accepted");}catch(ArgumentException){}
Check(studio.Snapshot().Jobs.Count==count,"Batch partially accepted");
await studio.EnqueueAsync([new("slow",null,null),new("after cancellation",null,null)]);
await Until(()=>studio.Snapshot().Jobs.Any(j=>j.Text=="slow"&&j.Status=="processing"));studio.CancelCurrent();await Until(()=>studio.Snapshot().Jobs.Last().Status=="completed");
Check(studio.Snapshot().Jobs.Find(j=>j.Text=="slow")!.Status=="failed","Cancellation missing");
await File.AppendAllTextAsync(reference,"changed");await studio.EnqueueAsync([new("changed reference",null,null)]);await Until(()=>studio.Snapshot().Jobs.Last().Status=="completed");Check(studio.Snapshot().Jobs.Last().Options.ReferenceTranscript=="one test","Changed reference was not transcribed automatically");
await studio.StopAsync();
var leftover=studio.Snapshot().Jobs.First(j=>j.Path!=null).Path+".speed-"+Guid.NewGuid().ToString("N")+".partial";await File.WriteAllTextAsync(leftover,"orphan");
using var restored=new StudioService(root,new FakeEngine());Check(restored.Snapshot().Jobs.Count==5,"Persisted history lost");Check(!File.Exists(leftover),"Interrupted speed scratch was not cleaned");await restored.StopAsync();
var relocated=root+"-moved";Directory.CreateDirectory(Path.Combine(relocated,"user-data"));File.Copy(Path.Combine(root,"user-data","state.json"),Path.Combine(relocated,"user-data","state.json"));
using var moved=new StudioService(relocated,new FakeEngine());Check(moved.Snapshot().Settings.Folder.StartsWith(relocated)&&moved.Snapshot().Settings.Reference.StartsWith(relocated),"Internal settings paths were not rebased");Check(moved.Snapshot().Jobs.Where(j=>j.Path!=null).All(j=>j.Path!.StartsWith(relocated)),"History paths were not rebased");await moved.StopAsync();
// Uninstall ownership: leave unrelated files, edited files, external inputs and empty user folders intact.
var cleanupRoot=Path.Combine(root,"cleanup");Directory.CreateDirectory(cleanupRoot);
var owned=new RuntimeFiles(cleanupRoot);owned.CreateDirectory(Path.Combine(cleanupRoot,"user-data","webview"));
var unrelated=Path.Combine(cleanupRoot,"user-data","keep.txt");File.WriteAllText(unrelated,"user document");
var emptyUserFolder=Path.Combine(cleanupRoot,"empty-user-folder");Directory.CreateDirectory(emptyUserFolder);
var stateFile=Path.Combine(cleanupRoot,"user-data","state.json");File.WriteAllText(stateFile,"app state");owned.Track(stateFile);
var audioFile=Path.Combine(cleanupRoot,"clip.flac");File.WriteAllText(audioFile,"audio");owned.Track(audioFile,"audio");
var edited=Path.Combine(cleanupRoot,"edited.flac");File.WriteAllText(edited,"audio");owned.Track(edited,"audio");File.WriteAllText(edited,"replacement by user");
var initialCache=owned.ExistingUnownedCache();owned.CreateDirectory(Path.Combine(cleanupRoot,"user-data","webview","EBWebView"));var cache=Path.Combine(cleanupRoot,"user-data","webview","EBWebView","Local State");File.WriteAllText(cache,"cache");
var cacheDocument=Path.Combine(cleanupRoot,"user-data","webview","EBWebView","notes.json");File.WriteAllText(cacheDocument,"not a cache");owned.CaptureCache(initialCache);
Check(RuntimeFiles.Cleanup(cleanupRoot,false)==0,"Uninstall cleanup failed");
Check(!File.Exists(stateFile)&&!File.Exists(cache),"Settings/cache residue left");
Check(File.Exists(audioFile)&&File.Exists(edited)&&File.Exists(unrelated)&&File.Exists(cacheDocument)&&Directory.Exists(emptyUserFolder),"Cleanup deleted user data");
var cleanupAudio=new RuntimeFiles(cleanupRoot);cleanupAudio.Track(audioFile,"audio");
Check(RuntimeFiles.Cleanup(cleanupRoot,true)==0&&!File.Exists(audioFile)&&File.Exists(edited),"Optional recording cleanup failed");
Console.WriteLine("PASS: punctuation, lexicon, word alignment, strict retry/flag, FIFO, history, delete, atomic speed update/failure, ratings, reference binding, cancellation, persistence and cleanup.");

sealed class FakeEngine : IVoiceEngine
{
    public event Action<string>? Status;
    public string LastLog=>"test engine";
    public List<string> Order {get;}=[];
    public bool FailEncoding {get;set;}
    private readonly Dictionary<string,Job> active=[];
    public async Task GenerateAsync(Job job,string work,Action<string> phase,CancellationToken ct){Order.Add(job.Text);active[work]=job;Status?.Invoke("test");await Task.Delay(job.Text=="slow"?10000:10,ct);await File.WriteAllBytesAsync(Path.Combine(work,"speech.wav"),new byte[100],ct);}
    public Task<string> TranscribeAsync(string audio,string work,CancellationToken ct){if(!active.TryGetValue(work,out var job))return Task.FromResult("one test");return Task.FromResult(job.Text.StartsWith("mismatch")||job.Text.StartsWith("retry")&&job.Attempts==1?job.Text.Replace(" sir",""):job.SpokenText);}
    public Task EncodeAsync(IEnumerable<string> args,CancellationToken ct){if(FailEncoding)throw new IOException("Simulated encoding failure");return File.WriteAllBytesAsync(args.Last(),new byte[100],ct);}
    public void Stop(){}public void Dispose(){}
}


