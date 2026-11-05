using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TermoDom.Studio;

public enum SandboxState { Available, Disabled, RestartRequired, Unavailable }
public sealed record SandboxReadiness(SandboxState State, string Message)
{
 public bool CanLaunch => State == SandboxState.Available;
}
public sealed record SandboxEnableResult(bool Success, bool RestartNeeded, string Message,
 DateTimeOffset CompletedUtc = default, string? FeatureState = null);
public sealed record SandboxStartResult(bool Started, string Message);

/// <summary>Starts only Microsoft's outer VM; never falls back to a host agent.</summary>
public static class SandboxLaunch
{
 static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
 public static string ExecutablePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsSandbox.exe");
 public static string PowerShellPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
 static string SetupDirectory => Path.Combine(Isolation.HostRoot, "Setup");
 public static string SetupResultPath => Path.Combine(SetupDirectory, "last-result.json");

 public static SandboxReadiness Classify(bool executableAvailable, int? installState, DateTimeOffset? bootUtc, SandboxEnableResult? lastSetup = null)
 {
  if (lastSetup is { Success: true, RestartNeeded: true } && (!bootUtc.HasValue || lastSetup.CompletedUtc >= bootUtc.Value))
   return new(SandboxState.RestartRequired, "Windows Sandbox je uključen, ali Windows treba ponovno pokretanje. Spremi rad i ponovno pokreni računalo; Studio će zatim nastaviti s postojećim podacima.");
  if (installState is 2 or 3)
   return new(SandboxState.Disabled, "Windows Sandbox je isključen. Gumb Otvori Studio pripremit će Microsoftovu značajku uz Windowsovu administratorsku potvrdu. Možda će biti potrebno ponovno pokretanje računala.");
  if (executableAvailable && installState is null or 1)
   return new(SandboxState.Available, "Windows Sandbox je dostupan. Studio automatski koristi spremljeni projekt i ovaj razgovor.");
  if (installState == 1)
   return new(SandboxState.Unavailable, "Windows prijavljuje uključen Sandbox, ali nedostaje WindowsSandbox.exe. Ponovno pokreni računalo; ako i dalje nedostaje, pokreni pripremu Sandboxa radi provjere Windowsove instalacije.");
  return new(SandboxState.Unavailable, "Windows Sandbox trenutačno nije dostupan. Pokreni pripremu Sandboxa za provjeru podrške i uključivanje značajke. Potrebni su podržano izdanje Windowsa i uključena virtualizacija.");
 }

