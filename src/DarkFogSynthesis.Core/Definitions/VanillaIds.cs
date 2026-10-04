namespace DarkFogSynthesis.Core.Definitions
{
    /// <summary>
    /// IDs cross-checked against author-maintained prototype export 0.10.34.28529, not a current-game validation.
    /// https://github.com/soarqin/DSPSeedCalc/tree/142cc01517060be96be77e687d44512d756de851/Prototypes
    /// The runtime must validate required prototypes by ID and structural semantics before mutation.
    /// </summary>
    public static class VanillaIds
    {
        public const string SourceCommit = "142cc01517060be96be77e687d44512d756de851";
        public const string SourceGameVersion = "0.10.34.28529";

        public static class Items
        {
            public static readonly ItemId EnergyShard = new ItemId(5206);
            public static readonly ItemId DarkFogMatrix = new ItemId(5201);
            public static readonly ItemId SiliconNeuron = new ItemId(5202);
            public static readonly ItemId MatterRecombinator = new ItemId(5203);
            public static readonly ItemId NegentropySingularity = new ItemId(5204);
            public static readonly ItemId CoreElement = new ItemId(5205);
            public static readonly ItemId CombustibleUnit = new ItemId(1128);
            public static readonly ItemId EnergeticGraphite = new ItemId(1109);
            public static readonly ItemId Glass = new ItemId(1110);
            public static readonly ItemId CrystalSilicon = new ItemId(1113);
            public static readonly ItemId PhotonCombiner = new ItemId(1404);
            public static readonly ItemId PlasmaExciter = new ItemId(1401);
            public static readonly ItemId TitaniumGlass = new ItemId(1119);
            public static readonly ItemId MicrocrystallineComponent = new ItemId(1302);
            public static readonly ItemId ParticleBroadband = new ItemId(1402);
            public static readonly ItemId PlaneFilter = new ItemId(1304);
            public static readonly ItemId SuperMagneticRing = new ItemId(1205);
            public static readonly ItemId Hydrogen = new ItemId(1120);
            public static readonly ItemId StrangeMatter = new ItemId(1127);
            public static readonly ItemId CasimirCrystal = new ItemId(1126);
            public static readonly ItemId DeuteronFuelRod = new ItemId(1802);
            public static readonly ItemId Antimatter = new ItemId(1122);
            public static readonly ItemId FrameMaterial = new ItemId(1125);
            public static readonly ItemId ElectromagneticMatrix = new ItemId(6001);
            public static readonly ItemId EnergyMatrix = new ItemId(6002);
            public static readonly ItemId StructureMatrix = new ItemId(6003);
        }

        public static class Techs
        {
            public static readonly TechId BattlefieldAnalysisBase = new TechId(1826);
            public static readonly TechId SignalTower = new TechId(1808);
            public static readonly TechId StructureMatrix = new TechId(1124);
            public static readonly TechId InformationMatrix = new TechId(1312);
            public static readonly TechId QuantumPrinting = new TechId(1203);
            public static readonly TechId PlaneMetallurgy = new TechId(1417);
            public static readonly TechId ControlledAnnihilation = new TechId(1145);
            public static readonly TechId DigitalAnalogComputation = new TechId(1901);
            public static readonly TechId MatterRecombination = new TechId(1902);
            public static readonly TechId NegentropyRecursion = new TechId(1903);
            public static readonly TechId HighDensityControlledAnnihilation = new TechId(1904);
            public static readonly TechId PrecisionDrone = new TechId(1820);
            public static readonly TechId MagnetizedPlasmaCannon = new TechId(1811);
            public static readonly TechId PlanetaryDefenseSystem = new TechId(1809);
            public static readonly TechId AntimatterCapsule = new TechId(1818);
        }
    }
}
