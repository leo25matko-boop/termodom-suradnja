using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace TermoDom.Studio;

public partial class StudioWindow
{
 bool WorkspaceActionAllowed()
 {
  if(preview){Status.Text="Ovo je pregled sučelja. Projektne radnje dostupne su u izoliranom Studiju.";return false;}
  if(workspace==null||!Isolation.IsWorker){Status.Text="Projektna radnja zahtijeva pripremljeni Windows Sandbox.";return false;}
  if(busy||commandLifetime!=null){Status.Text="Pričekaj završetak trenutnog zadatka ili zaustavi proces prije ove radnje.";return false;}
  return true;
 }

 void ShowWorkspaceTool(int tab)
 {
  ToolPane.Visibility=Visibility.Visible;ToolsColumn.Width=new GridLength(430);
  if(ToolPane.Child is TabControl tabs&&tab<tabs.Items.Count)tabs.SelectedIndex=tab;
 }

 async Task WorkspaceActionAsync(Func<CancellationToken,Task> action)
 {
  if(!WorkspaceActionAllowed())return;
  busy=true;commandLifetime=new CancellationTokenSource(TimeSpan.FromMinutes(3));UpdateActions();
  try{await action(commandLifetime.Token);}
  catch(OperationCanceledException){Status.Text="Radnja je zaustavljena ili je isteklo vrijeme. Provjeri terminal i Git status prije ponavljanja.";}
  catch(Exception ex){Fail(ex);}
  finally{commandLifetime.Dispose();commandLifetime=null;busy=false;UpdateActions();}
 }

