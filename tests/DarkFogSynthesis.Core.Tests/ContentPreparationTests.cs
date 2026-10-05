using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Definitions;
using DarkFogSynthesis.Core.Registration;

namespace DarkFogSynthesis.Core.Tests
{
    internal static class ContentPreparationTests
    {
        internal static void Run(Action<bool, string> check)
        {
            int icons = 0, tabs = 0, queued = 0;
            Exception? failure = null;
            try
            {
                ContentPreparation.Prepare(d => ++icons == 2 ? throw new InvalidOperationException("second icon") : new object(),
                    () => { tabs++; return 4; }, _ => { });
                queued++;
            }
            catch (InvalidOperationException e) { failure = e; }
            check(failure != null && icons == 2 && tabs == 0 && queued == 0, "Second asset failure escaped preparation");
            var slots = new List<int>();
            failure = null;
            try
            {
                ContentPreparation.Prepare(_ => new object(), () => { tabs++; return 4; }, grid =>
                {
                    slots.Add(grid);
                    if (grid == 4106) throw new InvalidOperationException("occupied final slot");
                });
                queued++;
            }
            catch (InvalidOperationException e) { failure = e; }
            check(failure != null && tabs == 1 && queued == 0 && slots.SequenceEqual(Enumerable.Range(4101, 6)),
                "All six slots must be checked before the caller queues any prototype");
            var first = ContentPreparation.Prepare(_ => new object(), () => 4, _ => { });
            var second = ContentPreparation.Prepare(_ => new object(), () => 4, _ => { });
            check(first.Technologies.Count == 2 && first.Recipes.Count == 6, "Incorrect batch size");
            for (int i = 0; i < first.Recipes.Count; i++)
            {
                var recipe = first.Recipes[i];
                check(recipe.Items.SequenceEqual(recipe.Definition.Inputs.Select(v => v.Item.Value)), "Incorrect native item arguments");
                check(recipe.ItemCounts.SequenceEqual(recipe.Definition.Inputs.Select(v => v.Count)), "Incorrect native counts");
                check(!ReferenceEquals(recipe.Items, second.Recipes[i].Items), "Mutable arguments shared between attempts");
                int original = recipe.Definition.Inputs[0].Item.Value;
                recipe.Items[0] = -1;
                check(recipe.Definition.Inputs[0].Item.Value == original && second.Recipes[i].Items[0] == original,
                    "Native arrays mutated frozen definitions or another attempt");
            }
            foreach (var tech in first.Technologies)
            {
                int original = tech.Definition.ExplicitPrerequisites[0].Value;
                tech.Prerequisites[0] = -1;
                tech.ItemPoints[0] = -1;
                tech.UnlockRecipes[0] = -1;
                check(tech.Definition.ExplicitPrerequisites[0].Value == original && tech.Definition.CandidateItemPoints[0] > 0 &&
                    tech.Definition.UnlockRecipes[0].Value > 0, "Technology arrays alias frozen definitions");
            }
            bool overflow = false;
            try { ContentPreparation.Prepare(_ => new object(), () => int.MaxValue, _ => { }); }
            catch (OverflowException) { overflow = true; }
            check(overflow, "Grid arithmetic overflow was not refused");
            bool invalid = false;
            try { ContentPreparation.Prepare(_ => new object(), () => 0, _ => { }); }
            catch (InvalidOperationException) { invalid = true; }
            check(invalid, "Invalid tab was not refused");
        }
    }
}
