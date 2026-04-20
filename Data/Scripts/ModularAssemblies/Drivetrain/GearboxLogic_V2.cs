using EmptyKeys.UserInterface;
using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;
using static VRage.Game.MyObjectBuilder_BehaviorTreeDecoratorNode;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary"
    )]
    public class GearboxLogic_V2 : MyGameLogicComponent, IMyEventProxy
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private IMyCubeBlock GearboxBlock;
        private IMyFunctionalBlock GearboxFunctional;
        private IMyTerminalBlock GearboxTerminal;
        private GearboxStats_V2 GearboxStats;
        private float BrakeEngagement = 0f;
        private double RawLoadRequest = 0;
        private double RawInputRPM = 0;
        private double RawInputTorque = 0;
        private double GearedOutputRPM = 0;
        private double GearedOutputTorque = 0;
        private double CurrentShaftRPM = 0;
        static GearboxLogic_V2 GetLogic(IMyTerminalBlock gearbox) => gearbox?.GameLogic?.GetAs<GearboxLogic_V2>();

        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        MySync<float, SyncDirection.BothWays> Terminal_ShaftBrake;

        public static readonly HashSet<string> ValidUpstream = new HashSet<string>
        {
            "NPS_Turbine_MT7",
            "NPS_Turbine_LM2500",
            "NPS_Turbine_LM2500Plus",
            "NPS_Turbine_LM2500PlusG4",
            "NPS_Turbine_MT30"
        };

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            GearboxFunctional = (IMyFunctionalBlock)Entity;
            GearboxBlock = (MyCubeBlock)Entity;
            GearboxTerminal = (IMyTerminalBlock)Entity;

            GearboxTerminal.AppendingCustomInfo += AppendingCustomInfo;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"Gear Ratio: {GearboxStats.GearRatio}");
            info.AppendLine($"Incoming Torque: {RawInputTorque:0.00}"); //From Engines
            info.AppendLine($"Incoming RPM: {RawInputRPM:0.00}"); //From Engines
            info.AppendLine($"Outgoing Torque: {GearedOutputTorque:0.00}");
            info.AppendLine($"Outgoing RPM: {CurrentShaftRPM:0.00}");
        }

        public override void UpdateOnceBeforeFrame()
        {
            GearboxStats = Drivetrain_Config.GearboxSettings_V2[GearboxBlock.BlockDefinition.SubtypeId];

            if (!ControlsInitialized)
                CreateControls();
            if (!ActionsInitialized)
                CreateActions();

            Terminal_ShaftBrake.SetLocalValue(BrakeEngagement);
            Terminal_ShaftBrake.ValueChanged += Terminal_ShaftBrake_ValueChanged;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            //Clean slate before information gathering
            RawLoadRequest = 0;
            RawInputRPM = 0;
            RawInputTorque = 0;
            GearedOutputRPM = 0;
            GearedOutputTorque = 0;
        }

        private void Terminal_ShaftBrake_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            BrakeEngagement = obj.Value;
            UpdateControls();
        }

        private void CreateControls()
        {
            if (ControlsInitialized) return;

            ControlsInitialized = true;

            {
                var Control_ShaftBrake = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>("NPS_Gearbox_TerminalControl_ShaftBrake");
                Control_ShaftBrake.Title = MyStringId.GetOrCompute("Shaft Brake Override");
                Control_ShaftBrake.Visible = Control_ShaftBrake_Visible;
                Control_ShaftBrake.SetLimits(0f, 1f);
                Control_ShaftBrake.SupportsMultipleBlocks = true;
                Control_ShaftBrake.Getter = Control_Terminal_ShaftBrake_Getter;
                Control_ShaftBrake.Setter = Control_Terminal_ShaftBrake_Setter;
                Control_ShaftBrake.Writer = Control_Terminal_ShaftBrake_Writer;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_ShaftBrake);
            }

            //Shaft brake
            //Reverse select
        }

        private void CreateActions()
        {
            if (ActionsInitialized) return;

            ActionsInitialized = true;

            {

            }

            //Shaft brake
            //Reverse select
        }

        public static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Gearbox_TerminalControl_ShaftBrake":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        static bool Control_ShaftBrake_Visible(IMyTerminalBlock gearbox)
        {
            var logic = GetLogic(gearbox);
            return (logic == null ? false : logic.GearboxStats.MaxBrakeTorque > 0);
        }

        static float Control_Terminal_ShaftBrake_Getter(IMyTerminalBlock engine)
        {
            var logic = GetLogic(engine);
            return logic == null ? 0.01f : logic.Terminal_ShaftBrake;
        }

        static void Control_Terminal_ShaftBrake_Setter(IMyTerminalBlock engine, float value)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                logic.Terminal_ShaftBrake.ValidateAndSet(value);
        }

        static void Control_Terminal_ShaftBrake_Writer(IMyTerminalBlock engine, StringBuilder writer)
        {
            var logic = GetLogic(engine);
            if (logic != null)
                writer.Append((int)(logic.Terminal_ShaftBrake * 100f)).Append('%');
        }
    }
}
