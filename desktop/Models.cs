using System.Text;
using System.Text.RegularExpressions;

namespace JarvisStudio;

public static partial class TextRules
{
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    public static string Clean(string? text) => Spaces().Replace((text ?? "").Normalize(NormalizationForm.FormC), " ").Trim();
    public static string[] Lines(string? raw) => (raw ?? "").Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).Select(Clean).Where(x => x.Length > 0).ToArray();
}

public record Settings
{
    public string Folder {get;set;} = "";
    public string Format {get;set;} = "flac";
    public string Reference {get;set;} = "";
    public double Speed {get;set;} = 1;
    public string Tone {get;set;} = "natural";
    public bool Normalize {get;set;} = true;
    public string ReferenceTranscript {get;set;} = "";
    public bool ReferenceVerified {get;set;}
    public string ReferenceHash {get;set;} = "";
    public string Delivery {get;set;} = "Neutral";
    public List<PronunciationRule> Lexicon {get;set;} = [];
    public int MaxRetries {get;set;} = 1;
    public int Seed {get;set;} = 42;
    public string Backend {get;set;} = "auto";
}
public record PronunciationRule(string Word, string SayAs);
public record SpeechCheck(string Transcript, string Expected, int Errors, double WordErrorRate, string[] Differences)
{
    public bool Passed => Errors == 0;
}
public record ListeningReview(int Voice=0, int Tone=0, int Emotion=0, string Notes="");
public record Job
{
    public string Id {get;set;} = Guid.NewGuid().ToString("N");
    public string BatchId {get;set;} = "";
    public string Text {get;set;} = "";
    public string Notes {get;set;} = "";
    public string Status {get;set;} = "pending";
    public string Phase {get;set;} = "Waiting";
    public string? Error {get;set;}
    public string? Path {get;set;}
    public DateTime Created {get;set;} = DateTime.UtcNow;
    public double Seconds {get;set;}
    public Settings Options {get;set;} = new();
    public string SpokenText {get;set;} = "";
    public SpeechCheck? Verification {get;set;}
    public int Attempts {get;set;}
    public int GenerationSeed {get;set;}
    public bool Archived {get;set;}
    public int FileRevision {get;set;}
    public bool FileBusy {get;set;}
    public double AppliedSpeed {get;set;} = 1;
    public ListeningReview Review {get;set;} = new();
}
public record DiskState(Settings Settings, List<Job> Jobs, string Root="", int Version=3);
public record Snapshot(Settings Settings, List<Job> Jobs, bool Paused, string Engine, string? Notice);
public record InputPrompt(string Text, string? Notes, Settings? Options);
