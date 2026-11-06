using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TermoDom.Studio;

/// <summary>
/// Copies only offline SQLite database bytes. The worker caller supplies fixed app-owned
/// roots and must stop the complete Codex process before Save. Auth, sessions and config
/// remain in CODEX_HOME. This class never opens a SQLite connection or deletes old data.
/// Explicit directory parameters also allow synthetic, private-data-free tests.
/// </summary>
public static class StudioDatabaseStorage
{
 public const int MaximumFiles=32;
 public const long MaximumBytes=512L*1024*1024;
 const int MaximumManifestBytes=64*1024,MaximumGenerationCandidates=4096;
 static readonly Regex DatabaseName=new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,180}\.sqlite(?:-wal|-journal)?$",RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);
 static readonly Regex GenerationName=new(@"^generation-[0-9]{8}T[0-9]{13}Z-[a-f0-9]{32}$",RegexOptions.CultureInvariant);
 static readonly Regex RunName=new(@"^run-[a-f0-9]{32}$",RegexOptions.CultureInvariant);
 static readonly Regex HashText=new(@"^[a-fA-F0-9]{64}$",RegexOptions.CultureInvariant);
 static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=false};

 public sealed record DatabaseFile(string Name,long Length,string Sha256);
 public sealed record GenerationManifest(int SchemaVersion,string Generation,DateTimeOffset CreatedUtc,List<DatabaseFile> Files);
 public sealed record CurrentPointer(int SchemaVersion,string Generation,string ManifestSha256);

 /// <summary>Returns a new, never-reused guest-local directory ready for sqlite_home.</summary>
 public static string Prepare(string snapshotRoot,string legacyStudioHome,string runtimeRoot)
 {
  snapshotRoot=Root(snapshotRoot);legacyStudioHome=Root(legacyStudioHome);runtimeRoot=Root(runtimeRoot);
  Separate(snapshotRoot,legacyStudioHome);Separate(snapshotRoot,runtimeRoot);Separate(legacyStudioHome,runtimeRoot);
  EnsureDirectory(runtimeRoot);
  CurrentPointer? pointer=null;var hasPointer=false;
  var generations=new List<string>();
  if(Directory.Exists(snapshotRoot))
  {
   var current=Path.Combine(snapshotRoot,"CURRENT");hasPointer=File.Exists(current);
   if(hasPointer)
   {
    try
    {
     pointer=JsonSerializer.Deserialize<CurrentPointer>(ReadSmall(current,8192),JsonOptions);
     if(pointer==null||pointer.SchemaVersion!=1||string.IsNullOrEmpty(pointer.Generation)||!GenerationName.IsMatch(pointer.Generation)||string.IsNullOrEmpty(pointer.ManifestSha256)||!HashText.IsMatch(pointer.ManifestSha256))pointer=null;
    }
    catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException){pointer=null;}
   }
   foreach(var directory in Directory.EnumerateDirectories(snapshotRoot,"generation-*"))
   {
    if(generations.Count>=MaximumGenerationCandidates)throw new IOException("Previše generacija baze za automatski oporavak. Postojeći podaci nisu promijenjeni.");
    if(GenerationName.IsMatch(Path.GetFileName(directory)))generations.Add(directory);
   }
  }
  foreach(var generation in generations.OrderByDescending(path=>Path.GetFileName(path)==pointer?.Generation).ThenByDescending(Path.GetFileName,StringComparer.Ordinal))
  {
   try
   {
    var manifest=ReadGeneration(generation,pointer);
    var runtime=NewRuntime(runtimeRoot);
    CopyFiles(generation,runtime,manifest.Files);
    return runtime;
   }
   catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException)
   {
    // An incomplete/corrupt generation is never mixed with another one. The next
    // attempt always receives a fresh local directory; all originals are preserved.
   }
  }
  if(generations.Count>0||hasPointer)throw new IOException("Nema potpune provjerene snimke baze. Sve stare snimke i izvorni podaci su sačuvani.");
  var initial=NewRuntime(runtimeRoot);
  if(Directory.Exists(legacyStudioHome))CopyFiles(legacyStudioHome,initial,DatabaseFiles(legacyStudioHome));
  return initial;
 }

 /// <summary>
 /// Call only after Codex has fully stopped. FileShare.Read refuses an active writer;
 /// this is additional protection, not a replacement for the caller's process check.
 /// A new immutable generation and atomic CURRENT pointer preserve previous snapshots.
 /// </summary>
 public static void Save(string snapshotRoot,string runtimeDirectory)
 {
  snapshotRoot=Root(snapshotRoot);runtimeDirectory=Root(runtimeDirectory);Separate(snapshotRoot,runtimeDirectory);
  if(!RunName.IsMatch(Path.GetFileName(runtimeDirectory))||!Directory.Exists(runtimeDirectory))throw new IOException("Neprepoznata lokalna radna mapa baze.");
  var files=DatabaseFiles(runtimeDirectory);
  EnsureDirectory(snapshotRoot);
  var name="generation-"+DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfffffff'Z'",System.Globalization.CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N");
  var stage=Path.Combine(snapshotRoot,"pending-"+Guid.NewGuid().ToString("N"));EnsureNewDirectory(stage);
  var copied=CopyFiles(runtimeDirectory,stage,files);
  var manifest=new GenerationManifest(1,name,DateTimeOffset.UtcNow,copied);
  var bytes=JsonSerializer.SerializeToUtf8Bytes(manifest,JsonOptions);
  WriteNew(Path.Combine(stage,"manifest.json"),bytes);
  NoReparseAncestors(stage);NoReparseAncestors(snapshotRoot);
  var completed=Path.Combine(snapshotRoot,name);Directory.Move(stage,completed);
  var pointer=new CurrentPointer(1,name,Convert.ToHexString(SHA256.HashData(bytes)));
  var temporary=Path.Combine(snapshotRoot,"CURRENT.tmp-"+Guid.NewGuid().ToString("N"));
  WriteNew(temporary,JsonSerializer.SerializeToUtf8Bytes(pointer,JsonOptions));
  var current=Path.Combine(snapshotRoot,"CURRENT");NoReparseAncestors(current);
  File.Move(temporary,current,true);
 }

 static GenerationManifest ReadGeneration(string directory,CurrentPointer? pointer)
 {
  NoReparseAncestors(directory);
  var bytes=ReadSmall(Path.Combine(directory,"manifest.json"),MaximumManifestBytes);
  var name=Path.GetFileName(directory);
  if(pointer?.Generation==name&&!Convert.ToHexString(SHA256.HashData(bytes)).Equals(pointer.ManifestSha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Kontrolni zbroj manifesta baze nije ispravan.");
  var manifest=JsonSerializer.Deserialize<GenerationManifest>(bytes,JsonOptions);
  if(manifest==null||manifest.SchemaVersion!=1||manifest.Generation!=name||manifest.Files==null)throw new IOException("Neispravan manifest baze.");
  ValidateFiles(manifest.Files);
  var expected=manifest.Files.Select(f=>f.Name).Append("manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
  var seen=0;
  foreach(var entry in Directory.EnumerateFileSystemEntries(directory))
  {
   if(++seen>MaximumFiles+1||!expected.Remove(Path.GetFileName(entry)))throw new IOException("Neočekivan sadržaj generacije baze.");
   NoReparseAncestors(entry);
   if((File.GetAttributes(entry)&FileAttributes.Directory)!=0)throw new IOException("Generacija baze ne smije sadržavati podmape.");
  }
  if(expected.Count!=0)throw new IOException("Generacija baze nije potpuna.");
  return manifest;
 }

 static List<DatabaseFile> DatabaseFiles(string directory)
 {
  NoReparseAncestors(directory);var files=new List<DatabaseFile>();long total=0;
  foreach(var path in Directory.EnumerateFiles(directory))
  {
   var name=Path.GetFileName(path);if(!DatabaseName.IsMatch(name))continue;
   NoReparseAncestors(path);var length=new FileInfo(path).Length;
   if(length<0||length>MaximumBytes||total>MaximumBytes-length||files.Count>=MaximumFiles)throw new IOException("Baza prelazi ograničenje od 32 datoteke ili 512 MB.");
   total+=length;files.Add(new(name,length,""));
  }
  ValidateFiles(files,false);return files.OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ToList();
 }

 static void ValidateFiles(List<DatabaseFile> files,bool requireHashes=true)
 {
  if(files.Count>MaximumFiles)throw new IOException("Previše datoteka baze.");
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
  foreach(var file in files)
  {
   if(file==null||string.IsNullOrEmpty(file.Name)||!DatabaseName.IsMatch(file.Name)||Path.GetFileName(file.Name)!=file.Name||!names.Add(file.Name))throw new IOException("Nedopušten naziv datoteke baze.");
   if(file.Length<0||file.Length>MaximumBytes||total>MaximumBytes-file.Length)throw new IOException("Baza prelazi ograničenje veličine.");
   total+=file.Length;if(requireHashes&&(string.IsNullOrEmpty(file.Sha256)||!HashText.IsMatch(file.Sha256)))throw new IOException("Nedostaje kontrolni zbroj datoteke baze.");
  }
  foreach(var file in files)
  {
   var suffix=file.Name.EndsWith("-wal",StringComparison.OrdinalIgnoreCase)?4:file.Name.EndsWith("-journal",StringComparison.OrdinalIgnoreCase)?8:0;
   if(suffix>0&&!names.Contains(file.Name[..^suffix]))throw new IOException("Prateća SQLite datoteka nema pripadajuću bazu.");
  }
 }

 static List<DatabaseFile> CopyFiles(string source,string destination,List<DatabaseFile> files)
 {
  ValidateFiles(files,files.Any(f=>f.Sha256.Length>0));NoReparseAncestors(source);NoReparseAncestors(destination);
  var opened=new List<(DatabaseFile Entry,FileStream Stream)>();
  try
  {
   // Hold every source file against writes/deletion for the complete offline copy.
   foreach(var file in files)
   {
    var path=Path.Combine(source,file.Name);NoReparseAncestors(path);
    var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);opened.Add((file,input));
    if(input.Length!=file.Length)throw new IOException("Baza se promijenila prije kopiranja.");
   }
   var copied=new List<DatabaseFile>();var buffer=new byte[81920];
   foreach(var (entry,input) in opened)
   {
    var target=Path.Combine(destination,entry.Name);NoReparseAncestors(target);
    using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);
    using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long written=0;int read;
    while((read=input.Read(buffer,0,buffer.Length))>0)
    {
     if(written>entry.Length-read)throw new IOException("Baza se promijenila tijekom kopiranja.");
     output.Write(buffer,0,read);hash.AppendData(buffer,0,read);written+=read;
    }
    output.Flush(true);var digest=Convert.ToHexString(hash.GetHashAndReset());
    if(written!=entry.Length||(entry.Sha256.Length>0&&!digest.Equals(entry.Sha256,StringComparison.OrdinalIgnoreCase)))throw new IOException("Kontrolni zbroj kopije baze nije ispravan.");
    copied.Add(new(entry.Name,written,digest));
   }
   return copied;
  }
  finally{foreach(var entry in opened)entry.Stream.Dispose();}
 }

 static byte[] ReadSmall(string path,int maximum)
 {
  NoReparseAncestors(path);
  using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
  if(file.Length>maximum)throw new IOException("Metapodaci snimke baze prelaze ograničenje.");
  var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);return bytes;
 }
 static void WriteNew(string path,byte[] bytes)
 {
  NoReparseAncestors(path);using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);file.Write(bytes);file.Flush(true);
 }
 static string NewRuntime(string root)
 {
  var path=Path.Combine(root,"run-"+Guid.NewGuid().ToString("N"));EnsureNewDirectory(path);return path;
 }
 static string Root(string path)
 {
  if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\",StringComparison.Ordinal))throw new IOException("Baza zahtijeva lokalne apsolutne mape.");
  var full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
  if(full.Equals(Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new IOException("Korijen diska nije mapa baze.");
  NoReparseAncestors(full);return full;
 }
 static void Separate(string first,string second)
 {
  if(first.Equals(second,StringComparison.OrdinalIgnoreCase)||first.StartsWith(second+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||second.StartsWith(first+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Mape baze, snimki i izvornika moraju biti odvojene.");
 }
 static void EnsureDirectory(string path){NoReparseAncestors(path);Directory.CreateDirectory(path);NoReparseAncestors(path);}
 static void EnsureNewDirectory(string path)
 {
  NoReparseAncestors(path);if(File.Exists(path)||Directory.Exists(path))throw new IOException("Nova mapa baze već postoji.");Directory.CreateDirectory(path);NoReparseAncestors(path);
 }
 static void NoReparseAncestors(string path)
 {
  for(string? current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))
  {
   try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Poveznice u putanji baze nisu dopuštene.");}
   catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
  }
 }
}
