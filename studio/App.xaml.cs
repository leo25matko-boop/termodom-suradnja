using System.Windows;

namespace TermoDom.Studio;
public partial class App : Application
{
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  DispatcherUnhandledException += (_, a) => { MessageBox.Show(a.Exception.Message, "TermoDom Studio"); a.Handled = true; };
  if(e.Args.Contains("--preview")) { new StudioWindow(true).Show(); return; }
  if(e.Args.Contains("--inside"))
  {
   if(!Isolation.IsWorker) { MessageBox.Show("Radni Codex može se pokrenuti samo u TermoDom Windows Sandboxu. Otvori aplikaciju bez parametara."); Shutdown(2); return; }
   new StudioWindow(false).Show(); return;
  }
  new LauncherWindow().Show();
 }
}
