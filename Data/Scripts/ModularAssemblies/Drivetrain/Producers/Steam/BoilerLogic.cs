using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRageMath;

namespace NavalPowerSystems.Drivetrain.Producers.Steam
{
    public class BoilerLogic : SteamSystemPart<IMyFunctionalBlock>
    {
        #region Overhead Variables
        private static BoilerLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<BoilerLogic>();
        private const double SpecificHeatWater = 4184.0;
        #endregion

        #region Boiler-specific Variables
        private BoilerStats MyStats => Steam_Config.BoilerSettings[SubtypeName];
        private MyEntitySubpart MySubpart_Signage;
        private MyEntitySubpart MySubpart_PressureNeedle;
        private Matrix MySubpart_PressureNeedleMatrix;

        private float FuelFlow;
        private float MaxFuelFlow;
        
        private MyResourceSinkComponent SinkWater;
        private float CurrentWaterUse;
        private MyResourceSinkComponent SinkFuel;
        private float CurrentFuelUse;
        private MyResourceSinkComponent SinkO2;
        private float CurrentO2Use;

        MySync<bool, SyncDirection.BothWays> Terminal_Arcade;
        private bool Arcade = false;
        MySync<float, SyncDirection.BothWays> Terminal_FuelValve;
        private float FuelValve;
        MySync<float, SyncDirection.BothWays> Terminal_SteamValve;
        private float SteamValve;
        #endregion

        #region Init and Setup
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            Entity.TryGetSubpart("Signage", out MySubpart_Signage);
            Entity.TryGetSubpart("PressureNeedle", out MySubpart_PressureNeedle);
            if (MySubpart_PressureNeedle != null)
                MySubpart_PressureNeedleMatrix = MySubpart_PressureNeedle.PositionComp.LocalMatrixRef;

            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME
                | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }
        #endregion

        #region Update Cycles
        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            if (!MySubpart_Signage.IsPreview)
                MySubpart_Signage.Render.Visible = false;
        }
        #endregion

        #region Main Operation
        private void BuildSteam()
        {
            if (!Block.IsWorking) return;

            double fuelEnergy = FuelFlow * Drivetrain_Config.FuelOilEnergyDensity;
        }
        #endregion
    }
}
