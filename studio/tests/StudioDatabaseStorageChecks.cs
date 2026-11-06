using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TermoDom.Studio;

public static class StudioDatabaseStorageChecks
{
 public static void Run(string root,Action<bool,string> Check,Action<Action,string> Refuses)
 {
  (string Snapshot,string Legacy,string Runtime) Fixture(string name)
  {
   var fixture=Path.Combine(root,"database-storage",name);var legacy=Path.Combine(fixture,"persistent","Codex");Directory.CreateDirectory(legacy);
   return(Path.Combine(fixture,"persistent","DatabaseSnapshots"),legacy,Path.Combine(fixture,"local","Sqlite"));
  }
  void Write(string path,string content){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,content,new UTF8Encoding(false));}
  string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
  StudioDatabaseStorage.CurrentPointer Pointer(string path)=>JsonSerializer.Deserialize<StudioDatabaseStorage.CurrentPointer>(File.ReadAllText(Path.Combine(path,"CURRENT")))!;
  string Generation(string snapshots)=>Path.Combine(snapshots,Pointer(snapshots).Generation);
  void Seed((string Snapshot,string Legacy,string Runtime) fixture)=>Write(Path.Combine(fixture.Legacy,"state_5.sqlite"),"SQLite format 3\0synthetic database bytes");

  var roundtrip=Fixture("roundtrip");Seed(roundtrip);
  Write(Path.Combine(roundtrip.Legacy,"state_5.sqlite-wal"),"synthetic wal");
  Write(Path.Combine(roundtrip.Legacy,"state_5.sqlite-journal"),"synthetic journal");
  Write(Path.Combine(roundtrip.Legacy,"state_5.sqlite-shm"),"volatile lock cache");
  Write(Path.Combine(roundtrip.Legacy,"auth.json"),"synthetic auth must stay here");
  Write(Path.Combine(roundtrip.Legacy,"config.toml"),"synthetic config must stay here");
  Write(Path.Combine(roundtrip.Legacy,"sessions","synthetic.jsonl"),"synthetic session must stay here");
  var legacyBefore=Hash(Path.Combine(roundtrip.Legacy,"state_5.sqlite"));
  var first=StudioDatabaseStorage.Prepare(roundtrip.Snapshot,roundtrip.Legacy,roundtrip.Runtime);
  Check(Hash(Path.Combine(first,"state_5.sqlite"))==legacyBefore,"offline SQLite legacy bytes seed a new local directory");
  Check(Directory.GetFiles(first).Select(Path.GetFileName).Order().SequenceEqual(new[]{"state_5.sqlite","state_5.sqlite-journal","state_5.sqlite-wal"}.Order()),"only SQLite database, WAL and journal are copied; auth config sessions and SHM stay excluded");
  Check(File.Exists(Path.Combine(roundtrip.Legacy,"state_5.sqlite-shm"))&&File.Exists(Path.Combine(roundtrip.Legacy,"auth.json")),"excluded legacy files remain untouched");
  Write(Path.Combine(first,"state_5.sqlite"),"first saved database");
  StudioDatabaseStorage.Save(roundtrip.Snapshot,first);
  var firstGeneration=Generation(roundtrip.Snapshot);var oldManifest=Hash(Path.Combine(firstGeneration,"manifest.json"));var oldDatabase=Hash(Path.Combine(firstGeneration,"state_5.sqlite"));
  var second=StudioDatabaseStorage.Prepare(roundtrip.Snapshot,roundtrip.Legacy,roundtrip.Runtime);
  Check(first!=second&&File.ReadAllText(Path.Combine(second,"state_5.sqlite"))=="first saved database","snapshot round trip uses a fresh directory instead of stale legacy state");
  Check(Hash(Path.Combine(roundtrip.Legacy,"state_5.sqlite"))==legacyBefore,"preparing and saving snapshots never overwrite legacy SQLite bytes");
  Write(Path.Combine(second,"state_5.sqlite"),"second saved database");
  StudioDatabaseStorage.Save(roundtrip.Snapshot,second);
  Check(Directory.GetDirectories(roundtrip.Snapshot,"generation-*").Length==2&&Hash(Path.Combine(firstGeneration,"manifest.json"))==oldManifest&&Hash(Path.Combine(firstGeneration,"state_5.sqlite"))==oldDatabase,"new generation preserves the previous immutable manifest and database");
  var latest=Generation(roundtrip.Snapshot);Write(Path.Combine(latest,"state_5.sqlite"),"corrupt latest database");
  var recovered=StudioDatabaseStorage.Prepare(roundtrip.Snapshot,roundtrip.Legacy,roundtrip.Runtime);
  Check(File.ReadAllText(Path.Combine(recovered,"state_5.sqlite"))=="first saved database","corrupt latest snapshot falls back to a complete verified previous generation");
  Check(Directory.GetDirectories(roundtrip.Snapshot,"generation-*").Length==2,"fallback does not delete corrupted or previous generations");

  var tampered=Fixture("tampered-only-generation");Seed(tampered);
  var tamperedRun=StudioDatabaseStorage.Prepare(tampered.Snapshot,tampered.Legacy,tampered.Runtime);StudioDatabaseStorage.Save(tampered.Snapshot,tamperedRun);
  Write(Path.Combine(Generation(tampered.Snapshot),"state_5.sqlite"),"modified database");
  Refuses(()=>StudioDatabaseStorage.Prepare(tampered.Snapshot,tampered.Legacy,tampered.Runtime),"unverified snapshot bytes are rejected without silently reseeding stale legacy state");

