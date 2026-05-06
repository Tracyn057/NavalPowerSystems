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
using VRageRender.Messages;
using static NavalPowerSystems.Drivetrain.Producers.Steam.SteamTables;

namespace NavalPowerSystems.Drivetrain.Producers.Steam
{
    public class BoilerLogic : SteamSystemPart<IMyFunctionalBlock>
    {
        #region Overhead Variables
        private static BoilerLogic GetLogic(IMyTerminalBlock terminalBlock) => terminalBlock?.GameLogic?.GetAs<BoilerLogic>();
        private const double SpecificHeatWater = 4184.0;
        #endregion

        #region Boiler-specific Variables
        private BoilerStats MyStats => Config_Steam.BoilerSettings[SubtypeName];
        private MyEntitySubpart MySubpart_Signage;
        private MyEntitySubpart MySubpart_PressureNeedle;
        private Matrix MySubpart_PressureNeedleMatrix;

        private const double BoilerEfficiency = 0.8;
        private const double FuelOilEnergyDensity = 38900000;
        private float LastError = 0f;
        private float Integral;
        private float SteamMassInDrum;
        private float CurrentBar;
        private double StoredEnergy;
        private float WaterLevel;
        private double SystemTemp;
        private float MaxFuelFlow;
        private float MaxSteamFlow;
        private float PrevKp;
        private float CurKp;
        private float PrevKi;
        private float CurKi;
        private float PrevKd;
        private float CurKd;
        
        private MyResourceSinkComponent SinkWater;
        private float CurrentWaterUse;
        private MyResourceSinkComponent SinkFuel;
        private float CurrentFuelUse;
        private MyResourceSinkComponent SinkO2;
        private float CurrentO2Use;

        MySync<bool, SyncDirection.BothWays> Terminal_Arcade;
        private bool Arcade = false;
        MySync<bool, SyncDirection.BothWays> Terminal_Auto;
        private bool AutoMode = true;
        MySync<float, SyncDirection.BothWays> Terminal_FuelValve;
        private float FuelValve = 0f;
        MySync<float, SyncDirection.BothWays> Terminal_SteamValve;
        private float SteamValve = 0f;
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
        private void ThrottleControl()
        {
            var throttleStepUp = 0.005f;
            var throttleStepDn = 0.0025f;
            var moveIndicator = MyGridManager?.ForwardInput ?? 0f;

            if (AutoMode)
            {
                AdaptiveControl();

            }
        }
        private void AdaptiveControl()
        {
            float error = MyStats.OperatingBar - CurrentBar;
            float deltaTime = (float)PhysicsStep;
            float derivative = (error - LastError) / deltaTime;

            CurKp = PrevKp * (error * Math.Abs(error));

            float derivativeMultiplier = 1.0f + MathHelper.Clamp(Math.Abs(derivative) / 5f, 0f, 3f);
            CurKd = (PrevKd * derivativeMultiplier) * derivative;

            if (Math.Abs(error) < 5.0f)
            {
                Integral += error * deltaTime;
            }
            CurKi = PrevKi * Integral;

            LastError = error;

            PrevKd = CurKd;
            PrevKp = CurKp;
            PrevKi = CurKi;
        }
        private void UpdateBoilerSteam()
        {
            if (!Block.IsWorking) return;

            // Burn fuel
            double fuelLitersPerSec = FuelValve * MyStats.MaxFuelFlow * Config.globalFuelMult;
            double heatAddedJoules = fuelLitersPerSec * FuelOilEnergyDensity * BoilerEfficiency;

            // Heat exchange
            StoredEnergy += (float)(heatAddedJoules * PhysicsStep);
            SystemTemp = StoredEnergy / (MyStats.ThermalMass * 1000f);

            SteamState currentState = GetSteamState((float)SystemTemp, Bar_Out);

            // Steam generation
            float saturationTemp = CalculateSaturationTemp(Bar_Out);

            if (SystemTemp >= saturationTemp)
            {
                float latentHeat = 2000f;
                float producedMass = (float)(heatAddedJoules / (latentHeat * 1000f));

                SteamMassInDrum += producedMass * PhysicsStep;
            }

            // Load calc
            float exportedMass = (float)Load_In;
            SteamMassInDrum -= exportedMass * PhysicsStep;
            WaterLevel -= exportedMass * PhysicsStep;

            // Pressure calc
            if (SteamMassInDrum > 0)
            {
                float calculatedBar = (SteamMassInDrum * currentState.SpecificVolume) / MyStats.DrumVolume;

                Bar_Out = MathHelper.Lerp(Bar_Out, calculatedBar, 0.1f);
            }

            // Lose energy with steam loss
            float energyLost = exportedMass * currentState.Enthalpy * 1000f;
            StoredEnergy -= energyLost * PhysicsStep;
        }

        private float CalculateSaturationTemp(float bar)
        {
            if (bar <= 1.0f) return 373.15f;
            float satTemp = (float)(130.0 * Math.Pow(bar, 0.25) + 240.0);
            
            return MathHelper.Clamp(satTemp, 373.15f, 850f);
        }
        #endregion
    }
}
