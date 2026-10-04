using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Progression;

namespace DarkFogSynthesis.Progression
{
    /// <summary>Observes current LDB prototypes and references on every call; never repairs caches.</summary>
    internal static class LiveProgressionValidator
    {
        internal static void ValidateHidden(bool isPeaceMode, bool extendToCombat) =>
            LiveProgressionPolicy.ValidateHidden(Read, isPeaceMode, extendToCombat);

        internal static void ValidateSynthesis(IReadOnlyCollection<int> combinedPreCacheTechs) =>
            LiveProgressionPolicy.ValidateSynthesis(Read, combinedPreCacheTechs);

        internal static IReadOnlyCollection<int> CaptureSynthesisPreCacheModes() =>
            LiveProgressionPolicy.CaptureSynthesisPreCacheModes(Read);

        private static LiveTechnologyState? Read(int id)
        {
            var tech = LDB.techs.Select(id);
            if (tech == null) return null;
            return new LiveTechnologyState(tech.ID, tech, tech.Published, tech.IsObsolete,
                tech.PreTechs ?? Array.Empty<int>(), tech.PreTechsImplicit ?? Array.Empty<int>(),
                References(tech.preTechArray), References(tech.postTechArray));
        }

        private static IEnumerable<LiveTechnologyReference> References(TechProto[]? cache) =>
            (cache ?? Array.Empty<TechProto>()).Select(tech => new LiveTechnologyReference(tech?.ID ?? 0, tech));
    }
}
