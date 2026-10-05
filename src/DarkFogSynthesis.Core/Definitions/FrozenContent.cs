using System.Collections.Generic;

namespace DarkFogSynthesis.Core.Definitions
{
    /// <summary>The frozen V1 gameplay specification. No save-mode/configuration state belongs here.</summary>
    public static class FrozenContent
    {
        public static IReadOnlyList<RecipeDefinition> Recipes { get; } = FrozenList.Copy(new[]
        {
            new RecipeDefinition(ProtoIds.EnergyShard, "energy_shard", A(VanillaIds.Items.EnergyShard, 2),
                new[] { A(VanillaIds.Items.CombustibleUnit, 1), A(VanillaIds.Items.EnergeticGraphite, 1), A(VanillaIds.Items.Glass, 1) },
                120, ProductionMachine.Smelter, ProtoIds.EnergyAnalysis),
            new RecipeDefinition(ProtoIds.DarkFogMatrix, "dark_fog_matrix", A(VanillaIds.Items.DarkFogMatrix, 1),
                new[] { A(VanillaIds.Items.CrystalSilicon, 2), A(VanillaIds.Items.PhotonCombiner, 1), A(VanillaIds.Items.PlasmaExciter, 1), A(VanillaIds.Items.TitaniumGlass, 1) },
                240, ProductionMachine.MatrixLab, ProtoIds.InformationTopology),
            new RecipeDefinition(ProtoIds.SiliconNeuron, "silicon_neuron", A(VanillaIds.Items.SiliconNeuron, 1),
                new[] { A(VanillaIds.Items.MicrocrystallineComponent, 2), A(VanillaIds.Items.TitaniumAlloy, 2), A(VanillaIds.Items.CrystalSilicon, 2) },
                240, ProductionMachine.Assembler, VanillaIds.Techs.InformationMatrix),
            new RecipeDefinition(ProtoIds.MatterRecombinator, "matter_recombinator", A(VanillaIds.Items.MatterRecombinator, 1),
                new[] { A(VanillaIds.Items.PlaneFilter, 1), A(VanillaIds.Items.SuperMagneticRing, 2), A(VanillaIds.Items.Hydrogen, 2), A(VanillaIds.Items.CrystalSilicon, 2) },
                360, ProductionMachine.Assembler, VanillaIds.Techs.QuantumPrinting),
            new RecipeDefinition(ProtoIds.NegentropySingularity, "negentropy_singularity", A(VanillaIds.Items.NegentropySingularity, 1),
                new[] { A(VanillaIds.Items.StrangeMatter, 1), A(VanillaIds.Items.CasimirCrystal, 2), A(VanillaIds.Items.DeuteronFuelRod, 1), A(VanillaIds.Items.CrystalSilicon, 2) },
                480, ProductionMachine.Assembler, VanillaIds.Techs.PlaneMetallurgy),
            new RecipeDefinition(ProtoIds.CoreElement, "core_element", A(VanillaIds.Items.CoreElement, 1),
                new[] { A(VanillaIds.Items.Antimatter, 2), A(VanillaIds.Items.FrameMaterial, 2), A(VanillaIds.Items.SuperMagneticRing, 2), A(VanillaIds.Items.CrystalSilicon, 4) },
                600, ProductionMachine.Assembler, VanillaIds.Techs.ControlledAnnihilation)
        });

        public static IReadOnlyList<TechDefinition> Technologies { get; } = FrozenList.Copy(new[]
        {
            new TechDefinition(ProtoIds.EnergyAnalysis, "energy_analysis",
                new[] { VanillaIds.Techs.BattlefieldAnalysisBase }, new TechId[0],
                new[] { A(VanillaIds.Items.ElectromagneticMatrix, 100) }, 36000L, new[] { 10 },
                new[] { ProtoIds.EnergyShard }, new TechPosition(21, -55)),
            new TechDefinition(ProtoIds.InformationTopology, "information_topology",
                new[] { VanillaIds.Techs.SignalTower }, new[] { VanillaIds.Techs.StructureMatrix },
                new[] { A(VanillaIds.Items.ElectromagneticMatrix, 300), A(VanillaIds.Items.EnergyMatrix, 300), A(VanillaIds.Items.StructureMatrix, 300) },
                180000L, new[] { 6, 6, 6 }, new[] { ProtoIds.DarkFogMatrix }, new TechPosition(29, -43))
        });

        public static IReadOnlyList<CombatPrerequisite> CombatPrerequisites { get; } = FrozenList.Copy(new[]
        {
            new CombatPrerequisite(VanillaIds.Techs.DigitalAnalogComputation, VanillaIds.Techs.PrecisionDrone),
            new CombatPrerequisite(VanillaIds.Techs.MatterRecombination, VanillaIds.Techs.MagnetizedPlasmaCannon),
            new CombatPrerequisite(VanillaIds.Techs.NegentropyRecursion, VanillaIds.Techs.PlanetaryDefenseSystem),
            new CombatPrerequisite(VanillaIds.Techs.HighDensityControlledAnnihilation, VanillaIds.Techs.AntimatterCapsule)
        });

        private static ItemAmount A(ItemId item, int count) => new ItemAmount(item, count);
    }
}
