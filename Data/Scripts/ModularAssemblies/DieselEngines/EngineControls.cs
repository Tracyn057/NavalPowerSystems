using NavalPowerSystems.Communication;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Text;
using VRage.ModAPI;
using VRage.Utils;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.DieselEngines
{
    public static class EngineControls
    {
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private const string ID_PREFIX = "NPS_Engine_";
        private static bool _controlsCreated = false;

        public static void DoControls()
        {
            if (_controlsCreated) return;

            var speedList = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyFunctionalBlock>(ID_PREFIX + "SpeedPreset");
            speedList.Title = MyStringId.GetOrCompute("Engine Order");
            speedList.ListContent = (block, list, selected) =>
            {
                list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("Stop"), MyStringId.GetOrCompute("0%"), SpeedSetting.Stop));
                list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("Half"), MyStringId.GetOrCompute("50%"), SpeedSetting.Half));
                list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("Full"), MyStringId.GetOrCompute("80%"), SpeedSetting.Full));
                list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("Flank"), MyStringId.GetOrCompute("100%"), SpeedSetting.Flank));
                list.Add(new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("Custom"), MyStringId.GetOrCompute("Slider"), SpeedSetting.Custom));
            };
            speedList.ItemSelected = (block, selected) =>
            {
                if (selected.Count > 0) ApplyThrottle(block, (float)selected[0].UserData);
            };
            speedList.Visible = (block) => block.BlockDefinition.SubtypeName == "NPSEnginesController";
            MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(speedList);

            var speedInput = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, IMyFunctionalBlock>(ID_PREFIX + "TargetSpeedText");
            speedInput.Title = MyStringId.GetOrCompute("Target Speed");
            speedInput.Tooltip = MyStringId.GetOrCompute("Enter speed in m/s, or append kn or kts for knots.");

            speedInput.Getter = (block) => {
                var system = GetSystem(block);
                return new StringBuilder(system?.UserSpeedInput ?? "0");
            };

            speedInput.Setter = (block, sb) => {
                string input = sb.ToString();
                var system = GetSystem(block);
                if (system != null)
                {
                    system.UserSpeedInput = input;
                    system.ParseSpeedInput(input);
                }
            };

            speedInput.Visible = (block) => block.BlockDefinition.SubtypeName == "NPSEnginesController";
            MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(speedInput);

            var customSlider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, IMyFunctionalBlock>(ID_PREFIX + "TargetThrottle");
            customSlider.Title = MyStringId.GetOrCompute("Target Throttle");
            customSlider.SetLimits(0, 125);
            customSlider.Writer = (block, sb) => sb.Append((int)customSlider.Getter(block)).Append("%");
            customSlider.Getter = (block) => GetCustomThrottle(block);
            customSlider.Setter = (block, val) => SetCustomThrottle(block, val);
            customSlider.Visible = (block) => block.BlockDefinition.SubtypeName == "NPSEnginesController";
            MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(customSlider);

            _controlsCreated = true;
        }

        private static void ApplyThrottle(IMyTerminalBlock block, float val)
        {
            EngineSystem system;
            if (EngineManager.EngineSystems.TryGetValue(GetAssemblyId(block), out system))
            {
                system.SetTargetThrottle(val);
            }
        }

        private static int GetAssemblyId(IMyTerminalBlock block)
        {
            return ModularApi.GetContainingAssembly(block, "Engine_Definition");
        }

        private static void SetCustomThrottle(IMyTerminalBlock block, float val)
        {
            int assemblyId = GetAssemblyId(block);

            EngineSystem system;
            if (EngineManager.EngineSystems.TryGetValue(assemblyId, out system))
            {
                system.SetTargetThrottle(val);
            }
        }

        private static float GetCustomThrottle(IMyTerminalBlock block)
        {
            int assemblyId = GetAssemblyId(block);

            EngineSystem system;
            if (EngineManager.EngineSystems.TryGetValue(assemblyId, out system))
            {
                return system.TargetThrottle;
            }

            return 0f;
        }

        private static EngineSystem GetSystem(IMyTerminalBlock block)
        {
            if (block == null) return null;

            int assemblyId = ModularApi.GetContainingAssembly(block, "Engine_Definition");

            EngineSystem system;
            if (EngineManager.EngineSystems.TryGetValue(assemblyId, out system))
            {
                return system;
            }

            return null;
        }
    }
}