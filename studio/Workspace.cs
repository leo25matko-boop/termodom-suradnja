using System.Diagnostics;
using System.IO;

namespace TermoDom.Studio;
public sealed class Workspace(string root)
{
 public string Root {get;}=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
 static readonly HashSet<string> Excluded=new(StringComparer.OrdinalIgnoreCase){".git",".codex",".vs",".idea","bin","obj","build",".gradle","node_modules","publish","installer-output"};
 public string Resolve(string relative)
 {
  if(Path.IsPathRooted(relative)||relative.Split('/','\\').Any(s=>s is ".."||Excluded.Contains(s)))throw new IOException("Datoteka je izvan dostupnog projekta.");
  var path=Path.GetFullPath(Path.Combine(Root,relative));
  if(!path.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Datoteka je izvan projekta.");
  var current=Root;
  foreach(var part in Path.GetRelativePath(Root,path).Split(Path.DirectorySeparatorChar))
  {current=Path.Combine(current,part);if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Poveznice izvan projekta nisu podržane.");}
  return path;
 }
 public IEnumerable<string> Files()
 {
  IEnumerable<string> Scan(string directory)
  {
   foreach(var file in Directory.EnumerateFiles(directory))if((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)yield return Path.GetRelativePath(Root,file);
   foreach(var child in Directory.EnumerateDirectories(directory))if(!Excluded.Contains(Path.GetFileName(child))&&(File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)
    foreach(var file in Scan(child))yield return file;
  }
  return Scan(Root).OrderBy(s=>s,StringComparer.OrdinalIgnoreCase);
 }
 public static async Task<(int Code,string Output)> RunAsync(string exe,IEnumerable<string> args,string cwd,CancellationToken token)
 {
  var start=new ProcessStartInfo(exe){WorkingDirectory=cwd,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  foreach(var arg in args)start.ArgumentList.Add(arg);
  using var process=Process.Start(start)??throw new IOException("Proces se nije pokrenuo.");
  using var registration=token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}});
  var output=process.StandardOutput.ReadToEndAsync(token);var error=process.StandardError.ReadToEndAsync(token);
  await process.WaitForExitAsync(token);return(process.ExitCode,await output+await error);
 }
}
