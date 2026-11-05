using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace TermoDom.Studio;

public partial class StudioWindow
{
 bool compactionPending;
 Func<Task>? refreshAgentTools;
 sealed record AgentSkill(string Name, string Path, bool Enabled)
 {
  public override string ToString() => (Enabled ? "✓  " : "–  ") + Name;
 }
 sealed record AgentMcp(string Name, string AuthStatus)
 {
  public override string ToString() => Name + " · " + AuthStatus;
 }

 bool AgentToolsAvailable(bool requireAccount = false)
 {
  if (preview || !engineReady || !Isolation.IsWorker) { Status.Text = "Ove opcije rade samo sa stvarnim Codex agentom u izoliranom Studiju."; return false; }
  if (requireAccount && !authenticated) { Status.Text = "Prvo dovrši službenu ChatGPT prijavu."; return false; }
  return true;
 }

 void RefreshAgentTools_Click(object sender, RoutedEventArgs e) => AgentTools_Click(sender, e);
 void AgentTools_Click(object sender, RoutedEventArgs e)
 {
  if (!AgentToolsAvailable()) return;
  var area = SystemParameters.WorkArea;
  var dialog = new Window { Owner = this, Title = "Codex · Alati i korištenje", Width = Math.Min(800, area.Width), Height = Math.Min(700, area.Height), WindowStartupLocation = WindowStartupLocation.CenterOwner, Style = (Style)Application.Current.FindResource(typeof(Window)) };
  var panel = new DockPanel { Margin = new Thickness(18) }; dialog.Content = panel;
  var note = new TextBlock { Text = "Stvarni podaci ovog izoliranog Studija. Povezane aplikacije i dodaci osobnog Codexa ne prenose se automatski.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 4, 12) };
  DockPanel.SetDock(note, Dock.Top); panel.Children.Add(note);
  var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
  var refresh = new Button { Content = "Osvježi" }; actions.Children.Add(refresh);
  var close = new Button { Content = "Zatvori  ×", IsCancel = true }; close.Click += (_, _) => dialog.Close(); actions.Children.Add(close);
  var tabs = new TabControl(); panel.Children.Add(tabs);
  TextBox Output() => new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 13, Margin = new Thickness(4) };
  var skillsText = Output(); var mcpText = Output(); var usageText = Output();
  var skills = new ListBox { Height = 110, Margin = new Thickness(4) }; var servers = new ComboBox { MinWidth = 210, HorizontalAlignment = HorizontalAlignment.Left };
  var skillPage = new DockPanel(); var skillControls = new StackPanel(); DockPanel.SetDock(skillControls, Dock.Top); skillPage.Children.Add(skillControls); skillControls.Children.Add(skills);
  var skillActions = new StackPanel { Orientation = Orientation.Horizontal }; skillControls.Children.Add(skillActions);
  var addSkill = new Button { Content = "Nova projektna vještina" }; var toggleSkill = new Button { Content = "Uključi / isključi odabranu" }; skillActions.Children.Add(addSkill); skillActions.Children.Add(toggleSkill); skillPage.Children.Add(skillsText);
  tabs.Items.Add(new TabItem { Header = "Vještine", Content = skillPage });
  var mcpPage = new DockPanel(); var mcpControls = new StackPanel(); DockPanel.SetDock(mcpControls, Dock.Top); mcpPage.Children.Add(mcpControls); mcpControls.Children.Add(servers);
  var mcpActions = new WrapPanel(); mcpControls.Children.Add(mcpActions);
  var addMcp = new Button { Content = "Dodaj MCP (HTTPS)" }; var mcpLogin = new Button { Content = "Prijavi odabrani MCP" }; var reloadMcp = new Button { Content = "Ponovno učitaj MCP" }; mcpActions.Children.Add(addMcp); mcpActions.Children.Add(mcpLogin); mcpActions.Children.Add(reloadMcp); mcpPage.Children.Add(mcpText);
  tabs.Items.Add(new TabItem { Header = "MCP", Content = mcpPage }); tabs.Items.Add(new TabItem { Header = "Limiti računa", Content = usageText });
  var loading = false; var changing = false;
  async Task RefreshAsync()
  {
   if (loading) return; loading = true; refresh.IsEnabled = false;
   async Task SafeRead(TextBox output, Func<Task> read) { try { await read(); } catch { output.Text = "Codex trenutačno nije vratio ove podatke. Pokušaj ponovno nakon prijave ili završetka zadatka."; } }
   try
   {
    await Task.WhenAll(
     SafeRead(skillsText, async () =>
     {
      var result = await rpc.CallAsync("skills/list", new { cwds = new[] { Isolation.Project }, forceReload = true });
      var entries = new List<AgentSkill>(); var text = new StringBuilder();
      foreach (var group in result.GetProperty("data").EnumerateArray())
      {
       foreach (var skill in group.GetProperty("skills").EnumerateArray())
       {
        var enabled = skill.TryGetProperty("enabled", out var flag) && flag.ValueKind == JsonValueKind.True;
        var name = Text(skill, "name"); var path = Text(skill, "path"); entries.Add(new(name, path, enabled));
        text.AppendLine(name + (enabled ? " · uključena" : " · isključena")).AppendLine(Text(skill, "description")).AppendLine();
       }
       if (group.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0) text.AppendLine("Neke vještine nije moguće učitati. Provjeri format projektnih SKILL.md datoteka.");
      }
      skills.ItemsSource = entries; if (entries.Count > 0) skills.SelectedIndex = 0;
      skillsText.Text = text.Length == 0 ? "U ovom Studiju nema učitanih vještina. Možeš dodati projektnu vještinu ili opisati zadatak agentu. Osobne vještine s drugog računala nisu kopirane." : text.ToString();
     }),
     SafeRead(mcpText, async () =>
     {
      var entries = new List<AgentMcp>(); var text = new StringBuilder(); string? cursor = null; var seen = new HashSet<string>();
      do
      {
       var result = await rpc.CallAsync("mcpServerStatus/list", new { cursor, limit = 100, detail = "toolsAndAuthOnly", threadId = string.IsNullOrEmpty(current?.Id) ? null : current.Id });
       foreach (var server in result.GetProperty("data").EnumerateArray())
       {
        var name = Text(server, "name"); var auth = Text(server, "authStatus"); entries.Add(new(name, auth)); text.AppendLine(name + " · prijava: " + auth);
        if (server.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Object) foreach (var tool in tools.EnumerateObject()) text.AppendLine("  • " + tool.Name);
        text.AppendLine();
       }
       cursor = result.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;
      } while (cursor != null && seen.Add(cursor));
      servers.ItemsSource = entries; if (entries.Count > 0) servers.SelectedIndex = 0;
      mcpText.Text = text.Length == 0 ? "Nema konfiguriranih MCP poslužitelja. Dodaj HTTPS adresu MCP usluge koju želiš koristiti. Službenu prijavu dovršavaš kod te usluge; pristupi iz host Codexa nisu preneseni." : text.ToString();
     }),
     SafeRead(usageText, async () =>
     {
      if (!authenticated) { usageText.Text = "Limiti su dostupni nakon službene ChatGPT prijave."; return; }
      var result = await rpc.CallAsync("account/rateLimits/read", new { }); usageText.Text = DescribeRateLimits(result);
     }));
   }
   finally { loading = false; refresh.IsEnabled = true; }
  }
  async Task ChangeAsync(Func<Task> change)
  {
   if (busy || loading || changing) { note.Text = "Pričekaj završetak zadatka ili osvježavanja."; return; }
   changing = true; skillActions.IsEnabled = mcpActions.IsEnabled = false;
   try { await change(); await RefreshAsync(); }
   catch (Exception ex) { note.Text = ex.Message; }
   finally { changing = false; skillActions.IsEnabled = mcpActions.IsEnabled = true; }
  }
  refresh.Click += async (_, _) => await RefreshAsync();
  addMcp.Click += async (_, _) => await ChangeAsync(async () =>
  {
   var name = AskAgentSetting(dialog, "Novi MCP", "Kratki naziv poslužitelja (slova, brojevi, _ ili -):"); if (name == null) return;
   if (!Regex.IsMatch(name, "^[a-zA-Z0-9_-]{1,64}$")) throw new IOException("Naziv mora sadržavati 1–64 slova, brojeva, _ ili -.");
   if (servers.Items.Cast<AgentMcp>().Any(server => server.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new IOException("MCP s tim nazivom već postoji. Odaberi drugi naziv kako bi postojeće postavke ostale sačuvane.");
   var address = AskAgentSetting(dialog, "Novi MCP", "HTTPS adresa MCP poslužitelja (bez lozinke ili API ključa):"); if (address == null) return;
   if (!Uri.TryCreate(address, UriKind.Absolute, out var url) || url.Scheme != "https" || url.UserInfo.Length != 0 || url.Fragment.Length != 0) throw new IOException("Unesi valjanu HTTPS adresu bez ugrađene prijave.");
   await rpc.CallAsync("config/value/write", new { keyPath = "mcp_servers." + name + ".url", value = url.AbsoluteUri, mergeStrategy = "replace", filePath = Path.Combine(Isolation.Data, "Codex", "config.toml") });
   await rpc.CallAsync("config/mcpServer/reload", null!); note.Text = "MCP je spremljen u ovoj izoliranoj instalaciji. Ako traži prijavu, odaberi ga i klikni Prijavi.";
  });
  reloadMcp.Click += async (_, _) => await ChangeAsync(async () => { await rpc.CallAsync("config/mcpServer/reload", null!); note.Text = "Zatraženo je ponovno učitavanje MCP konfiguracije."; });
  mcpLogin.Click += async (_, _) => await ChangeAsync(async () =>
  {
   if (servers.SelectedItem is not AgentMcp server) throw new IOException("Prvo odaberi konfigurirani MCP poslužitelj.");
   var result = await rpc.CallAsync("mcpServer/oauth/login", new { name = server.Name, threadId = string.IsNullOrEmpty(current?.Id) ? null : current.Id });
   if (!Uri.TryCreate(Text(result, "authorizationUrl"), UriKind.Absolute, out var url) || (url.Scheme != "https" && !(url.IsLoopback && url.Scheme == "http")) || url.UserInfo.Length != 0) throw new IOException("MCP nije vratio podržanu adresu službene prijave.");
   Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }); note.Text = "Dovrši prijavu u pregledniku Sandboxa. Ovdje se ne prikazuju i ne spremaju lozinke ili kodovi.";
  });
  addSkill.Click += async (_, _) => await ChangeAsync(async () =>
  {
   var name = AskAgentSetting(dialog, "Nova projektna vještina", "Naziv vještine (mala slova, brojevi i -):"); if (name == null) return;
   if (!Regex.IsMatch(name, "^[a-z0-9][a-z0-9-]{0,63}$")) throw new IOException("Naziv mora imati 1–64 mala slova, broja ili -.");
   var instructions = AskAgentSetting(dialog, "Nova projektna vještina", "Opiši kada i kako agent treba koristiti ovu vještinu:", true); if (string.IsNullOrWhiteSpace(instructions)) return;
   var path = workspace!.Resolve(".agents/skills/" + name + "/SKILL.md"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
   using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
   using (var writer = new StreamWriter(file, new UTF8Encoding(false))) await writer.WriteAsync("---\nname: " + name + "\ndescription: \"Project instructions for " + name + ".\"\n---\n\n" + instructions.Trim() + "\n");
   RefreshFiles(); note.Text = "Projektna SKILL.md datoteka spremljena je bez prepisivanja postojećih datoteka. Možeš je urediti u projektnom uređivaču.";
  });
  toggleSkill.Click += async (_, _) => await ChangeAsync(async () =>
  {
   if (skills.SelectedItem is not AgentSkill skill) throw new IOException("Odaberi učitanu vještinu.");
   var full = Path.GetFullPath(skill.Path);
   if (!new[] { Isolation.Project, Isolation.AppRoot, Path.Combine(Isolation.Data, "Codex") }.Any(root => full.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))) throw new IOException("Vještina nije dio dostupnog izoliranog projekta ili Studija.");
   await rpc.CallAsync("skills/config/write", new { path = full, enabled = !skill.Enabled }); note.Text = "Postavka odabrane vještine spremljena je u ovom Studiju.";
  });
  refreshAgentTools = RefreshAsync; dialog.Loaded += async (_, _) => await RefreshAsync(); dialog.Closed += (_, _) => refreshAgentTools = null; dialog.ShowDialog();
 }

