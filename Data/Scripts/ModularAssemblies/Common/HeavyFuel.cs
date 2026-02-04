using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace NavalPowerSystems
{

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class HeavyGasSession : MySessionComponentBase
    {
        public static bool EnableNPCs = false;

        class HeavyGasSettings
        {
            public bool EnableNPCs = false;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenTank), false)]
    public class HeavyDiesel : MyGameLogicComponent
    {
        public const double densityCrude = 0.92;
        public const double densityFuel = 0.96;
        public const double densityDiesel = 0.85;

        private IMyGasTank tank;
        bool SetupComplete = false;
        double massMultiplier = 0;
        bool NPCOwned = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            tank = (IMyGasTank)Entity;

            MyGasTankDefinition tankDef = (MyGasTankDefinition)tank.SlimBlock.BlockDefinition;

            if (tankDef != null && tankDef.StoredGasId.SubtypeName == "CrudeOil")
                massMultiplier = densityCrude;
            if (tankDef != null && tankDef.StoredGasId.SubtypeName == "FuelOil")
                massMultiplier = densityFuel;
            if (tankDef != null && tankDef.StoredGasId.SubtypeName == "DieselFuel")
                massMultiplier = densityDiesel;

            NeedsUpdate = massMultiplier > 0f ? MyEntityUpdateEnum.EACH_10TH_FRAME : MyEntityUpdateEnum.NONE;
        }

        private void CheckIfNPCOwned(IMyCubeGrid grid)
        {
            NPCOwned = true;
            foreach (var owner in grid.BigOwners)
            {
                if (owner == 0)
                    continue;

                if (MyAPIGateway.Players.TryGetSteamId(owner) > 0)
                    NPCOwned = false;
            }
        }

        private void OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            arg1.OnBlockOwnershipChanged -= CheckIfNPCOwned;
            arg1.OnGridSplit -= OnGridSplit;
            arg2.OnBlockOwnershipChanged -= CheckIfNPCOwned;
            arg2.OnGridSplit -= OnGridSplit;

            tank.CubeGrid.OnBlockOwnershipChanged += CheckIfNPCOwned;
            tank.CubeGrid.OnGridSplit += OnGridSplit;

            CheckIfNPCOwned(tank.CubeGrid);
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (SetupComplete == false)
            {
                tank.CubeGrid.OnBlockOwnershipChanged += CheckIfNPCOwned;
                tank.CubeGrid.OnGridSplit += OnGridSplit;
                CheckIfNPCOwned(tank.CubeGrid);

                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
                SetupComplete = true;
            }
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            MyInventory inv = (MyInventory)tank.GetInventory();
            MyFixedPoint newExternalMass = (MyFixedPoint)((tank.FilledRatio * tank.Capacity) * massMultiplier);

            if (HeavyGasSession.EnableNPCs == false && NPCOwned == true)
            {
                newExternalMass = 0;
            }

            if (inv != null && newExternalMass != inv.ExternalMass)
            {
                inv.ExternalMass = newExternalMass;
                inv.Refresh();
            }
        }
    }
}

