using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TermoDom.Studio;

public partial class StudioWindow
{
 bool restoringPreferences,preferencesLoaded;
 string lastConversationQuery="";int conversationMatch;
 TextBlock? lastConversationMatch;Brush? previousMatchBackground;
 string PreferencesPath=>Path.Combine(Isolation.Data,"studio-preferences.json");

 void BeginPreferencesRestore()=>restoringPreferences=true;
 void RestorePreferences()
 {
  if(preview){restoringPreferences=false;return;}
  restoringPreferences=true;
  try
  {
   var saved=StudioPreferences.Load(PreferencesPath);
   var model=Models.Items.OfType<ModelChoice>().FirstOrDefault(m=>m.Id==saved.ModelId);
   if(model!=null)Models.SelectedItem=model;
   if(Effort.Items.OfType<string>().Contains(saved.Effort))Effort.SelectedItem=saved.Effort;
   var speed=Speed.Items.OfType<SpeedChoice>().FirstOrDefault(s=>s.Id==saved.SpeedId);
   if(speed!=null)Speed.SelectedItem=speed;
   Access.SelectedIndex=saved.FullAccess?1:0;
  }
  catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException)
  {Status.Text="Spremljene postavke nisu učitane: "+ex.Message;}
  finally{restoringPreferences=false;preferencesLoaded=true;}
 }
 void SavePreferences()
 {
  if(preview||!preferencesLoaded||restoringPreferences||Models.SelectedItem is not ModelChoice model)return;
  try
  {
   new StudioPreferences{ModelId=model.Id,Effort=Effort.SelectedItem as string??"",SpeedId=(Speed.SelectedItem as SpeedChoice)?.Id??"default",FullAccess=Access.SelectedIndex==1}.Save(PreferencesPath);
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Fail(ex);}
 }
 void SelectionPreferences_Changed(object sender,SelectionChangedEventArgs e)=>SavePreferences();

 void ExportConversation_Click(object sender,RoutedEventArgs e)
 {
  if(preview||current==null){Status.Text="Izvoz je dostupan u stvarnom izoliranom razgovoru.";return;}
  if(busy){Status.Text="Prije izvoza pričekaj dovršetak zadatka ili ga zaustavi.";return;}
  try
  {
   var directory=Path.Combine(Isolation.Data,"Exports");Directory.CreateDirectory(directory);
   if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Mapa privatnog izvoza ne smije biti poveznica.");
   var messages=new List<JsonElement>();
   for(var i=0;i<current.Messages.Count;i++)
   {
    var message=current.Messages[i];var role=message.Role=="TI"?"user":message.Role.StartsWith("CODEX",StringComparison.Ordinal)?"assistant":null;
    if(role==null)continue;
    if(privateImport!=null&&i<privateImport.Messages.Count&&Text(privateImport.Messages[i],"role")==role)messages.Add(privateImport.Messages[i]);
    else
    {
     var content=new List<object>{new{type=role=="user"?"input_text":"output_text",text=message.Text}};
     if(role=="user"&&message.Attachments!=null)
     {
      foreach(var attachment in message.Attachments)
      {
       var extension=Path.GetExtension(attachment).ToLowerInvariant();
       var mime=extension switch{".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",".webp"=>"image/webp",_=>null};
       if(mime==null)continue;
       var source=ResolveAttachment(attachment);
       if(!File.Exists(source))throw new IOException("Slika za izvoz više nije dostupna: "+Path.GetFileName(attachment));
       if(new FileInfo(source).Length>32_000_000)throw new IOException("Slika za izvoz je veća od 32 MB: "+Path.GetFileName(attachment));
       content.Add(new{type="input_image",image_url="data:"+mime+";base64,"+Convert.ToBase64String(File.ReadAllBytes(source))});
      }
     }
     messages.Add(JsonSerializer.SerializeToElement(new{role,content}));
    }
   }
   var export=new{schemaVersion=1,title=current.Label,sourceThreadId=current.Id.Length>0?current.Id:privateImport?.SourceId??"",exportedAt=DateTimeOffset.UtcNow,repository="https://github.com/leo25matko-boop/termodom-suradnja",messages,scope="Visible messages and attached images from this one Studio conversation. No authentication stores, system instructions or tool logs."};
   var path=Path.Combine(directory,"TermoDom-razgovor-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]+".json");
   var bytes=JsonSerializer.SerializeToUtf8Bytes(export,new JsonSerializerOptions{WriteIndented=true});
   if(bytes.Length>32_000_000)throw new IOException("Razgovor sa slikama prelazi 32 MB, što je ograničenje privatnog uvoza. Izvoz nije spremljen.");
   using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write(bytes);
   Status.Text="Privatni izvoz je spremljen: "+path+". Sadrži razgovor; nemoj ga objavljivati u Git repozitoriju.";
  }
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){Fail(ex);}
 }

 void FindConversation_Click(object sender,RoutedEventArgs e)
 {
  var query=Ask("Pronađi u ovom razgovoru. Ponovi isti izraz za sljedeći rezultat:");
  if(string.IsNullOrWhiteSpace(query))return;
  query=query.Trim();
  if(lastConversationMatch!=null)lastConversationMatch.Background=previousMatchBackground;
  var matches=Chat.Children.OfType<StackPanel>().SelectMany(p=>p.Children.OfType<TextBlock>().Skip(1)).Where(b=>b.Text.Contains(query,StringComparison.CurrentCultureIgnoreCase)).ToArray();
  if(matches.Length==0){lastConversationMatch=null;Status.Text="U ovom razgovoru nema rezultata za: "+query;return;}
  conversationMatch=query.Equals(lastConversationQuery,StringComparison.CurrentCultureIgnoreCase)?(conversationMatch+1)%matches.Length:0;
  lastConversationQuery=query;lastConversationMatch=matches[conversationMatch];previousMatchBackground=lastConversationMatch.Background;
  lastConversationMatch.Background=new SolidColorBrush(Color.FromRgb(77,67,38));lastConversationMatch.BringIntoView();
  Status.Text=$"Pronađeno {conversationMatch+1} od {matches.Length} poruka za: {query}";
 }
}
