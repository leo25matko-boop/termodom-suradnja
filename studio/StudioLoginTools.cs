using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace TermoDom.Studio;
public partial class StudioWindow
{
 string? pendingLoginId;Window? deviceLoginWindow;bool startingLogin;
 async void DeviceLogin_Click(object sender,RoutedEventArgs e)=>await BeginLoginAsync(true);
 async Task BeginLoginAsync(bool device=false)
 {
  if(preview||!engineReady||!Isolation.IsWorker){Status.Text="Prijava radi u stvarnom Windows Sandboxu. Pregled sučelja ne pokreće AI. Ako Windows traži restart, prvo spremi rad i ponovno pokreni računalo.";return;}
  if(authenticated||startingLogin)return;
  startingLogin=true;loginPending=true;UpdateActions();
  try
  {
   if(pendingLoginId!=null){var old=pendingLoginId;pendingLoginId=null;await rpc.CallAsync("account/login/cancel",new{loginId=old});}
   deviceLoginWindow?.Close();deviceLoginWindow=null;
   var result=await rpc.CallAsync("account/login/start",new{type=device?"chatgptDeviceCode":"chatgpt"});
   if(shuttingDown)return;
   pendingLoginId=Text(result,"loginId");
   var url=Text(result,device?"verificationUrl":"authUrl");
   if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Host is not("auth.openai.com" or "chatgpt.com"))throw new IOException("Neočekivana adresa službene prijave.");
   if(device)ShowDeviceLogin(url,Text(result,"userCode"));
   Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
   Status.Text=device?"Službena prijava kodom je otvorena. Ako račun ne dopušta ovaj način, koristi običnu prijavu kroz ChatGPT.":"Dovrši službenu OpenAI prijavu u pregledniku Sandboxa. Možeš odabrati Google. Ako povratak iz preglednika ne radi, odaberi Agent → Rezervna službena prijava kodom.";
  }
  catch(Exception ex){loginPending=false;Status.Text="Prijava nije pokrenuta: "+ex.Message+". Ponovi prijavu ili odaberi prijavu kodom u izborniku Agent.";}
  finally{startingLogin=false;UpdateActions();}
 }
 void ShowDeviceLogin(string url,string code)
 {
  var area=SystemParameters.WorkArea;
  var dialog=new Window{Title="Službena ChatGPT prijava kodom",Owner=this,Width=Math.Min(590,area.Width),Height=Math.Min(360,area.Height),WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)Application.Current.FindResource(typeof(Window))};
  var panel=new StackPanel{Margin=new Thickness(22)};dialog.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  panel.Children.Add(new TextBlock{Text="U službenom prozoru prijavi svoj račun i unesi ovaj jednokratni kod. Kod nemoj slati u razgovor.",TextWrapping=TextWrapping.Wrap});
  panel.Children.Add(new TextBox{Text=code,IsReadOnly=true,FontSize=25,Margin=new Thickness(0,18,0,14)});
  panel.Children.Add(new TextBlock{Text=url,TextWrapping=TextWrapping.Wrap});
  var close=new Button{Content="× Zatvori prozor"};close.Click+=(_,_)=>dialog.Close();panel.Children.Add(close);deviceLoginWindow=dialog;dialog.Show();
 }
}
