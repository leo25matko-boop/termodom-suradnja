using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TermoDom.Studio;
public sealed class ConversationImport
{
 public string Title {get;private set;}="";
 public string SourceId {get;private set;}="";
 public List<JsonElement> Messages {get;}=[];
 public static ConversationImport Load(string path)
 {
  if(new FileInfo(path).Length>32_000_000)throw new IOException("Privatni izvoz razgovora je prevelik.");
  using var doc=JsonDocument.Parse(File.ReadAllText(path));var root=doc.RootElement;
  if(root.GetProperty("schemaVersion").GetInt32()!=1)throw new IOException("Nepodržan oblik izvoza razgovora.");
  var result=new ConversationImport{Title=root.GetProperty("title").GetString()??"Projektni razgovor",SourceId=root.GetProperty("sourceThreadId").GetString()??""};
  foreach(var message in root.GetProperty("messages").EnumerateArray())
  {
   var role=message.GetProperty("role").GetString();if(role is not("user" or "assistant"))throw new IOException("Uvoze se samo vidljive korisničke i asistentske poruke.");
   foreach(var content in message.GetProperty("content").EnumerateArray())
   {
    var type=content.GetProperty("type").GetString();
    if(type is not("input_text" or "output_text" or "input_image"))throw new IOException("Nepodržan sadržaj privatnog razgovora.");
    if(type=="input_image"&&(role!="user"||!content.GetProperty("image_url").GetString()!.StartsWith("data:image/",StringComparison.Ordinal)))throw new IOException("Uvoz slike mora sadržavati lokalni podatkovni zapis.");
   }
   result.Messages.Add(message.Clone());
  }
  return result;
 }
 public object[] ToItems()=>Messages.Select(m=>(object)new{type="message",role=m.GetProperty("role").GetString(),content=m.GetProperty("content")}).ToArray();
 public IEnumerable<(string Role,string Text)> DisplayMessages()=>Messages.Select(m=>
 {
  var text=string.Join("\n",m.GetProperty("content").EnumerateArray().Where(p=>p.TryGetProperty("text",out _)).Select(p=>p.GetProperty("text").GetString()));
  var images=m.GetProperty("content").EnumerateArray().Count(p=>p.GetProperty("type").GetString()=="input_image");
  if(images>0)text+=$"\n[Prenesene priložene slike: {images}; uključene u privatni kontekst agenta.]";
  return(m.GetProperty("role").GetString()=="user"?"TI":"CODEX",text);
 });
}
