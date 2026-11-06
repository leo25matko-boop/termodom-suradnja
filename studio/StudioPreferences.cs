using System.IO;
using System.Text.Json;

namespace TermoDom.Studio;

/// <summary>Only UI choices, never credentials, account details or conversation text.</summary>
public sealed record StudioPreferences
{
 public int SchemaVersion { get; init; } = 1;
 public string ModelId { get; init; } = "";
 public string Effort { get; init; } = "";
 public string SpeedId { get; init; } = "default";
 public bool FullAccess { get; init; }

 public static StudioPreferences Load(string path)
 {
  if(!File.Exists(path))return new();
  if(new FileInfo(path).Length>65_536)throw new IOException("Datoteka postavki Studija je prevelika.");
  var result=JsonSerializer.Deserialize<StudioPreferences>(File.ReadAllText(path))??throw new IOException("Postavke Studija su prazne.");
  result.Validate();return result;
 }
 public void Save(string path)
 {
  Validate();
  if(!Path.IsPathFullyQualified(path))throw new IOException("Postavke trebaju apsolutnu putanju Studio podataka.");
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
  var temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
  try
  {
   File.WriteAllText(temporary,JsonSerializer.Serialize(this),new System.Text.UTF8Encoding(false));
   File.Move(temporary,path,true);
  }
  finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 void Validate()
 {
  if(SchemaVersion!=1)throw new IOException("Nepodržana verzija postavki Studija.");
  if(ModelId is null||Effort is null||SpeedId is null||ModelId.Length>256||Effort.Length>64||SpeedId.Length>64)
   throw new IOException("Neispravne postavke modela Studija.");
 }
}
