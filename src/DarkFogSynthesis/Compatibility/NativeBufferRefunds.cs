using System;
using System.Collections.Generic;
using System.Linq;
using DarkFogSynthesis.Core.Compatibility;
using DarkFogSynthesis.Core.Definitions;

namespace DarkFogSynthesis.Compatibility
{
    /// <summary>
    /// Read-only idle-buffer ledger and detached native package-capacity diagnostic.
    /// Deliberately has no Apply/refund/restore method: receiver routing and target-game
    /// TakeBackItems side effects still need verification before cleanup can use them.
    /// </summary>
    internal static class NativeBufferRefunds
    {
        private static readonly HashSet<int> OwnedRecipes = new HashSet<int>(FrozenContent.Recipes.Select(r => r.Id.Value));

        internal static RefundPlan Plan(GameData data)
        {
            RequirePaused(data);
            long tick = GameMain.gameTick;
            if (data.factories == null || data.factoryCount < 0 || data.factoryCount > data.factories.Length)
                throw new InvalidOperationException("Factory inventory is unavailable or inconsistent.");
            var snapshots = new List<RefundBufferSnapshot>();
            for (int f = 0; f < data.factoryCount; ++f)
            {
                PlanetFactory factory = data.factories[f];
                if (factory == null) continue;
                FactorySystem system = factory.factorySystem;
                if (system == null || system.assemblerPool == null || system.labPool == null
                    || system.assemblerCursor < 0 || system.assemblerCursor > system.assemblerPool.Length
                    || system.labCursor < 0 || system.labCursor > system.labPool.Length)
                    throw new InvalidOperationException("Factory component inventory is unavailable or inconsistent.");
                for (int i = 1; i < system.assemblerCursor; ++i)
                {
                    AssemblerComponent component = system.assemblerPool[i];
                    if (component.id != i || !OwnedRecipes.Contains(component.recipeId)) continue;
                    RequireEntity(factory, component.entityId, i, false);
                    var recipe = component.recipeExecuteData;
                    if (recipe == null) throw new InvalidOperationException("Owned assembler has no native recipe execution data.");
                    snapshots.Add(new RefundBufferSnapshot(component.recipeId,
                        recipe.requires, recipe.requireCounts, recipe.products, recipe.productCounts,
                        component.served, component.incServed, component.produced, component.time, component.extraTime,
                        component.cycleCount, component.extraCycleCount, component.replicating));
                }
                for (int i = 1; i < system.labCursor; ++i)
                {
                    LabComponent component = system.labPool[i];
                    if (component.id != i || !OwnedRecipes.Contains(component.recipeId)) continue;
                    RequireEntity(factory, component.entityId, i, true);
                    var recipe = component.recipeExecuteData;
                    if (recipe == null) throw new InvalidOperationException("Owned lab has no native recipe execution data.");
                    snapshots.Add(new RefundBufferSnapshot(component.recipeId,
                        recipe.requires, recipe.requireCounts, recipe.products, recipe.productCounts,
                        component.served, component.incServed, component.produced, component.time, component.extraTime,
                        component.cycleCount, component.extraCycleCount, component.replicating, component.researchMode,
                        component.techId, component.hashBytes, component.extraHashBytes, component.matrixServed, component.matrixIncServed));
                }
            }
            RequirePaused(data);
            if (GameMain.gameTick != tick) throw new InvalidOperationException("Simulation advanced during the buffer diagnostic.");
            return RefundLedger.Create(snapshots);
        }

        internal static PackageCapacityResult CheckPackageCapacity(Player player, RefundPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            RequirePlayer(player);
            long tick = GameMain.gameTick;
            var before = new PackageSnapshot(player.package);
            long requestedCount = 0, acceptedCount = 0, requestedInc = 0, remainingInc = 0;
            bool fits = true;
            string reason = "All planned packets fit in one detached player-package copy. Native refunds remain unverified and disabled.";
            StorageComponent? copy = null;
            try
            {
                copy = before.CreateDetachedCopy();
                foreach (RefundPacket packet in plan.Packets)
                {
                    if (LDB.items.Select(packet.ItemId) == null) throw new InvalidOperationException("Refund item prototype is unavailable.");
                    requestedCount = checked(requestedCount + packet.Count);
                    requestedInc = checked(requestedInc + packet.Inc);
                    int added = copy.AddItemStacked(packet.ItemId, packet.Count, packet.Inc, out int unacceptedInc);
                    if (added < 0 || added > packet.Count || unacceptedInc < 0 || unacceptedInc > packet.Inc)
                        throw new InvalidOperationException("Native package simulation returned invalid item/point acceptance.");
                    acceptedCount = checked(acceptedCount + added);
                    remainingInc = checked(remainingInc + unacceptedInc);
                    if (added != packet.Count || unacceptedInc != 0)
                    {
                        fits = false;
                        reason = "The full batch does not fit in the player package; delivery/hand/trash overflow is not qualified. Native refunds remain disabled.";
                    }
                }
                RequirePlayer(player);
                if (GameMain.gameTick != tick) throw new InvalidOperationException("Simulation advanced during the capacity diagnostic.");
            }
            finally
            {
                try { copy?.Free(); }
                finally { before.AssertUnchanged(player.package); }
            }
            return new PackageCapacityResult(fits, reason, requestedCount, acceptedCount, requestedInc, remainingInc);
        }