 public static async Task<SandboxReadiness> GetReadinessAsync()
 {
  int? state = null; DateTimeOffset? boot = null;
  // CIM can query optional-feature state without an elevated DISM session.
  const string command = "$ErrorActionPreference='Stop';$f=Get-CimInstance Win32_OptionalFeature -Filter \"Name='Containers-DisposableClientVM'\";$o=Get-CimInstance Win32_OperatingSystem;@{InstallState=$(if($f){[int]$f.InstallState}else{0});BootUtc=$o.LastBootUpTime.ToUniversalTime().ToString('o')}|ConvertTo-Json -Compress";
  try
  {
   var start = new ProcessStartInfo(PowerShellPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
   foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(command)) }) start.ArgumentList.Add(arg);
   using var process = Process.Start(start) ?? throw new IOException("Nije moguće provjeriti Windows Sandbox.");
   var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
   using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
   try { await process.WaitForExitAsync(timeout.Token); }
   catch (OperationCanceledException) { try { process.Kill(true); } catch { } throw; }
   var json = await output; await error;
   if (process.ExitCode == 0)
   {
    using var doc = JsonDocument.Parse(json);
    state = doc.RootElement.GetProperty("InstallState").GetInt32();
    if (DateTimeOffset.TryParse(doc.RootElement.GetProperty("BootUtc").GetString(), out var parsed)) boot = parsed;
   }
  }
  catch (Exception ex) when (ex is IOException or Win32Exception or OperationCanceledException or JsonException or InvalidOperationException) { }
  SandboxEnableResult? last = null;
  try { if (File.Exists(SetupResultPath)) last = JsonSerializer.Deserialize<SandboxEnableResult>(await File.ReadAllTextAsync(SetupResultPath), JsonOptions); }
  catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
  return Classify(File.Exists(ExecutablePath), state, boot, last);
 }

 public static ProcessStartInfo CreateEnableStartInfo(string installDirectory, string resultPath)
 {
  if (!Path.IsPathFullyQualified(installDirectory) || !Path.IsPathFullyQualified(resultPath)) throw new ArgumentException("Potrebne su apsolutne putanje pripreme.");
  var start = new ProcessStartInfo(PowerShellPath) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = installDirectory };
  foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(installDirectory, "Enable-Sandbox.ps1"), "-ResultPath", resultPath }) start.ArgumentList.Add(arg);
  return start;
 }

 public static async Task<SandboxEnableResult> EnableAsync()
 {
  Isolation.SecureDirectory(Isolation.HostRoot);
  Directory.CreateDirectory(SetupDirectory);
  var resultPath = Path.Combine(SetupDirectory, "enable-" + Guid.NewGuid().ToString("N") + ".json");
  try
  {
   var start = CreateEnableStartInfo(AppContext.BaseDirectory, resultPath);
   if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Enable-Sandbox.ps1"))) throw new FileNotFoundException("Instalaciji nedostaje priprema Windows Sandboxa. Ponovno instaliraj Studio.");
   using var process = Process.Start(start) ?? throw new IOException("Windows nije pokrenuo pripremu Sandboxa.");
   // Do not terminate servicing or restart Windows when the user closes the launcher.
   await process.WaitForExitAsync();
   if (!File.Exists(resultPath)) return new(false, false, $"Windowsova priprema nije vratila rezultat (izlaz {process.ExitCode}). Ponovno pokreni pripremu i potvrdi Windowsov administratorski prozor.");
   var result = JsonSerializer.Deserialize<SandboxEnableResult>(await File.ReadAllTextAsync(resultPath), JsonOptions)
    ?? throw new IOException("Windowsova priprema vratila je prazan rezultat.");
   if (result.Success && process.ExitCode is not (0 or 3010)) result = result with { Success = false, Message = $"Priprema je završila neočekivanim izlazom {process.ExitCode}. " + result.Message };
   if(result.Success)result=result with { Message=result.RestartNeeded?"Windows Sandbox je uključen. Spremi rad i ponovno pokreni računalo, zatim otvori prečac TermoDom Studio. Podaci su sačuvani.":"Windows Sandbox je spreman. Otvaram Studio sa spremljenim projektom i razgovorom." };
   await File.WriteAllTextAsync(SetupResultPath, JsonSerializer.Serialize(result));
   return result;
  }
  catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new(false, false, "Windowsova administratorska potvrda je otkazana. Studio i spremljeni podaci ostaju sačuvani."); }
 }

 public static ProcessStartInfo CreateSandboxStartInfo(string configurationPath)
 {
  if (!Path.IsPathFullyQualified(configurationPath) || !configurationPath.EndsWith(".wsb", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Potrebna je apsolutna putanja .wsb konfiguracije.");
  var start = new ProcessStartInfo(ExecutablePath) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(configurationPath)! };
  start.ArgumentList.Add(configurationPath);
  return start;
 }

 public static async Task<SandboxStartResult> StartAsync(string configurationPath)
 {
  var readiness = await GetReadinessAsync();
  if (!readiness.CanLaunch) return new(false, readiness.Message);
  if (!File.Exists(configurationPath)) throw new FileNotFoundException("Nedostaje pripremljena konfiguracija Sandboxa.", configurationPath);
  using var process = Process.Start(CreateSandboxStartInfo(configurationPath)) ?? throw new IOException("Windows nije pokrenuo Sandbox.");
  await Task.Delay(2500);
  if (process.HasExited && process.ExitCode != 0)
   return new(false, $"Windows Sandbox je zaustavio pokretanje (izlaz 0x{unchecked((uint)process.ExitCode):X8}). Provjeri Windowsov prikaz greške, virtualizaciju i treba li ponovno pokretanje računala.");
  return new(true, "Pokretanje je predano Windows Sandboxu. Pri prvom otvaranju priprema Windowsa može potrajati. Projekt, razgovor i zasebna prijava ostaju spremljeni nakon zatvaranja.");
 }
}
