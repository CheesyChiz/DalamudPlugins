using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Newtonsoft.Json.Linq;
using System.Text;
namespace XivGearProg;
public sealed partial class Plugin {
 JObject? importPreview;
 string importMessage="";
 static readonly string[] ImportSlots=["Weapon","OffHand","Head","Body","Hand","", "Legs","Feet","Ears","Neck","Wrist","RingRight","RingLeft"];
 void ValidateImport(JObject doc){
  if(doc.Value<string>("format")!="xivgear-prog-game-set"||doc.Value<int>("version")!=1||!Jobs.Contains(doc.Value<string>("job")??""))throw new Exception("Send a valid set using To game on the website.");
  if(doc["set"] is not JObject set||set["items"] is not JObject items||items.Count==0||items.Count>12||string.IsNullOrWhiteSpace(set.Value<string>("name")))throw new Exception("Set is empty or invalid.");
  if(items["Weapon"] is null)throw new Exception("Choose a weapon before importing.");
  string[] fields=["MainHand","OffHand","Head","Body","Gloves","", "Legs","Feet","Ears","Neck","Wrists","FingerR","FingerL"];
  foreach(var p in items.Properties()){
   int n=Array.IndexOf(ImportSlots,p.Name);if(n<0||n==5||p.Value is not JObject value)throw new Exception("Unknown gear slot.");
   if(!Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().TryGetRow(value.Value<uint>("id"),out var row))throw new Exception("Unknown item.");
   object category=row.ClassJobCategory.Value;var allowed=category.GetType().GetProperty(doc.Value<string>("job")!);if(allowed?.GetValue(category) is not true)throw new Exception("Item is not usable by this job: "+row.Name.ToString());
   object equip=row.EquipSlotCategory.Value;var field=equip.GetType().GetProperty(fields[n]);if(Convert.ToInt32(field?.GetValue(equip)??0)<=0)throw new Exception("Item is in the wrong slot: "+row.Name.ToString());
   if(value["materia"] is JArray melds&&melds.Count>5)throw new Exception("Too many materia slots.");
  }
 }
 unsafe void CreateImportedSet(JObject doc){
  ValidateImport(doc);if(!Player.IsLoaded)throw new Exception("Log in first.");
  if(Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>().GetRow(Player.ClassJob.RowId).Abbreviation.ToString()!=doc.Value<string>("job"))throw new Exception("Switch to "+doc.Value<string>("job")+" before importing.");
  var module=RaptureGearsetModule.Instance();if(module==null||module->FirstEmptyGearsetSlot()==255)throw new Exception("No free game-set slot.");
  // Build and validate all fields before creating a native entry.
  var values=new RaptureGearsetModule.GearsetItem[13];var items=(JObject)doc["set"]!["items"]!;int total=0;
  foreach(var p in items.Properties()){
   int n=Array.IndexOf(ImportSlots,p.Name);var row=Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(p.Value.Value<uint>("id"));
   values[n].ItemId=row.RowId+(row.CanBeHq&&p.Value.Value<bool?>("forceNq")!=true?1000000u:0u);total+=(int)row.LevelItem.RowId;
   if(p.Value["materia"] is JArray melds)for(int m=0;m<melds.Count;m++){int id=melds[m].Value<int>("id");if(id<=0)continue;bool found=false;foreach(var mat in Data.GetExcelSheet<Lumina.Excel.Sheets.Materia>()){for(int g=0;g<mat.Item.Count;g++)if(mat.Item[g].RowId==id){values[n].Materia[m]=(ushort)mat.RowId;values[n].MateriaGrades[m]=(byte)g;found=true;break;}if(found)break;}if(!found)throw new Exception("Unknown materia.");}
  }
  var name=doc["set"]!.Value<string>("name")!;while(Encoding.UTF8.GetByteCount(name)>47)name=name[..^1];
  int idNew=module->CreateGearset();if(idNew<0||idNew>=100)throw new Exception("The game could not create a gear set.");
  var entry=module->GetGearset(idNew);if(entry==null)throw new Exception("The new game set was not found.");
  for(int n=0;n<13;n++)entry->Items[n]=values[n];
  entry->Name.Clear();Encoding.UTF8.GetBytes(name,entry->Name);entry->ClassJob=(byte)Player.ClassJob.RowId;entry->BannerIndex=0;entry->GlamourSetLink=0;entry->ItemLevel=(short)(total/Math.Max(1,items.Count));module->SaveFile(true);
  importMessage="Created game set #"+(idNew+1)+". Equipped gear is unchanged. Check missing items and materia in the game set list.";
 }
}
