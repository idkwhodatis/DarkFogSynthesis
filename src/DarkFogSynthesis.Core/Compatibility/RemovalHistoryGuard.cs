using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>Read-only checks of the history that must survive removal and native save callbacks.</summary>
    public static class RemovalHistoryGuard
    {
        public static void EnsurePreserved<TState>(IEnumerable<int> expectedRecipes,
            IReadOnlyDictionary<int, TState> expectedTechs, IEnumerable<int> expectedQueue, int expectedCurrentTech,
            IEnumerable<int> recipes, IReadOnlyDictionary<int, TState> techs, IEnumerable<int> queue, int currentTech)
            where TState : struct
        {
            if (expectedRecipes == null) throw new ArgumentNullException(nameof(expectedRecipes));
            if (expectedTechs == null) throw new ArgumentNullException(nameof(expectedTechs));
            if (expectedQueue == null) throw new ArgumentNullException(nameof(expectedQueue));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            if (techs == null) throw new ArgumentNullException(nameof(techs));
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            if (!new HashSet<int>(expectedRecipes).SetEquals(recipes))
                throw new InvalidOperationException("Unrelated recipe unlocks changed; the removal candidate is not accepted.");
            if (techs.Count != expectedTechs.Count || expectedTechs.Any(k =>
                !techs.TryGetValue(k.Key, out var state) || !EqualityComparer<TState>.Default.Equals(k.Value, state)))
                throw new InvalidOperationException("Unrelated research states changed; the removal candidate is not accepted.");
            // Native queues have unused zero slots. Preserve every real entry, its order and duplicates.
            if (currentTech != expectedCurrentTech || !expectedQueue.Where(id => id != 0).SequenceEqual(queue.Where(id => id != 0)))
                throw new InvalidOperationException("Unrelated current research or queue entries changed; the removal candidate is not accepted.");
        }

        /// <summary>Do not report preservation until the same checks pass after the save callback boundary.</summary>
        public static void VerifyAcrossSave(Action verifyHistory, Action save)
        {
            if (verifyHistory == null) throw new ArgumentNullException(nameof(verifyHistory));
            if (save == null) throw new ArgumentNullException(nameof(save));
            verifyHistory();
            save();
            verifyHistory();
        }
    }
}
