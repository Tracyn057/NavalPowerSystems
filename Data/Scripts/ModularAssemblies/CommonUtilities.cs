using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace NavalPowerSystems.Utilities
{
    public class CommonUtilities
    {
        public static void UpdatePowerConsumption(IMyFunctionalBlock block, bool isActive)
        {
            if (block == null) return;

            var sink = block.Components.Get<MyResourceSinkComponent>();
            if (sink == null) return;

            var definition = block.SlimBlock.BlockDefinition as IMyFunctionalBlock;
            if (definition == null) return;

            float maxPower = definition.ResourceSink.MaxRequiredInputByType(MyResourceDistributorComponent.ElectricityId);

            float requiredInput = isActive ? maxPower : 0.0f;

            sink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, requiredInput);
            sink.Update();
        }

        public static void ChangeTankLevel(IMyGasTank tank, double amountLiters)
        {
            if (tank == null || !tank.Enabled)
            {
                return;
            }
            float capacity = tank.Capacity;
            if (capacity <= 0)
            {
                return;
            }

            double currentGas = tank.Capacity * tank.FilledRatio;
            double newGas = currentGas + amountLiters;
            double newRatio = Math.Round((newGas / tank.Capacity), 6, MidpointRounding.AwayFromZero);

            if (Math.Abs(tank.FilledRatio - newRatio) >= 0.000001)
            {
                tank.ChangeFilledRatio((float)newRatio, true);
            }
        }

        public static bool ShouldRemoveTankControls(IMyTerminalBlock block)
        {
            if (block == null) return false;
            string subtype = block.BlockDefinition.SubtypeName;

            return Config.Engines.Contains(subtype) || 
                Config.Propellers.Contains(subtype) ||
                subtype.Contains("NPSExtractionCrudeOutput");
        }

        public static void RemoveControls()
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyGasTank>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "Stockpile":
                    case "Refill":
                    case "Auto-Refill":
                    case "ShowInInventory":
                        control.Visible = (block) => !ShouldRemoveTankControls(block);
                        break;
                }
            }
        }

        public static void RemoveActions()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyGasTank>(out actions);

            foreach (IMyTerminalAction action in actions)
            {
                switch (action.Id)
                {
                    case "Stockpile":
                    case "Stockpile_On":
                    case "Stockpile_Off":
                    case "Refill":
                    case "Auto-Refill":
                        {
                            action.Enabled = (block) => !ShouldRemoveTankControls(block);
                            break;
                        }
                }
            }
        }
    }
}
