using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Core.Registration
{
    public sealed class TechLayoutCandidate
    {
        internal TechLayoutCandidate(TechId tech, TechPosition position) { Tech = tech; Position = position; }
        public TechId Tech { get; }
        public TechPosition Position { get; }
    }

    public sealed class TechLayoutResolution
    {
        internal TechLayoutResolution(string gameVersion, IEnumerable<TechLayoutCandidate> candidates)
        {
            GameVersion = gameVersion; Candidates = FrozenList.Copy(candidates);
        }
        public string GameVersion { get; }
        public IReadOnlyList<TechLayoutCandidate> Candidates { get; }
        // There is deliberately no verified profile shipped before actual game measurements and screenshots exist.
        public bool IsVerified => false;
        public bool DiagnosticCopyOnly => true;
        public string Warning => "Candidate coordinates only. This game/dependency/mod layout has not been verified; use a copied diagnostic save.";
    }

    public readonly struct LayoutRectangle
    {
        public LayoutRectangle(float left, float bottom, float width, float height)
        {
            if (!Finite(left) || !Finite(bottom) || !Finite(width) || !Finite(height) || width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "A finite nonempty rectangle is required.");
            Left = left; Bottom = bottom; Width = width; Height = height;
            if (!Finite(left + width) || !Finite(bottom + height)) throw new ArgumentOutOfRangeException(nameof(width));
        }
        public float Left { get; }
        public float Bottom { get; }
        public float Width { get; }
        public float Height { get; }
        public float Right => Left + Width;
        public float Top => Bottom + Height;
        // Touching edges count as a collision, avoiding a false clearance at pixel boundaries.
        public bool Intersects(LayoutRectangle other)
            => Left <= other.Right && Right >= other.Left && Bottom <= other.Top && Top >= other.Bottom;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class MeasuredTechBounds
    {
        public MeasuredTechBounds(TechId tech, IEnumerable<LayoutRectangle> allStateBounds)
        {
            if (tech.Value <= 0) throw new ArgumentOutOfRangeException(nameof(tech));
            Tech = tech; AllStateBounds = FrozenList.Copy(allStateBounds);
            if (AllStateBounds.Count == 0) throw new ArgumentException("At least one measured bound is required.", nameof(allStateBounds));
            if (AllStateBounds.Any(bounds => bounds.Width <= 0 || bounds.Height <= 0))
                throw new ArgumentException("Default/empty rectangles are not valid measurements.", nameof(allStateBounds));
        }
        public TechId Tech { get; }
        /// <summary>Caller supplies normal, hover, expanded, locale and zoom bounds in the same coordinate system.</summary>
        public IReadOnlyList<LayoutRectangle> AllStateBounds { get; }
    }

    /// <summary>Single resolver for our two nodes. No guessed footprint sizes or reflow of vanilla nodes.</summary>
    public static class TechLayoutResolver
    {
        public static TechLayoutResolution Resolve(string gameVersion)
        {
            if (string.IsNullOrWhiteSpace(gameVersion)) throw new ArgumentException("A game version is required.", nameof(gameVersion));
            return new TechLayoutResolution(gameVersion,
                FrozenContent.Technologies.Select(tech => new TechLayoutCandidate(tech.Id, tech.CandidatePosition)));
        }

        /// <summary>
        /// Reports collisions within caller-supplied measured rectangles, including hidden nodes if supplied.
        /// An empty result is not proof of a complete layout: connectors, previews, missing states and other mods
        /// still need in-game review. Never changes the proposed or existing node positions.
        /// </summary>
        public static IReadOnlyList<TechId> FindMeasuredCollisions(MeasuredTechBounds proposed,
            IEnumerable<MeasuredTechBounds> existing)
        {
            if (proposed == null) throw new ArgumentNullException(nameof(proposed));
            if (existing == null) throw new ArgumentNullException(nameof(existing));
            return FrozenList.Copy(existing
                .Where(other => other.Tech != proposed.Tech && other.AllStateBounds.Any(bounds => proposed.AllStateBounds.Any(bounds.Intersects)))
                .Select(other => other.Tech).Distinct().OrderBy(id => id.Value));
        }
    }
}
