using System.Diagnostics;
using System.IO;

namespace TermoDom.Studio;

public static class BrowserLauncher
{
 public const string GuestFirefoxProfile=@"C:\TermoDom\BrowserProfile";
 public static Uri ValidateAddress(string address)
 {
  if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
      (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)) || uri.UserInfo.Length > 0)
   throw new IOException("Podržane su HTTPS adrese i lokalni HTTP povratak službene prijave.");
  return uri;
 }

 public static string? FindGuestBrowser()
 {
  var roots=new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) }.Where(path => path.Length > 0).ToArray();
  foreach (var root in roots)
  {
   var firefox = Path.Combine(root, "Mozilla Firefox", "firefox.exe");
   if (File.Exists(firefox)) return firefox;
  }
  foreach (var root in roots)
  {
   var executable = Path.Combine(root, "Microsoft", "Edge", "Application", "msedge.exe");
   if (File.Exists(executable)) return executable;
  }
  return null;
 }

 public static ProcessStartInfo CreateGuestStartInfo(string executable, string address)
 {
  if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("Potrebna je apsolutna putanja preglednika.");
  var uri = ValidateAddress(address);
  var start = new ProcessStartInfo(executable) { UseShellExecute = false };
  if(Path.GetFileName(executable).Equals("firefox.exe",StringComparison.OrdinalIgnoreCase)){start.ArgumentList.Add("-profile");start.ArgumentList.Add(GuestFirefoxProfile);start.ArgumentList.Add("-new-window");}
  else{start.ArgumentList.Add("--no-first-run");start.ArgumentList.Add("--no-default-browser-check");}
  start.ArgumentList.Add(uri.AbsoluteUri);
  return start;
 }

 public static void Open(string address)
 {
  var uri = ValidateAddress(address);
  var inside = Isolation.IsWorker;
  var start = inside
   ? CreateGuestStartInfo(FindGuestBrowser() ?? throw new IOException("Preglednik u Sandboxu još nije pripremljen. Zatvori Sandbox i ponovno pokreni Studio radi automatske pripreme službenog preglednika."), uri.AbsoluteUri)
   : new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
  if(inside&&Path.GetFileName(start.FileName).Equals("firefox.exe",StringComparison.OrdinalIgnoreCase))
  {
   Directory.CreateDirectory(GuestFirefoxProfile);
   File.WriteAllText(Path.Combine(GuestFirefoxProfile,"user.js"),"user_pref(\"browser.aboutwelcome.enabled\", false);\nuser_pref(\"browser.shell.checkDefaultBrowser\", false);\nuser_pref(\"startup.homepage_welcome_url\", \"\");\nuser_pref(\"startup.homepage_welcome_url.additional\", \"\");\nuser_pref(\"browser.startup.homepage_override.mstone\", \"ignore\");\n");
  }
  using var process = Process.Start(start);
  if (inside && process == null) throw new IOException("Preglednik nije moguće pokrenuti.");
 }
}
