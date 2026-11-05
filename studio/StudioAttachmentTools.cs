using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TermoDom.Studio;
public partial class StudioWindow
{
 void RenderSavedAttachments(IEnumerable<string> files,StackPanel container)
 {
  foreach(var file in files)
  {
   if(!new[]{".png",".jpg",".jpeg",".webp"}.Contains(Path.GetExtension(file).ToLowerInvariant()))continue;
   try
   {
    using var stream=File.OpenRead(ResolveAttachment(file));
    var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=250;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
    container.Children.Add(new Image{Source=bitmap,MaxHeight=230,MaxWidth=250,HorizontalAlignment=HorizontalAlignment.Left,Stretch=Stretch.Uniform,Margin=new Thickness(0,10,0,0)});
   }catch(Exception ex) when(ex is IOException or NotSupportedException or UnauthorizedAccessException){Status.Text="Jedan slikovni privitak nije dostupan za prikaz.";}
  }
 }
 string ResolveAttachment(string path)
 {
  if(!Path.IsPathRooted(path))return workspace!.Resolve(path);
  var root=Path.Combine(Isolation.Data,"Attachments");
  if(Directory.Exists(root)&&(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Mapa privitaka ne smije biti poveznica.");
  return new Workspace(root).Resolve(Path.GetRelativePath(root,path));
 }
 string PrepareAttachment(string selected)
 {
  if(preview||!Isolation.IsWorker)throw new IOException("Privitci se dodaju samo unutar izoliranog Studija.");
  var full=Path.GetFullPath(selected);
  if((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new IOException("Privitak ne smije biti poveznica.");
  for(var parent=new DirectoryInfo(Path.GetDirectoryName(full)!);parent!=null;parent=parent.Parent)
   if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Putanja privitka ne smije prolaziti kroz poveznice.");
  var authRoot=Path.Combine(Isolation.Data,"Codex")+Path.DirectorySeparatorChar;
  if(full.StartsWith(authRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("Datoteke prijave i Codex pohrane ne dodaju se kao privitci.");
  if(new FileInfo(full).Length>32_000_000)throw new IOException("Odaberi privitak do 32 MB.");
  if(full.StartsWith(workspace!.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
   return Path.GetRelativePath(workspace.Root,workspace.Resolve(Path.GetRelativePath(workspace.Root,full)));
  var root=Path.Combine(Isolation.Data,"Attachments");Directory.CreateDirectory(root);
  if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Mapa privitaka ne smije biti poveznica.");
  var target=new Workspace(root).Resolve(Guid.NewGuid().ToString("N")+"-"+Path.GetFileName(full));
  File.Copy(full,target,false);return target;
 }
}
