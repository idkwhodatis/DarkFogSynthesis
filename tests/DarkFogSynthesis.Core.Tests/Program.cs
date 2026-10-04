using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Progression;
using DarkFogSynthesis.Core.Registration;

namespace DarkFogSynthesis.Core.Tests
{
    // Dependency-free executable tests of real pure logic. There are deliberately no game-type stubs here.
    internal static class Program
    {
        private static int failures;
        private static int assertions;

        private static int Main()
        {
            var tests = new (string Name, Action Run)[]
            {
                ("D01 exact six frozen recipes", FrozenRecipes),
                ("D02 fixed namespace and content counts", FixedIds),
                ("D04 ideal base rates", BaseRates),
                ("T01 frozen research totals and independent branches", FrozenTechnologies),
                ("T01 candidate ItemPoints encode totals, not rates of 100/300", ResearchEncoding),
                ("D01 immutable definitions and lists", ImmutableDefinitions),
                ("D01 industrial routes, handcraft and native proliferation contract", ProductionContract),
                ("R02 six native research matrices retain their original order", NativeMatrixRegistry),
                ("R02 altered matrix registry fails without repairing another mod", AlteredMatrixRegistry),
                ("R02 native-array and interface matrix checks preserve live validation",
                    () => NativeMatrixContractTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("S06 exact execution cache validation preserves current array checks",
                    () => RecipeExecutionContractTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("C01 unsupported lab replacements are rejected by exact source-backed identity",
                    () => LabRuntimeCompatibilityPolicyTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("U03 native shared buffer identity is checked before snapshots",
                    () => RefundBufferAliasGuardTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("C01 diagnostic failures and session compatibility latches have separate lifecycles",
                    () => SessionLifecycleTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("C01 live progression validates required edges, caches and technology availability",
                    () => LiveProgressionPolicyTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("C01 critical startup entrypoints stay closed through initialization failures",
                    () => StartupGuardTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("S06 incompatible import buffer shapes are rejected before repair", ImportedBufferShapes),
                ("S06 buffer shape checks preserve all original quantities and points", BufferShapeCheckIsPure),
                ("D01 typed ID equality and invalid arguments", TypedIds),
                ("D03 all four configuration combinations", TruthTable),
                ("T03 exact hidden/combat mapping", CombatMapping),
                ("S04 peace-combat-peace restoration", CrossSaveIsolation),
                ("S03 ten repeated session loads are idempotent", RepeatedSessionLoads),
                ("S04 existing vanilla or third-party edges stay unowned", PreexistingEdges),
                ("S04 later unrelated third-party edges survive restore", ThirdPartyAppend),
                ("S04 later duplicate third-party edge survives single removal", ThirdPartyDuplicate),
                ("S04 ambiguous ownership fails atomically", AmbiguousOwnership),
                ("S04 already externally removed own edge is harmless", AlreadyRemoved),
                ("S04 missing target fails closed", MissingTargets),
                ("S04 no baseline overwrite or unrelated-tech changes", NarrowPolicyScope),
                ("S04 policy plans and ownership snapshots are immutable", ImmutablePolicyPlans),
                ("S04 planner does not mutate caller snapshots", PolicyPlanningIsPure),
                ("S04 randomized ownership preservation", RandomizedPolicyPreservation),
                ("S04 reverse-cache own append and exact restoration", ReverseOwnAppend),
                ("S04 preexisting reverse-cache links remain unowned", ReversePreexisting),
                ("S04 surviving foreign forward edge retains its reverse link", ReverseForeignForwardSurvives),
                ("S04 reverse-cache ownership drift fails without mutation", ReverseAmbiguous),
                ("S04 reverse-cache repeated plans are idempotent", ReverseRepeated),
                ("S04 unowned forward edge cannot acquire reverse ownership", ReverseUnowned),
                ("S04 reverse-cache plans and evidence are immutable", ReverseImmutable),
                ("S04 integrated forward/reverse foreign-duplicate restoration", CombinedForwardReverseRestoration),
                ("T02/T05 pure edits cannot express discovery/research gifts", NoDiscoveryOrResearchActions),
                ("S02 late vanilla progress only repairs four advanced recipes", OldSaveReconciliation),
                ("S03 reconciliation is idempotent across ten loads", RepeatedReconciliation),
                ("S02 exhaustive completed/unlocked recipe combinations", ExhaustiveReconciliation),
                ("S05 partial/custom research is never automatically completed", NoFreeCustomResearch),
                ("S02 reconciliation leaves caller progress and unlocks unchanged", ReconciliationIsPure),
                ("C02 all fixed ID collisions fail, no reassignment", CollisionGuard),
                ("V01 no version currently claims verified layout", CandidateLayoutOnly),
                ("V01 measured bounds cover expanded states and edge contact", MeasuredLayoutCollisions),
                ("V01 pending and inserted main-tree anchors both collide", AnchorPendingCollisions),
                ("V01 main-page boundary is separate from upgrade coordinates", AnchorPageBoundary),
                ("V01 own anchor ignored and duplicate peers reported once", AnchorOwnAndDuplicateIds),
                ("V01 anchor checks are exact and do not certify bounds", AnchorExactOnly),
                ("V01 anchor invalid arguments are rejected", AnchorInvalidArguments),
                ("C02 checked-in fixed ID manifest matches executable definitions", ProtoManifest),
                ("V01 both localization resources cover all content keys", LocalizationCoverage),
                ("U03 preflight allows only empty production state", RemovalEmptyProduction),
                ("U03 every production scalar and replicating flag blocks", RemovalProductionScalars),
                ("U03 inc-only/output-only/multiple-buffer state blocks", RemovalProductionBuffers),
                ("U03 negative resource/progress values fail closed", RemovalNegativeState),
                ("U03 research bytes and matrix buffers block", RemovalResearchState),
                ("U03 each custom queued/current ID blocks", RemovalCustomQueues),
                ("U03 unknown or invalid queue snapshots fail closed", RemovalUnknownQueues),
                ("U03 preflight predicates do not mutate inputs", RemovalPreflightIsPure),
                ("U03 required frameworks do not trigger the peer blocker", PeerFrameworksDoNotBlock),
                ("U03 peer GUID identity uses exact ordinal matching", PeerGuidIdentity),
                ("U03 known peer blocks without a version exemption", PeerUnknownVersionsBlock),
                ("U03 peer blocker names the mod and explains uncovered saves", PeerBlockerReason),
                ("U03 peer inventory and blocker results are immutable", PeerInventoryIsPure),
                ("U03 invalid peer inventories fail closed", PeerInvalidInventories),
                ("U03 refund ledger preserves exact idle inventory and rejects uncertain state",
                    () => RefundLedgerTests.Run((condition,message) => { assertions++; if (!condition) throw new Exception(message); })),
                ("C02 invalid arguments are rejected", InvalidArguments)
            };
            foreach (var test in tests)
            {
                try { test.Run(); Console.WriteLine("PASS " + test.Name); }
                catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + test.Name + ": " + ex); }
            }
            Console.WriteLine($"{tests.Length - failures}/{tests.Length} pure tests passed; {assertions} assertions.");
            Console.WriteLine("Scope: pure definitions, edit planning, removal preflight and geometry only. Game registration, device behavior, discovery, costs, saves, removal, achievements, Metadata and online eligibility are NOT verified here.");
            return failures == 0 ? 0 : 1;
        }

        private static void FrozenRecipes()
        {
            // Literal independent oracle from the frozen plan and historical vanilla prototype export.
            var expected = new[]
            {
                ("energy_shard", 48101, 5206, 2, 120, 1951, new[] { (1128,1), (1109,1), (1110,1) }),
                ("dark_fog_matrix", 48102, 5201, 1, 240, 1952, new[] { (1113,2), (1404,1), (1401,1), (1119,1) }),
                ("silicon_neuron", 48103, 5202, 1, 240, 1312, new[] { (1302,2), (1402,1), (1113,2) }),
                ("matter_recombinator", 48104, 5203, 1, 360, 1203, new[] { (1304,1), (1205,2), (1120,2), (1113,2) }),
                ("negentropy_singularity", 48105, 5204, 1, 480, 1417, new[] { (1127,1), (1126,2), (1802,1), (1113,2) }),
                ("core_element", 48106, 5205, 1, 600, 1145, new[] { (1122,2), (1125,2), (1205,2), (1113,4) })
            };
            Equal(6, FrozenContent.Recipes.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                var r = FrozenContent.Recipes[i]; var e = expected[i];
                Equal(e.Item1, r.Key); Equal(e.Item2, r.Id.Value); Equal(e.Item3, r.Output.Item.Value);
                Equal(e.Item4, r.Output.Count); Equal(e.Item5, r.TimeSpendTicks); Equal(e.Item6, r.UnlockTech.Value);
                Sequence(e.Item7, r.Inputs.Select(input => (input.Item.Value, input.Count)));
            }
        }

        private static void NativeMatrixRegistry()
        {
            True(NativeMatrixContract.IsSupported(new[] {6001,6002,6003,6004,6005,6006}));
            True(!NativeMatrixContract.IsSupported(null));
            True(!NativeMatrixContract.IsSupported(Array.Empty<int>()));
            True(!NativeMatrixContract.IsSupported(new[] {6001,6002,6003,6004,6005}));
        }

        private static void ImportedBufferShapes()
        {
            foreach (var recipe in FrozenContent.Recipes)
            {
                int n = recipe.Inputs.Count;
                True(RecipeBufferContract.Matches(recipe,new int[n],new int[n],new int[1]));
                True(!RecipeBufferContract.Matches(recipe,null,new int[n],new int[1]));
                True(!RecipeBufferContract.Matches(recipe,new int[n],null,new int[1]));
                True(!RecipeBufferContract.Matches(recipe,new int[n],new int[n],null));
                True(!RecipeBufferContract.Matches(recipe,new int[n-1],new int[n],new int[1]));
                True(!RecipeBufferContract.Matches(recipe,new int[n],new int[n+1],new int[1]));
                True(!RecipeBufferContract.Matches(recipe,new int[n],new int[n],new int[2]));
            }
            Throws<ArgumentNullException>(() => RecipeBufferContract.Matches(null!,null,null,null));
        }

        private static void BufferShapeCheckIsPure()
        {
            var recipe = FrozenContent.Recipes[0];
            var served = new[] {4,5,6}; var points = new[] {7,8,9}; var produced = new[] {2};
            True(RecipeBufferContract.Matches(recipe,served,points,produced));
            Sequence(new[] {4,5,6},served); Sequence(new[] {7,8,9},points); Sequence(new[] {2},produced);
            True(!RecipeBufferContract.Matches(recipe,served,points,new[] {2,3}));
            Sequence(new[] {4,5,6},served); Sequence(new[] {7,8,9},points);
        }

        private static void AlteredMatrixRegistry()
        {
            var ids = new[] {6001,6002,6003,6004,6005,6006};
            var before = ids.ToArray();
            for (int i = 0; i < ids.Length; ++i)
            {
                var changed = ids.ToArray(); changed[i] = 5201;
                var snapshot = changed.ToArray();
                True(!NativeMatrixContract.IsSupported(changed)); Sequence(snapshot,changed);
            }
            True(!NativeMatrixContract.IsSupported(Enumerable.Reverse(ids).ToArray()));
            True(!NativeMatrixContract.IsSupported(ids.Concat(new[] {6007}).ToArray()));
            Sequence(before,ids);
        }

        private static void FixedIds()
        {
            Sequence(new[] {1951,1952}, FrozenContent.Technologies.Select(t => t.Id.Value));
            Sequence(Enumerable.Range(48101,6), FrozenContent.Recipes.Select(r => r.Id.Value));
            Equal(2, FrozenContent.Technologies.Count); Equal(4, FrozenContent.CombatPrerequisites.Count);
            Equal("dark_fog_synthesis", ProtoIds.Namespace); Equal(1, ProtoIds.SchemaVersion);
            Equal("142cc01517060be96be77e687d44512d756de851", VanillaIds.SourceCommit);
            var keys = FrozenContent.Recipes.Select(r => r.NameKey).Concat(FrozenContent.Technologies
                .SelectMany(t => new[] {t.NameKey,t.DescriptionKey,t.ConclusionKey})).ToArray();
            Equal(keys.Length, keys.Distinct().Count()); True(keys.All(key => key.StartsWith("dark_fog_synthesis.", StringComparison.Ordinal)));
            True(FrozenContent.Technologies.All(t => t.Id.Value < 2000));
            // All six outputs are existing vanilla IDs, no new item or building definition type exists.
            Sequence(new[] {5201,5202,5203,5204,5205,5206}, FrozenContent.Recipes.Select(r => r.Output.Item.Value).OrderBy(id => id));
            True(!typeof(FrozenContent).Assembly.GetTypes().Any(type => type.Name == "ItemDefinition" || type.Name == "BuildingDefinition"));
        }

        private static void BaseRates()
        {
            Sequence(new[] {60.0,15.0,15.0,10.0,7.5,6.0}, FrozenContent.Recipes.Select(r => r.BaseOutputPerMinute));
            Sequence(new[] {2.0,4.0,4.0,6.0,8.0,10.0}, FrozenContent.Recipes.Select(r => r.BaseSeconds));
            True(FrozenContent.Recipes.All(r => r.TicksPerSecond == 60));
        }

        private static void FrozenTechnologies()
        {
            var energy = FrozenContent.Technologies[0]; var info = FrozenContent.Technologies[1];
            Equal("energy_analysis", energy.Key); Equal("information_topology", info.Key);
            Sequence(new[] {1826}, energy.ExplicitPrerequisites.Select(id => id.Value)); Equal(0, energy.ImplicitPrerequisites.Count);
            Sequence(new[] {1808}, info.ExplicitPrerequisites.Select(id => id.Value));
            Sequence(new[] {1124}, info.ImplicitPrerequisites.Select(id => id.Value));
            Sequence(new[] {(6001,100)}, energy.ResearchCost.Select(x => (x.Item.Value,x.Count)));
            Sequence(new[] {(6001,300),(6002,300),(6003,300)}, info.ResearchCost.Select(x => (x.Item.Value,x.Count)));
            Equal(36000L, energy.HashNeeded); Equal(180000L, info.HashNeeded);
            Sequence(new[] {48101}, energy.UnlockRecipes.Select(x => x.Value)); Sequence(new[] {48102}, info.UnlockRecipes.Select(x => x.Value));
            Equal(new TechPosition(21,-55), energy.CandidatePosition); Equal(new TechPosition(29,-43), info.CandidatePosition);
            True(!info.ExplicitPrerequisites.Contains(energy.Id) && !info.ImplicitPrerequisites.Contains(energy.Id));
            True(FrozenContent.Technologies.All(t => t.ResearchCost.All(x => x.Item.Value >= 6001 && x.Item.Value <= 6003)));
        }

        private static void ResearchEncoding()
        {
            Sequence(new[] {10}, FrozenContent.Technologies[0].CandidateItemPoints);
            Sequence(new[] {6,6,6}, FrozenContent.Technologies[1].CandidateItemPoints);
            foreach (var tech in FrozenContent.Technologies)
                for (int i = 0; i < tech.ResearchCost.Count; i++)
                    Equal((long)tech.ResearchCost[i].Count, tech.HashNeeded * tech.CandidateItemPoints[i] / 3600L);
        }

        private static void ImmutableDefinitions()
        {
            ReadOnly(FrozenContent.Recipes); ReadOnly(FrozenContent.Technologies); ReadOnly(FrozenContent.CombatPrerequisites);
            foreach (var recipe in FrozenContent.Recipes) ReadOnly(recipe.Inputs);
            foreach (var tech in FrozenContent.Technologies)
            {
                ReadOnly(tech.ResearchCost); ReadOnly(tech.ExplicitPrerequisites); ReadOnly(tech.ImplicitPrerequisites);
                ReadOnly(tech.CandidateItemPoints); ReadOnly(tech.UnlockRecipes);
            }
            foreach (var type in new[] {typeof(RecipeDefinition),typeof(TechDefinition)})
                True(type.GetProperties().All(p => p.SetMethod == null));
        }

        private static void ProductionContract()
        {
            Sequence(new[] {ProductionMachine.Smelter,ProductionMachine.MatrixLab,ProductionMachine.Assembler,
                ProductionMachine.Assembler,ProductionMachine.Assembler,ProductionMachine.Assembler}, FrozenContent.Recipes.Select(r => r.Machine));
            True(FrozenContent.Recipes.All(r => r.Handcraft && r.SupportsAcceleration && r.SupportsExtraProducts));
            True(FrozenContent.Recipes[1].Inputs.All(x => x.Item.Value < 6001 || x.Item.Value > 6006));
            True(!FrozenContent.Recipes[3].Inputs.Any(x => x.Item == VanillaIds.Items.StrangeMatter));
            True(FrozenContent.Recipes.All(r => r.Inputs.All(x => x.Item.Value < 5201 || x.Item.Value > 5206)));
        }

        private static void TypedIds()
        {
            Equal(new ItemId(1), new ItemId(1)); True(new ItemId(1) != new ItemId(2));
            Equal(new TechId(1),new TechId(1)); True(new TechId(1) != new TechId(2));
            Equal(new RecipeId(1),new RecipeId(1)); True(new RecipeId(1) != new RecipeId(2));
            True(!new ItemId(1).Equals((object)new TechId(1)));
            Equal("1951", ProtoIds.EnergyAnalysis.ToString());
            Equal(1, new HashSet<ItemId> {new ItemId(1),new ItemId(1)}.Count);
            Throws<ArgumentOutOfRangeException>(() => new ItemId(0)); Throws<ArgumentOutOfRangeException>(() => new RecipeId(-1));
            Throws<ArgumentOutOfRangeException>(() => new TechId(0)); Throws<ArgumentOutOfRangeException>(() => new ItemAmount(default,1));
            Throws<ArgumentOutOfRangeException>(() => new ItemAmount(new ItemId(1),0));
        }

        private static void TruthTable()
        {
            True(!ProgressionPolicy.DefaultApplyCombatPrerequisitesInNonPeaceMode);
            foreach (bool peace in new[] {false,true}) foreach (bool setting in new[] {false,true})
            {
                Equal(peace || setting, ProgressionPolicy.ShouldApply(peace,setting));
                var plan = ProgressionPolicy.PlanSession(Baseline(),OwnedPrerequisiteChanges.Empty,peace,setting);
                True(plan.CanApply); Equal(peace || setting ? 4 : 0, plan.Edits.Count);
                Equal(peace || setting ? 4 : 0, plan.OwnedChanges.Entries.Count);
                Equal(6, FrozenContent.Recipes.Count); Equal(2, FrozenContent.Technologies.Count);
            }
        }

        private static void CombatMapping()
            => Sequence(new[] {(1901,1820),(1902,1811),(1903,1809),(1904,1818)},
                FrozenContent.CombatPrerequisites.Select(p => (p.HiddenTech.Value,p.RequiredCombatTech.Value)));

        private static void CrossSaveIsolation()
        {
            var original = Baseline(); var current = Clone(original); var owned = OwnedPrerequisiteChanges.Empty;
            foreach (bool peace in new[] {true,false,true,false})
            {
                var plan = ProgressionPolicy.PlanSession(current,owned,peace,false); Apply(current,plan); owned = plan.OwnedChanges;
                foreach (var pair in FrozenContent.CombatPrerequisites)
                    Equal(peace, current[pair.HiddenTech].Contains(pair.RequiredCombatTech));
            }
            GraphEqual(original,current); Equal(0,owned.Entries.Count);
        }

        private static void RepeatedSessionLoads()
        {
            var current = Baseline(); var owned = OwnedPrerequisiteChanges.Empty;
            for (int i = 0; i < 10; i++)
            {
                var plan = ProgressionPolicy.PlanSession(current,owned,true,false);
                Equal(i == 0 ? 4 : 0,plan.Edits.Count); Apply(current,plan); owned = plan.OwnedChanges;
                foreach (var p in FrozenContent.CombatPrerequisites) Equal(1,current[p.HiddenTech].Count(id => id == p.RequiredCombatTech));
            }
            var restore = ProgressionPolicy.PlanRestore(current,owned); Apply(current,restore); GraphEqual(Baseline(),current);
            var again = ProgressionPolicy.PlanRestore(current,restore.OwnedChanges); Equal(0,again.Edits.Count); True(again.CanApply);
        }

        private static void PreexistingEdges()
        {
            var current = Baseline(); var edge = FrozenContent.CombatPrerequisites[0];
            current[edge.HiddenTech] = current[edge.HiddenTech].Concat(new[] {edge.RequiredCombatTech,edge.RequiredCombatTech}).ToArray();
            var original = Clone(current); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false);
            Equal(3,plan.OwnedChanges.Entries.Count); Apply(current,plan);
            var restore = ProgressionPolicy.PlanRestore(current,plan.OwnedChanges); Apply(current,restore); GraphEqual(original,current);
        }

        private static void ThirdPartyAppend()
        {
            var current = Baseline(); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false); Apply(current,plan);
            var target = FrozenContent.CombatPrerequisites[0].HiddenTech; current[target] = current[target].Concat(new[] {new TechId(9001)}).ToArray();
            var restore = ProgressionPolicy.PlanRestore(current,plan.OwnedChanges); Apply(current,restore);
            Sequence(new[] {1101,1102,9001},current[target].Select(x => x.Value));
        }

        private static void ThirdPartyDuplicate()
        {
            var current = Baseline(); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false); Apply(current,plan);
            var edge = FrozenContent.CombatPrerequisites[0]; current[edge.HiddenTech] = current[edge.HiddenTech].Concat(new[] {edge.RequiredCombatTech}).ToArray();
            var restore = ProgressionPolicy.PlanRestore(current,plan.OwnedChanges); Apply(current,restore);
            Equal(1,current[edge.HiddenTech].Count(id => id == edge.RequiredCombatTech));
            var next = ProgressionPolicy.PlanSession(current,restore.OwnedChanges,true,false);
            Equal(3,next.OwnedChanges.Entries.Count);
        }

        private static void AmbiguousOwnership()
        {
            var current = Baseline(); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false); Apply(current,plan);
            var edge = FrozenContent.CombatPrerequisites[0]; current[edge.HiddenTech] = new[] {new TechId(9001)}.Concat(current[edge.HiddenTech]).ToArray();
            var before = Clone(current); var restore = ProgressionPolicy.PlanRestore(current,plan.OwnedChanges);
            True(!restore.CanApply); Equal(0,restore.Edits.Count); Equal(1,restore.Conflicts.Count);
            True(ReferenceEquals(plan.OwnedChanges,restore.OwnedChanges)); GraphEqual(before,current);
            var switchSave = ProgressionPolicy.PlanSession(current,plan.OwnedChanges,false,false);
            True(!switchSave.CanApply); Equal(0,switchSave.Edits.Count); GraphEqual(before,current);
        }

        private static void AlreadyRemoved()
        {
            var current = Baseline(); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false); Apply(current,plan);
            var edge = FrozenContent.CombatPrerequisites[0]; current[edge.HiddenTech] = new[] {new TechId(9001)};
            var restore = ProgressionPolicy.PlanRestore(current,plan.OwnedChanges); True(restore.CanApply); Apply(current,restore);
            Sequence(new[] {9001},current[edge.HiddenTech].Select(x => x.Value)); Equal(0,restore.OwnedChanges.Entries.Count);
        }

