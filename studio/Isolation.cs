using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Xml.Linq;

namespace TermoDom.Studio;
public static class Isolation
{
 public const string Project = @"C:\TermoDom\Project";
 public const string Data = @"C:\TermoDom\Data";
 public const string AppRoot = @"C:\TermoDom\App";
 public static bool IsWorker => Environment.UserName.Equals("WDAGUtilityAccount", StringComparison.OrdinalIgnoreCase)
  && Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\').Equals(AppRoot,StringComparison.OrdinalIgnoreCase)
  && File.Exists(@"C:\TermoDom\worker-ready.json") && Directory.Exists(Project) && Directory.Exists(Data);
 public static string HostRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TermoDomStudio","Isolated");
 public static bool SandboxInstalled => File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsSandbox.exe"));
 public static void SecureDirectory(string path)
 {
  Directory.CreateDirectory(path);
  var acl=new DirectorySecurity(); acl.SetAccessRuleProtection(true,false);
  foreach(var sid in new[]{WindowsIdentity.GetCurrent().User!,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})
   acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
  new DirectoryInfo(path).SetAccessControl(acl);
 }
 public static string CreateConfiguration(string install, string project, string data, bool readOnly=false)
 {
  foreach(var path in new[]{install,project,data}) if(!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\")) throw new InvalidOperationException("Potrebne su lokalne apsolutne putanje.");
  XElement Mapping(string host,string guest,bool ro) => new("MappedFolder",new XElement("HostFolder",host),new XElement("SandboxFolder",guest),new XElement("ReadOnly",ro.ToString().ToLowerInvariant()));
  return new XDocument(new XElement("Configuration",new XElement("VGpu","Disable"),new XElement("Networking","Enable"),
   new XElement("AudioInput","Disable"),new XElement("VideoInput","Disable"),new XElement("PrinterRedirection","Disable"),
   new XElement("ClipboardRedirection","Disable"),new XElement("ProtectedClient","Enable"),new XElement("MemoryInMB",8192),
   new XElement("MappedFolders",Mapping(install,AppRoot,true),Mapping(project,Project,readOnly),Mapping(data,Data,false)),
   new XElement("LogonCommand",new XElement("Command",@"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File C:\TermoDom\App\SandboxBoot.ps1")))).ToString();
 }
 public static string Prepare()
 {
  var root=HostRoot; SecureDirectory(root);
  var project=Path.Combine(root,"Project"); var data=Path.Combine(root,"Data");
  Directory.CreateDirectory(data);
  if(!Directory.Exists(project))
  {
   var seed=Path.Combine(AppContext.BaseDirectory,"ProjectSeed.zip");
   if(!File.Exists(seed))throw new FileNotFoundException("Nedostaje pregledani projektni paket.",seed);
   var stage=Path.Combine(root,"Seed-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   try { ExtractSeed(seed,stage); Directory.Move(stage,project); }
   finally { if(Directory.Exists(stage)) Directory.Delete(stage,true); }
  }
  foreach(var p in new[]{root,project,data}) if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Mapirana mapa ne smije biti poveznica.");
  var config=Path.Combine(root,"TermoDom.wsb");
  File.WriteAllText(config,CreateConfiguration(AppContext.BaseDirectory.TrimEnd('\\'),project,data));
  return config;
 }
 public static void ExtractSeed(string archive,string destination)
 {
  var root=Path.GetFullPath(destination).TrimEnd('\\')+Path.DirectorySeparatorChar;
  using var zip=ZipFile.OpenRead(archive);
  foreach(var entry in zip.Entries)
  {
   var target=Path.GetFullPath(Path.Combine(root,entry.FullName));
   if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)||entry.FullName.Split('/','\\').Any(p=>p.Equals(".codex",StringComparison.OrdinalIgnoreCase)||p.Equals(".git",StringComparison.OrdinalIgnoreCase)))
    throw new IOException("Nepodržana putanja u početnom projektnom paketu.");
   if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(target);continue;}
   Directory.CreateDirectory(Path.GetDirectoryName(target)!); entry.ExtractToFile(target,false);
  }
 }
}