  var malformed=Fixture("malformed-manifest");Seed(malformed);
  var malformedRun=StudioDatabaseStorage.Prepare(malformed.Snapshot,malformed.Legacy,malformed.Runtime);StudioDatabaseStorage.Save(malformed.Snapshot,malformedRun);
  Write(Path.Combine(Generation(malformed.Snapshot),"manifest.json"),"{not valid JSON");
  Refuses(()=>StudioDatabaseStorage.Prepare(malformed.Snapshot,malformed.Legacy,malformed.Runtime),"malformed generation manifest is rejected");

  var traversal=Fixture("manifest-traversal");Seed(traversal);
  var traversalRun=StudioDatabaseStorage.Prepare(traversal.Snapshot,traversal.Legacy,traversal.Runtime);StudioDatabaseStorage.Save(traversal.Snapshot,traversalRun);
  var traversalPointer=Pointer(traversal.Snapshot);var traversalGeneration=Generation(traversal.Snapshot);
  var manifest=JsonSerializer.Deserialize<StudioDatabaseStorage.GenerationManifest>(File.ReadAllText(Path.Combine(traversalGeneration,"manifest.json")))!;
  var malicious=manifest with{Files=[manifest.Files[0] with{Name="../outside.sqlite"}]};
  var maliciousBytes=JsonSerializer.SerializeToUtf8Bytes(malicious);File.WriteAllBytes(Path.Combine(traversalGeneration,"manifest.json"),maliciousBytes);
  Write(Path.Combine(traversal.Snapshot,"CURRENT"),JsonSerializer.Serialize(traversalPointer with{ManifestSha256=Convert.ToHexString(SHA256.HashData(maliciousBytes))}));
  Refuses(()=>StudioDatabaseStorage.Prepare(traversal.Snapshot,traversal.Legacy,traversal.Runtime),"manifest traversal is rejected even when its checksum matches the pointer");
  Check(!File.Exists(Path.Combine(traversal.Runtime,"outside.sqlite")),"malformed manifest did not write outside a fresh runtime directory");

  var nullPointer=Fixture("null-pointer");Directory.CreateDirectory(nullPointer.Snapshot);
  Write(Path.Combine(nullPointer.Snapshot,"CURRENT"),"{\"SchemaVersion\":1,\"Generation\":null,\"ManifestSha256\":null}");
  Refuses(()=>StudioDatabaseStorage.Prepare(nullPointer.Snapshot,nullPointer.Legacy,nullPointer.Runtime),"null pointer fields produce a bounded data error instead of a null argument crash");

  var clock=Fixture("pointer-priority");Seed(clock);
  var clockRun=StudioDatabaseStorage.Prepare(clock.Snapshot,clock.Legacy,clock.Runtime);Write(Path.Combine(clockRun,"state_5.sqlite"),"older committed value");StudioDatabaseStorage.Save(clock.Snapshot,clockRun);
  var olderGeneration=Generation(clock.Snapshot);
  Write(Path.Combine(clockRun,"state_5.sqlite"),"current committed value");StudioDatabaseStorage.Save(clock.Snapshot,clockRun);
  // Simulate an old generation stamped by an ahead-of-time clock. CURRENT still
  // identifies the last successful commit and must take priority over its name.
  var futureName="generation-99991231T2359599999999Z-"+Guid.NewGuid().ToString("N");var futureDirectory=Path.Combine(clock.Snapshot,futureName);Directory.Move(olderGeneration,futureDirectory);
  var futureManifest=JsonSerializer.Deserialize<StudioDatabaseStorage.GenerationManifest>(File.ReadAllText(Path.Combine(futureDirectory,"manifest.json")))!;
  Write(Path.Combine(futureDirectory,"manifest.json"),JsonSerializer.Serialize(futureManifest with{Generation=futureName}));
  var clockRecovered=StudioDatabaseStorage.Prepare(clock.Snapshot,clock.Legacy,clock.Runtime);
  Check(File.ReadAllText(Path.Combine(clockRecovered,"state_5.sqlite"))=="current committed value","atomic CURRENT pointer takes priority over clock-skewed generation names");

  var countLimit=Fixture("file-limit");
  for(var i=0;i<=StudioDatabaseStorage.MaximumFiles;i++)Write(Path.Combine(countLimit.Legacy,$"state_{i}.sqlite"),"fixture");
  Refuses(()=>StudioDatabaseStorage.Prepare(countLimit.Snapshot,countLimit.Legacy,countLimit.Runtime),"more than 32 SQLite files are refused");

  var writer=Fixture("active-writer");Seed(writer);
  var writerRun=StudioDatabaseStorage.Prepare(writer.Snapshot,writer.Legacy,writer.Runtime);
  using(var activeWriter=new FileStream(Path.Combine(writerRun,"state_5.sqlite"),FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite))
   Refuses(()=>StudioDatabaseStorage.Save(writer.Snapshot,writerRun),"offline snapshot refuses a database still open for writing");
  Check(!File.Exists(Path.Combine(writer.Snapshot,"CURRENT")),"failed offline save publishes no current generation");
  Refuses(()=>StudioDatabaseStorage.Prepare(roundtrip.Legacy,roundtrip.Legacy,roundtrip.Runtime),"overlapping SQLite roots are refused");
  Refuses(()=>StudioDatabaseStorage.Save(roundtrip.Snapshot,roundtrip.Legacy),"snapshot save rejects a directory not created as a unique runtime");
 }
}