        private static void MissingTargets()
        {
            var current = Baseline(); current.Remove(FrozenContent.CombatPrerequisites[0].HiddenTech);
            foreach (bool peace in new[] {true,false})
            {
                var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,peace,false);
                True(!plan.CanApply); Equal(0,plan.Edits.Count);
            }
            var complete = Baseline(); var started = ProgressionPolicy.PlanSession(complete,OwnedPrerequisiteChanges.Empty,true,false); Apply(complete,started);
            complete.Remove(FrozenContent.CombatPrerequisites[1].HiddenTech);
            True(!ProgressionPolicy.PlanRestore(complete,started.OwnedChanges).CanApply);
        }

        private static void NarrowPolicyScope()
        {
            var current = Baseline();
            foreach (var r in FrozenContent.Recipes) current[r.UnlockTech] = new[] {new TechId(9010)};
            var before = Clone(current); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false); Apply(current,plan);
            True(plan.Edits.All(edit => FrozenContent.CombatPrerequisites.Any(edge => edge.HiddenTech == edit.Tech)));
            foreach (var r in FrozenContent.Recipes) Sequence(before[r.UnlockTech],current[r.UnlockTech]);
            Apply(current,ProgressionPolicy.PlanRestore(current,plan.OwnedChanges)); GraphEqual(before,current);
        }

        private static void ImmutablePolicyPlans()
        {
            var current = Baseline(); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false);
            ReadOnly(plan.Edits); ReadOnly(plan.OwnedChanges.Entries); ReadOnly(plan.Conflicts);
            foreach (var edit in plan.Edits) {ReadOnly(edit.Before); ReadOnly(edit.After);}
            foreach (var entry in plan.OwnedChanges.Entries) ReadOnly(entry.Baseline);
            var key = FrozenContent.CombatPrerequisites[0].HiddenTech;
            ((TechId[])current[key])[0] = new TechId(9999); Equal(1101,plan.OwnedChanges.Entries[0].Baseline[0].Value);
        }

        private static void PolicyPlanningIsPure()
        {
            var current = Baseline(); var before = Clone(current);
            var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false);
            GraphEqual(before,current); Equal(4,plan.Edits.Count);
            Apply(current,plan); before = Clone(current);
            ProgressionPolicy.PlanRestore(current,plan.OwnedChanges); GraphEqual(before,current);
        }

        private static void RandomizedPolicyPreservation()
        {
            var random = new Random(94721);
            for (int n = 0; n < 200; n++)
            {
                var current = Baseline();
                foreach (var pair in FrozenContent.CombatPrerequisites)
                {
                    var list = current[pair.HiddenTech].Concat(Enumerable.Range(0,random.Next(0,8)).Select(_ => new TechId(random.Next(8000,8100)))).ToList();
                    if (random.Next(2) == 0) list.Add(pair.RequiredCombatTech);
                    current[pair.HiddenTech] = list.ToArray();
                }
                var original = Clone(current); var plan = ProgressionPolicy.PlanSession(current,OwnedPrerequisiteChanges.Empty,true,false); Apply(current,plan);
                foreach (var pair in FrozenContent.CombatPrerequisites)
                {
                    var suffix = new[] {new TechId(random.Next(9000,9100)),new TechId(random.Next(9000,9100))};
                    current[pair.HiddenTech] = current[pair.HiddenTech].Concat(suffix).ToArray();
                    original[pair.HiddenTech] = original[pair.HiddenTech].Concat(suffix).ToArray();
                }
                Apply(current,ProgressionPolicy.PlanRestore(current,plan.OwnedChanges)); GraphEqual(original,current);
            }
        }

        private static void NoDiscoveryOrResearchActions()
        {
            // Structural safety boundary, NOT proof that native discovery cannot be indirectly affected.
            Sequence(new[] {"Tech","Before","After"}, typeof(PrerequisiteEdit).GetProperties().Select(p => p.Name));
            True(typeof(PrerequisiteEdit).GetProperties().All(p => p.PropertyType == typeof(TechId) || p.PropertyType == typeof(IReadOnlyList<TechId>)));
            Equal(typeof(IReadOnlyList<RecipeId>),typeof(SaveReconciler).GetMethod(nameof(SaveReconciler.PlanMissingRecipes))!.ReturnType);
            True(typeof(ProgressionPolicy).GetMethods().Where(m => m.DeclaringType == typeof(ProgressionPolicy))
                .All(m => m.ReturnType == typeof(bool) || m.ReturnType == typeof(PrerequisitePlan)));
        }

        private static void ReverseOwnAppend()
        {
            var current = new[] {new TechId(1101),new TechId(1102)}; var child = new TechId(1901);
            var add = ReversePrerequisitePolicy.Plan(current,child,null,true,true);
            True(add.CanApply && add.HasEdit && add.OwnedChange != null);
            Sequence(new[] {1101,1102,1901},add.After.Select(id => id.Value));
            var restore = ReversePrerequisitePolicy.Plan(add.After,child,add.OwnedChange,false,false);
            True(restore.CanApply && restore.HasEdit && restore.OwnedChange == null); Sequence(current,restore.After);
            Sequence(new[] {1101,1102},current.Select(id => id.Value));
        }

        private static void ReversePreexisting()
        {
            var child = new TechId(1901); var current = new[] {new TechId(1101),child,child};
            var add = ReversePrerequisitePolicy.Plan(current,child,null,true,true);
            True(add.CanApply && !add.HasEdit && add.OwnedChange == null);
            var restore = ReversePrerequisitePolicy.Plan(add.After,child,add.OwnedChange,false,false);
            True(restore.CanApply && !restore.HasEdit && restore.OwnedChange == null); Sequence(current,restore.After);
        }

        private static void ReverseForeignForwardSurvives()
        {
            var child = new TechId(1901); var add = ReversePrerequisitePolicy.Plan(Array.Empty<TechId>(),child,null,true,true);
            var relinquish = ReversePrerequisitePolicy.Plan(add.After,child,add.OwnedChange,false,true);
            True(relinquish.CanApply && !relinquish.HasEdit && relinquish.OwnedChange == null);
            Sequence(new[] {child},relinquish.After);
            var next = ReversePrerequisitePolicy.Plan(relinquish.After,child,relinquish.OwnedChange,true,true);
            True(next.CanApply && !next.HasEdit && next.OwnedChange == null);
            var exit = ReversePrerequisitePolicy.Plan(next.After,child,next.OwnedChange,false,false);
            True(exit.CanApply && !exit.HasEdit && exit.OwnedChange == null); Sequence(new[] {child},exit.After);
        }

        private static void ReverseAmbiguous()
        {
            var child = new TechId(1901); var add = ReversePrerequisitePolicy.Plan(Array.Empty<TechId>(),child,null,true,true);
            foreach (var changed in new[] {new[] {child,new TechId(9999)},new[] {child,child},Array.Empty<TechId>()})
            {
                var restore = ReversePrerequisitePolicy.Plan(changed,child,add.OwnedChange,false,false);
                True(!restore.CanApply && !restore.HasEdit); Sequence(changed,restore.After);
                True(ReferenceEquals(add.OwnedChange,restore.OwnedChange));
            }
        }

        private static void ReverseRepeated()
        {
            var child = new TechId(1901); var plan = ReversePrerequisitePolicy.Plan(Array.Empty<TechId>(),child,null,true,true);
            for (int n = 0; n < 10; n++)
            {
                plan = ReversePrerequisitePolicy.Plan(plan.After,child,plan.OwnedChange,true,true);
                True(plan.CanApply && !plan.HasEdit && plan.OwnedChange != null); Equal(1,plan.After.Count);
            }
            plan = ReversePrerequisitePolicy.Plan(plan.After,child,plan.OwnedChange,false,false);
            Equal(0,plan.After.Count); True(plan.OwnedChange == null);
        }

        private static void ReverseUnowned()
        {
            foreach (bool exists in new[] {false,true})
            {
                var plan = ReversePrerequisitePolicy.Plan(Array.Empty<TechId>(),new TechId(1901),null,false,exists);
                True(plan.CanApply && !plan.HasEdit && plan.OwnedChange == null);
            }
            Throws<ArgumentException>(() => ReversePrerequisitePolicy.Plan(Array.Empty<TechId>(),new TechId(1901),null,true,false));
        }

        private static void ReverseImmutable()
        {
            var current = new[] {new TechId(1101)}; var plan = ReversePrerequisitePolicy.Plan(current,new TechId(1901),null,true,true);
            ReadOnly(plan.Before); ReadOnly(plan.After); ReadOnly(plan.OwnedChange!.Baseline); ReadOnly(plan.OwnedChange.Expected);
            current[0] = new TechId(9999); Equal(1101,plan.Before[0].Value); Equal(1101,plan.OwnedChange.Baseline[0].Value);
            Throws<ArgumentNullException>(() => ReversePrerequisitePolicy.Plan(null!,new TechId(1901),null,false,false));
        }

        private static void CombinedForwardReverseRestoration()
        {
            var graph = Baseline(); var edge = FrozenContent.CombatPrerequisites[0];
            var peace = ProgressionPolicy.PlanSession(graph,OwnedPrerequisiteChanges.Empty,true,false); Apply(graph,peace);
            var reverse = ReversePrerequisitePolicy.Plan(Array.Empty<TechId>(),edge.HiddenTech,null,true,true);
            // A second actor legitimately appends the same prerequisite while this mod owns its original slot.
            graph[edge.HiddenTech] = graph[edge.HiddenTech].Concat(new[] {edge.RequiredCombatTech}).ToArray();
            var combat = ProgressionPolicy.PlanSession(graph,peace.OwnedChanges,false,false); Apply(graph,combat);
            Equal(1,graph[edge.HiddenTech].Count(id => id == edge.RequiredCombatTech));
            reverse = ReversePrerequisitePolicy.Plan(reverse.After,edge.HiddenTech,reverse.OwnedChange,
                combat.OwnedChanges.Entries.Any(o => o.Tech == edge.HiddenTech),graph[edge.HiddenTech].Contains(edge.RequiredCombatTech));
            True(reverse.CanApply && !reverse.HasEdit && reverse.OwnedChange == null);
            Sequence(new[] {edge.HiddenTech},reverse.After);
            // Re-entering peace does not claim either foreign edge; exiting again preserves them.
            peace = ProgressionPolicy.PlanSession(graph,combat.OwnedChanges,true,false); Apply(graph,peace);
            True(!peace.OwnedChanges.Entries.Any(o => o.Tech == edge.HiddenTech));
            combat = ProgressionPolicy.PlanSession(graph,peace.OwnedChanges,false,false); Apply(graph,combat);
            reverse = ReversePrerequisitePolicy.Plan(reverse.After,edge.HiddenTech,reverse.OwnedChange,false,true);
            Sequence(new[] {edge.HiddenTech},reverse.After);
        }

        private static void OldSaveReconciliation()
        {
            var vanillaCompleted = FrozenContent.Recipes.Skip(2).Select(r => r.UnlockTech).ToArray();
            Sequence(new[] {48103,48104,48105,48106}, SaveReconciler.PlanMissingRecipes(vanillaCompleted,Array.Empty<RecipeId>()).Select(x => x.Value));
            var hiddenOnly = FrozenContent.CombatPrerequisites.Select(x => x.HiddenTech);
            Equal(0,SaveReconciler.PlanMissingRecipes(hiddenOnly,Array.Empty<RecipeId>()).Count);
            var combatOnly = FrozenContent.CombatPrerequisites.Select(x => x.RequiredCombatTech);
            Equal(0,SaveReconciler.PlanMissingRecipes(combatOnly,Array.Empty<RecipeId>()).Count);
        }

        private static void RepeatedReconciliation()
        {
            var completed = FrozenContent.Recipes.Select(r => r.UnlockTech).ToArray(); var unlocked = new HashSet<RecipeId>();
            for (int i = 0; i < 10; i++)
            {
                var missing = SaveReconciler.PlanMissingRecipes(completed,unlocked); Equal(i == 0 ? 6 : 0,missing.Count);
                unlocked.UnionWith(missing);
            }
            Equal(6,unlocked.Count);
        }

        private static void ExhaustiveReconciliation()
        {
            for (int completedMask = 0; completedMask < 64; completedMask++)
                for (int unlockedMask = 0; unlockedMask < 64; unlockedMask++)
                {
                    var completed = Enumerable.Range(0,6).Where(i => (completedMask & (1 << i)) != 0).Select(i => FrozenContent.Recipes[i].UnlockTech);
                    var unlocked = Enumerable.Range(0,6).Where(i => (unlockedMask & (1 << i)) != 0).Select(i => FrozenContent.Recipes[i].Id);
                    var expected = Enumerable.Range(0,6).Where(i => (completedMask & (1 << i)) != 0 && (unlockedMask & (1 << i)) == 0)
                        .Select(i => FrozenContent.Recipes[i].Id);
                    Sequence(expected,SaveReconciler.PlanMissingRecipes(completed,unlocked));
                }
        }

        private static void NoFreeCustomResearch()
        {
            Equal(0,SaveReconciler.PlanMissingRecipes(Array.Empty<TechId>(),Array.Empty<RecipeId>()).Count);
            var prerequisites = FrozenContent.Technologies.SelectMany(t => t.ExplicitPrerequisites.Concat(t.ImplicitPrerequisites));
            Equal(0,SaveReconciler.PlanMissingRecipes(prerequisites,Array.Empty<RecipeId>()).Count);
            Sequence(new[] {ProtoIds.EnergyShard},SaveReconciler.PlanMissingRecipes(new[] {ProtoIds.EnergyAnalysis},Array.Empty<RecipeId>()));
            Sequence(new[] {ProtoIds.DarkFogMatrix},SaveReconciler.PlanMissingRecipes(new[] {ProtoIds.InformationTopology},Array.Empty<RecipeId>()));
        }

        private static void ReconciliationIsPure()
        {
            var completed = FrozenContent.Recipes.Select(r => r.UnlockTech).ToList(); var unlocked = new List<RecipeId> {new RecipeId(5)};
            var beforeCompleted = completed.ToArray(); var beforeUnlocked = unlocked.ToArray();
            var result = SaveReconciler.PlanMissingRecipes(completed,unlocked); Sequence(beforeCompleted,completed); Sequence(beforeUnlocked,unlocked);
            ReadOnly(result); Equal(6,result.Count);
        }

        private static void CollisionGuard()
        {
            ProtoIdCollisionGuard.AssertVacant(_ => false,_ => false);
            foreach (var tech in FrozenContent.Technologies)
            {
                var error = Throws<InvalidOperationException>(() => ProtoIdCollisionGuard.AssertVacant(id => id == tech.Id,_ => false));
                True(error.Message.Contains(tech.Id.ToString(),StringComparison.Ordinal));
            }
            foreach (var recipe in FrozenContent.Recipes)
            {
                var error = Throws<InvalidOperationException>(() => ProtoIdCollisionGuard.AssertVacant(_ => false,id => id == recipe.Id));
                True(error.Message.Contains(recipe.Id.ToString(),StringComparison.Ordinal));
            }
            var all = Throws<InvalidOperationException>(() => ProtoIdCollisionGuard.AssertVacant(_ => true,_ => true));
            foreach (var id in new[] {1951,1952,48101,48102,48103,48104,48105,48106}) True(all.Message.Contains(id.ToString(),StringComparison.Ordinal));
            FixedIds();
        }

        private static void CandidateLayoutOnly()
        {
            foreach (var version in new[] {VanillaIds.SourceGameVersion,"unknown","future-version"})
            {
                var result = TechLayoutResolver.Resolve(version); Equal(version,result.GameVersion);
                True(!result.IsVerified && result.DiagnosticCopyOnly); Equal(2,result.Candidates.Count);
                Sequence(new[] {1951,1952},result.Candidates.Select(c => c.Tech.Value));
                Equal(new TechPosition(21,-55),result.Candidates[0].Position); Equal(new TechPosition(29,-43),result.Candidates[1].Position);
                ReadOnly(result.Candidates);
            }
        }

        private static void MeasuredLayoutCollisions()
        {
            var proposed = new MeasuredTechBounds(ProtoIds.EnergyAnalysis,new[] {new LayoutRectangle(0,0,1,1),new LayoutRectangle(0,0,3,3)});
            var existing = new[]
            {
                new MeasuredTechBounds(new TechId(1101),new[] {new LayoutRectangle(2,2,1,1)}),
                new MeasuredTechBounds(new TechId(1102),new[] {new LayoutRectangle(3,0,1,1)}),
                new MeasuredTechBounds(new TechId(1103),new[] {new LayoutRectangle(10,10,1,1)}),
                proposed
            };
            Sequence(new[] {1101,1102},TechLayoutResolver.FindMeasuredCollisions(proposed,existing).Select(x => x.Value));
            Equal(0,TechLayoutResolver.FindMeasuredCollisions(proposed,Array.Empty<MeasuredTechBounds>()).Count);
            True(!TechLayoutResolver.Resolve("unverified").IsVerified);
        }

        private static void AnchorPendingCollisions()
        {
            var position = FrozenContent.Technologies[0].CandidatePosition;
            var inserted = new[] {new KeyValuePair<TechId,TechPosition>(new TechId(1908),position)};
            var pending = new[] {new KeyValuePair<TechId,TechPosition>(new TechId(1909),position)};
            var combined = inserted.Concat(pending).ToArray(); var before = combined.ToArray();
            var result = TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.EnergyAnalysis,position,combined);
            Sequence(new[] {1908,1909},result.Select(id => id.Value));
            Sequence(before,combined); ReadOnly(result);
            Equal(0,TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.EnergyAnalysis,position,
                Array.Empty<KeyValuePair<TechId,TechPosition>>()).Count);
        }

        private static void AnchorPageBoundary()
        {
            var position = FrozenContent.Technologies[1].CandidatePosition;
            var existing = new[]
            {
                new KeyValuePair<TechId,TechPosition>(new TechId(3506),position),
                new KeyValuePair<TechId,TechPosition>(new TechId(2001),position),
                new KeyValuePair<TechId,TechPosition>(new TechId(2000),position),
                new KeyValuePair<TechId,TechPosition>(new TechId(1),position)
            };
            Sequence(new[] {1,2000},TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.InformationTopology,position,existing)
                .Select(id => id.Value));
            Sequence(new[] {1},TechLayoutResolver.FindMainTreeAnchorCollisions(new TechId(2000),position,existing)
                .Select(id => id.Value));
        }

        private static void AnchorOwnAndDuplicateIds()
        {
            var position = FrozenContent.Technologies[0].CandidatePosition;
            var own = new KeyValuePair<TechId,TechPosition>(ProtoIds.EnergyAnalysis,position);
            var peer = new KeyValuePair<TechId,TechPosition>(new TechId(1918),position);
            Sequence(new[] {1918},TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.EnergyAnalysis,position,
                new[] {own,peer,peer,own}).Select(id => id.Value));
            Equal(0,TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.EnergyAnalysis,position,new[] {own,own}).Count);
        }

        private static void AnchorExactOnly()
        {
            var position = FrozenContent.Technologies[0].CandidatePosition;
            var close = new[]
            {
                new KeyValuePair<TechId,TechPosition>(new TechId(1918),new TechPosition(position.X + 0.01f,position.Y)),
                new KeyValuePair<TechId,TechPosition>(new TechId(1919),new TechPosition(position.X,position.Y + 0.01f))
            };
            Equal(0,TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.EnergyAnalysis,position,close).Count);
            True(!TechLayoutResolver.Resolve("peer-layout-not-measured").IsVerified);
        }

        private static void AnchorInvalidArguments()
        {
            var empty = Array.Empty<KeyValuePair<TechId,TechPosition>>();
            Throws<ArgumentOutOfRangeException>(() => TechLayoutResolver.FindMainTreeAnchorCollisions(default,default,empty));
            Throws<ArgumentOutOfRangeException>(() => TechLayoutResolver.FindMainTreeAnchorCollisions(new TechId(2001),default,empty));
            Throws<ArgumentOutOfRangeException>(() => TechLayoutResolver.FindMainTreeAnchorCollisions(new TechId(3506),default,empty));
            Throws<ArgumentNullException>(() => TechLayoutResolver.FindMainTreeAnchorCollisions(ProtoIds.EnergyAnalysis,default,null!));
        }

        private static void InvalidArguments()
        {
            Throws<ArgumentNullException>(() => ProgressionPolicy.PlanRestore(null!,OwnedPrerequisiteChanges.Empty));
            Throws<ArgumentNullException>(() => ProgressionPolicy.PlanRestore(Baseline(),null!));
            Throws<ArgumentNullException>(() => SaveReconciler.PlanMissingRecipes(null!,Array.Empty<RecipeId>()));
            Throws<ArgumentNullException>(() => SaveReconciler.PlanMissingRecipes(Array.Empty<TechId>(),null!));
            Throws<ArgumentNullException>(() => ProtoIdCollisionGuard.AssertVacant(null!,_ => false));
            Throws<ArgumentNullException>(() => ProtoIdCollisionGuard.AssertVacant(_ => false,null!));
            Throws<ArgumentException>(() => ProtoIds.StringKey(" "));
            Throws<ArgumentException>(() => TechLayoutResolver.Resolve(""));
            Throws<ArgumentOutOfRangeException>(() => new TechPosition(float.NaN,0));
            Throws<ArgumentOutOfRangeException>(() => new LayoutRectangle(0,0,0,1));
            Throws<ArgumentOutOfRangeException>(() => new LayoutRectangle(float.MaxValue,0,float.MaxValue,1));
            Throws<ArgumentException>(() => new MeasuredTechBounds(new TechId(1),Array.Empty<LayoutRectangle>()));
            Throws<ArgumentException>(() => new MeasuredTechBounds(new TechId(1),new[] {default(LayoutRectangle)}));
        }

        private static void ProtoManifest()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),"docs","compatibility","proto-ids.json")));
            var root = document.RootElement;
            Equal(ProtoIds.SchemaVersion,root.GetProperty("schemaVersion").GetInt32());
            Equal(0,root.GetProperty("itemsAdded").GetInt32()); Equal(0,root.GetProperty("buildingsAdded").GetInt32());
            True(!root.GetProperty("runtimeValidated").GetBoolean());
            foreach (var tech in FrozenContent.Technologies) Equal(tech.Id.Value,root.GetProperty("techs").GetProperty(tech.Key).GetInt32());
            foreach (var recipe in FrozenContent.Recipes) Equal(recipe.Id.Value,root.GetProperty("recipes").GetProperty(recipe.Key).GetInt32());
            Equal(2,root.GetProperty("techs").EnumerateObject().Count()); Equal(6,root.GetProperty("recipes").EnumerateObject().Count());
        }

        private static void LocalizationCoverage()
        {
            var required = FrozenContent.Technologies.SelectMany(t => new[] {t.NameKey,t.DescriptionKey,t.ConclusionKey})
                .Concat(FrozenContent.Recipes.SelectMany(r => new[] {r.NameKey,ProtoIds.StringKey("recipe." + r.Key + ".description")})).ToArray();
            foreach (var language in new[] {"en-US","zh-CN"})
            {
                using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),"src","DarkFogSynthesis","Localization","Strings." + language + ".json")));
                var root = document.RootElement;
                foreach (var key in required) True(!string.IsNullOrWhiteSpace(root.GetProperty(key).GetString()));
                Equal(language == "en-US" ? "Dark Fog Energy Analysis" : "黑雾能量解析",root.GetProperty(FrozenContent.Technologies[0].NameKey).GetString());
                Equal(language == "en-US" ? "Dark Fog Information Topology" : "黑雾信息拓扑",root.GetProperty(FrozenContent.Technologies[1].NameKey).GetString());
                Equal(root.EnumerateObject().Count(),root.EnumerateObject().Select(p => p.Name).Distinct().Count());
            }
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName,"DarkFogSynthesis_Implementation_Plan_ZH.md"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Run the tests from a build inside this repository; the frozen plan root could not be located.");
        }

        private static void RemovalEmptyProduction()
        {
            True(!RemovalSafetyPolicy.HasProductionState(0,0,0,0,false));
            True(!RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,null));
            True(!RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,new IReadOnlyList<int>?[] {null,Array.Empty<int>(),new[] {0,0,0}}));
            True(!RemovalSafetyPolicy.HasResearchState(0,0));
            True(!RemovalSafetyPolicy.HasResearchState(0,0,null));
            True(!RemovalSafetyPolicy.HasResearchState(0,0,null,Array.Empty<int>(),new[] {0,0}));
        }

        private static void RemovalProductionScalars()
        {
            foreach (int value in new[] {1,17,int.MaxValue})
            {
                True(RemovalSafetyPolicy.HasProductionState(value,0,0,0,false));
                True(RemovalSafetyPolicy.HasProductionState(0,value,0,0,false));
                True(RemovalSafetyPolicy.HasProductionState(0,0,value,0,false));
                True(RemovalSafetyPolicy.HasProductionState(0,0,0,value,false));
            }
            True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,true));
        }

        private static void RemovalProductionBuffers()
        {
            var empty = new[] {0,0,0};
            // Parameter order supplied by runtime: served, incServed, produced. Inc-only and output-only must block.
            for (int slot = 0; slot < 3; slot++)
            {
                var buffers = new IReadOnlyList<int>?[] {empty,empty,empty}; buffers[slot] = new[] {0,1,0};
                True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,buffers));
            }
            True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,null,empty,Array.Empty<int>(),new[] {0,0,1}));
            True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,new[] {1},new[] {2},new[] {3}));
        }

        private static void RemovalNegativeState()
        {
            foreach (int value in new[] {-1,-17,int.MinValue})
            {
                True(RemovalSafetyPolicy.HasProductionState(value,0,0,0,false));
                True(RemovalSafetyPolicy.HasProductionState(0,value,0,0,false));
                True(RemovalSafetyPolicy.HasProductionState(0,0,value,0,false));
                True(RemovalSafetyPolicy.HasProductionState(0,0,0,value,false));
                True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,new[] {0,value,0}));
                True(RemovalSafetyPolicy.HasResearchState(value,0));
                True(RemovalSafetyPolicy.HasResearchState(0,value));
                True(RemovalSafetyPolicy.HasResearchState(0,0,new[] {0,value,0}));
            }
            // Positive and negative values must not cancel through a mistaken sum-based check.
            True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,new[] {1,-1}));
            True(RemovalSafetyPolicy.HasResearchState(0,0,new[] {1,-1}));
        }

        private static void RemovalResearchState()
        {
            True(RemovalSafetyPolicy.HasResearchState(1,0)); True(RemovalSafetyPolicy.HasResearchState(0,1));
            True(RemovalSafetyPolicy.HasResearchState(int.MaxValue,int.MaxValue));
            True(RemovalSafetyPolicy.HasResearchState(0,0,new[] {0,1},new[] {0,0}));
            True(RemovalSafetyPolicy.HasResearchState(0,0,new[] {0,0},new[] {0,1}));
            True(RemovalSafetyPolicy.HasResearchState(0,0,null,null,new[] {1}));
        }

        private static void RemovalCustomQueues()
        {
            var empty = Array.Empty<int>();
            True(!RemovalSafetyPolicy.HasCustomQueueReferences(0,empty,empty));
            True(!RemovalSafetyPolicy.HasCustomQueueReferences(1901,new[] {0,1101,1801,1802,1902,0},new[] {0,1,2,5201,0}));
            foreach (var tech in FrozenContent.Technologies)
            {
                True(RemovalSafetyPolicy.HasCustomQueueReferences(tech.Id.Value,empty,empty));
                True(RemovalSafetyPolicy.HasCustomQueueReferences(0,new[] {0,tech.Id.Value,0},empty));
                // Distinct ID namespaces: the same integer in a recipe queue is not one of our Recipe IDs.
                True(!RemovalSafetyPolicy.HasCustomQueueReferences(0,empty,new[] {tech.Id.Value}));
            }
            foreach (var recipe in FrozenContent.Recipes)
            {
                True(RemovalSafetyPolicy.HasCustomQueueReferences(0,empty,new[] {0,recipe.Id.Value,0}));
                True(!RemovalSafetyPolicy.HasCustomQueueReferences(0,new[] {recipe.Id.Value},empty));
            }
        }

        private static void RemovalUnknownQueues()
        {
            var empty = Array.Empty<int>();
            True(RemovalSafetyPolicy.HasCustomQueueReferences(null,empty,empty));
            True(RemovalSafetyPolicy.HasCustomQueueReferences(0,null,empty));
            True(RemovalSafetyPolicy.HasCustomQueueReferences(0,empty,null));
            True(RemovalSafetyPolicy.HasCustomQueueReferences(null,null,null));
            True(RemovalSafetyPolicy.HasCustomQueueReferences(-1,empty,empty));
            True(RemovalSafetyPolicy.HasCustomQueueReferences(0,new[] {-1},empty));
            True(RemovalSafetyPolicy.HasCustomQueueReferences(0,empty,new[] {-1}));
        }

        private static void RemovalPreflightIsPure()
        {
            var served = new[] {0,0}; var inc = new[] {0,1}; var produced = new[] {3,0};
            var before = new[] {served.ToArray(),inc.ToArray(),produced.ToArray()};
            var research = new[] {1901,1951,0}; var crafting = new[] {1,48101,0};
            var beforeResearch = research.ToArray(); var beforeCrafting = crafting.ToArray();
            for (int n = 0; n < 10; n++)
            {
                True(RemovalSafetyPolicy.HasProductionState(0,0,0,0,false,served,inc,produced));
                True(RemovalSafetyPolicy.HasResearchState(0,0,served,inc));
                True(RemovalSafetyPolicy.HasCustomQueueReferences(0,research,crafting));
            }
            Sequence(before[0],served); Sequence(before[1],inc); Sequence(before[2],produced);
            Sequence(beforeResearch,research); Sequence(beforeCrafting,crafting);
        }

        private static void PeerFrameworksDoNotBlock()
        {
            Equal(0,PeerPersistenceGuard.FindBlockers(Array.Empty<string>()).Count);
            Equal(0,PeerPersistenceGuard.FindBlockers(new[] {
                "dsp.common-api.CommonAPI", "me.xiaoye97.plugin.Dyson.LDBTool", "crecheng.DSPModSave",
                "idkwhodatis.darkfogsynthesis", "unrelated.plugin" }).Count);
        }

        private static void PeerGuidIdentity()
        {
            const string verifiedGuid = "Gnimaerd.DSP.plugin.MoreMegaStructure";
            Equal(verifiedGuid,PeerPersistenceGuard.MoreMegaStructureGuid);
            Equal(1,PeerPersistenceGuard.FindBlockers(new[] {verifiedGuid}).Count);
            foreach (var nearMatch in new[] {verifiedGuid.ToLowerInvariant(),verifiedGuid.ToUpperInvariant(),
                " " + verifiedGuid,verifiedGuid + " ",verifiedGuid + ".extra","MoreMegaStructure"})
                Equal(0,PeerPersistenceGuard.FindBlockers(new[] {nearMatch}).Count);
        }

        private static void PeerUnknownVersionsBlock()
        {
            // Runtime supplies only registry keys. No absent, unparsable or future version can bypass the guard.
            foreach (string? version in new string?[] {null,"","unknown","1.9.3","99.0.0"})
            {
                var loadedPlugins = new Dictionary<string,string?> { [PeerPersistenceGuard.MoreMegaStructureGuid] = version };
                Equal(1,PeerPersistenceGuard.FindBlockers(loadedPlugins.Keys).Count);
                Equal(version,loadedPlugins[PeerPersistenceGuard.MoreMegaStructureGuid]);
            }
        }

        private static void PeerBlockerReason()
        {
            var blocker = PeerPersistenceGuard.FindBlockers(new[] {PeerPersistenceGuard.MoreMegaStructureGuid}).Single();
            Equal(PeerPersistenceGuard.MoreMegaStructureGuid,blocker.PluginGuid);
            Equal("MoreMegaStructure",blocker.DisplayName);
            True(blocker.Reason.Contains(blocker.DisplayName,StringComparison.Ordinal));
            True(blocker.Reason.Contains("separate mod-save",StringComparison.Ordinal));
            True(blocker.Reason.Contains("recipe IDs",StringComparison.Ordinal));
            True(blocker.Reason.Contains("blocked",StringComparison.Ordinal));
            True(blocker.Reason.Contains("Keep DarkFogSynthesis installed",StringComparison.Ordinal));
            True(blocker.Reason.Contains("独立 Mod 存档",StringComparison.Ordinal));
            True(blocker.Reason.Contains("已阻止",StringComparison.Ordinal));
        }

        private static void PeerInventoryIsPure()
        {
            var plugins = new List<string> {PeerPersistenceGuard.MoreMegaStructureGuid,"unrelated.plugin",PeerPersistenceGuard.MoreMegaStructureGuid};
            var before = plugins.ToArray();
            var result = PeerPersistenceGuard.FindBlockers(plugins);
            Equal(1,result.Count); Sequence(before,plugins); ReadOnly(result);
            True(typeof(PeerPersistenceBlocker).GetProperties().All(property => property.SetMethod == null));
            plugins.Clear(); Equal(1,result.Count);
            Equal(0,PeerPersistenceGuard.FindBlockers(plugins).Count);
            ReadOnly(PeerPersistenceGuard.FindBlockers(plugins));
        }

        private static void PeerInvalidInventories()
        {
            Throws<ArgumentNullException>(() => PeerPersistenceGuard.FindBlockers(null!));
            foreach (string? invalid in new string?[] {null,""," "})
                Throws<ArgumentException>(() => PeerPersistenceGuard.FindBlockers(new[] {invalid!}));
            // Do not stop scanning after a match and silently accept malformed remaining inventory entries.
            Throws<ArgumentException>(() => PeerPersistenceGuard.FindBlockers(new[] {PeerPersistenceGuard.MoreMegaStructureGuid,null!}));
        }

        private static Dictionary<TechId,IReadOnlyList<TechId>> Baseline()
            => FrozenContent.CombatPrerequisites.ToDictionary(p => p.HiddenTech,p => (IReadOnlyList<TechId>)new[] {new TechId(1101),new TechId(1102)});
        private static Dictionary<TechId,IReadOnlyList<TechId>> Clone(IReadOnlyDictionary<TechId,IReadOnlyList<TechId>> graph)
            => graph.ToDictionary(p => p.Key,p => (IReadOnlyList<TechId>)p.Value.ToArray());
        private static void Apply(Dictionary<TechId,IReadOnlyList<TechId>> graph, PrerequisitePlan plan)
        {
            True(plan.CanApply);
            foreach (var edit in plan.Edits) Sequence(edit.Before,graph[edit.Tech]);
            foreach (var edit in plan.Edits) graph[edit.Tech] = edit.After.ToArray();
        }
        private static void GraphEqual(IReadOnlyDictionary<TechId,IReadOnlyList<TechId>> expected,IReadOnlyDictionary<TechId,IReadOnlyList<TechId>> actual)
        {
            Sequence(expected.Keys.OrderBy(x => x.Value),actual.Keys.OrderBy(x => x.Value));
            foreach (var p in expected) Sequence(p.Value,actual[p.Key]);
        }
        private static void ReadOnly<T>(IReadOnlyList<T> list)
        {
            True(list is IList<T> typed && typed.IsReadOnly);
            Throws<NotSupportedException>(() => ((IList<T>)list).Add(default!));
            if (list.Count > 0) Throws<NotSupportedException>(() => ((IList<T>)list)[0] = default!);
        }
        private static void Equal<T>(T expected,T actual)
        {
            assertions++; if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}; actual {actual}");
        }
        private static void True(bool condition)
        {
            assertions++; if (!condition) throw new Exception("Expected true");
        }
        private static void Sequence<T>(IEnumerable<T> expected,IEnumerable<T> actual)
        {
            assertions++; var a = expected.ToArray(); var b = actual.ToArray();
            if (!a.SequenceEqual(b)) throw new Exception("Expected [" + string.Join(",",a) + "]; actual [" + string.Join(",",b) + "]");
        }
        private static T Throws<T>(Action action) where T:Exception
        {
            assertions++; try { action(); } catch (T exception) { return exception; }
            throw new Exception("Expected " + typeof(T).Name);
        }
    }
}
