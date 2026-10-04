using System;

namespace DarkFogSynthesis.Core.Definitions
{
    // Separate value types prevent confusing an item ID with a recipe or technology ID.
    public readonly struct ItemId : IEquatable<ItemId>
    {
        public ItemId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool Equals(ItemId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is ItemId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(ItemId a, ItemId b) => a.Equals(b);
        public static bool operator !=(ItemId a, ItemId b) => !a.Equals(b);
    }

    public readonly struct RecipeId : IEquatable<RecipeId>
    {
        public RecipeId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool Equals(RecipeId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is RecipeId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(RecipeId a, RecipeId b) => a.Equals(b);
        public static bool operator !=(RecipeId a, RecipeId b) => !a.Equals(b);
    }

    public readonly struct TechId : IEquatable<TechId>
    {
        public TechId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool Equals(TechId other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is TechId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(TechId a, TechId b) => a.Equals(b);
        public static bool operator !=(TechId a, TechId b) => !a.Equals(b);
    }

    /// <summary>Fixed provisional IDs. They are not a globally reserved range: fail on collision, never renumber at runtime.</summary>
    public static class ProtoIds
    {
        public const string Namespace = "dark_fog_synthesis";
        public const int SchemaVersion = 1;
        public static readonly TechId EnergyAnalysis = new TechId(1951);
        public static readonly TechId InformationTopology = new TechId(1952);
        public static readonly RecipeId EnergyShard = new RecipeId(48101);
        public static readonly RecipeId DarkFogMatrix = new RecipeId(48102);
        public static readonly RecipeId SiliconNeuron = new RecipeId(48103);
        public static readonly RecipeId MatterRecombinator = new RecipeId(48104);
        public static readonly RecipeId NegentropySingularity = new RecipeId(48105);
        public static readonly RecipeId CoreElement = new RecipeId(48106);
        public static string StringKey(string suffix)
        {
            if (string.IsNullOrWhiteSpace(suffix)) throw new ArgumentException("A nonempty key is required.", nameof(suffix));
            return Namespace + "." + suffix;
        }
    }
}
