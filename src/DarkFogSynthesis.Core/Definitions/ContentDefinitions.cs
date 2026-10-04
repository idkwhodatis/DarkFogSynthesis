using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DarkFogSynthesis.Core.Definitions
{
    internal static class FrozenList
    {
        internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return Array.AsReadOnly(values.ToArray());
        }
    }

    public readonly struct ItemAmount : IEquatable<ItemAmount>
    {
        public ItemAmount(ItemId item, int count)
        {
            if (item.Value <= 0) throw new ArgumentOutOfRangeException(nameof(item));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            Item = item; Count = count;
        }
        public ItemId Item { get; }
        public int Count { get; }
        public bool Equals(ItemAmount other) => Item == other.Item && Count == other.Count;
        public override bool Equals(object? obj) => obj is ItemAmount other && Equals(other);
        public override int GetHashCode() => (Item.GetHashCode() * 397) ^ Count;
        public override string ToString() => Item + " x" + Count;
    }

    public enum ProductionMachine { Smelter, MatrixLab, Assembler }

    public sealed class RecipeDefinition
    {
        internal RecipeDefinition(RecipeId id, string key, ItemAmount output, IEnumerable<ItemAmount> inputs,
            int timeSpendTicks, ProductionMachine machine, TechId unlockTech)
        {
            if (id.Value <= 0 || unlockTech.Value <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (timeSpendTicks <= 0) throw new ArgumentOutOfRangeException(nameof(timeSpendTicks));
            Id = id; Key = key; NameKey = ProtoIds.StringKey("recipe." + key + ".name");
            Output = output; Inputs = FrozenList.Copy(inputs); TimeSpendTicks = timeSpendTicks;
            Machine = machine; UnlockTech = unlockTech;
        }
        public RecipeId Id { get; }
        public string Key { get; }
        public string NameKey { get; }
        public ItemAmount Output { get; }
        public IReadOnlyList<ItemAmount> Inputs { get; }
        public int TimeSpendTicks { get; }
        public int TicksPerSecond => 60;
        public double BaseSeconds => TimeSpendTicks / (double)TicksPerSecond;
        /// <summary>Ideal 1x machine, continuous power/supply/output capacity, no proliferation.</summary>
        public double BaseOutputPerMinute => Output.Count * 60.0 / BaseSeconds;
        public ProductionMachine Machine { get; }
        public bool Handcraft => true;
        public bool SupportsAcceleration => true;
        public bool SupportsExtraProducts => true;
        public TechId UnlockTech { get; }
    }

    public readonly struct TechPosition : IEquatable<TechPosition>
    {
        public TechPosition(float x, float y)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y;
        }
        public float X { get; }
        public float Y { get; }
        public bool Equals(TechPosition other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is TechPosition other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
    }

    public sealed class TechDefinition
    {
        internal TechDefinition(TechId id, string key, IEnumerable<TechId> explicitPrerequisites,
            IEnumerable<TechId> implicitPrerequisites, IEnumerable<ItemAmount> researchCost, long hashNeeded,
            IEnumerable<int> candidateItemPoints, IEnumerable<RecipeId> unlockRecipes, TechPosition candidatePosition)
        {
            Id = id; Key = key; NameKey = ProtoIds.StringKey("tech." + key + ".name");
            DescriptionKey = ProtoIds.StringKey("tech." + key + ".description");
            ConclusionKey = ProtoIds.StringKey("tech." + key + ".conclusion");
            ExplicitPrerequisites = FrozenList.Copy(explicitPrerequisites);
            ImplicitPrerequisites = FrozenList.Copy(implicitPrerequisites);
            ResearchCost = FrozenList.Copy(researchCost); HashNeeded = hashNeeded;
            CandidateItemPoints = FrozenList.Copy(candidateItemPoints);
            UnlockRecipes = FrozenList.Copy(unlockRecipes); CandidatePosition = candidatePosition;
            if (ResearchCost.Count != CandidateItemPoints.Count) throw new ArgumentException("Research item/rate lengths differ.");
            if (hashNeeded <= 0) throw new ArgumentOutOfRangeException(nameof(hashNeeded));
            for (int i = 0; i < ResearchCost.Count; i++)
                if (CandidateItemPoints[i] <= 0 || hashNeeded * CandidateItemPoints[i] != 3600L * ResearchCost[i].Count)
                    throw new ArgumentException("Candidate ItemPoints do not encode the frozen research totals.");
        }
        public TechId Id { get; }
        public string Key { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public string ConclusionKey { get; }
        public IReadOnlyList<TechId> ExplicitPrerequisites { get; }
        public IReadOnlyList<TechId> ImplicitPrerequisites { get; }
        public IReadOnlyList<ItemAmount> ResearchCost { get; }
        public long HashNeeded { get; }
        /// <summary>CommonAPI encoding candidates only; verify UI and actual consumption against the target game.</summary>
        public IReadOnlyList<int> CandidateItemPoints { get; }
        public IReadOnlyList<RecipeId> UnlockRecipes { get; }
        public TechPosition CandidatePosition { get; }
    }

    public readonly struct CombatPrerequisite
    {
        public CombatPrerequisite(TechId hiddenTech, TechId requiredCombatTech)
        {
            if (hiddenTech.Value <= 0 || requiredCombatTech.Value <= 0) throw new ArgumentOutOfRangeException(nameof(hiddenTech));
            HiddenTech = hiddenTech; RequiredCombatTech = requiredCombatTech;
        }
        public TechId HiddenTech { get; }
        public TechId RequiredCombatTech { get; }
    }
}
