using System;

namespace DarkFogSynthesis.Core.Compatibility
{
    /// <summary>Identity/configuration observed by removal preflight, not ownership of native buffers.</summary>
    public readonly struct RemovalTargetIdentity : IEquatable<RemovalTargetIdentity>
    {
        public int ComponentId { get; }
        public int EntityId { get; }
        public int RecipeId { get; }
        public int TechId { get; }
        public bool ResearchMode { get; }
        public bool MatrixMode { get; }

        public RemovalTargetIdentity(int componentId, int entityId, int recipeId, int techId,
            bool researchMode, bool matrixMode)
        {
            ComponentId = componentId; EntityId = entityId; RecipeId = recipeId; TechId = techId;
            ResearchMode = researchMode; MatrixMode = matrixMode;
        }

        public bool Equals(RemovalTargetIdentity other) => ComponentId == other.ComponentId &&
            EntityId == other.EntityId && RecipeId == other.RecipeId && TechId == other.TechId &&
            ResearchMode == other.ResearchMode && MatrixMode == other.MatrixMode;
        public override bool Equals(object? obj) => obj is RemovalTargetIdentity other && Equals(other);
        public override int GetHashCode() => ComponentId ^ EntityId ^ RecipeId ^ TechId ^
            ResearchMode.GetHashCode() ^ MatrixMode.GetHashCode();
    }

    public static class RemovalTargetGuard
    {
        /// <summary>The native adapter must also validate factory/pool membership and entity backlinks.</summary>
        public static void EnsureUnchanged(RemovalTargetIdentity expected, RemovalTargetIdentity current,
            bool isOwned, bool hasBufferedState)
        {
            if (expected.ComponentId <= 0 || expected.EntityId <= 0 || !expected.Equals(current) || !isOwned)
                throw new InvalidOperationException("Removal target identity or owned configuration changed after preflight; refusing reset.");
            if (hasBufferedState)
                throw new InvalidOperationException("Removal target acquired resources or progress after preflight; refusing reset.");
        }

        /// <summary>
        /// Export is a callback boundary too. Do not enroll a stale snapshot for rollback or reset a
        /// changed target. Capture/register delegates only prepare memory; reset invokes the native API.
        /// </summary>
        public static void Reset(Action validateTarget, Func<Action> captureRollback,
            Action<Action> registerRollback, Action reset)
        {
            if (validateTarget == null) throw new ArgumentNullException(nameof(validateTarget));
            if (captureRollback == null) throw new ArgumentNullException(nameof(captureRollback));
            if (registerRollback == null) throw new ArgumentNullException(nameof(registerRollback));
            if (reset == null) throw new ArgumentNullException(nameof(reset));
            validateTarget();
            Action restore = captureRollback() ?? throw new InvalidOperationException("Missing removal rollback snapshot.");
            validateTarget();
            registerRollback(restore);
            reset();
        }
    }
}
