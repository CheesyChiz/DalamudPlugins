using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
namespace XivGearProg;
public sealed partial class Plugin {
 bool receivingPlans,plansPending;
 CancellationTokenSource? planEvents;
 string eventIdentity="";
 void UpdatePlanConnection(){
  var identity=Player.IsLoaded&&config.Token.Length>0?Player.ContentId+":"+config.Token:"";
  if(identity==eventIdentity)return;eventIdentity=identity;planEvents?.Cancel();planEvents?.Dispose();planEvents=null;
  if(identity.Length==0)return;
  planEvents=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);_ = ListenForPlans(Player.ContentId.ToString(),config.Token,planEvents.Token);
 }
 async Task ListenForPlans(string owner,string token,CancellationToken cancel){
  while(!cancel.IsCancellationRequested){
   try{
    using var request=new HttpRequestMessage(HttpMethod.Get,"https://xivgearprog.top/api/plugin/events?characterId="+Uri.EscapeDataString(owner));
    request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
    using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel);response.EnsureSuccessStatusCode();
    using var stream=await response.Content.ReadAsStreamAsync(cancel);using var reader=new System.IO.StreamReader(stream);
    while(await reader.ReadLineAsync(cancel) is string line){
     if(!line.StartsWith("data:"))continue;
     await Framework.RunOnFrameworkThread(()=>{if(!disposed&&!cancel.IsCancellationRequested&&config.Token==token&&Player.ContentId.ToString()==owner)FetchPlans();});
    }
   }catch(OperationCanceledException) when(cancel.IsCancellationRequested){return;}catch{}
   try{await Task.Delay(TimeSpan.FromSeconds(10),cancel);}catch(OperationCanceledException){return;}
  }
 }
 void FetchPlans(){
  if(!Player.IsLoaded||config.Token.Length==0)return;
  if(receivingPlans){plansPending=true;return;}
  receivingPlans=true;_ = ReceivePlans(Player.ContentId.ToString(),config.Token);
 }
 async Task ReceivePlans(string owner,string token){
  try{
   using var request=new HttpRequestMessage(HttpMethod.Get,"https://xivgearprog.top/api/plugin/inbox?characterId="+Uri.EscapeDataString(owner));
   request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
   using var response=await http.SendAsync(request,lifetime.Token);response.EnsureSuccessStatusCode();
   var result=JObject.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));int count=0;
   foreach(var entry in result["plans"]??new JArray()){
    if(disposed||config.Token!=token)return;
    var doc=(JObject)entry["payload"]!;string id=entry.Value<string>("id")!;
    await Framework.RunOnFrameworkThread(()=>{
     if(disposed||config.Token!=token)throw new OperationCanceledException();
     ValidateImport(doc);doc["deliveryId"]=id;
     if(!config.ImportedPlans.TryGetValue(owner,out var plans)){plans=[];config.ImportedPlans[owner]=plans;}
     int index=plans.FindIndex(s=>{try{var p=JObject.Parse(s);return p.Value<string>("deliveryId")==id||p.Value<string>("job")==doc.Value<string>("job")&&p["set"]?.Value<string>("name")==doc["set"]?.Value<string>("name");}catch{return false;}});
     if(index<0&&plans.Count>=50)throw new Exception("Saved set limit reached (50). Remove a plan and fetch again.");
     var serialized=doc.ToString(Formatting.None);if(index>=0)plans[index]=serialized;else plans.Add(serialized);
     PI.SavePluginConfig(config);
    });
    // Acknowledge only after durable local storage. Retries replace the same delivery.
    using var ack=new HttpRequestMessage(HttpMethod.Post,"https://xivgearprog.top/api/plugin/inbox/ack");
    ack.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);ack.Content=new StringContent(JsonConvert.SerializeObject(new{ id,characterId=owner}),Encoding.UTF8,"application/json");
    using var acknowledged=await http.SendAsync(ack,lifetime.Token);acknowledged.EnsureSuccessStatusCode();count++;
   }
   await Framework.RunOnFrameworkThread(()=>{if(!disposed)importMessage=count>0?$"Received {count} set(s) from the website. Choose one from the list.":"Checked website — no new sets.";});
  }catch(Exception e){if(!disposed)await Framework.RunOnFrameworkThread(()=>importMessage=e.Message.StartsWith("Saved set limit")?e.Message:"Could not receive sets. Check Connection and try Fetch sets again.");}
  finally{receivingPlans=false;if(!disposed&&plansPending)await Framework.RunOnFrameworkThread(()=>{plansPending=false;FetchPlans();});}
 }
}
