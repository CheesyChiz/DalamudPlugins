using Dalamud.Bindings.ImGui;
using Dalamud.Game.Inventory;
using Newtonsoft.Json.Linq;
using System.Numerics;
using System.Diagnostics;
using System.Collections.Concurrent;
namespace XivGearProg;
public sealed partial class Plugin {
 [Dalamud.IoC.PluginService] internal static Dalamud.Plugin.Services.ITextureProvider Textures {get;private set;}=null!;
 static readonly Vector4 Accent=new(.46f,.89f,.81f,1),Warning=new(1,.76f,.38f,1),Muted=new(.63f,.71f,.80f,1);
 readonly Dictionary<string,int> localGear=new();
 readonly Dictionary<string,string> localLocations=new();
 DateTime nextPlanCheck=DateTime.MinValue;
 ulong planCharacter;
 bool onlyMissing;
 readonly ConcurrentDictionary<uint,string> priceText=new();
 readonly SemaphoreSlim priceGate=new(2);
 void Draw(){
  if(!open)return;
  ImGui.SetNextWindowSize(new Vector2(780,620),ImGuiCond.FirstUseEver);
  ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.055f,.08f,.12f,1));ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(.12f,.25f,.28f,1));ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.13f,.24f,.29f,1));ImGui.PushStyleColor(ImGuiCol.ButtonHovered,new Vector4(.18f,.38f,.40f,1));ImGui.PushStyleColor(ImGuiCol.FrameBg,new Vector4(.08f,.12f,.18f,1));ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,6);ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18,16));
  if(ImGui.Begin("XIVGear Prog",ref open)){
   ImGui.TextColored(Accent,"XIVGEAR PROG");ImGui.SameLine();ImGui.TextDisabled(Player.IsLoaded?Player.CharacterName+" @ "+Player.HomeWorld.Value.Name.ToString():"Log in to view your character's plans");
   ImGui.Separator();
   if(ImGui.BeginTabBar("xgp-tabs")){
    if(ImGui.BeginTabItem("Gear plans")){DrawImport();ImGui.EndTabItem();}
    if(ImGui.BeginTabItem("Connection")){DrawConnection();ImGui.EndTabItem();}
    if(ImGui.BeginTabItem("Sync filters")){DrawSetFilters();ImGui.EndTabItem();}
    ImGui.EndTabBar();
   }
  }ImGui.End();ImGui.PopStyleVar(2);ImGui.PopStyleColor(5);
 }
 static void OpenItem(uint id)=>Process.Start(new ProcessStartInfo("https://www.garlandtools.org/db/#item/"+id){UseShellExecute=true});
 List<string> Plans(){var owner=Player.ContentId.ToString();if(!config.ImportedPlans.TryGetValue(owner,out var plans)){plans=[];config.ImportedPlans[owner]=plans;}return plans;}
 void SavePlan(){if(importPreview is null||!Player.IsLoaded)return;var plans=Plans();var name=importPreview["set"]!.Value<string>("name");int index=plans.FindIndex(s=>{try{var p=JObject.Parse(s);return p.Value<string>("job")==importPreview.Value<string>("job")&&p["set"]?.Value<string>("name")==name;}catch{return false;}});if(index>=0)plans[index]=importPreview.ToString(Newtonsoft.Json.Formatting.None);else{if(plans.Count>=50)throw new Exception("Remove a saved plan before adding another (limit 50).");plans.Add(importPreview.ToString(Newtonsoft.Json.Formatting.None));}PI.SavePluginConfig(config);}
 void RefreshPlanInventory(){
  localGear.Clear();localLocations.Clear();
  int[] containers=[0,1,2,3,1000,3200,3201,3202,3203,3204,3205,3206,3207,3208,3209,3300,3500];
  foreach(int container in containers)foreach(var v in Inventory.GetInventoryItems((GameInventoryType)container)){
   if(v.IsEmpty||v.ItemId==0)continue;var id=v.ItemId%1000000;if(!Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(id,out var row)||row.EquipSlotCategory.RowId==0)continue;
   var key=id+(row.CanBeHq&&!v.IsHq?":nq":"");localGear[key]=localGear.GetValueOrDefault(key)+(int)v.Quantity;var location=container==1000?"Equipped":container<4?"Inventory":"Armoury Chest";if(!localLocations.GetValueOrDefault(key,"").Contains(location))localLocations[key]=localLocations.GetValueOrDefault(key,"")+(localLocations.ContainsKey(key)?", ":"")+location;
  }nextPlanCheck=DateTime.UtcNow.AddSeconds(30);
 }
 void DrawImport(){
  if(!Player.IsLoaded){ImGui.TextWrapped("Log in to a character. Plans are saved separately for each character.");return;}
  if(planCharacter!=Player.ContentId){planCharacter=Player.ContentId;importPreview=null;importMessage="";nextPlanCheck=DateTime.MinValue;}
  if(DateTime.UtcNow>=nextPlanCheck)RefreshPlanInventory();
  ImGui.Spacing();ImGui.TextWrapped("Copy a build using To game on the website. Keep it here as a shopping checklist, or create a new game gear set.");
  if(ImGui.Button("Paste plan")){try{var text=ImGui.GetClipboardText();if(text.Length>100000)throw new Exception("Set data is too large.");var doc=JObject.Parse(text);ValidateImport(doc);importPreview=doc;SavePlan();importMessage="Plan saved for this character.";}catch(Exception e){importPreview=null;importMessage=e.Message;}}
  ImGui.SameLine();if(ImGui.Button("Refresh inventory"))RefreshPlanInventory();ImGui.SameLine();if(ImGui.Button("Open website / simulate"))Process.Start(new ProcessStartInfo("https://xivgearprog.top/"){UseShellExecute=true});
  ImGui.TextDisabled("Simulations run on the website. Local inventory refreshes every 30 seconds while this tab is open.");
  if(ImGui.BeginCombo("Saved plans",importPreview?["set"]?.Value<string>("name")??"Choose a plan")){
   foreach(var saved in Plans().ToArray()){try{var doc=JObject.Parse(saved);var name=doc["set"]!.Value<string>("name")+" · "+doc.Value<string>("job");if(ImGui.Selectable(name)){ValidateImport(doc);importPreview=doc;importMessage="";}}catch{} }ImGui.EndCombo();
  }
  if(importPreview is not null){
   ImGui.Separator();ImGui.TextColored(Accent,importPreview["set"]!.Value<string>("name")??"Build");
   if(importPreview["set"]!.Value<string>("notes") is string notes)ImGui.TextWrapped(notes);
   ImGui.Checkbox("Only missing / on retainers",ref onlyMissing);
   var available=new Dictionary<string,int>(localGear);var retainers=JObject.Parse(config.Retainers.GetValueOrDefault(Player.ContentId.ToString())??"{}");var retCounts=new Dictionary<string,int>();var retNames=new Dictionary<string,string>();
   foreach(var r in retainers.Properties())if(r.Value["owned"] is JObject owned)foreach(var item in owned.Properties()){retCounts[item.Name]=retCounts.GetValueOrDefault(item.Name)+item.Value.Value<int>();retNames[item.Name]=retNames.GetValueOrDefault(item.Name,"")+r.Value.Value<string>("name")+" ";}
   int have=0,stored=0,missing=0;
   foreach(var p in ((JObject)importPreview["set"]!["items"]!).Properties()){
    var value=p.Value;uint id=value.Value<uint>("id");var row=Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(id);var key=PlanOwnership.Key(id,row.CanBeHq,value.Value<bool?>("forceNq")==true);
    int ownership=PlanOwnership.Take(key,available,retCounts);bool local=ownership==1,ret=ownership==2;if(local)have++;else if(ret)stored++;else missing++;
    if(onlyMissing&&local)continue;
    ImGui.PushID(p.Name);ImGui.Separator();ImGui.TextColored(Muted,p.Name+" · i"+row.LevelItem.RowId+(row.CanBeHq?(value.Value<bool?>("forceNq")==true?" · NQ":" · HQ"):""));var icon=Textures.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(row.Icon)).GetWrapOrDefault();if(icon is not null){ImGui.Image(icon.Handle,new Vector2(32,32));ImGui.SameLine();}ImGui.TextWrapped(row.Name.ToString());
    ImGui.TextColored(local?Accent:Warning,local?"Owned · "+localLocations.GetValueOrDefault(key):ret?"Retainer snapshot · "+retNames.GetValueOrDefault(key):"Not found in checked inventories");
    if(value["relicStats"] is JObject stats)ImGui.TextWrapped("Target relic stats: "+string.Join(" / ",stats.Properties().Where(x=>x.Value.Value<int>()>0).Select(x=>x.Name+" "+x.Value)));
    if(value["materia"] is JArray melds&&melds.Any(x=>x.Value<int>("id")>0))ImGui.TextWrapped("Target materia: "+string.Join(", ",melds.Where(x=>x.Value<int>("id")>0).Select(x=>Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(x.Value<uint>("id")).Name.ToString())));
    if(ImGui.SmallButton("Where to get it"))OpenItem(id);ImGui.SameLine();if(ImGui.SmallButton("Exchange costs"))_ = LoadPlanPrices(id);
    if(priceText.TryGetValue(id,out var price))ImGui.TextWrapped(price);
    ImGui.PopID();
   }
   ImGui.Separator();ImGui.TextColored(Accent,$"Owned {have}  ·  Retainers {stored}  ·  Missing {missing}");
   ImGui.TextWrapped("Owned means the item and quality match, not its materia or relic stats. Retainer snapshots update only after opening their inventories. Unchecked retainers may have more items.");
   foreach(var r in retainers.Properties()){var checkedAt=r.Value.Value<long?>("checkedAt");ImGui.TextDisabled((r.Value.Value<string>("name")??"Retainer")+" · "+(checkedAt.HasValue?DateTimeOffset.FromUnixTimeMilliseconds(checkedAt.Value).LocalDateTime.ToString("g"):"not checked"));}
   if(ImGui.Button("Create NEW game set")){try{SavePlan();CreateImportedSet(importPreview);}catch(Exception e){importMessage=e.Message;}}
   ImGui.SameLine();if(ImGui.Button("Remove saved plan")){Plans().RemoveAll(s=>{var p=JObject.Parse(s);return p.Value<string>("job")==importPreview.Value<string>("job")&&p["set"]!.Value<string>("name")==importPreview["set"]!.Value<string>("name");});PI.SavePluginConfig(config);importPreview=null;}
   ImGui.TextWrapped("Creates a separate game-set slot, without equipping it. Missing gear is not obtained. Change materia and relic stats yourself in game.");
  }
  if(importMessage.Length>0)ImGui.TextWrapped(importMessage);
 }
 async Task LoadPlanPrices(uint id){
  if(!priceText.TryAdd(id,"Loading server prices…"))return;
  try{await priceGate.WaitAsync(lifetime.Token);try{using var response=await http.GetAsync("https://xivgearprog.top/api/catalog/exchanges/"+id,lifetime.Token);response.EnsureSuccessStatusCode();var data=JObject.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));var offers=data["exchanges"] as JArray;priceText[id]=offers?.Count>0?"Garland Tools · server catalog\n"+string.Join("\n",offers.Take(8).Select(o=>(o.Value<uint>("itemId")==id?"":"Base item · ")+o.Value<string>("shop")+": "+string.Join(" + ",((JArray)o["costs"]!).Select(c=>c.Value<int>("count")+" × "+c.Value<string>("name"))))):"No exchange price in the server catalog. Open Where to get it for quests, drops or crafting.";}finally{priceGate.Release();}}
  catch{priceText.TryRemove(id,out _);if(!disposed)importMessage="Could not load prices. Try Exchange costs again or open Where to get it.";}
 }
}
