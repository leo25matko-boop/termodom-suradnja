using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TermoDom.Studio;
public sealed record ModelChoice(string Id,string Label,string DefaultEffort,List<string> Efforts,List<SpeedChoice> Speeds){public override string ToString()=>Label;}
public sealed record SpeedChoice(string Id,string Label){public override string ToString()=>Label;}
public sealed record ChatEntry(string Role,string Text,List<string>? Attachments=null);
public sealed class StudioThread
{
 public string Id {get;set;}="";public string Label {get;set;}="";public List<ChatEntry> Messages {get;set;}=[];public bool ImportPending {get;set;}
 public override string ToString()=>Label;
}
public partial class StudioWindow : Window
{
 readonly bool preview;readonly RpcClient rpc=new();FileStream? sessionLock;
 Workspace? workspace;StudioThread? current;List<StudioThread> threads=[];ConversationImport? privateImport;
 readonly Dictionary<string,TextBlock> liveMessages=[];
 readonly HashSet<string> announced=[],streamedCommands=[];
 string? currentTurn,editorPath,editorHash;bool editorDirty,loadingEditor,busy,authenticated,engineReady,loginPending,shuttingDown;
 readonly List<string> attachments=[];
 CancellationTokenSource? commandLifetime;
 string IndexPath=>Path.Combine(Isolation.Data,"studio-chats.json");
 public StudioWindow(bool preview)
 {
  InitializeComponent();this.preview=preview;
  var area=SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,area.Width);MinHeight=Math.Min(MinHeight,area.Height);Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);
  Loaded+=async(_,_)=>await InitializeAsync();
  Closing+=(_,e)=>{if(!CanDiscardEditor())e.Cancel=true;else{shuttingDown=true;rpc.Dispose();commandLifetime?.Cancel();sessionLock?.Dispose();}};
  rpc.Notification+=(name,p)=>Dispatcher.BeginInvoke(()=>HandleNotification(name,p));
  rpc.Disconnected+=message=>Dispatcher.BeginInvoke(()=>{authenticated=false;engineReady=false;loginPending=false;pendingLoginId=null;busy=false;Connection.Text=message;UpdateActions();});
  rpc.ServerRequest+=HandleRequestAsync;
 }
 async Task InitializeAsync()
 {
  try
  {
   if(shuttingDown)return;
   if(preview)
   {
    Title="TermoDom Studio · Pregled sučelja (bez agenta)";
    Connection.Text="PREGLED SUČELJA • Codex i prijava nisu pokrenuti";
    var seed=Path.Combine(AppContext.BaseDirectory,"ProjectSeed.zip");
    if(File.Exists(seed))
    {
     var root=Path.Combine(Path.GetTempPath(),"TermoDomStudio-Preview-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Isolation.ExtractSeed(seed,root);workspace=new Workspace(root);
     Closed+=(_,_)=>{try{Directory.Delete(root,true);}catch(IOException){}};
    }
    AddMessage("PROJEKT","TermoDom je spreman za nastavak razvoja.\n\nGrijanje i rasporedi s klizačima · FusionSolar · Xiaomi Mi 360\nNativni Windows i Android · razvoj s lokalnim testnim podacima\n\nOvo je pregled izgleda. Za stvarni rad otvori izolirani Studio i prijavi se u službenom prozoru.",false);
    Models.ItemsSource=new[]{new ModelChoice("","Modeli nakon prijave","",[],[])};Models.SelectedIndex=0;
    Status.Text="Pregled ne pokreće agenta, terminal, prijavu niti spremanje datoteka. Nije provjera rada u Sandboxu.";
   }
   else
   {
    sessionLock=new FileStream(Path.Combine(Isolation.Data,"studio-worker.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    workspace=new Workspace(Isolation.Project);
    if(File.Exists(IndexPath))threads=JsonSerializer.Deserialize<List<StudioThread>>(File.ReadAllText(IndexPath))??[];
    if(threads.Count>1)throw new IOException("Studio je ograničen na jedan razgovor. Višestruka povijest se ne otvara automatski.");
    current=threads.SingleOrDefault();
    var importPath=Path.Combine(Isolation.Data,"Imports","current-chat.json");
    if(File.Exists(importPath))
    {
     privateImport=ConversationImport.Load(importPath);
     if(threads.Count==0)
     {
      current=new StudioThread{Label=privateImport.Title,ImportPending=true,Messages=privateImport.DisplayMessages().Select(m=>new ChatEntry(m.Role,m.Text)).ToList()};threads.Add(current);SaveThreads();
     }
    }
    if(current==null)throw new IOException("Za nastavak nedostaje privatni izvoz ovog razgovora. Studio neće stvarati zamjenski razgovor.");
    UpdateConversationTitle();
    var exe=Path.Combine(Isolation.AppRoot,"Tools","codex","vendor","x86_64-pc-windows-msvc","bin","codex.exe");
    await rpc.StartAsync(exe,Isolation.Project,Path.Combine(Isolation.Data,"Codex"));
    engineReady=true;
    await RefreshAccountAsync();
    if(current.Id.Length>0)await rpc.CallAsync("thread/resume",new{threadId=current.Id,cwd=Isolation.Project,excludeTurns=true,sandbox="danger-full-access",approvalPolicy=Access.SelectedIndex==1?"never":"on-request"});
    for(var i=0;i<current.Messages.Count;i++)
    {
     var message=current.Messages[i];var block=AddMessage(message.Role,message.Text,false);
     if(privateImport!=null&&i<privateImport.Messages.Count)RenderImportedImages(privateImport.Messages[i],(StackPanel)block.Parent);
     if(message.Attachments!=null)RenderSavedAttachments(message.Attachments,(StackPanel)block.Parent);
    }
    if(current.ImportPending)AddMessage("STUDIO","Prenesen je sadržaj ovog razgovora. Sljedeći zadatak nastavlja s tim porukama i priloženim slikama. Izvorni Codex chat se ne sinkronizira automatski.",false);
   }
   RefreshFiles();LoadContext();UpdateActions();
   if(!preview&&!authenticated)await BeginLoginAsync();
   if(preview&&Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--capture=")) is {} capture)
   {
    await Task.Delay(700);UpdateLayout();
    var visual=(FrameworkElement)Content;
    var bitmap=new RenderTargetBitmap((int)visual.ActualWidth+32,(int)visual.ActualHeight+32,96,96,PixelFormats.Pbgra32);
    var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(Background,null,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight));bitmap.Render(background);bitmap.Render(visual);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(capture[10..]);encoder.Save(file);
    Application.Current.Shutdown();
   }
  }
  catch(Exception ex){Fail(ex);UpdateActions();}
 }
 void UpdateActions()
 {
  Login.IsEnabled=!preview&&engineReady&&!busy&&!authenticated&&!startingLogin;Logout.IsEnabled=!preview&&authenticated&&!busy;
  Login.Content=loginPending?"Ponovi prijavu":"Prijavi se kroz ChatGPT";
  Send.IsEnabled=!preview&&authenticated&&!busy&&Models.SelectedItem is ModelChoice;
  Stop.IsEnabled=!preview&&busy;Models.IsEnabled=Effort.IsEnabled=Speed.IsEnabled=Access.IsEnabled=!preview&&!busy;
  Editor.IsReadOnly=preview;
  Attach.IsEnabled=!preview&&!busy;ClearAttachments.IsEnabled=!preview&&!busy;
 }
 void Fail(Exception ex){Status.Text=ex.Message;}
 void LoadContext()
 {
  if(workspace==null)return;
  var path=workspace.Resolve("docs/KONTEKST-ZA-CODEX.md");
  Context.Text=File.Exists(path)?File.ReadAllText(path):"Projektni kontekst nije pronađen.";
 }
 async Task RefreshAccountAsync()
 {
  var response=await rpc.CallAsync("account/read",new{refreshToken=false});
  authenticated=response.TryGetProperty("account",out var account)&&account.ValueKind==JsonValueKind.Object&&Text(account,"type")=="chatgpt";
  Connection.Text=authenticated?$"Izolirani Studio • {Text(account,"email")} • {Text(account,"planType")}":"Izolacija je spremna • Prijavi se kroz ChatGPT";
  if(authenticated)await LoadModelsAsync();else{Models.ItemsSource=null;Effort.ItemsSource=null;Speed.ItemsSource=null;}
  UpdateActions();
 }
 async Task LoadModelsAsync()
 {
  BeginPreferencesRestore();
  var models=new List<ModelChoice>();string? cursor=null;
  do
  {
   var result=await rpc.CallAsync("model/list",new{limit=100,includeHidden=false,cursor});
   foreach(var model in result.GetProperty("data").EnumerateArray())
   {
    var efforts=model.GetProperty("supportedReasoningEfforts").EnumerateArray().Select(e=>Text(e,"reasoningEffort")).Where(s=>s.Length>0).ToList();
    var speeds=new List<SpeedChoice>{new("default","Standard")};
    if(model.TryGetProperty("serviceTiers",out var tiers))foreach(var tier in tiers.EnumerateArray())
    {
     var id=Text(tier,"id");if(id.Length==0)id=Text(tier,"serviceTier");
     if(id.Length>0&&id!="default")speeds.Add(new(id,Text(tier,"name") is {Length:>0} label?label:id));
    }
    else if(model.TryGetProperty("additionalSpeedTiers",out var old))foreach(var tier in old.EnumerateArray())if(tier.GetString() is {Length:>0} id&&id!="default")speeds.Add(new(id,id));
    models.Add(new(Text(model,"model"),Text(model,"displayName"),Text(model,"defaultReasoningEffort"),efforts,speeds));
   }
   cursor=result.TryGetProperty("nextCursor",out var next)&&next.ValueKind==JsonValueKind.String?next.GetString():null;
  }while(cursor!=null);
  Models.ItemsSource=models;Models.SelectedIndex=models.Count>0?0:-1;
  RestorePreferences();
  Status.Text=models.Count>0?"Modeli, razmišljanje i brzine dolaze iz Codex kataloga. Brže opcije mogu trošiti više raspoloživog limita.":"Račun nije vratio dostupne modele.";
 }
 void Model_Changed(object sender,SelectionChangedEventArgs e)
 {
  if(Models.SelectedItem is not ModelChoice m)return;
  Effort.ItemsSource=m.Efforts;Effort.SelectedItem=m.DefaultEffort;if(Effort.SelectedIndex<0&&m.Efforts.Count>0)Effort.SelectedIndex=0;
  Speed.ItemsSource=m.Speeds;Speed.SelectedIndex=m.Speeds.Count>0?0:-1;
  SavePreferences();
 }
 void Close_Click(object sender,RoutedEventArgs e)=>Close();
 async void Login_Click(object sender,RoutedEventArgs e)=>await BeginLoginAsync();
 async void Logout_Click(object sender,RoutedEventArgs e){try{await rpc.CallAsync("account/logout",new{});await RefreshAccountAsync();}catch(Exception ex){Fail(ex);}}
 async void Send_Click(object sender,RoutedEventArgs e)=>await SendAsync();
 async Task SendAsync()
 {
  if(preview||busy||!authenticated||Models.SelectedItem is not ModelChoice model||string.IsNullOrWhiteSpace(Prompt.Text))return;
  var text=Prompt.Text.Trim();busy=true;UpdateActions();
  try
  {
   if(current==null)throw new IOException("Nedostaje jedini projektni razgovor.");
   if(string.IsNullOrEmpty(current.Id))
   {
    var result=await rpc.CallAsync("thread/start",new{cwd=Isolation.Project,model=model.Id,sandbox="danger-full-access",approvalPolicy=Access.SelectedIndex==1?"never":"on-request",
     developerInstructions="You are the personal coding agent in TermoDom Studio, inside Windows Sandbox. Work only on the TermoDom project. Read AGENTS.md and docs/KONTEKST-ZA-CODEX.md before edits. Preserve existing changes; implement and test requests. Do not send commands to real heating/camera devices. Explain results in Croatian. This is a new isolated history, not a continuation containing the owner's private ChatGPT memories. Maintain project decisions in docs, and do not import external histories.\n\n"+Context.Text});
    var id=result.GetProperty("thread").GetProperty("id").GetString()!;
    current.Id=id;UpdateConversationTitle();SaveThreads();
   }
   if(current.ImportPending)
   {
    if(privateImport==null)throw new IOException("Nedostaje privatni projektni razgovor za nastavak.");
    await rpc.CallAsync("thread/inject_items",new{threadId=current.Id,items=privateImport.ToItems()});current.ImportPending=false;SaveThreads();
   }
   var input=new List<object>{new{type="text",text}};
   foreach(var path in attachments)
   {
    var resolved=ResolveAttachment(path);
    if(new[]{".png",".jpg",".jpeg",".webp"}.Contains(Path.GetExtension(path).ToLowerInvariant()))input.Add(new{type="localImage",path=resolved});
    else input.Add(new{type="text",text="Korisnik je priložio datoteku za ovaj zadatak. Pročitaj je kao podatke na putanji: "+resolved});
   }
   var userBlock=AddMessage("TI",text+(attachments.Count>0?"\nPrivitci: "+string.Join(", ",attachments.Select(Path.GetFileName)):""));
   if(attachments.Count>0){current.Messages[^1]=current.Messages[^1] with { Attachments=attachments.ToList() };RenderSavedAttachments(attachments,(StackPanel)userBlock.Parent);}
   Prompt.Clear();attachments.Clear();UpdateAttachments();SaveThreads();
   var turn=await rpc.CallAsync("turn/start",new{threadId=current.Id,input=input.ToArray(),cwd=Isolation.Project,model=model.Id,effort=Effort.SelectedItem as string,
    serviceTierForTurn=(Speed.SelectedItem as SpeedChoice)?.Id??"default",approvalPolicy=Access.SelectedIndex==1?"never":"on-request",sandboxPolicy=new{type="externalSandbox",networkAccess="enabled"}});
   currentTurn=turn.GetProperty("turn").GetProperty("id").GetString();
   Status.Text=Access.SelectedIndex==1?"Codex radi s punim pristupom unutar Sandboxa. Dostupne mapirane datoteke može promijeniti ili izbrisati.":"Codex radi u Sandboxu, uz potvrde naredbi koje zahtijevaju odobrenje.";
  }
  catch(Exception ex){busy=false;UpdateActions();Fail(ex);}
 }
 async void Stop_Click(object sender,RoutedEventArgs e){try{if(current!=null&&currentTurn!=null)await rpc.CallAsync("turn/interrupt",new{threadId=current.Id,turnId=currentTurn});}catch(Exception ex){Fail(ex);}}
 async void Prompt_KeyDown(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){e.Handled=true;await SendAsync();}}
 async void HandleNotification(string method,JsonElement p)
 {
  try
  {
   if(shuttingDown)return;
   if(p.TryGetProperty("threadId",out var notifiedThread)&&notifiedThread.ValueKind==JsonValueKind.String&&current is {Id.Length:>0}&&notifiedThread.GetString()!=current.Id)return;
   if(HandleAgentToolsNotification(method,p))return;
   if(method=="account/login/completed")
   {if(pendingLoginId==null||Text(p,"loginId")!=pendingLoginId)return;pendingLoginId=null;loginPending=false;deviceLoginWindow?.Close();deviceLoginWindow=null;startingLogin=true;UpdateActions();try{if(p.TryGetProperty("success",out var success)&&success.ValueKind==JsonValueKind.True)await RefreshAccountAsync();else Status.Text="Prijava nije dovršena. "+Text(p,"error")+". Možeš ponoviti prijavu ili odabrati Agent → Rezervna službena prijava kodom.";}finally{startingLogin=false;UpdateActions();}}
   else if(method=="item/agentMessage/delta")
   {
    var id=Text(p,"itemId");if(!liveMessages.TryGetValue(id,out var message)){message=AddMessage("CODEX","",false);liveMessages[id]=message;}message.Text+=Text(p,"delta");ChatScroll.ScrollToEnd();
   }
   else if(method=="item/started"||method=="item/completed")
   {
    var item=p.GetProperty("item");var id=Text(item,"id");var type=Text(item,"type");
    if(type=="agentMessage"&&method=="item/completed")
    {
     var text=Text(item,"text");if(liveMessages.TryGetValue(id,out var message)){if(text.Length>0)message.Text=text;}else liveMessages[id]=AddMessage("CODEX",text,false);
     if(current!=null)current.Messages.Add(new("CODEX",text));
    }
    else if(type is "commandExecution" or "fileChange" or "mcpToolCall" or "webSearch")
    {
     if(announced.Add(id))AddMessage("RAD AGENTA",type=="commandExecution"?Text(item,"command"):type=="fileChange"?"Izmjene datoteka projekta":type);
     if(method=="item/completed"&&type=="commandExecution")Terminal.AppendText("\n$ "+Text(item,"command")+"\n"+(streamedCommands.Contains(id)?"":Text(item,"aggregatedOutput"))+"\nStatus: "+Text(item,"status")+"\n");
    }
   }
   else if(method=="item/commandExecution/outputDelta"){streamedCommands.Add(Text(p,"itemId"));Terminal.AppendText(Text(p,"delta"));Terminal.ScrollToEnd();}
   else if(method=="turn/plan/updated"&&p.TryGetProperty("plan",out var plan))
   {PlanStatus.Text=string.Join("\n",plan.EnumerateArray().Select(step=>(Text(step,"status")=="completed"?"✓ ":Text(step,"status")=="inProgress"?"● ":"○ ")+Text(step,"step")));PlanStatus.Visibility=Visibility.Visible;}
   else if(method=="turn/diff/updated")Diff.Text=Text(p,"diff");
   else if(method=="turn/started"){currentTurn=p.GetProperty("turn").GetProperty("id").GetString();busy=true;UpdateActions();}
   else if(method=="thread/tokenUsage/updated"&&p.TryGetProperty("tokenUsage",out var usage)&&usage.TryGetProperty("last",out var last)&&last.TryGetProperty("inputTokens",out var tokens))
   {var window=usage.TryGetProperty("modelContextWindow",out var size)&&size.ValueKind==JsonValueKind.Number?$" / {size.GetInt64():N0}":"";ContextUsage.Text=$"Kontekst: {tokens.GetInt64():N0}{window}";if(usage.TryGetProperty("total",out var total)&&total.TryGetProperty("totalTokens",out var totalTokens))ContextUsage.ToolTip=$"Ukupno kroz razgovor: {totalTokens.GetInt64():N0} tokena";}
   else if(method=="turn/completed")
   {
    busy=false;currentTurn=null;UpdateActions();SaveThreads();RefreshFiles();var turn=p.GetProperty("turn");Status.Text="Zadatak: "+Text(turn,"status");
    if(turn.TryGetProperty("error",out var error)&&error.ValueKind==JsonValueKind.Object)Status.Text+=" · "+Text(error,"message");
    await RefreshGitAsync();
   }
   else if(method=="error")Status.Text=p.TryGetProperty("error",out var error)?Text(error,"message"):"Codex je prijavio grešku.";
  }catch(Exception ex){Fail(ex);}
 }
 Task<object> HandleRequestAsync(string method,JsonElement p)=>Dispatcher.InvokeAsync<object>(()=>
 {
  if(method is "item/commandExecution/requestApproval" or "item/fileChange/requestApproval")
  {
   return new{decision=ChooseApproval(method,p)};
  }
  if(method=="item/permissions/requestApproval")return new{permissions=new{},scope="turn"}; // no silent widening; external Sandbox is the outer boundary
  if(method=="item/tool/requestUserInput")
  {
   var answers=new Dictionary<string,object>();
   foreach(var question in p.GetProperty("questions").EnumerateArray())
   {
    var value=RequestInput(question);
    answers[Text(question,"id")]=new{answers=value==null?Array.Empty<string>():new[]{value}};
   }
   return new{answers};
  }
  throw new NotSupportedException("Unsupported server request");
 }).Task;
 TextBlock AddMessage(string role,string text,bool persist=true)
 {
  var block=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,LineHeight=25,FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(230,230,230))};
  var box=new StackPanel{Margin=new Thickness(0,5,0,24)};box.Children.Add(new TextBlock{Text=role,FontSize=10,Foreground=Brushes.Gray,Margin=new Thickness(0,0,0,7)});box.Children.Add(block);Chat.Children.Add(box);ChatScroll.ScrollToEnd();
  var menu=new ContextMenu();var copy=new MenuItem{Header="Kopiraj poruku"};copy.Click+=(_,_)=>{try{Clipboard.SetText(block.Text);}catch(Exception ex){Fail(ex);}};menu.Items.Add(copy);block.ContextMenu=menu;
  if(persist&&current!=null)current.Messages.Add(new(role,text));return block;
 }
 void RenderImportedImages(JsonElement message,StackPanel container)
 {
  foreach(var part in message.GetProperty("content").EnumerateArray())
  {
   if(Text(part,"type")!="input_image")continue;
   try
   {
    var uri=Text(part,"image_url");var comma=uri.IndexOf(',');if(comma<0)continue;
    using var stream=new MemoryStream(Convert.FromBase64String(uri[(comma+1)..]));
    var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=250;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
    container.Children.Add(new Image{Source=bitmap,MaxHeight=230,MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left,Stretch=Stretch.Uniform,Margin=new Thickness(0,10,0,0)});
   }catch(Exception ex) when(ex is FormatException or NotSupportedException or IOException){Status.Text="Jedna prenesena slika nije dostupna za prikaz.";}
  }
 }
 void UpdateConversationTitle(){ConversationTitle.Text=current?.Label??"Modernizuj aplikaciju za grijanje";}
 void SaveThreads(){if(preview)return;var temp=IndexPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(threads));File.Move(temp,IndexPath,true);}
 void ToggleTools_Click(object sender,RoutedEventArgs e){var show=ToolPane.Visibility!=Visibility.Visible;ToolPane.Visibility=show?Visibility.Visible:Visibility.Collapsed;ToolsColumn.Width=new GridLength(show?430:0);}
 void Repository_Click(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("https://github.com/leo25matko-boop/termodom-suradnja"){UseShellExecute=true});
 void Attach_Click(object sender,RoutedEventArgs e)
 {
  if(preview||workspace==null)return;
  var dialog=new Microsoft.Win32.OpenFileDialog{Title="Priloži slike ili datoteke zadatku",InitialDirectory=workspace.Root,Multiselect=true};
  if(dialog.ShowDialog(this)!=true)return;
  try{foreach(var selected in dialog.FileNames){var path=PrepareAttachment(selected);if(!attachments.Contains(path))attachments.Add(path);}UpdateAttachments();}catch(Exception ex){Fail(ex);}
 }
 void ClearAttachments_Click(object sender,RoutedEventArgs e){attachments.Clear();UpdateAttachments();}
 void UpdateAttachments(){AttachmentSummary.Text=attachments.Count>0?$"{attachments.Count} privitaka":"";ClearAttachments.Visibility=attachments.Count>0?Visibility.Visible:Visibility.Collapsed;}
 void RefreshFiles(){if(workspace==null)return;Files.ItemsSource=workspace.Files().Where(p=>p.Contains(Search.Text,StringComparison.OrdinalIgnoreCase)).ToList();}
 void Refresh_Click(object sender,RoutedEventArgs e){try{RefreshFiles();LoadContext();}catch(Exception ex){Fail(ex);}}
 void Search_Changed(object sender,TextChangedEventArgs e){if(Files!=null&&workspace!=null)RefreshFiles();}
 bool CanDiscardEditor()=>!editorDirty||MessageBox.Show(this,"Datoteka ima nespremljene izmjene. Nastaviti bez spremanja?","Uređivač",MessageBoxButton.YesNo)==MessageBoxResult.Yes;
 void Files_Selected(object sender,SelectionChangedEventArgs e)
 {
  if(Files.SelectedItem is not string relative||workspace==null)return;
  if(!CanDiscardEditor())return;try{OpenEditor(relative);}catch(Exception ex){Fail(ex);}
 }
 void OpenEditor(string relative)
 {
  var path=workspace!.Resolve(relative);if(new FileInfo(path).Length>2_000_000)throw new IOException("Za uređivač odaberi tekstnu datoteku do 2 MB.");
  ToolPane.Visibility=Visibility.Visible;ToolsColumn.Width=new GridLength(430);
  var bytes=File.ReadAllBytes(path);if(bytes.Contains((byte)0))throw new IOException("Binarna datoteka se ne otvara u tekstnom uređivaču.");
  loadingEditor=true;Editor.Text=System.Text.Encoding.UTF8.GetString(bytes);loadingEditor=false;editorPath=relative;editorHash=Convert.ToHexString(SHA256.HashData(bytes));editorDirty=false;EditorName.Text=relative;
 }
 void Editor_Changed(object sender,TextChangedEventArgs e){if(!loadingEditor&&editorPath!=null){editorDirty=true;EditorName.Text=editorPath+" • nespremljeno";}}
 void Save_Click(object sender,RoutedEventArgs e)
 {
  if(preview||workspace==null||editorPath==null)return;
  try
  {
   var path=workspace.Resolve(editorPath);
   if(!File.Exists(path)||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))!=editorHash)throw new IOException("Datoteka se promijenila na disku. Pregledaj je prije spremanja kako ne bi prepisao rad agenta.");
   var temp=path+".studio-"+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,Editor.Text,new System.Text.UTF8Encoding(false));File.Move(temp,path,true);OpenEditor(editorPath);Status.Text="Datoteka je spremljena u izoliranom projektu.";
  }catch(Exception ex){Fail(ex);}
 }
 void Reload_Click(object sender,RoutedEventArgs e){if(editorPath!=null&&CanDiscardEditor())try{OpenEditor(editorPath);}catch(Exception ex){Fail(ex);}}
 string GitExe=>Path.Combine(Isolation.AppRoot,"Tools","git","cmd","git.exe");
 async Task RefreshGitAsync()
 {
  if(preview)return;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
  var status=await Workspace.RunAsync(GitExe,new[]{"status","--short","--branch"},Isolation.Project,timeout.Token);
  var diff=await Workspace.RunAsync(GitExe,new[]{"diff","--no-ext-diff","--stat"},Isolation.Project,timeout.Token);
  var full=await Workspace.RunAsync(GitExe,new[]{"diff","--no-ext-diff"},Isolation.Project,timeout.Token);
  Diff.Text=status.Output+"\n"+diff.Output+"\n"+full.Output;
 }
 async void Git_Click(object sender,RoutedEventArgs e){try{await RefreshGitAsync();}catch(Exception ex){Fail(ex);}}
 async void Commit_Click(object sender,RoutedEventArgs e)
 {
  if(preview||busy)return;
  try
  {
   await RefreshGitAsync();var message=Ask("Opis commita (prije spremanja pregledaj Git diff; commit uključuje sve neignorirane datoteke projekta):");if(string.IsNullOrWhiteSpace(message))return;
   using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
   var add=await Workspace.RunAsync(GitExe,new[]{"add","--all"},Isolation.Project,timeout.Token);if(add.Code!=0)throw new IOException(add.Output);
   var commit=await Workspace.RunAsync(GitExe,new[]{"-c","user.name=TermoDom Studio","-c","user.email=studio@localhost","commit","-m",message},Isolation.Project,timeout.Token);
   Terminal.AppendText(commit.Output);Status.Text=commit.Code==0?"Lokalni commit spremljen. Objavu na GitHub zadaj agentu uz vlastitu službenu GitHub prijavu.":commit.Output;await RefreshGitAsync();
  }catch(Exception ex){Fail(ex);}
 }
 async void Command_Click(object sender,RoutedEventArgs e)=>await RunCommandAsync(Command.Text);
 async void Test_Click(object sender,RoutedEventArgs e)=>await RunCommandAsync("./scripts/team/Dev.ps1 -Action Test");
 async void Tools_Click(object sender,RoutedEventArgs e)=>await RunCommandAsync("& C:\\TermoDom\\App\\Prepare-Tools.ps1");
 void KillCommand_Click(object sender,RoutedEventArgs e)=>commandLifetime?.Cancel();
 async Task RunCommandAsync(string command)
 {
  if(preview||string.IsNullOrWhiteSpace(command)||commandLifetime!=null)return;
  commandLifetime=new CancellationTokenSource();Terminal.AppendText("\n$ "+command+"\n");
  try{var result=await Workspace.RunAsync("powershell.exe",new[]{"-NoLogo","-NoProfile","-ExecutionPolicy","Bypass","-Command",command},Isolation.Project,commandLifetime.Token);Terminal.AppendText(result.Output+"\nExit: "+result.Code+"\n");}
  catch(OperationCanceledException){Terminal.AppendText("\nProces zaustavljen.\n");}catch(Exception ex){Fail(ex);}finally{commandLifetime.Dispose();commandLifetime=null;}
 }
 string? Ask(string question)
 {
  var dialog=new Window{Title="TermoDom Studio",Owner=this,Width=550,Height=330,WindowStartupLocation=WindowStartupLocation.CenterOwner};
  var panel=new StackPanel{Margin=new Thickness(20)};dialog.Content=panel;panel.Children.Add(new TextBlock{Text=question,TextWrapping=TextWrapping.Wrap});
  var input=new TextBox{Margin=new Thickness(0,15,0,10),AcceptsReturn=true,Height=75};panel.Children.Add(input);
  var accept=new Button{Content="Potvrdi",IsDefault=true};accept.Click+=(_,_)=>{dialog.DialogResult=true;};panel.Children.Add(accept);input.Focus();return dialog.ShowDialog()==true?input.Text:null;
 }
 static string Text(JsonElement p,string key)=>p.ValueKind==JsonValueKind.Object&&p.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
}
