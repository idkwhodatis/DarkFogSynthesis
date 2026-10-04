using System.Diagnostics;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Core.Tests;

// Pure algorithm comparison only. This program does not reference or simulate game types.
Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; tiering={Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "default"}");
int regressionAssertions = 0;
RecipeExecutionContractTests.Run((condition, message) => { regressionAssertions++; if (!condition) throw new Exception(message); });
Console.WriteLine($"PASS actual new contract regression file: {regressionAssertions} assertions");
var recipes = FrozenContent.Recipes;
var byId = recipes.ToDictionary(r => r.Id.Value);
int passed = 0;
foreach (var d in recipes)
{
    int[] req = d.Inputs.Select(i => i.Item.Value).ToArray(), counts = d.Inputs.Select(i => i.Count).ToArray();
    int[] prod = { d.Output.Item.Value }, prodCounts = { d.Output.Count };
    var arrays = new int[]?[] { req, counts, prod, prodCounts };
    Check(d, true, d.TimeSpendTicks * 10000, d.TimeSpendTicks * 100000, arrays);
    Check(d, false, d.TimeSpendTicks * 10000, d.TimeSpendTicks * 100000, arrays);
    Check(d, true, d.TimeSpendTicks * 10000 - 1, d.TimeSpendTicks * 100000, arrays);
    Check(d, true, d.TimeSpendTicks * 10000, d.TimeSpendTicks * 100000 - 1, arrays);
    for (int a = 0; a < 4; a++)
    {
        foreach (int[]? mutation in new int[]?[] { null, Array.Empty<int>(), arrays[a]!.Concat(new[]{0}).ToArray(), arrays[a]![..^1] })
        { var candidate = (int[]?[])arrays.Clone(); candidate[a] = mutation; Check(d,true,d.TimeSpendTicks*10000,d.TimeSpendTicks*100000,candidate); }
        for (int j = 0; j < arrays[a]!.Length; j++)
        { var candidate = (int[]?[])arrays.Clone(); candidate[a]=(int[])arrays[a]!.Clone(); candidate[a]![j]++; Check(d,true,d.TimeSpendTicks*10000,d.TimeSpendTicks*100000,candidate); }
    }
    if (!ReferenceEquals(LookupOld(d.Id.Value), LookupNew(d.Id.Value))) throw new Exception("Lookup identity mismatch");
    passed++;
}
Console.WriteLine($"PASS {passed} comparison/identity cases across actual 6 immutable recipe definitions.");

foreach (int recipeIndex in new[]{0,5})
{
 var d=recipes[recipeIndex];
 var req=d.Inputs.Select(i=>i.Item.Value).ToArray();var counts=d.Inputs.Select(i=>i.Count).ToArray();var prod=new[]{d.Output.Item.Value};var prodCounts=new[]{d.Output.Count};
 Run($"lookup Single ID {d.Id}", ()=>LookupOld(d.Id.Value).Id.Value);
 Run($"lookup Dictionary ID {d.Id}", ()=>LookupNew(d.Id.Value).Id.Value);
 Run($"Execution LINQ ID {d.Id}", ()=>Old(d,true,d.TimeSpendTicks*10000,d.TimeSpendTicks*100000,req,counts,prod,prodCounts)?1:0);
 Run($"Execution indexed ID {d.Id}", ()=>New(d,true,d.TimeSpendTicks*10000,d.TimeSpendTicks*100000,req,counts,prod,prodCounts)?1:0);
}

// Mixed live arrays change between checks; no acceptance result may be memoized.
var mixed = recipes.SelectMany(d => Enumerable.Range(0, 4).Select(kind =>
{
 var req = d.Inputs.Select(i=>i.Item.Value).ToArray();
 var counts=d.Inputs.Select(i=>i.Count).ToArray();
 if (kind==1) req[0]++; if(kind==2) counts[^1]++;
 return (d, kind, req, counts, prod:new[]{d.Output.Item.Value},prodCounts:new[]{d.Output.Count});
})).ToArray();
int caseIndex=0;
Run("Execution LINQ mixed 25% valid", ()=> {var c=mixed[caseIndex++ % mixed.Length];return Old(c.d,c.kind!=3,c.d.TimeSpendTicks*10000,c.d.TimeSpendTicks*100000,c.req,c.counts,c.prod,c.prodCounts)?1:0;});
caseIndex=0;
Run("Execution actual contract mixed 25% valid", ()=> {var c=mixed[caseIndex++ % mixed.Length];return New(c.d,c.kind!=3,c.d.TimeSpendTicks*10000,c.d.TimeSpendTicks*100000,c.req,c.counts,c.prod,c.prodCounts)?1:0;});

RecipeDefinition LookupOld(int recipeId) => FrozenContent.Recipes.Single(r=>r.Id.Value == recipeId);
RecipeDefinition LookupNew(int recipeId) => byId[recipeId];
void Check(RecipeDefinition d, bool productive, int time, int extra, int[]?[] arrays)
{
 if (Old(d,productive,time,extra,arrays[0],arrays[1],arrays[2],arrays[3]) != New(d,productive,time,extra,arrays[0],arrays[1],arrays[2],arrays[3])) throw new Exception("Execution parity mismatch");
 passed++;
}
static bool Old(RecipeDefinition d,bool productive,int time,int extra,int[]? req,int[]? counts,int[]? prod,int[]? prodCounts) => productive &&
 time == d.TimeSpendTicks *10000 && extra == d.TimeSpendTicks*100000 &&
 req != null && req.SequenceEqual(d.Inputs.Select(i=>i.Item.Value)) &&
 counts != null && counts.SequenceEqual(d.Inputs.Select(i=>i.Count)) &&
 prod != null && prod.SequenceEqual(new[]{d.Output.Item.Value}) &&
 prodCounts != null && prodCounts.SequenceEqual(new[]{d.Output.Count});
static bool New(RecipeDefinition d,bool productive,int time,int extra,int[]? req,int[]? counts,int[]? prod,int[]? prodCounts)
    => RecipeExecutionContract.Matches(d,productive,time,extra,req,counts,prod,prodCounts);
static void Run(string name,Func<int> action)
{
 const int n=200000, samples=7;
 for(int i=0;i<20000;i++) action();
 var timings=new double[samples];var allocations=new long[samples];long? expected=null;
 for(int sample=0;sample<samples;sample++)
 {
  GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
  long allocated=GC.GetAllocatedBytesForCurrentThread();long start=Stopwatch.GetTimestamp();long sum=0;
  for(int i=0;i<n;i++) sum+=action();
  timings[sample]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
  allocations[sample]=GC.GetAllocatedBytesForCurrentThread()-allocated;
  if(expected.HasValue && expected.Value!=sum) throw new Exception("Sample checksum changed");
  expected=sum;
 }
 Array.Sort(timings);Array.Sort(allocations);
 Console.WriteLine($"{name}: median {allocations[samples/2]/n} bytes/op; {timings[samples/2]:F2} ms/{n:N0} ops; {samples} samples; guard={expected}");
}
