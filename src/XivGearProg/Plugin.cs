using Dalamud.Game.Command;
using Dalamud.Game.Inventory;
using Dalamud.Bindings.ImGui;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using XivGearExport;
namespace XivGearProg;
public sealed partial class Plugin : IDalamudPlugin {
 static readonly Dictionary<string,string> JobNames=new(){["PLD"]="Paladin",["MNK"]="Monk",["WAR"]="Warrior",["DRG"]="Dragoon",["BRD"]="Bard",["WHM"]="White Mage",["BLM"]="Black Mage",["SMN"]="Summoner",["SCH"]="Scholar",["NIN"]="Ninja",["MCH"]="Machinist",["DRK"]="Dark Knight",["AST"]="Astrologian",["SAM"]="Samurai",["RDM"]="Red Mage",["BLU"]="Blue Mage",["GNB"]="Gunbreaker",["DNC"]="Dancer",["RPR"]="Reaper",["SGE"]="Sage",["VPR"]="Viper",["PCT"]="Pictomancer"};
 [PluginService] internal static IDalamudPluginInterface PI {get;private set;}=null!;
 [PluginService] internal static ICommandManager Commands {get;private set;}=null!;
 [PluginService] internal static IPlayerState Player {get;private set;}=null!;
 [PluginService] internal static IDataManager Data {get;private set;}=null!;
 [PluginService] internal static IGameInventory Inventory {get;private set;}=null!;
 [PluginService] internal static IChatGui Chat {get;private set;}=null!;
 [PluginService] internal static IFramework Framework {get;private set;}=null!;
 readonly Configuration config;
 readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(25)};
 readonly CancellationTokenSource lifetime=new();
 ulong currentCharacter;
 readonly Dictionary<ulong,string> characterPayloads=new();
 HashSet<string> IgnoredJobs {get {var key=Player.ContentId.ToString();if(!config.CharacterIgnoredJobs.TryGetValue(key,out var jobs)){jobs=new(config.IgnoredJobs);config.CharacterIgnoredJobs[key]=jobs;}return jobs;}}
 bool open, busy,disposed; string status="Connect your account to start.",tokenInput="",lastPayload=""; DateTime next=DateTime.MinValue;
 static readonly HashSet<string> Jobs=["PLD","WAR","DRK","GNB","WHM","SCH","AST","SGE","MNK","DRG","NIN","SAM","RPR","VPR","BRD","MCH","DNC","BLM","SMN","RDM","PCT","BLU"];
 public Plugin(){config=PI.GetPluginConfig() as Configuration??new();tokenInput=config.Token;if(config.Token.Length>0)status="Token saved. Automatic sync every 5 minutes.";Commands.AddHandler("/xgp",new CommandInfo((_,_)=>open=true){HelpMessage="Connect and sync gear with XIVGear Prog."});PI.UiBuilder.Draw+=Draw;PI.UiBuilder.OpenConfigUi+=Show;Framework.Update+=Update;SetupResources();}
 void Show()=>open=true;
 void Update(IFramework _){
  var cid=Player.IsLoaded?Player.ContentId:0;
  if(cid!=currentCharacter){currentCharacter=cid;retainerWindows.Clear();checkedThisOpen.Clear();lastPayload=characterPayloads.GetValueOrDefault(cid,"");next=DateTime.UtcNow.AddMinutes(5);
   if(cid!=0){config.Characters[cid.ToString()]=Player.CharacterName+" @ "+Player.HomeWorld.Value.Name.ToString();PI.SavePluginConfig(config);status=config.CharacterSyncStatus.GetValueOrDefault(cid.ToString(),"Not synced yet. Automatic sync in 5 minutes, or use Sync now.");}
   else status="Log in to a character to sync. Saved character data is kept.";
  }
  UpdateResources();if(shopsReady&&config.AutoSync&&config.Token.Length>0&&!busy&&DateTime.UtcNow>=next){next=DateTime.UtcNow.AddMinutes(5);Start(false);}}
 void Draw(){if(!open)return;ImGui.SetNextWindowSize(new System.Numerics.Vector2(510,280),ImGuiCond.FirstUseEver);if(ImGui.Begin("XIVGear Prog",ref open)){
  ImGui.TextWrapped("Sends your equipped gear and saved combat gear sets, materia and relic stats to xivgearprog.top. Includes gear in your bags and Armoury Chest. Currencies and exchange materials are included. Retainer inventory is cached only after you open it in game.");
  ImGui.TextWrapped("On the website: account > Connect game > Create token. Paste that token here.");
  ImGui.InputText("Token",ref tokenInput,256,ImGuiInputTextFlags.Password);
  if(ImGui.Button("Save token")&&tokenInput.StartsWith("xgp_")&&tokenInput.Length<150){config.Token=tokenInput.Trim();config.AutoSync=true;next=DateTime.MinValue;tokenInput=config.Token;lastPayload="";characterPayloads.Clear();PI.SavePluginConfig(config);status="Token saved. Automatic sync runs every 5 minutes; Sync now sends immediately.";}
  bool auto=config.AutoSync;if(ImGui.Checkbox("Automatically sync every 5 minutes",ref auto)){config.AutoSync=auto;PI.SavePluginConfig(config);}
  ImGui.BeginDisabled(busy||config.Token.Length==0);if(ImGui.Button("Sync now"))Start(true);ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Disconnect")){config.Token="";tokenInput="";config.AutoSync=false;lastPayload="";PI.SavePluginConfig(config);status="Disconnected. You can revoke the token on the website.";}
  if(Player.IsLoaded)ImGui.TextWrapped("Character: "+Player.CharacterName+" @ "+Player.HomeWorld.Value.Name.ToString());
  ImGui.TextWrapped(status);
  if(ImGui.CollapsingHeader("Characters on this account")){ImGui.TextWrapped("Use the same token for all characters. Log in to each character and sync once; select them on the website.");foreach(var c in config.Characters){ImGui.TextWrapped(c.Value);ImGui.TextWrapped(config.CharacterSyncStatus.GetValueOrDefault(c.Key,"Not synced yet."));}}
  DrawSetFilters();DrawImport();
 }ImGui.End();}
 void Start(bool force){if(!shopsReady){status="Preparing gear exchange data… Try Sync now shortly.";return;}if(!Player.IsLoaded||Player.ContentId==0){status="Log in to a character first.";return;}try{var payload=Capture();if(payload==lastPayload&&!force){status="Checked at "+DateTime.Now.ToShortTimeString()+" — no changes. No upload needed.";return;}busy=true;status="Sending gear…";_ = Send(payload,config.Token);}catch{status="Could not read gear. Try again after loading finishes.";}}
 async Task Send(string payload,string token){try{using var request=new HttpRequestMessage(HttpMethod.Post,"https://xivgearprog.top/api/plugin/snapshot");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);var upload=JObject.Parse(payload);(upload["resources"] as JObject)?.Remove("exchanges");request.Content=new StringContent(upload.ToString(Formatting.None),Encoding.UTF8,"application/json");using var response=await http.SendAsync(request,lifetime.Token);if(config.Token!=token||disposed)return;if(response.IsSuccessStatusCode){var owner=upload.Value<string>("characterId")!;characterPayloads[ulong.Parse(owner)]=payload;if(currentCharacter.ToString()==owner)lastPayload=payload;var reply=JObject.Parse(await response.Content.ReadAsStringAsync(lifetime.Token));var summary=reply["summary"];status="Synced at "+DateTime.Now.ToShortTimeString()+" — "+(summary?.Value<bool>("changed")==false?"no changes.":(summary?.Value<int>("updatedSets")??0)+" sets updated; inventory "+(summary?.Value<bool>("inventoryChanged")==true?"updated":"unchanged")+(summary?.Value<bool>("resourcesChanged")==true?"; currencies updated":"")+(summary?.Value<bool>("retainersChanged")==true?"; retainer snapshots updated":"")+".");config.CharacterSyncStatus[owner]=status;PI.SavePluginConfig(config);if(currentCharacter.ToString()!=owner)status=config.CharacterSyncStatus.GetValueOrDefault(currentCharacter.ToString(),"Current character not synced yet.");}else {next=DateTime.UtcNow.AddMinutes(5);status=response.StatusCode==System.Net.HttpStatusCode.Unauthorized?"Token rejected. Create a new token on the website.":"Sync failed (HTTP "+(int)response.StatusCode+"). Your website data was not replaced.";}}catch(OperationCanceledException){next=DateTime.UtcNow.AddMinutes(5);if(!disposed)status="Connection timed out; will retry.";}catch{next=DateTime.UtcNow.AddMinutes(5);status="Cannot reach XIVGear Prog; will retry.";}finally{busy=false;}}
 unsafe string Capture(){
  var materia=Data.GetExcelSheet<Lumina.Excel.Sheets.Materia>();var manderville=Data.GetExcelSheet<Lumina.Excel.Sheets.MandervilleWeaponEnhance>();var resistance=Data.GetExcelSheet<Lumina.Excel.Sheets.ResistanceWeaponAdjust>();var jobs=Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();var items=Data.GetExcelSheet<Lumina.Excel.Sheets.Item>();
  var levels=new JObject();var playerState=FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState.Instance();
  if(playerState==null)throw new InvalidOperationException();
  foreach(var row in jobs){var abbr=row.Abbreviation.ToString();var index=row.ExpArrayIndex;if(Jobs.Contains(abbr)&&index>=0&&index<playerState->ClassJobLevels.Length){var lv=playerState->ClassJobLevels[index];if(lv>0&&lv<=100)levels[abbr]=lv;}}
  var sets=new JArray();var module=RaptureGearsetModule.Instance();if(module==null)throw new InvalidOperationException();
  JObject Normalize(XivGearItems gear,Func<string,bool> hq){var result=JObject.FromObject(gear);foreach(var prop in result.Properties().ToArray()){if(prop.Value is not JObject v||v.Value<uint>("id")==0){prop.Remove();continue;}var row=items.GetRow(v.Value<uint>("id"));v["forceNq"]=row.CanBeHq&&!hq(prop.Name);}return result;}
  string? SlotName(int n)=>n switch{0=>"Weapon",1=>"OffHand",2=>"Head",3=>"Body",4=>"Hand",6=>"Legs",7=>"Feet",8=>"Ears",9=>"Neck",10=>"Wrist",11=>"RingRight",12=>"RingLeft",_=>null};
  var equipped=Inventory.GetInventoryItems(GameInventoryType.EquippedItems);var job=jobs.GetRow(Player.ClassJob.RowId).Abbreviation.ToString();
  if(Jobs.Contains(job)&&!IgnoredJobs.Contains(job)&&!config.IgnoredSets.Contains(Player.ContentId+":"+module->CurrentGearsetIndex)){var hq=new HashSet<string>();foreach(var v in equipped)if(v.IsHq&&SlotName((int)v.InventorySlot) is string slot)hq.Add(slot);var gear=XivGearItems.CreateItemsFromGameInventoryItems(equipped,materia,manderville,resistance,Chat);sets.Add(new JObject{["sourceId"]="equipped-"+job,["job"]=job,["name"]="Equipped · "+job,["items"]=Normalize(gear,hq.Contains)});}
  for(int i=0;i<100;i++){if(!module->IsValidGearset(i))continue;var gs=module->GetGearset(i);if(gs==null)continue;var j=jobs.GetRow(gs->ClassJob).Abbreviation.ToString();if(!Jobs.Contains(j)||IgnoredJobs.Contains(j)||config.IgnoredSets.Contains(Player.ContentId+":"+i))continue;var hq=new HashSet<string>();string[] names=["Weapon","OffHand","Head","Body","Hand","Legs","Feet","Ears","Neck","Wrist","RingRight","RingLeft"];RaptureGearsetModule.GearsetItemIndex[] indexes=[RaptureGearsetModule.GearsetItemIndex.MainHand,RaptureGearsetModule.GearsetItemIndex.OffHand,RaptureGearsetModule.GearsetItemIndex.Head,RaptureGearsetModule.GearsetItemIndex.Body,RaptureGearsetModule.GearsetItemIndex.Hands,RaptureGearsetModule.GearsetItemIndex.Legs,RaptureGearsetModule.GearsetItemIndex.Feet,RaptureGearsetModule.GearsetItemIndex.Ears,RaptureGearsetModule.GearsetItemIndex.Neck,RaptureGearsetModule.GearsetItemIndex.Wrists,RaptureGearsetModule.GearsetItemIndex.RingRight,RaptureGearsetModule.GearsetItemIndex.RingLeft];for(int n=0;n<names.Length;n++)if(gs->GetItem(indexes[n]).ItemId>=1_000_000)hq.Add(names[n]);sets.Add(new JObject{["sourceId"]="gearset-"+i,["job"]=j,["name"]=JobNames.GetValueOrDefault(j,j)+" · #"+(i+1),["items"]=Normalize(XivGearItems.CreateItemsFromGearset(gs,materia,manderville,resistance),hq.Contains)});}
  var owned=new JObject();
  // Only local inventory, equipment and Armoury Chest; never enumerate retainers.
  int[] containers=[0,1,2,3,1000,3200,3201,3202,3203,3204,3205,3206,3207,3208,3209,3300,3500];
  foreach(var container in containers)foreach(var v in Inventory.GetInventoryItems((GameInventoryType)container)){
   if(v.IsEmpty||v.ItemId==0)continue;var id=v.ItemId%1_000_000;if(!items.TryGetRow(id,out var row)||row.EquipSlotCategory.RowId==0||row.EquipSlotCategory.Value.SoulCrystal>0)continue;
   var key=id.ToString()+(row.CanBeHq&&!v.IsHq?":nq":"");owned[key]=Math.Min(99,(owned.Value<int?>(key)??0)+(int)v.Quantity);
  }
  // Avoid a second Equipped entry when an uploaded saved gear set is identical.
  var equippedSet=sets.OfType<JObject>().FirstOrDefault(x=>x.Value<string>("sourceId")?.StartsWith("equipped-")==true);
  if(equippedSet!=null&&sets.OfType<JObject>().Any(x=>x!=equippedSet&&x.Value<string>("job")==equippedSet.Value<string>("job")&&JToken.DeepEquals(x["items"],equippedSet["items"])))equippedSet.Remove();
  return new JObject{["version"]=1,["characterId"]=Player.ContentId.ToString(),["characterName"]=Player.CharacterName.ToString(),["homeWorld"]=Player.HomeWorld.Value.Name.ToString(),["jobLevels"]=levels,["sets"]=sets,["owned"]=owned,["resources"]=CaptureResources(),["retainers"]=JObject.Parse(config.Retainers.GetValueOrDefault(Player.ContentId.ToString())??"{}")}.ToString(Formatting.None);
 }
 public void Dispose(){disposed=true;DisposeResources();Framework.Update-=Update;PI.UiBuilder.Draw-=Draw;PI.UiBuilder.OpenConfigUi-=Show;Commands.RemoveHandler("/xgp");lifetime.Cancel();http.Dispose();lifetime.Dispose();}
}
