using NavalPowerSystems.Communication;
using NavalPowerSystems.DieselEngines;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.ModAPI;
using VRage.Utils;

namespace NavalPowerSystems.Common
{
    internal static class EngineTerminalHelpers
    {
        internal static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;

        internal static void AddEngineControls<T>(EngineManager session) where T : IMyTerminalBlock
        {
            AddCombobox<T>(session, "EngineOrder", "Engine Order", "",
                GetThrottleModes, RequestThrottleModes, ListThrottleModes, UiThrottleSlider);

            AddSliderThrottle<T>(session, "ThrottleOverride", "Throttle Override", "",
                GetThrottle, RequestSetThrottle, UiThrottleSlider, GetMinThrottle, GetMaxThrottle);
        }

        private static EngineSystem GetSystem(IMyTerminalBlock block)
        {
            if (block == null || EngineManager.I == null) return null;

            int assemblyId = NavalPowerSystems.ModularDefinition.ModularApi.GetGridEntityId(block.EntityId);

            EngineSystem system;
            return EngineManager.I.EngineSystems.TryGetValue(assemblyId, out system) ? system : null;
        }

        internal static bool UiThrottleSlider(IMyTerminalBlock block) => GetSystem(block) != null;

        internal static float GetThrottle(IMyTerminalBlock block) => GetSystem(block)?.TargetThrottle ?? 0f;

        internal static void RequestSetThrottle(IMyTerminalBlock block, float value)
        {
            var system = GetSystem(block);
            if (system != null) system.SetTargetThrottle(value);
        }

        internal static long GetThrottleModes(IMyTerminalBlock block)
        {
            float t = GetThrottle(block);
            if (t <= 0) return 0;
            if (t <= 0.25f) return 1;
            if (t <= 0.55f) return 2;
            if (t <= 0.85f) return 3;
            return 4;
        }

        internal static void RequestThrottleModes(IMyTerminalBlock block, long key)
        {
            float val = 0;
            if (key == 1) val = 0.2f;
            else if (key == 2) val = 0.5f;
            else if (key == 3) val = 0.8f;
            else if (key == 4) val = 1.0f;
            RequestSetThrottle(block, val);
        }

        internal static void ListThrottleModes(List<MyTerminalControlComboBoxItem> list)
        {
            list.Add(new MyTerminalControlComboBoxItem { Key = 0, Value = MyStringId.GetOrCompute("Stop") });
            list.Add(new MyTerminalControlComboBoxItem { Key = 1, Value = MyStringId.GetOrCompute("Slow") });
            list.Add(new MyTerminalControlComboBoxItem { Key = 2, Value = MyStringId.GetOrCompute("Half") });
            list.Add(new MyTerminalControlComboBoxItem { Key = 3, Value = MyStringId.GetOrCompute("Full") });
            list.Add(new MyTerminalControlComboBoxItem { Key = 4, Value = MyStringId.GetOrCompute("Flank") });
        }

        internal static float GetMinThrottle(IMyTerminalBlock block) => 0f;
        internal static float GetMaxThrottle(IMyTerminalBlock block) => 1.25f;

        internal static void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            var system = GetSystem(block);
            if (system == null) return;
            sb.AppendLine("\n--- NPS Engine Status ---");
            sb.Append("Throttle: ").Append((system.TargetThrottle * 100).ToString("F0")).AppendLine("%");
        }

        // --- FACTORY METHODS ---

        internal static void AddCombobox<T>(EngineManager session, string name, string title, string tooltip, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter, Action<List<MyTerminalControlComboBoxItem>> fill, Func<IMyTerminalBlock, bool> visible) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, T>("NPS_" + name);
            c.Title = MyStringId.GetOrCompute(title);
            c.ComboBoxContent = fill;
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visible;
            MyAPIGateway.TerminalControls.AddControl<T>(c);
        }

        internal static void AddSliderThrottle<T>(EngineManager session, string name, string title, string tooltip, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Func<IMyTerminalBlock, bool> visible, Func<IMyTerminalBlock, float> min, Func<IMyTerminalBlock, float> max) where T : IMyTerminalBlock
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>("NPS_" + name);
            c.Title = MyStringId.GetOrCompute(title);
            c.Getter = getter;
            c.Setter = setter;
            c.Visible = visible;
            c.SetLimits(min, max);
            c.Writer = (b, sb) => sb.Append((getter(b) * 100).ToString("F0")).Append("%");
            MyAPIGateway.TerminalControls.AddControl<T>(c);
            CreateCustomActions<T>.CreateSliderActionSetThrottle(session, c, name, 0, 1.25f, 0.05f, visible);
        }
    }
}