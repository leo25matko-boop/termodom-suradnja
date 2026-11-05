using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TermoDom.Studio.Setup;
public static class Program
{
 [STAThread] public static void Main()
 {
  var app=new Application();var window=new Window{Title="TermoDom Studio · Instalacija",Width=640,Height=430,Background=new SolidColorBrush(Color.FromRgb(12,21,29)),Foreground=Brushes.White,FontFamily=new FontFamily("Segoe UI"),WindowStartupLocation=WindowStartupLocation.CenterScreen};
  var panel=new StackPanel{Margin=new Thickness(34)};window.Content=panel;
  panel.Children.Add(new TextBlock{Text="TERMODOM  /  STUDIO",Foreground=Brushes.Turquoise,FontSize=14});
  panel.Children.Add(new TextBlock{Text="Osobni Codex za TermoDom",FontSize=27,Margin=new Thickness(0,24,0,16)});
  var status=new TextBlock{Text="Instalira nativni Studio, pregledani projektni kontekst, službeni Codex CLI, Git i .NET SDK. Koristi zaseban Windows Sandbox. Postojeće Codex prijave i razgovori se ne kopiraju.\n\nZa uključivanje Windows Sandboxa potrebna je administratorska potvrda i možda ponovno pokretanje.",TextWrapping=TextWrapping.Wrap,LineHeight=23};panel.Children.Add(status);
  var progress=new ProgressBar{Height=6,Margin=new Thickness(0,20,0,10)};panel.Children.Add(progress);
  var button=new Button{Content="Instaliraj za mene",Padding=new Thickness(16,10,16,10),HorizontalAlignment=HorizontalAlignment.Left};panel.Children.Add(button);
  string? installed=null;
  button.Click+=async(_,_)=>
  {
   if(installed!=null){Process.Start(new ProcessStartInfo(installed){UseShellExecute=true});window.Close();return;}
   button.IsEnabled=false;progress.IsIndeterminate=true;status.Text="Raspakiravanje Studija i razvojnih alata… Tvoji postojeći projekti i postavke ostaju odvojeni.";
   try
   {
    installed=await Task.Run(Install);status.Text="Studio je instaliran. Otvori ga i uključi Windows Sandbox ako još nije dostupan. Zatim se prijavi u službenom OpenAI prozoru unutar izoliranog Studija.\n\nPrečac: TermoDom Studio u izborniku Start.";button.Content="Otvori TermoDom Studio";
   }
   catch(Exception ex){status.Text="Instalacija nije dovršena: "+ex.Message;button.Content="Pokušaj ponovo";}
   finally{progress.IsIndeterminate=false;button.IsEnabled=true;}
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
  // WScript.Shell creates a normal current-user shortcut; no account or credential operations.
  var shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic shortcut=((dynamic)shell!).CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"TermoDom Studio.lnk"));shortcut.TargetPath=exe;shortcut.WorkingDirectory=root;shortcut.Description="Izolirani osobni Codex za TermoDom";shortcut.Save();
  return exe;
 }
}
