using System.IO.Compression;
using System.Xml.Linq;
using System.Text.Json;
using TermoDom.Studio;

var count=0;
void Check(bool value,string title){if(!value)throw new Exception(title);count++;Console.WriteLine("PASS "+title);}
void Refuses(Action action,string title){try{action();}catch(IOException){Check(true,title);return;}throw new Exception("Did not refuse: "+title);}
var root=Path.Combine(Path.GetTempPath(),"TermoDom-Studio-Tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
 var project=Path.Combine(root,"project");Directory.CreateDirectory(project);var workspace=new Workspace(project);
 Refuses(()=>workspace.Resolve("../owner/auth.json"),"parent traversal rejected");
 Refuses(()=>workspace.Resolve(@"C:\Users\owner\private.txt"),"absolute path rejected");
 Refuses(()=>workspace.Resolve(".codex/auth.json"),"credential home absent from editor");
 Check(workspace.Resolve("docs/context.md").StartsWith(project+Path.DirectorySeparatorChar),"project file resolves");
 Directory.CreateDirectory(Path.Combine(project,"bin"));File.WriteAllText(Path.Combine(project,"bin","ignored.txt"),"ignored");File.WriteAllText(Path.Combine(project,"README.md"),"source");
 Check(workspace.Files().SequenceEqual(new[]{"README.md"}),"build files excluded from editor");
 var config=XDocument.Parse(Isolation.CreateConfiguration(@"C:\Studio & Tools",@"C:\StudioProject",@"C:\StudioData"));
 var maps=config.Root!.Element("MappedFolders")!.Elements().ToArray();
 Check(maps.Length==3,"exactly three explicit mapped folders");
 Check(maps[0].Element("ReadOnly")!.Value=="true","application and tools are read only");
 Check(maps.Skip(1).All(x=>x.Element("ReadOnly")!.Value=="false"),"only project and Studio data writable");
 Check(maps[0].Element("HostFolder")!.Value==@"C:\Studio & Tools","XML paths escaped correctly");
 foreach(var feature in new[]{"ClipboardRedirection","AudioInput","VideoInput","PrinterRedirection"})Check(config.Root.Element(feature)!.Value=="Disable",feature+" disabled");
 Check(config.Root.Element("ProtectedClient")!.Value=="Enable","protected client enabled");
 Check(!Isolation.IsWorker,"ordinary host session cannot launch agent");
 var boot=DateTimeOffset.Parse("2026-11-06T07:00:00Z");
 Check(SandboxLaunch.Classify(true,1,boot).CanLaunch,"enabled Sandbox executable is ready to launch");
 Check(!SandboxLaunch.Classify(false,2,boot).CanLaunch,"disabled Windows feature cannot claim launch readiness");
 Check(SandboxLaunch.Classify(false,2,boot).State.ToString()=="Disabled","disabled Windows feature offers setup instead of launch");
 Check(!SandboxLaunch.Classify(false,0,boot).CanLaunch,"unavailable Windows feature cannot launch agent on host");
 Check(!SandboxLaunch.Classify(false,null,boot).CanLaunch,"unavailable diagnostics do not enable host fallback");
 var pendingSetup=new SandboxEnableResult(true,true,"Synthetic setup result",boot.AddMinutes(5));
 Check(SandboxLaunch.Classify(true,1,boot,pendingSetup).State.ToString()=="RestartRequired","setup requiring reboot overrides an executable that already exists");
 Check(!SandboxLaunch.Classify(true,1,boot,pendingSetup).CanLaunch,"pending Windows restart blocks startup");
 Check(SandboxLaunch.Classify(true,1,boot.AddHours(1),pendingSetup).CanLaunch,"completed reboot clears stale setup restart requirement");
 Check(!SandboxLaunch.Classify(true,1,null,pendingSetup).CanLaunch,"unknown boot timestamp cannot silently clear required restart");
 var enableDirectory=Path.Combine(root,"Studio & Tools");Directory.CreateDirectory(enableDirectory);
 File.WriteAllText(Path.Combine(enableDirectory,"Enable-Sandbox.ps1"),"# synthetic command construction fixture; never executed");
 var setupResult=Path.Combine(root,"setup result.json");
 var enableStart=SandboxLaunch.CreateEnableStartInfo(enableDirectory,setupResult);
 Check(Path.IsPathFullyQualified(enableStart.FileName)&&enableStart.UseShellExecute&&enableStart.Verb=="runas","Windows feature setup uses an absolute executable and official UAC elevation");
 Check(enableStart.ArgumentList.Contains("-ExecutionPolicy")&&enableStart.ArgumentList.Contains("Bypass")&&enableStart.ArgumentList.Contains("-NoProfile"),"trusted setup avoids profile and execution policy startup failures");
 Check(enableStart.ArgumentList.Contains(Path.Combine(enableDirectory,"Enable-Sandbox.ps1"))&&enableStart.ArgumentList.Contains(setupResult),"setup paths with spaces and ampersands remain separate arguments");
 var configPath=Path.Combine(root,"Studio & Work.wsb");
 File.WriteAllText(configPath,config.ToString());
 var sandboxStart=SandboxLaunch.CreateSandboxStartInfo(configPath);
 Check(Path.IsPathFullyQualified(sandboxStart.FileName)&&Path.GetFileName(sandboxStart.FileName).Equals("WindowsSandbox.exe",StringComparison.OrdinalIgnoreCase),"launch addresses Windows Sandbox explicitly instead of a file association");
 Check(sandboxStart.ArgumentList.SequenceEqual(new[]{configPath}),"Sandbox configuration path stays a single argument");
 Check(BrowserLauncher.ValidateAddress("https://auth.openai.com/authorize?state=synthetic&scope=openid").Scheme=="https","official browser login supports normal HTTPS addresses");
 Check(BrowserLauncher.ValidateAddress("http://localhost:1455/auth/callback").IsLoopback,"local OAuth callback remains supported");
 Refuses(()=>BrowserLauncher.ValidateAddress("file:///C:/private.txt"),"browser refuses local file navigation from tool URLs");
 Refuses(()=>BrowserLauncher.ValidateAddress("javascript:alert(1)"),"browser refuses executable URL schemes");
 Refuses(()=>BrowserLauncher.ValidateAddress("http://example.invalid/signin"),"browser refuses remote cleartext login URLs");
 Refuses(()=>BrowserLauncher.ValidateAddress("https://user:password@example.invalid"),"browser refuses credentials embedded in URLs");
 var browserStart=BrowserLauncher.CreateGuestStartInfo(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe","https://auth.openai.com/authorize?state=synthetic&scope=openid");
 Check(!browserStart.UseShellExecute&&browserStart.ArgumentList.Count==3&&browserStart.ArgumentList.Last().Contains("state=synthetic&scope=openid"),"guest login explicitly launches a browser with one intact URL argument");
 Check(!browserStart.ArgumentList.Contains("--no-sandbox")&&!browserStart.ArgumentList.Contains("--disable-web-security"),"browser startup keeps Chromium security boundaries enabled");
 var firefoxStart=BrowserLauncher.CreateGuestStartInfo(@"C:\Program Files\Mozilla Firefox\firefox.exe","https://auth.openai.com/authorize?state=synthetic&scope=openid");
 Check(firefoxStart.ArgumentList.SequenceEqual(new[]{"-profile",BrowserLauncher.GuestFirefoxProfile,"-new-window","https://auth.openai.com/authorize?state=synthetic&scope=openid"}),"Firefox opens official login with a separate guest profile and no Windows HTTPS association");
 var zip=Path.Combine(root,"bad.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry("../escape.txt").Open());w.Write("x");}
 var dest=Path.Combine(root,"extract");Directory.CreateDirectory(dest);Refuses(()=>Isolation.ExtractSeed(zip,dest),"archive traversal rejected");
 Check(!File.Exists(Path.Combine(root,"escape.txt")),"archive did not write outside destination");
 var zip2=Path.Combine(root,"auth.zip");using(var z=ZipFile.Open(zip2,ZipArchiveMode.Create)){using var w=new StreamWriter(z.CreateEntry(".codex/auth.json").Open());w.Write("{}");}
 Refuses(()=>Isolation.ExtractSeed(zip2,dest),"Codex credentials cannot enter seed archive");
 var run=await Workspace.RunAsync("cmd.exe",new[]{"/d","/c","echo STUDIO_TEST"},project,CancellationToken.None);
 Check(run.Code==0&&run.Output.Contains("STUDIO_TEST"),"terminal captures real process output and exit status");
 var importFile=Path.Combine(root,"conversation.json");
 File.WriteAllText(importFile,"""{"schemaVersion":1,"title":"Project","sourceThreadId":"synthetic","messages":[{"role":"user","content":[{"type":"input_text","text":"Continue existing project"}]},{"role":"assistant","content":[{"type":"output_text","text":"Project context"}]}]}""");
 var imported=ConversationImport.Load(importFile);Check(imported.Messages.Count==2&&imported.ToItems().Length==2,"visible project messages retain user and assistant roles");
 File.WriteAllText(importFile,"""{"schemaVersion":1,"title":"Project","sourceThreadId":"synthetic","messages":[{"role":"system","content":[{"type":"input_text","text":"not a visible message"}]}]}""");
 Refuses(()=>ConversationImport.Load(importFile),"hidden system/developer instructions cannot enter private chat import");
 File.WriteAllText(importFile,"""{"schemaVersion":1,"title":"Project","sourceThreadId":"synthetic","messages":[{"role":"user","content":[{"type":"input_image","image_url":"https://example.invalid/private-image.jpg"}]}]}""");
 Refuses(()=>ConversationImport.Load(importFile),"automatic conversation load cannot fetch remote images");
 File.WriteAllText(importFile,"""{"schemaVersion":1,"title":"Project","sourceThreadId":"synthetic","messages":[{"role":"assistant","content":[{"type":"input_image","image_url":"data:image/png;base64,iVBORw0KGgo="}]}]}""");
 Refuses(()=>ConversationImport.Load(importFile),"only user-attached images enter transferred conversation");
 var preferenceFile=Path.Combine(root,"preferences","studio-preferences.json");
 Check(StudioPreferences.Load(preferenceFile)==new StudioPreferences(),"new Studio uses safe empty UI preferences");
 var preferences=new StudioPreferences{ModelId="synthetic-model",Effort="high",SpeedId="fast",FullAccess=true};
 preferences.Save(preferenceFile);
 Check(StudioPreferences.Load(preferenceFile)==preferences,"model effort speed and explicitly chosen Sandbox access persist across restart");
 var changedPreferences=preferences with{Effort="medium",SpeedId="default",FullAccess=false};changedPreferences.Save(preferenceFile);
 Check(StudioPreferences.Load(preferenceFile)==changedPreferences,"new selections atomically replace earlier UI preferences");
 Check(Directory.GetFiles(Path.GetDirectoryName(preferenceFile)!).Length==1,"preference save leaves no temporary state files");
 using(var preferenceJson=JsonDocument.Parse(File.ReadAllText(preferenceFile)))
  Check(preferenceJson.RootElement.EnumerateObject().Select(p=>p.Name).OrderBy(s=>s).SequenceEqual(new[]{"Effort","FullAccess","ModelId","SchemaVersion","SpeedId"}),"UI preference file contains no account credentials or conversation data");
 File.WriteAllText(preferenceFile,"""{"SchemaVersion":2,"ModelId":"synthetic-model"}""");
 Refuses(()=>StudioPreferences.Load(preferenceFile),"unsupported preference schema cannot silently enable old access settings");
 File.WriteAllText(preferenceFile,"""{"ModelId":null}""");
 Refuses(()=>StudioPreferences.Load(preferenceFile),"invalid preference content is rejected before model selection");
 if(args.FirstOrDefault(a=>a.StartsWith("--codex=")) is {} arg)
 {
  using var rpc=new RpcClient();var home=Path.Combine(root,"empty-home");await rpc.StartAsync(arg[8..],project,home,probe:true);
  var account=await rpc.CallAsync("account/read",new{refreshToken=false});Check(account.GetProperty("account").ValueKind==JsonValueKind.Null,"new Codex home contains no existing account");
  string? testLoginId=null;var loginProbeFailed=false;
  try
  {
   // Never open the browser or print the authorization URL, nonce, or login ID.
   // No device-code request is made and no personal account is authenticated.
   var login=await rpc.CallAsync("account/login/start",new{type="chatgpt"});
   testLoginId=login.GetProperty("loginId").GetString();
   Check(!string.IsNullOrWhiteSpace(testLoginId),"official browser login returns a cancelable request identifier");
   var address=login.GetProperty("authUrl").GetString();
   Check(Uri.TryCreate(address,UriKind.Absolute,out var loginUri)&&loginUri.Scheme=="https"&&loginUri.IsDefaultPort&&loginUri.UserInfo.Length==0&&loginUri.Host is "auth.openai.com" or "chatgpt.com","official login returns only a supported HTTPS authorization endpoint");
   var canceled=await rpc.CallAsync("account/login/cancel",new{loginId=testLoginId});
   Check(canceled.GetProperty("status").GetString()=="canceled","official browser login can be canceled through the actual protocol");testLoginId=null;
   var afterCancel=await rpc.CallAsync("account/read",new{refreshToken=false});
   Check(afterCancel.GetProperty("account").ValueKind==JsonValueKind.Null,"canceling the synthetic login leaves the isolated home unauthenticated");
  }
  catch{loginProbeFailed=true;} // Do not propagate potentially sensitive auth error details into test logs.
  finally{if(testLoginId!=null)try{await rpc.CallAsync("account/login/cancel",new{loginId=testLoginId});}catch{}}
  Check(!loginProbeFailed,"browser login start/cancel probe finishes without authenticating a personal account");
  var models=await rpc.CallAsync("model/list",new{limit=100,includeHidden=false});Check(models.GetProperty("data").GetArrayLength()>0,"official model catalog protocol works without copying owner auth");
  var skills=await rpc.CallAsync("skills/list",new{cwds=new[]{project},forceReload=true});Check(skills.GetProperty("data").ValueKind==JsonValueKind.Array,"skills catalog reads only the synthetic project in a separate Codex home");
  var mcp=await rpc.CallAsync("mcpServerStatus/list",new{limit=100,detail="toolsAndAuthOnly",threadId=(string?)null,cursor=(string?)null});Check(mcp.GetProperty("data").ValueKind==JsonValueKind.Array,"MCP status catalog protocol works without importing host integrations");
  var thread=await rpc.CallAsync("thread/start",new{cwd=project,sandbox="danger-full-access",approvalPolicy="on-request"});var id=thread.GetProperty("thread").GetProperty("id").GetString();
  await rpc.CallAsync("thread/inject_items",new{threadId=id,items=imported.ToItems()});Check(true,"conversation import uses actual Codex protocol");
  var resumed=await rpc.CallAsync("thread/resume",new{threadId=id,cwd=project,excludeTurns=true,sandbox="danger-full-access",approvalPolicy="on-request"});Check(resumed.GetProperty("thread").GetProperty("id").GetString()==id,"imported conversation resumes with persistent engine history");
  var result=await rpc.CallAsync("command/exec",new{command=new[]{"cmd.exe","/d","/c","echo RPC_OK"},cwd=project,sandboxPolicy=new{type="externalSandbox",networkAccess="enabled"},timeoutMs=10000});
  Check(result.GetProperty("exitCode").GetInt32()==0&&result.GetProperty("stdout").GetString()!.Contains("RPC_OK"),"stdio JSON-RPC execution round trip");
  rpc.Dispose();using var restored=new RpcClient();await restored.StartAsync(arg[8..],project,home,probe:true);
  var afterRestart=await restored.CallAsync("thread/resume",new{threadId=id,cwd=project,excludeTurns=true,sandbox="danger-full-access",approvalPolicy="on-request"});Check(afterRestart.GetProperty("thread").GetProperty("id").GetString()==id,"imported conversation persists across agent process restart");
 }
 Console.WriteLine($"STUDIO_TESTS_OK: {count}. OAuth, model generation and Windows Sandbox isolation require actual interactive setup and are not claimed by these tests.");
}
catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
finally
{
 for(var retry=0;retry<10;retry++)
 {
  try{Directory.Delete(root,true);break;}
  catch(IOException){if(retry==9)Console.WriteLine("Temporary test directory still releasing process handles; no credentials were used.");else await Task.Delay(200);}
 }
}
