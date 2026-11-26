using Dalamud.Configuration;
namespace XivGearProg;
[Serializable] public sealed class Configuration : IPluginConfiguration {
 public Dictionary<string,string> Retainers {get;set;}=new();
 public HashSet<string> IgnoredJobs {get;set;}=new();
 public HashSet<string> IgnoredSets {get;set;}=new();
 public Dictionary<string,HashSet<string>> CharacterIgnoredJobs {get;set;}=new();
 public Dictionary<string,string> Characters {get;set;}=new();
 public Dictionary<string,string> CharacterSyncStatus {get;set;}=new();
 public Dictionary<string,List<string>> ImportedPlans {get;set;}=new();
 public int Version {get;set;}=1;
 public string Token {get;set;}="";
 public bool AutoSync {get;set;}=true;
}
