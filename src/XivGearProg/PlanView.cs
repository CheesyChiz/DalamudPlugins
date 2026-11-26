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
 string planSearch="";
 readonly ConcurrentDictionary<uint,string> priceText=new();
 readonly SemaphoreSlim priceGate=new(2);
 void Draw(){
  if(!open)return;
  ImGui.SetNextWindowSize(new Vector2(980,700),ImGuiCond.FirstUseEver);
  ImGui.SetNextWindowSizeConstraints(new Vector2(680,440),new Vector2(1800,1400));
  var colors=new (ImGuiCol,Vector4)[]{
   (ImGuiCol.WindowBg,new(.04f,.06f,.09f,1)),(ImGuiCol.ChildBg,new(.065f,.09f,.13f,1)),
   (ImGuiCol.Border,new(.18f,.24f,.29f,1)),(ImGuiCol.Text,new(.88f,.93f,.98f,1)),(ImGuiCol.TextDisabled,Muted),
   (ImGuiCol.Header,new(.12f,.28f,.29f,1)),(ImGuiCol.HeaderHovered,new(.16f,.35f,.35f,1)),(ImGuiCol.HeaderActive,new(.18f,.40f,.39f,1)),
   (ImGuiCol.Button,new(.12f,.25f,.29f,1)),(ImGuiCol.ButtonHovered,new(.17f,.36f,.38f,1)),(ImGuiCol.ButtonActive,new(.20f,.44f,.43f,1)),
   (ImGuiCol.FrameBg,new(.08f,.12f,.17f,1)),(ImGuiCol.FrameBgHovered,new(.12f,.21f,.26f,1)),(ImGuiCol.FrameBgActive,new(.13f,.28f,.31f,1)),
   (ImGuiCol.Tab,new(.08f,.12f,.17f,1)),(ImGuiCol.TabHovered,new(.16f,.35f,.35f,1)),(ImGuiCol.TabActive,new(.12f,.30f,.30f,1)),
   (ImGuiCol.CheckMark,Accent),(ImGuiCol.Separator,new(.18f,.24f,.29f,1)),(ImGuiCol.ResizeGrip,new(.12f,.25f,.29f,1))};
  foreach(var (color,value) in colors)ImGui.PushStyleColor(color,value);
  ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,5);ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding,8);ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(14,12));ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,6));ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(8,8));
  if(ImGui.Begin("XIVGear Prog",ref open)){
   ImGui.TextColored(Accent,"XIVGEAR PROG");ImGui.SameLine();ImGui.TextDisabled(Player.IsLoaded?Player.CharacterName+" @ "+Player.HomeWorld.Value.Name.ToString():"Log in to view your character's plans");
   ImGui.Separator();
   ImGui.BeginChild("main-content",new Vector2(0,-36),false);
   if(ImGui.BeginTabBar("xgp-tabs")){
    if(ImGui.BeginTabItem("Gear plans")){DrawImport();ImGui.EndTabItem();}
    if(ImGui.BeginTabItem("Connection")){DrawConnection();ImGui.EndTabItem();}
    if(ImGui.BeginTabItem("Sync filters")){DrawSetFilters();ImGui.EndTabItem();}
    ImGui.EndTabBar();
   }
   ImGui.EndChild();ImGui.Separator();ImGui.TextColored(Muted,importMessage.Length>0?importMessage:status);
  }ImGui.End();ImGui.PopStyleVar(5);ImGui.PopStyleColor(colors.Length);
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
  if(planCharacter!=Player.ContentId){planCharacter=Player.ContentId;comparisonItems=null;comparisonError="";comparisonSet=-1;importPreview=null;importMessage="";nextPlanCheck=DateTime.MinValue;}
  if(DateTime.UtcNow>=nextPlanCheck)RefreshPlanInventory();
  ImGui.Spacing();
  ImGui.BeginDisabled(receivingPlans||config.Token.Length==0);if(ImGui.Button("Fetch sets"))FetchPlans();ImGui.EndDisabled();
  if(ImGui.IsItemHovered())ImGui.SetTooltip("Receive sets sent with To game on the website. Automatic sync also checks every 5 minutes.");
  ImGui.SameLine();if(ImGui.Button("Refresh inventory"))RefreshPlanInventory();ImGui.SameLine();if(ImGui.Button("Open website"))Process.Start(new ProcessStartInfo("https://xivgearprog.top/"){UseShellExecute=true});
  ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##plan-search","Search saved sets, jobs or notes",ref planSearch,200);
  var width=ImGui.GetContentRegionAvail().X;float left=Math.Clamp(width*.32f,190,310);
  ImGui.BeginChild("plan-library",new Vector2(left,0),true);
  ImGui.TextColored(Accent,"SAVED SETS");ImGui.SameLine();ImGui.TextDisabled(Plans().Count.ToString());ImGui.Separator();
  int visible=0;
  foreach(var saved in Plans().ToArray()){
   try{var doc=JObject.Parse(saved);var set=doc["set"]!;string name=set.Value<string>("name")??"Build",job=doc.Value<string>("job")??"",notes=set.Value<string>("notes")??"";
    if(!(name+" "+job+" "+notes).Contains(planSearch,StringComparison.OrdinalIgnoreCase))continue;
    ImGui.PushID(visible++);bool selected=importPreview?["set"]?.Value<string>("name")==name&&importPreview?.Value<string>("job")==job;
    if(ImGui.Selectable(name,selected)){ValidateImport(doc);importPreview=doc;importMessage="";}
    ImGui.TextColored(Muted,job+" · Lv. "+(set.Value<int?>("calculationLevel")??set.Value<int?>("level")??100)+(set.Value<int?>("ilvlSync") is int sync?" · i"+sync+" sync":""));
    if(notes.Length>0)ImGui.TextWrapped(notes);ImGui.Spacing();ImGui.PopID();
   }catch{}
  }
  if(visible==0)ImGui.TextWrapped(Plans().Count==0?"No sets received yet. On the website, open a set and click To game. Then use Fetch sets here or wait for automatic sync.":"No sets match your search.");
  ImGui.EndChild();ImGui.SameLine();ImGui.BeginChild("plan-detail",new Vector2(0,0),true);
  if(importPreview is not null){
   ImGui.Separator();ImGui.TextColored(Accent,importPreview["set"]!.Value<string>("name")??"Build");
   if(importPreview["set"]!.Value<string>("notes") is string notes)ImGui.TextWrapped(notes);
   DrawPlanComparison();
   ImGui.Separator();ImGui.TextColored(Accent,"SHOPPING CHECKLIST");
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
  else {ImGui.Spacing();ImGui.TextColored(Accent,"YOUR GEAR PLAN");ImGui.TextWrapped("Choose a saved set on the left to see owned gear, missing items and exchange costs.");ImGui.Separator();ImGui.TextWrapped("1. Website: open a set > To game\n2. Here: Fetch sets (or automatic sync)\n3. Review and compare before creating a game set");ImGui.TextColored(Muted,"DPS simulations are available on the website.");}
  ImGui.EndChild();
 }
 async Task LoadPlanPrices(uint id){
  if(!priceText.TryAdd(id,"Loading server prices…"))return;
  try{await priceGate.WaitAsync(lifetime.Token);try{using var response=await http.GetAsync("https://xivgearprog.top/api/catalog/exchanges/"+id,lifetime.Token);response.EnsureSuccessStatusCode();var data=JObject.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));var offers=data["exchanges"] as JArray;priceText[id]=offers?.Count>0?"Garland Tools · server catalog\n"+string.Join("\n",offers.Take(8).Select(o=>(o.Value<uint>("itemId")==id?"":"Base item · ")+o.Value<string>("shop")+": "+string.Join(" + ",((JArray)o["costs"]!).Select(c=>c.Value<int>("count")+" × "+c.Value<string>("name"))))):"No exchange price in the server catalog. Open Where to get it for quests, drops or crafting.";}finally{priceGate.Release();}}
  catch{priceText.TryRemove(id,out _);if(!disposed)importMessage="Could not load prices. Try Exchange costs again or open Where to get it.";}
 }
}
