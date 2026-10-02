using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JarvisStudio;

public static class Pronunciation
{
    public static string Apply(string text, IReadOnlyList<PronunciationRule> rules)
    {
        if(rules.Count==0)return text;
        var map=rules.ToDictionary(x=>x.Word,x=>x.SayAs,StringComparer.OrdinalIgnoreCase);
        var pattern=@"(?<![\p{L}\p{N}])(?:"+string.Join("|",map.Keys.OrderByDescending(x=>x.Length).Select(Regex.Escape))+@")(?![\p{L}\p{N}])";
        // One pass: replacement words never recursively trigger another rule.
        return Regex.Replace(text,pattern,m=>map[m.Value],RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    }
}

public static class SpeechValidation
{
    public static string[] Tokens(string text) => Regex.Matches(text.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Replace("’", "'").Replace("'", ""),@"[\p{L}\p{N}]+")
        .Select(m=>m.Value).ToArray();
    public static SpeechCheck Compare(string expected, string actual, IReadOnlyList<PronunciationRule>? lexicon=null)
    {
        var a=Tokens(expected);var literal=Tokens(actual);
        // An exact spoken-text match wins before aliases, so overlapping rules cannot corrupt a correct ASR transcript.
        if(a.SequenceEqual(literal))return new(actual,expected,0,0,[]);
        var b=Tokens(Pronunciation.Apply(actual,lexicon??[]));
        if(a.Length>6000||b.Length>6000)throw new ArgumentException("Speech validation text is too long.");
        var d=new int[a.Length+1,b.Length+1];
        for(int i=0;i<=a.Length;i++)d[i,0]=i;for(int j=0;j<=b.Length;j++)d[0,j]=j;
        for(int i=1;i<=a.Length;i++)for(int j=1;j<=b.Length;j++)d[i,j]=Math.Min(d[i-1,j]+1,Math.Min(d[i,j-1]+1,d[i-1,j-1]+(a[i-1]==b[j-1]?0:1)));
        var changes=new List<string>();int x=a.Length,y=b.Length;
        while(x>0||y>0){
            if(x>0&&y>0&&d[x,y]==d[x-1,y-1]+(a[x-1]==b[y-1]?0:1)){if(a[x-1]!=b[y-1])changes.Add($"Expected ‘{a[x-1]}’, heard ‘{b[y-1]}’");x--;y--;}
            else if(x>0&&d[x,y]==d[x-1,y]+1){changes.Add($"Missing ‘{a[--x]}’");}
            else changes.Add($"Extra ‘{b[--y]}’");
        }
        changes.Reverse();return new(actual,expected,d[a.Length,b.Length],(double)d[a.Length,b.Length]/Math.Max(1,a.Length),changes.ToArray());
    }
}
