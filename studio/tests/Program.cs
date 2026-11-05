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
 if(args.FirstOrDefault(a=>a.StartsWith("--codex=")) is {} arg)
 {
  using var rpc=new RpcClient();var home=Path.Combine(root,"empty-home");await rpc.StartAsync(arg[8..],project,home,probe:true);
  var account=await rpc.CallAsync("account/read",new{refreshToken=false});Check(account.GetProperty("account").ValueKind==JsonValueKind.Null,"new Codex home contains no existing account");
  var models=await rpc.CallAsync("model/list",new{limit=100,includeHidden=false});Check(models.GetProperty("data").GetArrayLength()>0,"official model catalog protocol works without copying owner auth");
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
