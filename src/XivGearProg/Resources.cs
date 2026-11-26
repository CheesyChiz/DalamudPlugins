using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json.Linq;
using Lumina.Excel.Sheets;
namespace XivGearProg;
public sealed partial class Plugin {
 [Dalamud.IoC.PluginService] internal static Dalamud.Plugin.Services.IAddonLifecycle Addons {get;private set;}=null!;
 readonly HashSet<string> retainerWindows=[];
 DateTime nextRetainerRead=DateTime.MinValue;
 IEnumerator<SpecialShop>? shopReader;
 readonly JArray exchanges=new();
 readonly HashSet<uint> resourceIds=[];
 readonly HashSet<string> exchangeKeys=[];
 readonly HashSet<string> checkedThisOpen=[];
 Dictionary<uint,uint> tomeItems=new();
 bool shopsReady;
 void SetupResources(){
  Addons.RegisterListener(AddonEvent.PostSetup,new[]{"InventoryRetainer","InventoryRetainerLarge"},RetainerWindow);
  Addons.RegisterListener(AddonEvent.PreFinalize,new[]{"InventoryRetainer","InventoryRetainerLarge"},RetainerWindow);
 }
 void DisposeResources(){Addons.UnregisterListener(RetainerWindow);shopReader?.Dispose();}
 void RetainerWindow(AddonEvent kind,AddonArgs args){if(kind==AddonEvent.PostSetup){retainerWindows.Add(args.AddonName);checkedThisOpen.Clear();nextRetainerRead=DateTime.UtcNow.AddSeconds(1);}else{CaptureRetainer();retainerWindows.Remove(args.AddonName);}}
 void UpdateResources(){
  if(!Player.IsLoaded||config.Token.Length==0)return;
  if(!shopsReady)BuildExchanges();
  if(retainerWindows.Count>0&&DateTime.UtcNow>=nextRetainerRead){nextRetainerRead=DateTime.UtcNow.AddSeconds(1);CaptureRetainer();}
 }
 void BuildExchanges(){
  var items=Data.GetExcelSheet<Item>();
  if(shopReader==null){tomeItems=Data.GetExcelSheet<TomestonesItem>().Where(t=>t.Tomestones.RowId>0&&t.CurrencyInventorySlot>0).GroupBy(t=>t.Tomestones.RowId).ToDictionary(g=>g.Key,g=>g.First().Item.RowId);shopReader=Data.GetExcelSheet<SpecialShop>().GetEnumerator();}
  // Spread sheet work across frames; no inventory scans or network requests here.
  for(int n=0;n<8;n++){
   if(!shopReader.MoveNext()){shopsReady=true;shopReader.Dispose();shopReader=null;return;}
   var shop=shopReader.Current;if(shop.UseCurrencyType==16||shop.UseCurrencyType==4)continue;
   foreach(var offer in shop.Item){
    var rewards=offer.ReceiveItems.Where(r=>r.Item.RowId>0&&r.ReceiveCount>0&&r.Item.Value.EquipSlotCategory.RowId>0&&r.Item.Value.LevelItem.RowId>=500&&r.Item.Value.EquipSlotCategory.Value.SoulCrystal==0).ToArray();if(rewards.Length!=1)continue;
    var costs=new JArray();bool valid=true;
    foreach(var cost in offer.ItemCosts){if(cost.ItemCost.RowId==0||cost.CurrencyCost==0)continue;uint id=cost.ItemCost.RowId;
     if(shop.UseCurrencyType==2&&id<10)id=tomeItems.GetValueOrDefault(id);
     if((id<100&&!tomeItems.ContainsValue(id))||cost.CollectabilityCost>0||!items.TryGetRow(id,out var item)){valid=false;break;}
     costs.Add(new JObject{["id"]=id,["name"]=item.Name.ToString(),["iconId"]=item.Icon,["count"]=cost.CurrencyCost,["hq"]=cost.CostType==1});
    }
    if(!valid||costs.Count==0)continue;
    var signature=rewards[0].Item.RowId+":"+costs.ToString(Newtonsoft.Json.Formatting.None);if(!exchangeKeys.Add(signature))continue;
    foreach(var c in costs)resourceIds.Add(c.Value<uint>("id"));
    var r=rewards[0];exchanges.Add(new JObject{["itemId"]=r.Item.RowId,["quantity"]=r.ReceiveCount,["hq"]=r.ReceiveHq,["shop"]=shop.Name.ToString(),["costs"]=costs});
   }
  }
 }
 unsafe JObject CaptureResources(){
  var inv=InventoryManager.Instance();if(inv==null)throw new InvalidOperationException();var items=Data.GetExcelSheet<Item>();var counts=new JObject();
  foreach(var id in resourceIds){int nq=Math.Max(0,inv->GetInventoryItemCount(id,false)),hq=Math.Max(0,inv->GetInventoryItemCount(id,true));counts[id.ToString()]=nq;counts[id+":hq"]=hq;}
  foreach(var t in Data.GetExcelSheet<TomestonesItem>())if(t.Tomestones.RowId>0&&t.Item.RowId>0){counts[t.Item.RowId.ToString()]=inv->GetTomestoneCount(t.Item.RowId);}
  return new JObject{["counts"]=counts,["exchanges"]=exchanges.DeepClone(),["weeklyEarned"]=Math.Max(0,inv->GetWeeklyAcquiredTomestoneCount()),["weeklyLimit"]=Math.Max(0,InventoryManager.GetLimitedTomestoneWeeklyLimit())};
 }
 unsafe void CaptureRetainer(){
  if(!Player.IsLoaded||Player.ContentId==0||retainerWindows.Count==0)return;
  var manager=RetainerManager.Instance();var inv=InventoryManager.Instance();if(manager==null||inv==null||!manager->IsReady)return;var ret=manager->GetActiveRetainer();if(ret==null||ret->RetainerId==0)return;
  for(int page=10000;page<=10006;page++){var c=inv->GetInventoryContainer((InventoryType)page);if(c==null||!c->IsLoaded)return;}
  var gear=new JObject();var resources=new JObject();var items=Data.GetExcelSheet<Item>();
  for(int page=10000;page<=10006;page++)foreach(var v in Inventory.GetInventoryItems((Dalamud.Game.Inventory.GameInventoryType)page)){
   if(v.IsEmpty||v.ItemId==0)continue;var id=v.ItemId%1_000_000;if(!items.TryGetRow(id,out var row))continue;
   if(row.EquipSlotCategory.RowId>0&&row.EquipSlotCategory.Value.SoulCrystal==0){var key=id+(row.CanBeHq&&!v.IsHq?":nq":"");gear[key]=Math.Min(99,(gear.Value<int?>(key)??0)+(int)v.Quantity);}
   if(resourceIds.Contains(id)){var key=id+(v.IsHq?":hq":"");resources[key]=(resources.Value<int?>(key)??0)+(int)v.Quantity;}
  }
  string owner=Player.ContentId.ToString(),keyId=ret->RetainerId.ToString();config.Retainers.TryGetValue(owner,out var saved);var cache=JObject.Parse(saved??"{}");var old=cache[keyId];
  // No timestamp churn while an unchanged inventory window stays open.
  if(checkedThisOpen.Contains(owner+":"+keyId)&&old!=null&&JToken.DeepEquals(old["owned"],gear)&&JToken.DeepEquals(old["resources"],resources))return;
  checkedThisOpen.Add(owner+":"+keyId);
  cache[keyId]=new JObject{["name"]=ret->NameString,["checkedAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),["owned"]=gear,["resources"]=resources};config.Retainers[owner]=cache.ToString(Newtonsoft.Json.Formatting.None);PI.SavePluginConfig(config);
 }
}
