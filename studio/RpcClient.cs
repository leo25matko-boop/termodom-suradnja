using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TermoDom.Studio;
public sealed class RpcClient : IDisposable
{
 readonly ConcurrentDictionary<long,TaskCompletionSource<JsonElement>> pending=new();
 readonly SemaphoreSlim writer=new(1,1);
 readonly CancellationTokenSource lifetime=new();
 Process? process; long nextId; bool disposed;
 public event Action<string,JsonElement>? Notification;
 public event Func<string,JsonElement,Task<object>>? ServerRequest;
 public event Action<string>? Disconnected;
 public async Task StartAsync(string executable,string project,string home,bool probe=false)
 {
  if(!probe&&!Isolation.IsWorker)throw new InvalidOperationException("Agent je blokiran izvan izoliranog Studija.");
  if(process!=null)throw new InvalidOperationException("Codex je već pokrenut.");
  Directory.CreateDirectory(home);
  var info=new ProcessStartInfo(executable){WorkingDirectory=project,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  info.ArgumentList.Add("app-server");info.ArgumentList.Add("--stdio");
  foreach(var setting in new[]{"cli_auth_credentials_store=\"file\"","forced_login_method=\"chatgpt\"","analytics.enabled=false","sandbox_mode=\"danger-full-access\"","approval_policy=\"on-request\"","features.apps=false","features.plugins=false","web_search=\"live\""}){info.ArgumentList.Add("-c");info.ArgumentList.Add(setting);}
  info.Environment["CODEX_HOME"]=Path.GetFullPath(home);
  foreach(var name in new[]{"OPENAI_API_KEY","OPENAI_BASE_URL","CHATGPT_API_KEY","GH_TOKEN","GITHUB_TOKEN","GIT_ASKPASS","SSH_AUTH_SOCK"})info.Environment.Remove(name);
  process=Process.Start(info)??throw new IOException("Codex se ne može pokrenuti.");
  _=ReadLoopAsync(process.StandardOutput);
  // Never persist stderr, protocol messages, OAuth URLs or auth/account responses to logs.
  _=DrainErrorAsync(process.StandardError);
  await CallAsync("initialize",new{clientInfo=new{name="termodom_studio",title="TermoDom Studio",version="1.1.1"},capabilities=new{experimentalApi=true}});
  await WriteAsync(new{method="initialized",@params=new{}});
 }
 async Task DrainErrorAsync(StreamReader reader){try{while(await reader.ReadLineAsync(lifetime.Token)!=null){}}catch(OperationCanceledException){}catch(IOException){}}
 public async Task<JsonElement> CallAsync(string method,object parameters)
 {
  if(disposed)throw new ObjectDisposedException(nameof(RpcClient));
  var id=Interlocked.Increment(ref nextId);
  var completion=new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
  pending[id]=completion;
  try{await WriteAsync(new{id,method,@params=parameters});return await completion.Task.WaitAsync(TimeSpan.FromSeconds(90),lifetime.Token);}
  finally{pending.TryRemove(id,out _);}
 }
 async Task WriteAsync(object value)
 {
  await writer.WaitAsync(lifetime.Token);
  try{if(process==null||process.HasExited)throw new IOException("Codex proces nije dostupan.");await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value));await process.StandardInput.FlushAsync();}
  finally{writer.Release();}
 }
 async Task ReadLoopAsync(StreamReader reader)
 {
  Exception? failure=null;
  try
  {
   while(await reader.ReadLineAsync(lifetime.Token) is {} line)
   {
    using var doc=JsonDocument.Parse(line);var message=doc.RootElement;
    if(message.TryGetProperty("method",out var method))
    {
     var name=method.GetString()!;var parameters=message.TryGetProperty("params",out var p)?p.Clone():JsonSerializer.SerializeToElement(new{});
     if(message.TryGetProperty("id",out var requestId))_ = ReplyToServerAsync(requestId.Clone(),name,parameters);
     else Notification?.Invoke(name,parameters);
    }
    else if(message.TryGetProperty("id",out var id)&&id.TryGetInt64(out var number)&&pending.TryGetValue(number,out var completion))
    {
     if(message.TryGetProperty("error",out var error))completion.TrySetException(new IOException(error.TryGetProperty("message",out var text)?text.GetString():"Codex RPC error"));
     else if(message.TryGetProperty("result",out var result))completion.TrySetResult(result.Clone());
    }
   }
  }
  catch(Exception ex) when(ex is IOException or JsonException or OperationCanceledException){failure=ex;}
  finally{foreach(var request in pending.Values)request.TrySetException(failure??new IOException("Codex spoj je zatvoren."));if(!disposed)Disconnected?.Invoke("Codex spoj je zatvoren. Ponovno otvori Studio; nespremljene promjene u datotekama provjeri u projektu.");}
 }
 async Task ReplyToServerAsync(JsonElement id,string method,JsonElement parameters)
 {
  try
  {
   if(ServerRequest==null)await WriteAsync(new{id,error=new{code=-32601,message="Unsupported request"}});
   else await WriteAsync(new{id,result=await ServerRequest(method,parameters)});
  }
  catch(Exception){try{await WriteAsync(new{id,error=new{code=-32601,message="Request not supported or cancelled by client"}});}catch(Exception){}}
 }
 public void Dispose()
 {
  if(disposed)return;disposed=true;lifetime.Cancel();
  if(process!=null)
  {
   try{process.StandardInput.Close();if(!process.WaitForExit(2000)){process.Kill(true);process.WaitForExit(3000);}}
   catch(InvalidOperationException){}catch(IOException){}finally{process.Dispose();}
  }
 }
}
