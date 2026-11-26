namespace XivGearProg;
internal static class PlanOwnership {
 public static string Key(uint id,bool canBeHq,bool forceNq)=>id+(canBeHq&&forceNq?":nq":"");
 public static int Take(string key,Dictionary<string,int> local,Dictionary<string,int> retainers){
  if(local.GetValueOrDefault(key)>0){local[key]--;return 1;}
  if(retainers.GetValueOrDefault(key)>0){retainers[key]--;return 2;}
  return 0;
 }
}
