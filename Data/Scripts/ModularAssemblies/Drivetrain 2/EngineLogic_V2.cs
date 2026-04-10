using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenTank), false,
            "NPS_Turbine_MT7",
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4",
            "NPS_Turbine_MT30",
            "NPSDieselEngine500KW",
            "NPSDieselEngine15MW",
            "NPSDieselEngine25MW"
    )]
    public class NewEngineLogic : MyGameLogicComponent, IMyEventProxy
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private IMyFunctionalBlock EngineBlock;
        private MyCubeBlock EngineCube;
        private IMyGasTank EngineTank;
        private double PeakRPM;
        private double PeakTorque;
        private double HeatRate;
        private float PowerCurveConstant;
        private double SystemInertia;
        private int AssemblyId = -1;
        public bool IsValid { get; set; } = false;
        public double CurrentRPM { get; private set; } = 0;
        public double CurrentTorque { get; private set; } = 0;
        public double TorqueLoad { get; set; } = 0;
        public double CurrentPower { get; private set; } = 0;
        public double PowerLoad { get; set; } = 0;
        private double RPMVarianceMult = 0.02;
        public float RequestedThrottle { get; set; } = 0f;
        private float CurrentFuelUse = 0f;
        private float CurrentO2Use = 0f;
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        private bool SinkInitialized = false;
        private MyResourceSinkComponent SinkFuel;
        private MyResourceSinkComponent SinkO2;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            EngineBlock = (IMyFunctionalBlock)Entity;
            EngineCube = (MyCubeBlock)Entity;
            EngineTank = Entity as IMyGasTank;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (EngineBlock == null || EngineCube == null || EngineTank == null)
                return;

            if (!Config.NewEngineSettings.ContainsKey(EngineBlock.BlockDefinition.SubtypeName))
                return;

            AssemblyId = ModularApi.GetContainingAssembly(EngineBlock, "Drivetrain_Definition");
            var EngineStats = Config.NewEngineSettings[EngineBlock.BlockDefinition.SubtypeName];
            PeakRPM = EngineStats.PeakRPM;
            PeakTorque = EngineStats.PeakTorque;
            HeatRate = EngineStats.HeatRate;
            PowerCurveConstant = EngineStats.PowerCurveConstant;
            SystemInertia = EngineStats.SystemInertia;
            EngineTank.Stockpile = true;
            EngineTank.AppendingCustomInfo += AppendCustomInfo;

            if (!ControlsInitialized)
            {
                CreateControls();
                ControlsInitialized = true;
            }
            if (!ActionsInitialized)
            {
                CreateActions();
                ActionsInitialized = true;
            }
            if (!SinkInitialized)
            {
                SetupResourceSinks();
                SinkInitialized = true;
            }
        }

        private void SetupResourceSinks()
        {
            var sinkFuelInfo = new MyResourceSinkInfo()
            {
                MaxRequiredInput = 250f,
                RequiredInputFunc = () => CurrentFuelUse,
                ResourceTypeId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/DieselFuel"),
            };
            var sinkO2Info = new MyResourceSinkInfo()
            {
                MaxRequiredInput = 1000f,
                RequiredInputFunc = () => CurrentO2Use,
                ResourceTypeId = MyResourceDistributorComponent.OxygenId
            };

            var fakeController = new MyShipController() { SlimBlock = EngineCube.SlimBlock };

            SinkFuel = EngineCube.Components?.Get<MyResourceSinkComponent>();
            if (SinkFuel != null)
            {
                SinkFuel.AddType(ref sinkFuelInfo);
            }
            else
            {
                SinkFuel = new MyResourceSinkComponent();
                SinkFuel.Init(MyStringHash.GetOrCompute("Thrust"), sinkFuelInfo);
                EngineCube.Components?.Add(SinkFuel);
            }

            SinkO2 = EngineCube.Components?.Get<MyResourceSinkComponent>();
            if (SinkO2 != null)
            {
                SinkO2.AddType(ref sinkO2Info);
            }
            else
            {
                SinkO2 = new MyResourceSinkComponent();
                SinkO2.Init(MyStringHash.GetOrCompute("Thrust"), sinkO2Info);
                EngineCube.Components?.Add(SinkO2);
            }

            var distributor = fakeController.GridResourceDistributor;
            if (distributor != null)
            {
                distributor.AddSink(SinkFuel);
                distributor.AddSink(SinkO2);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            if (EngineBlock == null || EngineCube == null || EngineTank == null || !EngineBlock.IsWorking)
                return;

            if (RequestedThrottle == 0f)
            {
                RequestedThrottle = 0.08f;
            }
            // Induce engine RPM flutter
            var variance = PeakRPM * RPMVarianceMult;
            var targetRPM = (int)(RequestedThrottle * (PeakRPM * 1.15)) + VRage.Utils.MyUtils.GetRandomDouble(-variance, variance); // Allow for some overspeed

            double deviation = (CurrentRPM - PeakRPM) / PeakRPM;
            double availableTorque = PeakTorque * (1 - PowerCurveConstant * Math.Pow(deviation, 2));
            availableTorque = Math.Max(availableTorque, 0);

            double governorRange = PeakRPM * 0.075; // RPM range over which the governor will adjust torque to try to reach target RPM
            double governorMult = (targetRPM - CurrentRPM) / governorRange;
            governorMult = MathHelper.Clamp(governorMult, 0, 1);
            CurrentTorque = availableTorque * governorMult;

            TorqueLoad = 0; // Drivetrain Logic will set
            var netTorque = CurrentTorque - TorqueLoad;
            var angularAcceleration = netTorque / SystemInertia;
            var changeInRPM = angularAcceleration * 9.5488 * (1f / 60f);
            CurrentRPM += changeInRPM;

            CalculateResourceUse();
            SinkFuel.Update();
            SinkO2.Update();
        }

        private void CalculateResourceUse()
        {
            if (CurrentRPM <= 0 || CurrentTorque <= 0)
            {
                CurrentFuelUse = 0;
                CurrentO2Use = 0;
                return;
            }

            double powerKw = CurrentTorque * CurrentRPM / 9.5488; // KW
            double requiredEnergy = powerKw * HeatRate / 3600; // Convert kW to kJ/s
            CurrentFuelUse = (float)(requiredEnergy / DieselEnergyDensity * Config.globalFuelMult);
            CurrentO2Use = CurrentFuelUse * 3.5f; // Approximate O2 use based on fuel use
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"RPM: {CurrentRPM:0}");
            info.AppendLine($"Torque: {CurrentTorque:0}");
            info.AppendLine($"Power: {CurrentPower:0}");
            info.AppendLine($"Fuel Flow: {CurrentFuelUse:0.00} L/s");
            info.AppendLine($"Mass Air Flow: {CurrentO2Use:0.00} L/s");
        }

        private void CreateControls()
        {

        }

        private void CreateActions()
        {

        }
    }
}