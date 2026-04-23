using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;

namespace NavalPowerSystems.Drivetrain.Transformers
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "NPS_Gearbox_MRG",
            "NPS_Gearbox_DoublePlanetary"
    )]
    public class GearboxLogic : DrivetrainPart<IMyFunctionalBlock>
    {
        static GearboxLogic GetLogic(IMyTerminalBlock gearbox) => gearbox?.GameLogic?.GetAs<GearboxLogic>();
        private GearboxStats MyStats => Drivetrain_Config.GearboxSettings[SubtypeName];

        #region Sync, Terminal and Settings Variables
        private static bool ControlsInitialized = false;
        MySync<float, SyncDirection.BothWays> Terminal_BrakeEngagement;
        public float BrakeEngagement;
        private GearboxSettings Settings;
        #endregion



        #region Init and Updates
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            //Block.AppendingCustomInfo += AppendingCustomInfo; //Nothing to keep track of yet? Maybe add a method to send info here from system later.

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (!ControlsInitialized)
            {
                CreateControls<IMyFunctionalBlock>();
                CreateActions<IMyFunctionalBlock>();
                ControlsInitialized = true;
            }

            LoadSettings();
            SaveSettings();

            Terminal_BrakeEngagement.SetLocalValue(BrakeEngagement);
            Terminal_BrakeEngagement.ValueChanged += Terminal_BrakeEngagement_ValueChanged;

            //NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        private void Terminal_BrakeEngagement_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            BrakeEngagement = obj.Value;
            UpdateControls();
            SaveSettings();
        }

        public override double GetLoad()
        {
            return BrakeEngagement * MyStats.MaxBrakeTorque;
        }

        public override float GetRatio()
        {
            return MyStats.GearRatio;
        }

        public override DrivetrainRole GetRole()
        {
            return DrivetrainRole.Transformer;
        }

        //private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder info)
        //{
        //    info.AppendLine($"Gear Ratio: {MyStats.GearRatio}");
        //    info.AppendLine($"Incoming Torque: {Torque_In:0.00}");
        //    info.AppendLine($"Incoming RPM: {RPM_In:0.00}");
        //    info.AppendLine($"Outgoing Torque: {Torque_Out:0.00}");
        //    info.AppendLine($"Outgoing RPM: {RPM_Out:0.00}");
        //}

        static bool Control_Visible(IMyTerminalBlock block)
        {
            return GetLogic(block) != null;
        }

        static void CreateControls<IMyFunctionalBlock>()
        {
            if (ControlsInitialized) return;

            {
                var Control_ShaftBrake = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>("NPS_Gearbox_TerminalControl_ShaftBrake");
                Control_ShaftBrake.Title = MyStringId.GetOrCompute("Shaft Brake Override");
                Control_ShaftBrake.Visible = Control_Visible;
                Control_ShaftBrake.SetLimits(0f, 1f);
                Control_ShaftBrake.SupportsMultipleBlocks = true;
                Control_ShaftBrake.Getter = Control_Terminal_ShaftBrake_Getter;
                Control_ShaftBrake.Setter = Control_Terminal_ShaftBrake_Setter;
                Control_ShaftBrake.Writer = Control_Terminal_ShaftBrake_Writer;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_ShaftBrake);
            }
        }

        static void CreateActions<IMyFunctionalBlock>()
        {
            if (ControlsInitialized) return;

            {
                //Increase Brake
            }

            {
                //Decrease Brake
            }

            {
                //Brake Off
            }

            {
                //Brake On
            }
        }

        public static void UpdateControls()
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);
            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "Gearbox_TerminalControl_ShaftBrake":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        static float Control_Terminal_ShaftBrake_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return logic == null ? 0.01f : logic.Terminal_BrakeEngagement;
        }

        static void Control_Terminal_ShaftBrake_Setter(IMyTerminalBlock block, float value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_BrakeEngagement.ValidateAndSet(value);
        }

        static void Control_Terminal_ShaftBrake_Writer(IMyTerminalBlock block, StringBuilder writer)
        {
            var logic = GetLogic(block);
            if (logic != null)
                writer.Append((int)(logic.Terminal_BrakeEngagement * 100f)).Append('%');
        }
        #endregion

        #region Settings and Saving
        private void SetDefaultSettings()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Settings.BrakeEngagement = 0f;
        }

        internal virtual bool LoadSettings()
        {
            if (Settings == null)
                Settings = new GearboxSettings();

            if (Block.Storage == null)
            {
                SetDefaultSettings();
                return false;
            }

            string rawData;
            if (!Block.Storage.TryGetValue(SettingsGuid, out rawData))
            {
                SetDefaultSettings();
                return false;
            }

            try
            {
                var loadedSettings =
                    MyAPIGateway.Utilities.SerializeFromBinary<GearboxSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings != null)
                {
                    Settings.BrakeEngagement = loadedSettings.BrakeEngagement;

                    return true;
                }
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole("Exception in loading Gearbox settings: " + e);
                MyAPIGateway.Utilities.ShowMessage("Naval Power Systems", "Exception in loading Gearbox settings: " + e);
                ModularApi.Log("Exception in loading Gearbox settings: " + e);
            }

            return false;
        }

        private void SaveSettings()
        {
            if (Block == null || Settings == null)
            {
                ModularApi.Log($"Block or Settings null on Gearbox.");
                return; // called too soon or after it was already closed, ignore
            }

            if (MyAPIGateway.Utilities == null)
                throw new NullReferenceException(
                    $"MyAPIGateway.Utilities == null; entId={Entity?.EntityId}; Test log 2");

            if (Block.Storage == null)
                Block.Storage = new MyModStorageComponent();

            Block.Storage.SetValue(SettingsGuid,
                Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(Settings)));
        }
        #endregion
    }

    [ProtoContract(UseProtoMembersOnly = true)]
    internal class GearboxSettings
    {
        [ProtoMember(1)]
        public float BrakeEngagement;
    }
}
