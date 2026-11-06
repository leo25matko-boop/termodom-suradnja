using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TermoDom.Studio;

public partial class StudioWindow
{
 string? RequestInput(JsonElement question)
 {
  var dialog=InteractionDialog(Text(question,"header") is {Length:>0} header?header:"Codex traži odgovor",out var panel);
  panel.Children.Add(new TextBlock{Text=Text(question,"question"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
  var secret=question.TryGetProperty("isSecret",out var hidden)&&hidden.ValueKind==JsonValueKind.True;
  var options=question.TryGetProperty("options",out var list)&&list.ValueKind==JsonValueKind.Array?list.EnumerateArray().ToArray():[];
  var other=question.TryGetProperty("isOther",out var otherValue)&&otherValue.ValueKind==JsonValueKind.True;
  var choices=new List<(RadioButton Button,string Label)>();
  foreach(var option in options)
  {
   var label=Text(option,"label");
   var optionPanel=new StackPanel();optionPanel.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,FontWeight=FontWeights.SemiBold});
   optionPanel.Children.Add(new TextBlock{Text=Text(option,"description"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0),FontSize=12});
   var radio=new RadioButton{Content=optionPanel,Foreground=Foreground,Margin=new Thickness(0,4,0,9),GroupName="response",IsChecked=choices.Count==0};
   choices.Add((radio,label));panel.Children.Add(radio);
  }
  RadioButton? otherRadio=null;
  if(options.Length>0&&other)
  {otherRadio=new RadioButton{Content="Drugi odgovor",Foreground=Foreground,Margin=new Thickness(0,6,0,8),GroupName="response"};panel.Children.Add(otherRadio);}
  TextBox? textInput=null;PasswordBox? secretInput=null;
  if(options.Length==0||other)
  {
   if(secret){secretInput=new PasswordBox{Background=new SolidColorBrush(Color.FromRgb(36,36,36)),Foreground=Foreground,Margin=new Thickness(0,5,0,12),Padding=new Thickness(9)};panel.Children.Add(secretInput);}
   else{textInput=new TextBox{Margin=new Thickness(0,5,0,12),MinHeight=70,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(textInput);}
   if(otherRadio!=null)
   {
    if(textInput!=null)textInput.GotKeyboardFocus+=(_,_)=>otherRadio.IsChecked=true;
    if(secretInput!=null)secretInput.GotKeyboardFocus+=(_,_)=>otherRadio.IsChecked=true;
   }
  }
  var controls=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(controls);
  string? answer=null;
  var cancel=new Button{Content="Odustani",IsCancel=true};controls.Children.Add(cancel);
  var accept=new Button{Content="Pošalji odgovor",IsDefault=true};controls.Children.Add(accept);
  accept.Click+=(_,_)=>
  {
   answer=otherRadio?.IsChecked==true||options.Length==0?secretInput?.Password??textInput?.Text:choices.FirstOrDefault(x=>x.Button.IsChecked==true).Label;
   if(string.IsNullOrWhiteSpace(answer))return;
   dialog.DialogResult=true;
  };
  return dialog.ShowDialog()==true?answer:null;
 }

 string ChooseApproval(string method,JsonElement request)
 {
  var command=method=="item/commandExecution/requestApproval";
  var dialog=InteractionDialog(command?"Odobrenje naredbe":"Odobrenje izmjena",out var panel);
  var detail=command?Text(request,"command"):"Agent traži dopuštenje za izmjene datoteka.";
  if(command&&Text(request,"kind")=="writeStdin")detail="Slanje unosa u postojeći terminal:\n"+detail;
  if(Text(request,"cwd") is {Length:>0} cwd)detail+="\n\nRadna mapa: "+cwd;
  if(Text(request,"reason") is {Length:>0} reason)detail+="\n\n"+reason;
  panel.Children.Add(new TextBox{Text=detail,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=250,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,12)});
  panel.Children.Add(new TextBlock{Text="Odobrenje vrijedi samo unutar Windows Sandboxa. Host mape i prijave neće se dodavati.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
  var known=new HashSet<string>{"accept","acceptForSession","decline","cancel"};
  var allowed=command&&request.TryGetProperty("availableDecisions",out var supplied)&&supplied.ValueKind==JsonValueKind.Array?
   supplied.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!).Where(known.Contains).ToArray():new[]{"accept","acceptForSession","decline","cancel"};
  var decision="decline";
  foreach(var candidate in allowed.Distinct())
  {
   var selected=candidate;
   var button=new Button{Content=selected switch{"accept"=>"Dopusti ovu radnju","acceptForSession"=>"Dopusti za ovu sesiju","cancel"=>"Odbij i zaustavi zadatak",_=>"Odbij ovu radnju"},HorizontalContentAlignment=HorizontalAlignment.Left};
   button.Click+=(_,_)=>{decision=selected;dialog.DialogResult=true;};panel.Children.Add(button);
  }
  if(allowed.Length==0)panel.Children.Add(new TextBlock{Text="Ova verzija Studija ne podržava ponuđene trajne promjene pravila. Zatvaranje odbija zahtjev.",TextWrapping=TextWrapping.Wrap});
  dialog.ShowDialog();return decision;
 }

 Window InteractionDialog(string title,out StackPanel panel)
 {
  var area=SystemParameters.WorkArea;
  var dialog=new Window{Title=title,Owner=this,Width=Math.Min(620,area.Width),Height=Math.Min(580,area.Height),WindowStartupLocation=WindowStartupLocation.CenterOwner,Style=(Style)Application.Current.FindResource(typeof(Window))};
  panel=new StackPanel{Margin=new Thickness(20)};dialog.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  return dialog;
 }
}
