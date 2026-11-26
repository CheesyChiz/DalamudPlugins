using XivGearProg;
var local=new Dictionary<string,int>{{"10",1},{"20:nq",1}};var ret=new Dictionary<string,int>{{"10",1}};
void Equal(object actual,object expected){if(!actual.Equals(expected))throw new Exception($"Expected {expected}, got {actual}");}
Equal(PlanOwnership.Take("10",local,ret),1);Equal(PlanOwnership.Take("10",local,ret),2);Equal(PlanOwnership.Take("10",local,ret),0);
Equal(PlanOwnership.Take(PlanOwnership.Key(20,true,false),local,ret),0);Equal(PlanOwnership.Take(PlanOwnership.Key(20,true,true),local,ret),1);
Equal(PlanOwnership.Key(30,false,true),"30");Console.WriteLine("PASS: local gear, retainer gear, missing quantities, duplicate rings, HQ/NQ separation.");
