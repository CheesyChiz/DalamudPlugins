using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace XivGearProg;
public sealed partial class Plugin {
 unsafe void DrawSetFilters(){
  if(!ImGui.CollapsingHeader("Choose gear sets to sync"))return;
  ImGui.TextWrapped("Unchecked jobs or sets are not uploaded. Existing website sets are kept; you can delete them there. Filters apply to this character's saved sets. Unsupported jobs are not uploaded.");
  int column=0;foreach(var job in Jobs.OrderBy(x=>x)){bool enabled=!config.IgnoredJobs.Contains(job);if(ImGui.Checkbox(job+"##jobfilter",ref enabled)){if(enabled)config.IgnoredJobs.Remove(job);else config.IgnoredJobs.Add(job);PI.SavePluginConfig(config);lastPayload="";}if(++column%4!=0)ImGui.SameLine();}ImGui.NewLine();
  if(!Player.IsLoaded)return;var module=RaptureGearsetModule.Instance();if(module==null)return;var jobs=Data.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
  if(ImGui.BeginChild("setfilters",new System.Numerics.Vector2(0,180),true))for(int i=0;i<100;i++){
   if(!module->IsValidGearset(i))continue;var set=module->GetGearset(i);if(set==null)continue;var job=jobs.GetRow(set->ClassJob).Abbreviation.ToString();if(!Jobs.Contains(job))continue;
   var key=Player.ContentId+":"+i;bool enabled=!config.IgnoredSets.Contains(key);ImGui.BeginDisabled(config.IgnoredJobs.Contains(job));if(ImGui.Checkbox($"#{i+1} {set->NameString} ({job})##set{i}",ref enabled)){if(enabled)config.IgnoredSets.Remove(key);else config.IgnoredSets.Add(key);PI.SavePluginConfig(config);lastPayload="";}ImGui.EndDisabled();
  }ImGui.EndChild();
 }
}