 static string DescribeRateLimits(JsonElement result)
 {
  var text = new StringBuilder();
  void Bucket(string label, JsonElement value)
  {
   text.AppendLine(label); var plan = Text(value, "planType"); if (plan.Length > 0) text.AppendLine("Plan: " + plan);
   foreach (var key in new[] { "primary", "secondary" })
   {
    if (!value.TryGetProperty(key, out var window) || window.ValueKind != JsonValueKind.Object) continue;
    var name = key == "primary" ? "Glavni prozor" : "Dodatni prozor";
    if (window.TryGetProperty("usedPercent", out var percent) && percent.TryGetInt32(out var used)) text.AppendLine(name + ": iskorišteno " + used + "%");
    if (window.TryGetProperty("windowDurationMins", out var minutes) && minutes.TryGetInt64(out var mins)) text.AppendLine("Trajanje: " + mins + " minuta");
    if (window.TryGetProperty("resetsAt", out var reset) && reset.TryGetInt64(out var unix))
    { try { text.AppendLine("Obnova: " + DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("g")); } catch (ArgumentOutOfRangeException) { } }
   }
   text.AppendLine();
  }
  if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object && buckets.EnumerateObject().Any()) foreach (var bucket in buckets.EnumerateObject()) Bucket(bucket.Name, bucket.Value);
  else if (result.TryGetProperty("rateLimits", out var limits)) Bucket("Codex", limits);
  if (result.TryGetProperty("ordinaryUsageAllowed", out var allowed) && allowed.ValueKind is JsonValueKind.True or JsonValueKind.False) text.AppendLine(allowed.GetBoolean() ? "Uključeno korištenje trenutačno je dopušteno." : "Račun trenutačno ne dopušta uključeno korištenje.");
  return text.Length > 0 ? text.ToString() : "Račun nije vratio podatke o limitima.";
 }

 string? AskAgentSetting(Window owner, string title, string prompt, bool multiline = false)
 {
  var area = SystemParameters.WorkArea;
  var dialog = new Window { Owner = owner, Title = title, Width = Math.Min(620, area.Width), Height = Math.Min(multiline ? 440 : 270, area.Height), WindowStartupLocation = WindowStartupLocation.CenterOwner, Style = (Style)Application.Current.FindResource(typeof(Window)) };
  var panel = new DockPanel { Margin = new Thickness(20) }; dialog.Content = panel;
  var label = new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(label, Dock.Top); panel.Children.Add(label);
  var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, Dock.Bottom); panel.Children.Add(actions);
  var save = new Button { Content = "Spremi", IsDefault = !multiline }; save.Click += (_, _) => dialog.DialogResult = true; actions.Children.Add(save);
  var cancel = new Button { Content = "Odustani", IsCancel = true }; actions.Children.Add(cancel);
  var input = new TextBox { AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(input); dialog.Loaded += (_, _) => input.Focus();
  return dialog.ShowDialog() == true ? input.Text.Trim() : null;
 }

 async void Compact_Click(object sender, RoutedEventArgs e)
 {
  if (!AgentToolsAvailable(true) || busy) return;
  if (string.IsNullOrEmpty(current?.Id)) { Status.Text = "Prvo pošalji zadatak u ovom razgovoru. Sažimanje zatim koristi isključivo taj razgovor."; return; }
  busy = true; compactionPending = true; UpdateActions();
  try { await rpc.CallAsync("thread/compact/start", new { threadId = current.Id }); if (compactionPending) Status.Text = "Codex sažima kontekst ovog razgovora; prikazane poruke ostaju sačuvane."; }
  catch (Exception ex) { compactionPending = false; busy = false; UpdateActions(); Fail(ex); }
 }

 async void Review_Click(object sender, RoutedEventArgs e)
 {
  if (!AgentToolsAvailable(true) || busy) return;
  if (string.IsNullOrEmpty(current?.Id)) { Status.Text = "Prvo pošalji zadatak u ovom razgovoru. Pregled koda zatim se pokreće u istom razgovoru."; return; }
  busy = true; UpdateActions();
  try
  {
   var result = await rpc.CallAsync("review/start", new { threadId = current.Id, target = new { type = "uncommittedChanges" }, delivery = "inline" });
   if (Text(result, "reviewThreadId") != current.Id) throw new IOException("Codex je vratio pregled za neočekivani razgovor.");
   if (busy) currentTurn = Text(result.GetProperty("turn"), "id");
   AddMessage("STUDIO", "Pokrenut je Codex pregled spremljenih, nespremljenih i novih datoteka u radnom Git stablu. Rezultat dolazi u ovaj razgovor."); SaveThreads(); Status.Text = "Codex pregledava nepredane izmjene u ovom razgovoru.";
  }
  catch (Exception ex) { busy = false; UpdateActions(); Fail(ex); }
 }

 bool HandleAgentToolsNotification(string method, JsonElement p)
 {
  if (method == "mcpServer/oauthLogin/completed")
  {
   Status.Text = p.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True ? "MCP prijava je dovršena. Osvježi alate za aktualni status." : "MCP prijava nije dovršena. Ponovno odaberi prijavu tog poslužitelja.";
   if (refreshAgentTools != null) _ = refreshAgentTools(); return true;
  }
  if (Text(p, "threadId") == current?.Id && (method == "item/started" || method == "item/completed") && p.TryGetProperty("item", out var reviewItem))
  {
   var type = Text(reviewItem, "type");
   if (compactionPending && type == "contextCompaction" && method == "item/started") { currentTurn = Text(p, "turnId"); return true; }
   if (type == "exitedReviewMode" && method == "item/completed")
   {
    var text = Text(reviewItem, "review");
    if (text.Length > 0 && announced.Add("review:" + Text(reviewItem, "id"))) { AddMessage("CODEX · PREGLED", text); SaveThreads(); }
    return true;
   }
  }
  var completed = method == "thread/compacted" || method == "item/completed" && p.TryGetProperty("item", out var item) && Text(item, "type") == "contextCompaction";
  if (compactionPending && completed && Text(p, "threadId") == current?.Id)
  {
   compactionPending = false; busy = false; currentTurn = null; UpdateActions(); SaveThreads(); Status.Text = "Kontekst ovog razgovora je sažet. Izvorne prikazane poruke ostaju spremljene."; return true;
  }
  if (compactionPending && method == "turn/completed") compactionPending = false;
  return false;
 }
}
