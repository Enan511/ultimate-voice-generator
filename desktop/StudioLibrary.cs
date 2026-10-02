using System.Globalization;

namespace JarvisStudio;

public sealed partial class StudioService
{
    private static IEnumerable<string> ExportArgs(string input,string output,string format,string filter)
    {
        var args=new List<string>{"-i",input,"-af",filter,"-ar","24000","-ac","1","-c:a",format switch{"mp3"=>"libmp3lame","flac"=>"flac",_=>"pcm_s16le"}};
        if(format=="mp3")args.AddRange(["-b:a","192k"]);args.AddRange(["-f",format,output]);return args;
    }
    private void CleanWork(string path)
    {
        var full=Path.GetFullPath(path);var parent=Path.GetFullPath(Path.Combine(root,"user-data","work"));
        if(Path.GetDirectoryName(full)!=parent||!Guid.TryParseExact(Path.GetFileName(full),"N",out _))throw new InvalidOperationException("Invalid scratch directory.");
        if(Directory.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)==0)Directory.Delete(full,true);
    }
    private void CleanOrphanWork()
    {
        var parent=Path.Combine(root,"user-data","work");if(!Directory.Exists(parent))return;
        foreach(var path in Directory.EnumerateDirectories(parent))if(Guid.TryParseExact(Path.GetFileName(path),"N",out _))try{CleanWork(path);}catch(IOException){}catch(UnauthorizedAccessException){}
        // Only remove staging files belonging to known generated records after an interrupted run.
        foreach(var job in jobs.Where(j=>Guid.TryParseExact(j.Id,"N",out _)))try{var path=Path.Combine(job.Options.Folder,$"jarvis-{job.Created:yyyyMMdd-HHmmss}-{job.Id[..8]}.{job.Options.Format}.partial");File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}
        foreach(var job in jobs.Where(j=>j.Path!=null))try{
            CheckManagedFile(job);var parentFolder=Path.GetDirectoryName(job.Path!)!;var prefix=Path.GetFileName(job.Path)+".speed-";
            if(Directory.Exists(parentFolder))foreach(var temp in Directory.EnumerateFiles(parentFolder,prefix+"*.partial")){
                var suffix=Path.GetFileName(temp)[prefix.Length..^8];if(Guid.TryParseExact(suffix,"N",out _))File.Delete(temp);
            }
        }catch(IOException){}catch(UnauthorizedAccessException){}catch(InvalidOperationException){}
    }
    private void Rebase(string previousRoot)
    {
        string Move(string path){
            if(string.IsNullOrEmpty(path))return path;
            if(!Path.IsPathFullyQualified(path))return Path.GetFullPath(Path.Combine(root,path));
            if(!string.IsNullOrEmpty(previousRoot)&&path.StartsWith(Path.TrimEndingDirectorySeparator(previousRoot)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return Path.Combine(root,Path.GetRelativePath(previousRoot,path));
            return path;
        }
        settings.Folder=Move(settings.Folder);settings.Reference=Move(settings.Reference);settings.Speed=1;settings.Tone="natural";
        foreach(var job in jobs){job.Options.Folder=Move(job.Options.Folder);job.Options.Reference=Move(job.Options.Reference);if(job.Path!=null)job.Path=Move(job.Path);}
        // Migrate the bundled v2 reference without treating its missing transcript as verified.
        if(settings.Reference.Contains("native-engine",StringComparison.OrdinalIgnoreCase)){
            settings.Reference=Path.Combine(root,"references","reference.mp3");settings.ReferenceVerified=false;settings.ReferenceHash="";
            var transcript=Path.Combine(root,"references","reference.txt");if(File.Exists(transcript))settings.ReferenceTranscript=File.ReadAllText(transcript).Trim();
        }
    }
    public async Task<string> ImportReferenceAsync(string source)
    {
        var ext=Path.GetExtension(source).ToLowerInvariant();if(!new[]{".wav",".flac",".mp3"}.Contains(ext))throw new ArgumentException("Choose WAV, FLAC, or MP3.");
        if(new FileInfo(source).Length>25*1024*1024)throw new ArgumentException("Reference must be under 25 MB.");
        var folder=Path.Combine(root,"references");Owned.CreateDirectory(folder);var dest=Path.Combine(folder,Guid.NewGuid().ToString("N")+ext);
        await using(var input=File.OpenRead(source))await using(var output=File.Create(dest))await input.CopyToAsync(output,shutdown.Token);Owned.Track(dest);return dest;
    }
    public async Task<string> DraftReferenceAsync(string reference)
    {
        await libraryLock.WaitAsync(shutdown.Token);var work=Path.Combine(root,"user-data","work",Guid.NewGuid().ToString("N"));
        try{await ReferenceHashAsync(reference);Owned.CreateDirectory(work);return await engine.TranscribeAsync(reference,work,shutdown.Token);}
        finally{try{CleanWork(work);}finally{libraryLock.Release();}}
    }
    private void CheckManagedFile(Job job)
    {
        if(job.Path==null)return;
        if(!Guid.TryParseExact(job.Id,"N",out _)||Path.GetFileName(job.Path)!=$"jarvis-{job.Created:yyyyMMdd-HHmmss}-{job.Id[..8]}.{job.Options.Format}")throw new InvalidOperationException("This file was not created by this app; it cannot be overwritten or deleted here.");
        if(!RuntimeFiles.IsUnlinked(job.Path))throw new IOException("Linked audio paths cannot be changed.");
        if(File.Exists(job.Path)&&!Owned.Matches(job.Path))throw new IOException("This audio was changed outside the app. It has been left untouched.");
    }
    public async Task DeleteRecordAsync(string id)
    {
        await libraryLock.WaitAsync(shutdown.Token);
        try{
            Job job;lock(sync){job=jobs.Find(x=>x.Id==id)??throw new ArgumentException("Record not found.");if(job.Status is "pending" or "processing"||job.FileBusy)throw new InvalidOperationException("Wait until this task finishes.");job.FileBusy=true;}Publish();
            try{CheckManagedFile(job);if(job.Path!=null){File.Delete(job.Path);Owned.Forget(job.Path);}lock(sync)jobs.Remove(job);await SaveAsync();}
            finally{lock(sync)job.FileBusy=false;Publish();}
        }finally{libraryLock.Release();}
    }
    public async Task ReviewAsync(string id,ListeningReview review)
    {
        if(new[]{review.Voice,review.Tone,review.Emotion}.Any(v=>v<0||v>5)||review.Notes.Length>2000)throw new ArgumentException("Ratings must be between zero and five.");
        lock(sync){var job=jobs.Find(x=>x.Id==id)??throw new ArgumentException("Record not found.");job.Review=review;}await SaveAsync();Publish();
    }
    public async Task UpdateFileSpeedAsync(string id,double speed)
    {
        if(!double.IsFinite(speed)||speed<.5||speed>2)throw new ArgumentException("Speed must be 0.5×–2×.");
        await libraryLock.WaitAsync(shutdown.Token);Job? job=null;string? staging=null;var work=Path.Combine(root,"user-data","work",Guid.NewGuid().ToString("N"));
        try{
            lock(sync){job=jobs.Find(x=>x.Id==id)??throw new ArgumentException("Recording not found.");if(job.Status is not ("completed" or "review")||job.Path==null||job.FileBusy)throw new InvalidOperationException("This recording is not ready for editing.");job.FileBusy=true;}Publish();
            CheckManagedFile(job);Owned.CreateDirectory(work);staging=job.Path+".speed-"+Guid.NewGuid().ToString("N")+".partial";
            await engine.EncodeAsync(ExportArgs(job.Path!,staging,job.Options.Format,"atempo="+speed.ToString(CultureInfo.InvariantCulture)),shutdown.Token);
            var transcript=await engine.TranscribeAsync(staging,work,shutdown.Token);
            var check=SpeechValidation.Compare(string.IsNullOrEmpty(job.SpokenText)?job.Text:job.SpokenText,transcript,job.Options.Lexicon);
            shutdown.Token.ThrowIfCancellationRequested();
            File.Replace(staging,job.Path!,null);staging=null;Owned.Track(job.Path!,"audio");
            lock(sync){job.FileRevision++;job.AppliedSpeed*=speed;job.Verification=check;job.Status=check.Passed?"completed":"review";job.Review=new();job.Phase="Speed updated · listening review reset";}
            await SaveAsync();
        }finally{try{if(staging!=null&&File.Exists(staging))File.Delete(staging);CleanWork(work);}finally{if(job!=null)lock(sync)job.FileBusy=false;Publish();libraryLock.Release();}}
    }
}

