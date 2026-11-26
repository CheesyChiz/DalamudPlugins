using Dalamud.Bindings.ImGui;
using Dalamud.Game.Inventory;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Newtonsoft.Json.Linq;
using System.Numerics;
using XivGearExport;
namespace XivGearProg;
public sealed partial class Plugin {
 int comparisonSet=-1;
 JObject? comparisonItems;
 string comparisonJob="";
 string comparisonError="";
 static readonly string[] DisplaySlots=["Weapon","OffHand","Head","Body","Hand","Legs","Feet","Ears","Neck","Wrist","RingRight","RingLeft"];
 static string SlotLabel(string slot)=>slot switch{"Hand"=>"Hands","Ears"=>"Earrings","Neck"=>"Necklace","Wrist"=>"Bracelets","RingRight"=>"Right ring","RingLeft"=>"Left ring","OffHand"=>"Off hand",_=>slot};
 unsafe void ReadComparison(){
  comparisonItems=null;comparisonError="";
  try{
   var materia=Data.GetExcelSheet<Lumina.Excel.Sheets.Materia>();var manderville=Data.GetExcelSheet<Lumina.Excel.Sheets.MandervilleWeaponEnhance>();var resistance=Data.GetExcelSheet<Lumina.Excel.Sheets.ResistanceWeaponAdjust>();
   var hq=new HashSet<string>();XivGearItems gear;
   if(comparisonSet<0){
    var equipped=Inventory.GetInventoryItems(GameInventoryType.EquippedItems);
    foreach(var v in equipped)if(v.IsHq&&(int)v.InventorySlot<ImportSlots.Length)hq.Add(ImportSlots[(int)v.InventorySlot]);
    gear=XivGearItems.CreateItemsFromGameInventoryItems(equipped,materia,manderville,resistance,Chat);comparisonJob=Player.ClassJob.Value.Abbreviation.ToString();
   }else{
    var module=RaptureGearsetModule.Instance();if(module==null||!module->IsValidGearset(comparisonSet))throw new Exception("This game set no longer exists.");
    var gs=module->GetGearset(comparisonSet);if(gs==null)throw new Exception("Game set is unavailable.");
    for(int n=0;n<ImportSlots.Length;n++)if(gs->Items[n].ItemId>=1000000)hq.Add(ImportSlots[n]);
    gear=XivGearItems.CreateItemsFromGearset(gs,materia,manderville,resistance);comparisonJob=Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().GetRow(gs->ClassJob).Abbreviation.ToString();
   }
   comparisonItems=JObject.FromObject(gear);
   foreach(var p in comparisonItems.Properties().ToArray()){
    if(p.Value is not JObject v||v.Value<uint>("id")==0){p.Remove();continue;}
    v["forceNq"]=Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(v.Value<uint>("id")).CanBeHq&&!hq.Contains(p.Name);
   }
  }catch(Exception e){comparisonError=e.Message;}
 }
 string ItemSummary(JToken? item){
  if(item is null)return "Empty slot";
  uint id=item.Value<uint>("id");if(!Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(id,out var row))return "Unknown item";
  string result=row.Name.ToString()+(row.CanBeHq?(item.Value<bool?>("forceNq")==true?" · NQ":" · HQ"):"")+" · i"+row.LevelItem.RowId;
  if(item["materia"] is JArray melds){var names=melds.Where(m=>m.Value<uint>("id")>0).Select(m=>Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(m.Value<uint>("id")).Name.ToString());result+="\nMateria: "+(names.Any()?string.Join(", ",names):"none");}
  if(item["relicStats"] is JObject stats)result+="\nRelic: "+string.Join(" / ",stats.Properties().Where(p=>p.Value.Value<int>()>0).Select(p=>p.Name+" "+p.Value));
  return result;
 }
 static JToken? Comparable(JToken? item)=>item is null?null:new JObject{["id"]=item["id"],["forceNq"]=item.Value<bool?>("forceNq")??false,["materia"]=new JArray((item["materia"] as JArray??new JArray()).Where(m=>m.Value<int>("id")>0).Select(m=>m.Value<int>("id")).Order()),["relicStats"]=item["relicStats"] is JObject stats?new JObject(stats.Properties().Where(p=>p.Value.Value<int>()>0).OrderBy(p=>p.Name).Select(p=>new JProperty(p.Name,p.Value))):new JObject()};
 void DrawEquipmentSlot(string slot,JToken? item){
  ImGui.PushID(slot);ImGui.TextColored(Muted,SlotLabel(slot));
  if(item is not null&&Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(item.Value<uint>("id"),out var row)){
   var icon=Textures.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(row.Icon)).GetWrapOrDefault();
   if(icon is not null){ImGui.Image(icon.Handle,new Vector2(40,40));if(ImGui.IsItemHovered())ImGui.SetTooltip(ItemSummary(item));ImGui.SameLine();}
   ImGui.TextWrapped(row.Name.ToString());
  }else ImGui.TextDisabled("—");ImGui.Spacing();ImGui.PopID();
 }
 unsafe void DrawPlanComparison(){
  var target=(JObject)importPreview!["set"]!["items"]!;
  ImGui.TextColored(Accent,"EQUIPMENT");
  if(ImGui.BeginTable("equipment-layout",2,ImGuiTableFlags.SizingStretchSame)){
   string[] left=["Weapon","Head","Body","Hand","Legs","Feet"],right=["OffHand","Ears","Neck","Wrist","RingRight","RingLeft"];
   for(int i=0;i<left.Length;i++){ImGui.TableNextRow();ImGui.TableNextColumn();DrawEquipmentSlot(left[i],target[left[i]]);ImGui.TableNextColumn();DrawEquipmentSlot(right[i],target[right[i]]);}ImGui.EndTable();
  }
  if(!ImGui.CollapsingHeader("Compare with equipped / saved game set",ImGuiTreeNodeFlags.DefaultOpen))return;
  if(ImGui.BeginCombo("Compare with",comparisonSet<0?"Equipped gear":"Game set #"+(comparisonSet+1))){
   if(ImGui.Selectable("Equipped gear",comparisonSet<0)){comparisonSet=-1;ReadComparison();}
   var module=RaptureGearsetModule.Instance();if(module!=null)for(int i=0;i<100;i++)if(module->IsValidGearset(i)){
    var gs=module->GetGearset(i);if(gs==null)continue;var job=Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().GetRow(gs->ClassJob).Abbreviation.ToString();
    if(ImGui.Selectable("#"+(i+1)+" · "+job+" · "+gs->NameString,comparisonSet==i)){comparisonSet=i;ReadComparison();}
   }ImGui.EndCombo();
  }
  if(comparisonItems is null&&comparisonError.Length==0)ReadComparison();
  if(ImGui.SmallButton("Refresh comparison"))ReadComparison();
  if(comparisonError.Length>0)ImGui.TextWrapped(comparisonError);
  if(comparisonItems is null)return;
  if(comparisonJob!=importPreview.Value<string>("job"))ImGui.TextColored(Warning,"Different jobs: "+comparisonJob+" / "+importPreview.Value<string>("job"));
  ImGui.TextWrapped("Item-by-item comparison. Hover equipment icons for target details. DPS and level-sync calculations are available on the website.");
  if(ImGui.BeginTable("set-comparison",2,ImGuiTableFlags.BordersInnerV|ImGuiTableFlags.SizingStretchSame)){
   ImGui.TableSetupColumn("CURRENT / SELECTED");ImGui.TableSetupColumn("WEBSITE PLAN");ImGui.TableHeadersRow();
   foreach(var slot in DisplaySlots){var before=comparisonItems[slot];var after=target[slot];if(before is null&&after is null)continue;bool same=JToken.DeepEquals(Comparable(before),Comparable(after));
    ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.Separator();ImGui.TextColored(same?Muted:Warning,SlotLabel(slot)+(same?" · Same":" · Changed"));ImGui.TextWrapped(ItemSummary(before));
    ImGui.TableNextColumn();ImGui.Separator();ImGui.TextColored(same?Muted:Accent,SlotLabel(slot));ImGui.TextWrapped(ItemSummary(after));
   }ImGui.EndTable();
  }
 }
}
