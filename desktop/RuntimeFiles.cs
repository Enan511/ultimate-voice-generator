using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JarvisStudio;

// The uninstaller removes only files recorded by the app whose contents still match.
// Inno Setup separately owns the immutable files installed from the package.
public sealed class RuntimeFiles
{
    private const string Product = "UltimateVoiceGenerator-v1";
    private readonly string root, manifest;
    private readonly object sync = new();
    private Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
    private sealed record Entry(string Path, string Kind, long Size, string Hash);
    private sealed record Ledger(string Product, List<Entry> Files, List<string>? Directories = null);
    public RuntimeFiles(string root)
    {
        this.root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        manifest = Path.Combine(this.root, "user-data", "owned-files.json");
        if (File.Exists(manifest)) {
            var saved = JsonSerializer.Deserialize<Ledger>(File.ReadAllText(manifest));
            if (saved?.Product != Product) throw new IOException("Unrecognized file ownership record.");
            entries = saved.Files.ToDictionary(e => e.Path, StringComparer.OrdinalIgnoreCase);
            directories = (saved.Directories ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }
    private string Key(string path) => IsWithin(path, root) ? Path.GetRelativePath(root, path) : path;
    private string Resolve(string path) => Path.GetFullPath(Path.IsPathFullyQualified(path) ? path : Path.Combine(root, path));
    private static bool IsWithin(string path, string parent) => Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static bool IsUnlinked(string path)
    {
        for (string? p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
            if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
    private static Entry Read(string path, string key, string kind)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new(key, kind, file.Length, Convert.ToHexString(SHA256.HashData(file)));
    }
    private void Save()
    {
        EnsureDirectory(Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest + ".tmp", JsonSerializer.Serialize(new Ledger(Product, entries.Values.ToList(), directories.ToList())));
        File.Move(manifest + ".tmp", manifest, true);
    }
    private void EnsureDirectory(string path)
    {
        path = Path.GetFullPath(path);
        if (!IsUnlinked(path)) throw new IOException("Linked data folders cannot be used.");
        for (string? p = path; p != null && !Directory.Exists(p); p = Path.GetDirectoryName(p))
            if(IsWithin(p, root)) directories.Add(Key(p));
        Directory.CreateDirectory(path);
    }
    public void CreateDirectory(string path) { lock(sync) { EnsureDirectory(path); Save(); } }
    public void Track(string path, string kind = "data")
    {
        path = Path.GetFullPath(path);
        if (!IsUnlinked(path) || !File.Exists(path)) return;
        if (kind != "audio" && !IsWithin(path, root)) throw new IOException("Application data must stay inside its installation.");
        lock (sync) { var key = Key(path); entries[key] = Read(path, key, kind); Save(); }
    }
    public void Forget(string path) { lock(sync) { entries.Remove(Key(Path.GetFullPath(path))); Save(); } }
    public bool Matches(string path)
    {
        lock(sync) {
            if (!entries.TryGetValue(Key(Path.GetFullPath(path)), out var entry) || !IsUnlinked(path)) return false;
            if (!File.Exists(path)) return true;
            var current = Read(path, entry.Path, entry.Kind);
            return current.Size == entry.Size && current.Hash == entry.Hash;
        }
    }
    public HashSet<string> ExistingUnownedCache()
    {
        lock(sync) return CacheFiles().Where(f => !entries.ContainsKey(Key(f)))
            .Concat(SafeDirectories(Path.Combine(root,"user-data","webview")).Where(d=>!directories.Contains(Key(d))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    private static IEnumerable<string> SafeDirectories(string path)
    {
        if(!Directory.Exists(path) || !IsUnlinked(path)) yield break;
        foreach(var sub in Directory.EnumerateDirectories(path)) {
            if(!IsUnlinked(sub)) continue;
            yield return sub;
            foreach(var nested in SafeDirectories(sub)) yield return nested;
        }
    }
    private IEnumerable<string> CacheFiles()
    {
        var path = Path.Combine(root, "user-data", "webview");
        return SafeFiles(path);
    }
    private static IEnumerable<string> SafeFiles(string path)
    {
        if (!Directory.Exists(path) || !IsUnlinked(path)) yield break;
        foreach (var file in Directory.EnumerateFiles(path)) if(IsUnlinked(file)) yield return file;
        foreach (var sub in Directory.EnumerateDirectories(path)) foreach(var file in SafeFiles(sub)) yield return file;
    }
    public void CaptureCache(HashSet<string> preexistingUnowned)
    {
        lock(sync) {
            foreach(var path in CacheFiles()) {
                if (preexistingUnowned.Contains(path)) continue;
                // Claim recognized WebView artifacts only, never arbitrary files added while the app is open.
                if(!IsCacheArtifact(path)) continue;
                try {
                    var key = Key(path); entries[key] = Read(path, key, "cache");
                    for(var parent=Path.GetDirectoryName(path);parent!=null && IsWithin(parent,Path.Combine(root,"user-data","webview"));parent=Path.GetDirectoryName(parent))
                        if(!preexistingUnowned.Contains(parent)) directories.Add(Key(parent));
                }
                catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            Save();
        }
    }
    private static readonly HashSet<string> CacheNames = new((
        "Breadcrumbs|BrowserMetrics-spare.pma|CrashpadMetrics-active.pma|Last Version|Local State|Variations|VariationsRuntimeSeedV2|VariationsSafeSeedV2|VariationsSeedV2|"+
        "crl-set|metadata|settings.dat|throttle_store.dat|BookmarkMergedSurfaceOrdering|declarative_performance_observer.db|DIPS|ExtensionActivityEdge|Favicons|favorites_diagnostic.log|"+
        "History|LOCK|LOG|LOG.old|Login Data|Login Data For Account|Network Action Predictor|Preferences|README|Secure Preferences|ServerCertificate|settings_diagnostic.log|"+
        "Top Sites|Vpn Tokens|Web Data|index|the-real-index|SessionRestoreLog|CURRENT|Cookies|Device Bound Sessions|Network Persistent State|NetworkDataMigrated|Reporting and NEL|"+
        "SCT Auditing Pending Reports|Sdch Dictionaries|Trust Tokens|db|cache.db|cache.journal|crs.pb|ct_config.pb|kp_pinslist.pb|downloadCache|downloadCache_|uriCache|uriCache_|"+
        "customSettings|edgeSettings|topTraffic|Microsoft.CognitiveServices.Speech.core.dll|LOCKFILE|Visited Links|TransportSecurity|QuotaManager|First Run").Split('|'), StringComparer.Ordinal);
    private bool IsCacheArtifact(string path)
    {
        var relative=Path.GetRelativePath(Path.Combine(root,"user-data","webview"),path).Replace('\\','/');
        if(!relative.StartsWith("EBWebView/",StringComparison.Ordinal)) return false;
        var name=Path.GetFileName(path);
        if(CacheNames.Contains(name)) return true;
        foreach(var suffix in new[]{"-journal","-wal","-shm"})
            if(name.EndsWith(suffix,StringComparison.Ordinal) && CacheNames.Contains(name[..^suffix.Length])) return true;
        if(Regex.IsMatch(name,@"^(?:[a-f0-9]{64}|[a-f0-9]{16}_[0-9]|f_[a-f0-9]{6}|data_[0-9]|[0-9]{6}\.(?:log|ldb)|MANIFEST-[0-9]{6}|BrowserMetrics-[A-F0-9-]+\.pma)$")) return true;
        if(Regex.IsMatch(name,@"^(?:customSettings_[A-F0-9]+|edgeSettings_[0-9.]+-[a-f0-9]+|topTraffic_[0-9]+)$")) return true;
        if(name=="metadata.json" && (relative.StartsWith("EBWebView/component_crx_cache/",StringComparison.Ordinal)||relative.StartsWith("EBWebView/extensions_crx_cache/",StringComparison.Ordinal))) return true;
        // Browser component manifests are confined to versioned component folders.
        return new[]{"manifest.json","protocols.json","keys.json","LICENSE","verified_contents.json"}.Contains(name)
            && Regex.IsMatch(relative,@"^EBWebView/[^/]+/[0-9]+(?:\.[0-9]+){1,4}/(?:_metadata/)?[^/]+$");
    }
    public static int Cleanup(string root, bool removeRecordings)
    {
        try {
            var owner = new RuntimeFiles(root);
            if(!IsUnlinked(owner.manifest)) return 1;
            foreach (var entry in owner.entries.Values) {
                var path = owner.Resolve(entry.Path);
                if(entry.Kind != "audio" && !IsWithin(path, owner.root)) continue;
                if(entry.Kind == "audio" && !removeRecordings) continue;
                if(owner.Matches(path)) File.Delete(path);
            }
            File.Delete(owner.manifest);
            foreach(var key in owner.directories.OrderByDescending(x=>x.Length)) {
                var path = owner.Resolve(key);
                if(IsWithin(path,owner.root) && Directory.Exists(path) && IsUnlinked(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
            }
            return 0;
        } catch(IOException) { return 1; } catch(UnauthorizedAccessException) { return 1; } catch(JsonException) { return 1; }
    }
}


