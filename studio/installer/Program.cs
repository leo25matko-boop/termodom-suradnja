using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TermoDom.Studio.Setup;
public static class Program
{
 [STAThread] public static void Main(string[] args)
 {
  if(args.Contains("--install-current-user"))
  {
   try{var exe=Install();CreateShortcuts(exe);Console.WriteLine("TermoDom Studio 1.1.0 installed for current user with Desktop and Start shortcuts.");}
   catch(Exception ex){Console.Error.WriteLine("Installation failed: "+ex.Message);Environment.ExitCode=1;}
   return;
  }
  var app=new Application();var window=new Window{Title="TermoDom Studio 1.1.0 · Instalacija",Width=660,Height=520,MaxHeight=SystemParameters.WorkArea.Height,MaxWidth=SystemParameters.WorkArea.Width,WindowStyle=WindowStyle.SingleBorderWindow,ResizeMode=ResizeMode.CanMinimize,Background=new SolidColorBrush(Color.FromRgb(12,21,29)),Foreground=Brushes.White,FontFamily=new FontFamily("Segoe UI"),WindowStartupLocation=WindowStartupLocation.CenterScreen};
  var panel=new StackPanel{Margin=new Thickness(30)};window.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  panel.Children.Add(new TextBlock{Text="TERMODOM  /  STUDIO",Foreground=Brushes.Turquoise,FontSize=14});
  panel.Children.Add(new TextBlock{Text="Osobni Codex za TermoDom",FontSize=27,Margin=new Thickness(0,24,0,16)});
  var status=new TextBlock{Text="Instalira nativni Studio, pregledani projekt, službeni Codex CLI i Git. Stvara prečac na radnoj površini i u izborniku Start. Zasebno spremljeni Studio razgovor i prijava ostaju sačuvani.\n\nWindows Sandbox pri prvom postavljanju može zatražiti administratorsku potvrdu i ponovno pokretanje. Prva OpenAI prijava potvrđuje se u službenom prozoru.",TextWrapping=TextWrapping.Wrap,LineHeight=23};panel.Children.Add(status);
  var progress=new ProgressBar{Height=6,Margin=new Thickness(0,20,0,10)};panel.Children.Add(progress);
  var actions=new WrapPanel();panel.Children.Add(actions);
  var button=new Button{Content="Instaliraj za mene",Padding=new Thickness(16,10,16,10),Margin=new Thickness(0,0,12,8)};actions.Children.Add(button);
  var close=new Button{Content="✕  Zatvori",Padding=new Thickness(16,10,16,10),Margin=new Thickness(0,0,0,8)};actions.Children.Add(close);
  close.Click+=(_,_)=>window.Close();
  var busy=false;
  window.Closing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Instalacija još traje. Pričekaj završetak raspakiravanja, zatim možeš zatvoriti prozor.";}};
  string? installed=null;
  button.Click+=async(_,_)=>
  {
   if(installed!=null)
   {
    try{Process.Start(new ProcessStartInfo(installed){UseShellExecute=true,Arguments="--open",WorkingDirectory=Path.GetDirectoryName(installed)!});window.Close();}
    catch(Exception ex){status.Text="Studio je instaliran, ali pokretanje nije uspjelo: "+ex.Message;}
    return;
   }
   busy=true;button.IsEnabled=false;close.IsEnabled=false;progress.IsIndeterminate=true;status.Text="Raspakiravanje Studija i razvojnih alata… Postojeći Studio razgovor, prijava i projekt ostaju sačuvani.";
   try
   {
    var exe=await Task.Run(Install);
    // Shortcut COM operations run on the WPF STA thread, after successful extraction.
    CreateShortcuts(exe);
    installed=exe;status.Text="Studio je instaliran. Prečac TermoDom Studio nalazi se na radnoj površini i u izborniku Start.\n\nOdaberi Otvori Studio za automatsku pripremu projekta i nastavak spremljenog razgovora. Prvu službenu prijavu potvrđuješ osobno.";button.Content="Otvori Studio";
   }
   catch(Exception ex){status.Text="Instalacija nije dovršena: "+ex.Message;button.Content="Pokušaj ponovo";}
   finally{busy=false;progress.IsIndeterminate=false;button.IsEnabled=true;close.IsEnabled=true;}
  };
  app.Run(window);
 }
 static string Install()
 {
  var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","TermoDomStudio");
  // Refuse reparse points along the complete destination path; don't follow junctions.
  var check=new DirectoryInfo(root);while(check!=null){if(check.Exists&&(check.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Instalacijska mapa ne smije biti poveznica.");check=check.Parent;}
  Directory.CreateDirectory(root);using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("StudioPayload.zip")??throw new IOException("Instalacijski paket nedostaje.");using var zip=new ZipArchive(stream);
  foreach(var entry in zip.Entries)
  {
   var target=Path.GetFullPath(Path.Combine(root,entry.FullName));
   if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Neispravan sadržaj paketa.");
   if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(target);continue;}
   var parent=new DirectoryInfo(Path.GetDirectoryName(target)!);while(parent.FullName.Length>root.Length){if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Poveznica u instalacijskoj mapi.");parent=parent.Parent!;}
   Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target,true);
  }
  var exe=Path.Combine(root,"TermoDomStudio.exe");
  if(!File.Exists(exe))throw new IOException("Instalacija ne sadrži izvršnu datoteku Studija.");
  return exe;
 }
 static void CreateShortcuts(string exe)
 {
  // These are current-user shell folders, including redirected/OneDrive desktops.
  object? shell=null;
  try
  {
   shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("Windows Shell nije dostupan za stvaranje prečaca."));
   foreach(var folder in new[]{Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.Programs})
   {
    var location=Environment.GetFolderPath(folder);
    if(string.IsNullOrWhiteSpace(location))throw new IOException("Windows nije vratio mapu za prečac: "+folder);
    Directory.CreateDirectory(location);
    object? link=null;
    try
    {
     link=((dynamic)shell!).CreateShortcut(Path.Combine(location,"TermoDom Studio.lnk"));
     dynamic shortcut=link;shortcut.TargetPath=exe;shortcut.Arguments="--open";shortcut.WorkingDirectory=Path.GetDirectoryName(exe)!;shortcut.Description="Izolirani osobni Studio za TermoDom";shortcut.IconLocation=exe+",0";shortcut.Save();
    }
    finally{if(link!=null&&Marshal.IsComObject(link))Marshal.FinalReleaseComObject(link);}
   }
  }
  finally{if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
 }
}
