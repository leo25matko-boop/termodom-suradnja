using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TermoDom.Studio;
public sealed class LauncherWindow : Window
{
 readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,15,4,16),Foreground=new SolidColorBrush(Color.FromRgb(245,190,110))};
 readonly StackPanel actions=new();bool working;
 public LauncherWindow(bool autoOpen=false)
 {
  Style=(Style)Application.Current.FindResource(typeof(Window));
  Title="TermoDom Studio · Pokretanje";Width=Math.Min(710,SystemParameters.WorkArea.Width);Height=Math.Min(690,SystemParameters.WorkArea.Height);WindowStartupLocation=WindowStartupLocation.CenterScreen;
  var dock=new DockPanel{Margin=new Thickness(24)};Content=dock;
  var header=new DockPanel();DockPanel.SetDock(header,Dock.Top);dock.Children.Add(header);
  var close=new Button{Content="×  Zatvori",HorizontalAlignment=HorizontalAlignment.Right,ToolTip="Zatvori aplikaciju"};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
  header.Children.Add(new TextBlock{Text="TERMODOM  /  STUDIO",FontSize=13,Foreground=Brushes.Turquoise,VerticalAlignment=VerticalAlignment.Center});
  var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};dock.Children.Add(scroll);var p=new StackPanel{Margin=new Thickness(8)};scroll.Content=p;
  p.Children.Add(new TextBlock{Text="Nastavi svoj projekt",FontSize=29,Margin=new Thickness(0,18,0,16)});
  p.Children.Add(new TextBlock{Text="Projekt i već pripremljeni razgovor otvaraju se automatski. Nakon prve službene prijave Studio pamti vlastitu sesiju za sljedeće pokretanje.",TextWrapping=TextWrapping.Wrap,FontSize=16,LineHeight=25});
  p.Children.Add(status);p.Children.Add(actions);
  Add("Pokreni Studio",()=>OpenStudioAsync(true));
  Add("Uključi Windows Sandbox",EnableAsync);
  Add("Ponovno provjeri spremnost",RefreshAsync);
  Add("Pregled sučelja",()=>{new StudioWindow(true){Owner=this}.Show();return Task.CompletedTask;});
  p.Children.Add(new TextBlock{Text="Windows traži UAC potvrdu za prvo uključivanje Sandboxa; možda će trebati restart. Prva OpenAI/Google prijava potvrđuje se samo u službenom prozoru.\n\nPrivatni razgovor i prijava ostaju izvan javnog instalatera. Na ovom računalu koristi se unaprijed pripremljeni privatni razgovor.",FontSize=12,Foreground=Brushes.LightSlateGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,18,4,0)});
  Loaded+=async(_,_)=>{if(autoOpen)await RunAsync(()=>OpenStudioAsync(true));else await RunAsync(RefreshAsync);};
 }
 async Task RefreshAsync(){var ready=await SandboxLaunch.GetReadinessAsync();status.Text=ready.Message+ConversationStatus();}
 static string ConversationStatus()=>File.Exists(Path.Combine(Isolation.HostRoot,"Data","studio-chats.json"))?"\nSpremljeni razgovor bit će nastavljen.":File.Exists(Path.Combine(Isolation.HostRoot,"Data","Imports","current-chat.json"))?"\nOvaj razgovor je pripremljen. Ručni uvoz nije potreban.":"\nPrivatni razgovor nije pripremljen na ovom računalu. Javni instalater ne sadrži tvoje poruke.";
 async Task EnableAsync(){status.Text="Čekam službenu Windows administratorsku potvrdu i uključivanje Sandboxa…";var result=await SandboxLaunch.EnableAsync();status.Text=result.Message+ConversationStatus();}
 async Task OpenStudioAsync(bool enableIfNeeded)
 {
  status.Text="Provjeravam Windows Sandbox i spremljeni projekt…";
  var ready=await SandboxLaunch.GetReadinessAsync();
  if(!ready.CanLaunch)
  {
   status.Text=ready.Message+ConversationStatus();
   if(enableIfNeeded&&ready.State==SandboxState.Disabled){await EnableAsync();ready=await SandboxLaunch.GetReadinessAsync();}
   if(!ready.CanLaunch)return;
  }
  var data=Path.Combine(Isolation.HostRoot,"Data");
  if(!File.Exists(Path.Combine(data,"studio-chats.json")))ConversationImport.Load(Path.Combine(data,"Imports","current-chat.json"));
  var config=Isolation.Prepare();var result=await SandboxLaunch.StartAsync(config);status.Text=result.Message;
 }
 async Task RunAsync(Func<Task> action){if(working)return;working=true;actions.IsEnabled=false;try{await action();}catch(Exception ex){status.Text="Pokretanje nije dovršeno: "+ex.Message;}finally{working=false;actions.IsEnabled=true;}}
 void Add(string text,Func<Task> action){var b=new Button{Content=text,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=async(_,_)=>await RunAsync(action);actions.Children.Add(b);}
}