        private static void RequireEntity(PlanetFactory factory, int entityId, int componentId, bool lab)
        {
            if (factory.entityPool == null || entityId <= 0 || entityId >= factory.entityCursor
                || entityId >= factory.entityPool.Length || factory.entityPool[entityId].id != entityId
                || (lab ? factory.entityPool[entityId].labId : factory.entityPool[entityId].assemblerId) != componentId)
                throw new InvalidOperationException("Production component/entity identity is inconsistent.");
        }

        private static void RequirePaused(GameData data)
        {
            if (data == null || !ReferenceEquals(data, GameMain.data) || !GameMain.isPaused || GameMain.isLoading)
                throw new InvalidOperationException("Pause the current loaded game before inspecting refund capacity.");
        }

        private static void RequirePlayer(Player player)
        {
            RequirePaused(GameMain.data);
            if (player == null || !ReferenceEquals(player, GameMain.mainPlayer) || player.package == null)
                throw new InvalidOperationException("Current player package is unavailable.");
        }

        internal sealed class PackageCapacityResult
        {
            internal bool Fits { get; }
            internal string Reason { get; }
            internal long RequestedCount { get; }
            internal long AcceptedCount { get; }
            internal long RequestedInc { get; }
            internal long RemainingInc { get; }
            internal PackageCapacityResult(bool fits, string reason, long requestedCount, long acceptedCount, long requestedInc, long remainingInc)
            {
                Fits = fits; Reason = reason; RequestedCount = requestedCount; AcceptedCount = acceptedCount;
                RequestedInc = requestedInc; RemainingInc = remainingInc;
            }
        }

        /// <summary>Captures only enough to clone and check unchanged state; never writes to the live package.</summary>
        private sealed class PackageSnapshot
        {
            private readonly StorageComponent source;
            private readonly StorageComponent.GRID[] originalGrids;
            private readonly StorageComponent.GRID[] grids;
            private readonly int size, bans, searchStart, lastFullItem, lastEmptyItem;
            private readonly EStorageType type;
            private readonly bool isPlayerInventory, changed;

            internal PackageSnapshot(StorageComponent source)
            {
                this.source = source;
                if (source.grids == null || source.size < 0 || source.grids.Length != source.size
                    || source.bans < 0 || source.bans > source.size || !source.isPlayerInventory)
                    throw new InvalidOperationException("Player package layout is unsupported.");
                originalGrids = source.grids;
                grids = (StorageComponent.GRID[])originalGrids.Clone();
                size = source.size; bans = source.bans; type = source.type; isPlayerInventory = source.isPlayerInventory;
                searchStart = source.searchStart; lastFullItem = source.lastFullItem; lastEmptyItem = source.lastEmptyItem;
                changed = source.changed;
                foreach (var grid in grids)
                {
                    if (grid.count < 0 || grid.inc < 0 || grid.filter < 0 || grid.itemId < 0 || grid.stackSize < 0
                        || (grid.count == 0 && grid.inc != 0) || (grid.count > 0 && (grid.itemId <= 0 || grid.stackSize <= 0))
                        || (grid.itemId > 0 && LDB.items.Select(grid.itemId) == null)
                        || (grid.filter > 0 && LDB.items.Select(grid.filter) == null))
                        throw new InvalidOperationException("Player package contains unsupported or malformed grid state.");
                }
            }

            internal StorageComponent CreateDetachedCopy()
            {
                var copy = new StorageComponent(size);
                Array.Copy(grids, copy.grids, size);
                copy.type = type; copy.bans = bans; copy.isPlayerInventory = isPlayerInventory;
                copy.searchStart = searchStart; copy.lastFullItem = lastFullItem; copy.lastEmptyItem = lastEmptyItem;
                copy.changed = changed;
                return copy;
            }

            internal void AssertUnchanged(StorageComponent current)
            {
                if (!ReferenceEquals(current, source) || !ReferenceEquals(current.grids, originalGrids)
                    || current.size != size || current.bans != bans || current.type != type
                    || current.isPlayerInventory != isPlayerInventory || current.searchStart != searchStart
                    || current.lastFullItem != lastFullItem || current.lastEmptyItem != lastEmptyItem || current.changed != changed
                    || current.grids.Length != grids.Length)
                    throw new InvalidOperationException("Live player-package identity or cache changed during capacity simulation; no cleanup is permitted.");
                for (int i = 0; i < grids.Length; ++i)
                {
                    var a = grids[i]; var b = current.grids[i];
                    if (a.itemId != b.itemId || a.filter != b.filter || a.count != b.count || a.stackSize != b.stackSize || a.inc != b.inc)
                        throw new InvalidOperationException("Live player-package contents changed during capacity simulation; no cleanup is permitted.");
                }
            }
        }
    }
}
