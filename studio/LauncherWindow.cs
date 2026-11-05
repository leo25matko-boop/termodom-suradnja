using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TermoDom.Studio;
public sealed class LauncherWindow : Window
{
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,15,4,16),Foreground=new SolidColorBrush(Color.FromRgb(245,190,110))};
 public LauncherWindow()
 {
  Style=(Style)Application.Current.FindResource(typeof(Window));
  Title="TermoDom Studio · Osobni razvoj"; Width=710; Height=680; WindowStartupLocation=WindowStartupLocation.CenterScreen;
  var p=new StackPanel{Margin=new Thickness(35)};Content=p;
  p.Children.Add(new TextBlock{Text="TERMODOM  /  STUDIO",FontSize=13,Foreground=Brushes.Turquoise});
  p.Children.Add(new TextBlock{Text="Tvoj projekt. Zaseban Codex.",FontSize=29,Margin=new Thickness(0,25,0,16)});
  p.Children.Add(new TextBlock{Text="Nativni uređivač, razgovori, terminal i stvarni Codex agent u Windows Sandboxu. Dostupni su TermoDom i zasebni podaci Studija. Pripremljeni privatni izvoz prenosi samo ovaj projektni razgovor; ostali razgovori ostaju odvojeni.",TextWrapping=TextWrapping.Wrap,FontSize=16,LineHeight=25});
  p.Children.Add(status);
  Add("Otvori izolirani Studio",()=> { if(!Isolation.SandboxInstalled) throw new InvalidOperationException("Prvo uključi Windows Sandbox i po potrebi ponovno pokreni računalo.");var config=Isolation.Prepare();Process.Start(new ProcessStartInfo(config){UseShellExecute=true});status.Text="Studio se pokreće u zasebnom Windows okruženju. Rad i vlastiti razgovori ostaju spremljeni nakon zatvaranja."; });
  Add("Uključi Windows Sandbox (administrator)",()=> {
   if(MessageBox.Show("Uključiti Microsoftovu značajku Windows Sandbox? Windows traži administratorsku potvrdu. Računalo se neće automatski ponovno pokrenuti.","Priprema izolacije",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
   var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=true,Verb="runas"};start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-File");start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"Enable-Sandbox.ps1"));Process.Start(start);
  });
  Add("Pregled sučelja bez prijave i bez agenta",()=>new StudioWindow(true).Show());
  Add("Uvezi privatnu kopiju ovog razgovora",()=> {
   var dialog=new Microsoft.Win32.OpenFileDialog{Title="Privatni izvoz projektnog razgovora",Filter="Privatni razgovor (*.json)|*.json"};
   if(dialog.ShowDialog(this)!=true)return;
   var imported=ConversationImport.Load(dialog.FileName);
   var data=Path.Combine(Isolation.HostRoot,"Data");
   if(File.Exists(Path.Combine(data,"studio-chats.json")))throw new IOException("Razgovor je već započet u Studiju. Postojeća povijest neće biti prepisana.");
   Isolation.SecureDirectory(Isolation.HostRoot);var folder=Path.Combine(data,"Imports");Directory.CreateDirectory(folder);
   File.Copy(dialog.FileName,Path.Combine(folder,"current-chat.json"),true);
   status.Text=$"Privatno prenesen razgovor: {imported.Title}. Ne šalje se na GitHub. Otvori izolirani Studio za nastavak.";
  });
  p.Children.Add(new TextBlock{Text="Osobna uporaba • Prijava kroz službeni OpenAI prozor unutar Sandboxa • Modeli i brzina ovise o računu.\nMreža je uključena radi Codexa i razvojnih alata. Nemoj u Sandbox unositi druge privatne podatke ili mrežne vjerodajnice.",FontSize=12,Foreground=Brushes.LightSlateGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,18,4,0)});
  status.Text=Isolation.SandboxInstalled?"Windows Sandbox je dostupan. Pri prvom otvaranju priprema se zasebna kopija projekta.":"Windows Sandbox trenutačno nije uključen. Pripremljena je administratorska aktivacija; možda će biti potrebno ponovno pokretanje.";
 }
 void Add(string text,Action action){var b=new Button{Content=text,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>{try{action();}catch(Exception ex){status.Text=ex.Message;}};((StackPanel)Content).Children.Add(b);}
}
