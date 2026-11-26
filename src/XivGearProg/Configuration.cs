using Dalamud.Configuration;
namespace XivGearProg;
[Serializable] public sealed class Configuration : IPluginConfiguration {
 public int Version {get;set;}=1;
 public string Token {get;set;}="";
 public bool AutoSync {get;set;}=true;
}
