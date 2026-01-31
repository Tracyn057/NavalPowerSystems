using NavalPowerSystems.DieselEngines;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Text;
using VRageMath;

namespace NavalPowerSystems.Common
{
    internal static class CreateCustomActions<T> where T : IMyTerminalBlock
    {
        internal static void CreateSliderActionSetThrottle(EngineManager session, IMyTerminalControlSlider slider, string name, float min, float max, float step, Func<IMyTerminalBlock, bool> visible)
        {
            var control = (IMyTerminalControl)slider;
            string baseId = control.Id;

            var inc = MyAPIGateway.TerminalControls.CreateAction<T>(baseId + "_Increase");
            inc.Name = new StringBuilder(slider.Title.String).Append(" +");
            inc.Icon = @"Textures\GUI\Icons\Actions\Increase.dds";
            inc.Action = (block) =>
            {
                float val = slider.Getter(block);
                slider.Setter(block, MathHelper.Clamp(val + step, min, max));
            };
            inc.Writer = (block, sb) => slider.Writer(block, sb);

            inc.Enabled = (block) => visible(block) && EngineTerminalHelpers.IsReady(block);

            MyAPIGateway.TerminalControls.AddAction<T>(inc);

            var dec = MyAPIGateway.TerminalControls.CreateAction<T>(baseId + "_Decrease");
            dec.Name = new StringBuilder(slider.Title.String).Append(" -");
            dec.Icon = @"Textures\GUI\Icons\Actions\Decrease.dds";
            dec.Action = (block) =>
            {
                float val = slider.Getter(block);
                slider.Setter(block, MathHelper.Clamp(val - step, min, max));
            };
            dec.Writer = (block, sb) => slider.Writer(block, sb);

            dec.Enabled = (block) => visible(block) && EngineTerminalHelpers.IsReady(block);

            MyAPIGateway.TerminalControls.AddAction<T>(dec);
        }

        internal static void CreateComboboxCycleAction(EngineManager session, IMyTerminalControlCombobox combo, string name, Func<IMyTerminalBlock, bool> visible)
        {
            var control = (IMyTerminalControl)combo;
            var action = MyAPIGateway.TerminalControls.CreateAction<T>(control.Id + "_Cycle");
            action.Name = new StringBuilder(combo.Title.String).Append(" Cycle");
            action.Icon = @"Textures\GUI\Icons\Actions\SwitchOn.dds";

            action.Action = (block) =>
            {
                long current = combo.Getter(block);
                long next = (current + 1) % 5;
                combo.Setter(block, next);
            };

            action.Writer = (block, sb) =>
            {
                long val = combo.Getter(block);
                string label = "Stop";
                if (val == 1) label = "Slow";
                else if (val == 2) label = "Half";
                else if (val == 3) label = "Full";
                else if (val == 4) label = "Flank";
                sb.Append(label);
            };

            action.Enabled = (block) => visible(block) && EngineTerminalHelpers.IsReady(block);

            action.ValidForGroups = true;

            MyAPIGateway.TerminalControls.AddAction<T>(action);
        }
    }
}