 async void SearchContent_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  var term=Ask("Tekst za pretraživanje sadržaja projektnih datoteka (do 200 znakova):");
  if(string.IsNullOrEmpty(term))return;
  if(term.Length>200){Status.Text="Upit za pretraživanje može imati najviše 200 znakova.";return;}
  await WorkspaceActionAsync(async token=>
  {
   var active=workspace!;
   var result=await Task.Run(()=>SearchProject(active,term,token),token);
   Diff.Text=result;ShowWorkspaceTool(1);Status.Text="Rezultati pretraživanja sadržaja prikazani su u panelu Git / rezultati.";
  });
 }

 static string SearchProject(Workspace active,string term,CancellationToken token)
 {
  const int entryLimit=5000,resultLimit=200,fileLimit=1_000_000,totalLimit=16_000_000;
  var result=new StringBuilder();var pending=new Stack<(string Path,int Depth)>();pending.Push((active.Root,0));
  var entries=0;var scanned=0;var skipped=0;var total=0;var matches=0;var limited=false;
  while(pending.Count>0&&!limited)
  {
   var directory=pending.Pop();if(directory.Depth>32){skipped++;continue;}
   foreach(var entry in Directory.EnumerateFileSystemEntries(directory.Path))
   {
    token.ThrowIfCancellationRequested();if(++entries>entryLimit){limited=true;break;}
    string path,relative;FileAttributes attributes;
    try{relative=Path.GetRelativePath(active.Root,entry);path=active.Resolve(relative);attributes=File.GetAttributes(path);}
    catch(IOException){skipped++;continue;}
    catch(UnauthorizedAccessException){skipped++;continue;}
    if((attributes&FileAttributes.ReparsePoint)!=0){skipped++;continue;}
    if((attributes&FileAttributes.Directory)!=0){pending.Push((path,directory.Depth+1));continue;}
    try
    {
     using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
     if(file.Length>fileLimit){skipped++;continue;}
     if(total+file.Length>totalLimit){limited=true;break;}
     var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);total+=bytes.Length;
     if(bytes.Contains((byte)0)){skipped++;continue;}
     var text=new UTF8Encoding(false,true).GetString(bytes);scanned++;
     using var reader=new StringReader(text);string? line;var lineNumber=0;
     while((line=reader.ReadLine())!=null)
     {
      token.ThrowIfCancellationRequested();lineNumber++;
      if(!line.Contains(term,StringComparison.OrdinalIgnoreCase))continue;
      result.Append(relative).Append(':').Append(lineNumber).Append("  ").AppendLine(line.Length>500?line[..500]+"…":line);
      if(++matches>=resultLimit){limited=true;break;}
     }
    }
    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or DecoderFallbackException){skipped++;}
    if(limited)break;
   }
  }
  var header=$"Pretraživanje: {term}\n{matches} rezultata · {scanned} tekstnih datoteka · {skipped} preskočenih stavki\n";
  header+="Granice: 200 rezultata, 5000 stavki, 1 MB po datoteci, 16 MB ukupno; binarne, razvojne i skrivene Git/Codex mape se preskaču.\n";
  if(limited)header+="Dosegnuta je granica. Suzi upit za potpuniji rezultat.\n";
  return header+"\n"+(matches==0?"Nema pronađenog teksta u pregledanim datotekama.":result.ToString());
 }

 void NewFile_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed()||!CanDiscardEditor())return;
  var relative=Ask("Putanja nove datoteke unutar projekta, npr. docs/moja-biljeska.md:");if(string.IsNullOrWhiteSpace(relative))return;
  try
  {
   relative=relative.Trim();var path=workspace!.Resolve(relative);
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){}
   RefreshFiles();OpenEditor(relative);ShowWorkspaceTool(0);Status.Text="Nova projektna datoteka je stvorena.";
  }catch(Exception ex){Fail(ex);}
 }

 void NewFolder_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  var relative=Ask("Putanja nove mape unutar projekta, npr. docs/nadogradnja:");if(string.IsNullOrWhiteSpace(relative))return;
  try{Directory.CreateDirectory(workspace!.Resolve(relative.Trim()));RefreshFiles();Status.Text="Projektna mapa je spremna. Popis datoteka prikazuje je nakon dodavanja prve datoteke.";}catch(Exception ex){Fail(ex);}
 }

 void OpenProject_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  try{var start=new ProcessStartInfo("explorer.exe"){UseShellExecute=true};start.ArgumentList.Add(workspace!.Root);Process.Start(start);Status.Text="Otvorena je projektna mapa unutar Sandboxa.";}catch(Exception ex){Fail(ex);}
 }

 async void RunApp_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  ShowWorkspaceTool(2);await RunCommandAsync("./scripts/team/Dev.ps1 -Action Run");
 }

 async Task<(int Code,string Output)> ProjectGitAsync(CancellationToken token,params string[] args)
 {
  token.ThrowIfCancellationRequested();
  var start=new ProcessStartInfo(GitExe){WorkingDirectory=workspace!.Root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  // Permit configured official credential-helper UI, but never hang on a hidden terminal password prompt.
  start.Environment["GIT_TERMINAL_PROMPT"]="0";start.Environment["GCM_INTERACTIVE"]="always";
  start.ArgumentList.Add("--literal-pathspecs");
  foreach(var arg in args)start.ArgumentList.Add(arg);
  using var process=Process.Start(start)??throw new IOException("Git se nije pokrenuo.");
  using var registration=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
  var output=process.StandardOutput.ReadToEndAsync(token);var error=process.StandardError.ReadToEndAsync(token);
  await process.WaitForExitAsync(token);var text=await output+await error;
  Terminal.AppendText("\n$ git "+string.Join(" ",args)+"\n"+(text.Length>250_000?text[..250_000]+"\n[Izlaz je skraćen]":text)+"\nExit: "+process.ExitCode+"\n");
  return(process.ExitCode,text);
 }

 async Task<string> CheckedGitAsync(CancellationToken token,params string[] args)
 {
  var result=await ProjectGitAsync(token,args);
  if(result.Code!=0)throw new IOException("Git nije dovršio radnju. "+(result.Output.Length>1500?result.Output[..1500]:result.Output)+"\nAko GitHub traži prijavu, dovrši vlastitu službenu prijavu unutar Sandboxa. Host prijava se ne prenosi.");
  return result.Output.Trim();
 }

 async void GitBranch_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  var branch=Ask("Naziv nove radne grane, npr. vlasnik/moja-izmjena:");if(string.IsNullOrWhiteSpace(branch))return;branch=branch.Trim();
  await WorkspaceActionAsync(async token=>
  {
   await CheckedGitAsync(token,"check-ref-format","--branch",branch);
   await CheckedGitAsync(token,"switch","-c",branch);await RefreshGitAsync();ShowWorkspaceTool(1);Status.Text="Stvorena i otvorena grana: "+branch;
  });
 }

 async void GitFetch_Click(object sender,RoutedEventArgs e)=>await WorkspaceActionAsync(async token=>
 {
  ShowWorkspaceTool(2);await CheckedGitAsync(token,"fetch","--prune","origin");await RefreshGitAsync();Status.Text="Git je preuzeo stanje origin repozitorija. Radne datoteke nisu mijenjane.";
 });

 async void GitPull_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  if(editorDirty){Status.Text="Prvo spremi ili ponovno učitaj otvorenu datoteku, zatim preuzmi promjene.";return;}
  var branch=Ask("Koju origin granu želiš preuzeti u trenutnu granu? Za glavnu projektnu granu upiši mail:");if(string.IsNullOrWhiteSpace(branch))return;branch=branch.Trim();
  await WorkspaceActionAsync(async token=>
  {
   if((await CheckedGitAsync(token,"status","--porcelain=v1")).Length>0)throw new IOException("Projekt ima lokalne ili nove datoteke. Spremi ih u commit prije povlačenja promjena; ništa nije prepisano.");
   await CheckedGitAsync(token,"check-ref-format","--branch",branch);ShowWorkspaceTool(2);
   await CheckedGitAsync(token,"pull","--ff-only","--no-rebase","--no-overwrite-ignore","origin",branch);RefreshFiles();LoadContext();
   if(editorPath!=null)
   {
    if(File.Exists(workspace!.Resolve(editorPath)))OpenEditor(editorPath);
    else{editorPath=null;editorHash=null;loadingEditor=true;Editor.Clear();loadingEditor=false;EditorName.Text="Prethodna datoteka više nije u preuzetom projektu.";}
   }
   await RefreshGitAsync();Status.Text="Promjene su preuzete samo ako je moguć fast-forward, bez automatskog spajanja.";
  });
 }

 async void GitPush_Click(object sender,RoutedEventArgs e)=>await WorkspaceActionAsync(async token=>
 {
  var branch=await CheckedGitAsync(token,"branch","--show-current");
  if(branch.Length==0)throw new IOException("Projekt nije na radnoj grani. Prvo stvori granu.");
  if(branch is "mail" or "main" or "master")throw new IOException("Za predaju promjena stvori zasebnu radnu granu i otvori pull request.");
  ShowWorkspaceTool(2);await CheckedGitAsync(token,"push","--set-upstream","origin","HEAD:refs/heads/"+branch);await RefreshGitAsync();Status.Text="Grana je poslana na origin. Odaberi Pull request za predaju na pregled.";
 });

 async void GitRestoreFile_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  if(editorPath==null){Status.Text="Prvo otvori datoteku koju želiš vratiti na zadnji commit.";return;}
  if(editorDirty){Status.Text="Otvorena datoteka ima nespremljene izmjene. Prvo je spremi ili ponovno učitaj s diska.";return;}
  var relative=editorPath;
  await WorkspaceActionAsync(async token=>
  {
   var path=workspace!.Resolve(relative);
   if((await ProjectGitAsync(token,"ls-files","--error-unmatch","--",relative)).Code!=0)throw new IOException("Obnova je dovoljena samo za datoteku koju Git već prati. Nova datoteka nije promijenjena.");
   var diff=await CheckedGitAsync(token,"diff","HEAD","--no-ext-diff","--",relative);Diff.Text=diff;ShowWorkspaceTool(1);
   var before=File.Exists(path)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))):"missing";
   if(!ConfirmFileRestore(relative,diff))return;
   var after=File.Exists(path)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))):"missing";
   if(after!=before)throw new IOException("Datoteka se promijenila tijekom potvrde. Obnova nije izvršena; prvo pregledaj novo stanje.");
   await CheckedGitAsync(token,"restore","--source=HEAD","--worktree","--",relative);RefreshFiles();OpenEditor(relative);await RefreshGitAsync();Status.Text="Odabrana radna datoteka vraćena je na HEAD. Ostale datoteke i staged izmjene nisu mijenjane.";
  });
 }

 bool ConfirmFileRestore(string relative,string diff)
 {
  var area=SystemParameters.WorkArea;
  var dialog=new Window{Title="Pregled obnove datoteke",Owner=this,Width=Math.Min(800,area.Width),Height=Math.Min(650,area.Height),WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)Application.Current.FindResource(typeof(Window))};
  var panel=new DockPanel{Margin=new Thickness(18)};dialog.Content=panel;
  var explanation=new TextBlock{Text=relative+"\n\nVrati radnu datoteku na zadnji commit HEAD? Izmjene ove datoteke na disku bit će izgubljene. Git index (staged izmjene) ostaje nepromijenjen. Pregledaj diff prije potvrde.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(explanation,Dock.Top);panel.Children.Add(explanation);
  var actions=new WrapPanel();DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
  var accept=new Button{Content="Vrati ovu datoteku na HEAD"};accept.Click+=(_,_)=>dialog.DialogResult=true;actions.Children.Add(accept);
  var cancel=new Button{Content="Odustani",IsCancel=true,IsDefault=true};actions.Children.Add(cancel);
  panel.Children.Add(new TextBox{Text=diff.Length==0?"Git ne prikazuje razliku prema HEAD-u.":diff,IsReadOnly=true,AcceptsReturn=true,AcceptsTab=true,FontFamily=new System.Windows.Media.FontFamily("Consolas"),FontSize=12,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  return dialog.ShowDialog()==true;
 }

 async void GithubPR_Click(object sender,RoutedEventArgs e)=>await WorkspaceActionAsync(async token=>
 {
  var origin=await CheckedGitAsync(token,"remote","get-url","origin");
  var match=Regex.Match(origin,@"^(?:https://github\.com/|git@github\.com:)([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?/?$");
  if(!match.Success)throw new IOException("Origin nije prepoznatljiva GitHub adresa bez ugrađenih vjerodajnica. Uredi projektni remote prije pripreme pull requesta.");
  var branch=await CheckedGitAsync(token,"branch","--show-current");if(branch.Length==0)throw new IOException("Za pull request prvo otvori radnu granu.");
  var url="https://github.com/"+match.Groups[1].Value+"/"+match.Groups[2].Value+"/compare/mail..."+Uri.EscapeDataString(branch)+"?expand=1";
  BrowserLauncher.Open(url);Status.Text="Otvoren je službeni GitHub obrazac za pregled i predaju pull requesta. Ništa nije poslano automatski.";
 });

 void NewRepository_Click(object sender,RoutedEventArgs e)
 {
  if(!WorkspaceActionAllowed())return;
  try{BrowserLauncher.Open("https://github.com/new");Status.Text="Otvoren je službeni GitHub obrazac za novi repozitorij. Postojeći projekt i remote ostaju isti dok ih sam ne promijeniš.";}catch(Exception ex){Fail(ex);}
 }
}
