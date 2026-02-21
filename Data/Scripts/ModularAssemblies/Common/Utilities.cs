using NavalPowerSystems;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace NavalPowerSystems.Common
{
    public class Utilities
    {
        //Utility method to change gas level in a tank by a certain amount of liters, with checks for validity and capacity
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
            double newRatio = Math.Round(newGas / tank.Capacity, 6, MidpointRounding.AwayFromZero);
            newRatio = Math.Max(0.0, Math.Min(1.0, newRatio));

            if (Math.Abs(tank.FilledRatio - newRatio) >= 0.000001)
            {
                tank.ChangeFilledRatio((float)newRatio, true);
            }
        }
        //Utility method to add items to an inventory, with checks for fitting and server authority
        public static void AddNewItem(IMyInventory inventory, MyObjectBuilder_PhysicalObject newItem, VRage.MyFixedPoint count)
        {
            if (!MyAPIGateway.Session.IsServer) return;

            var newInv = (MyInventory)inventory;
            MyDefinitionId defId = newItem.GetId();
            VRage.MyFixedPoint fittingAmount = newInv.ComputeAmountThatFits(defId);

            if (fittingAmount >= count)
            {
                inventory.AddItems(count, newItem);
            }
            else if (fittingAmount > 0)
            {
                inventory.AddItems(fittingAmount, newItem);
            }
        }
        //Utility method to determine if tank controls should be removed based on block subtype, used for hiding stockpile/refill options on certain tanks
        public static bool ShouldRemoveTankControls(IMyTerminalBlock block)
        {
            if (block == null) return false;
            string subtype = block.BlockDefinition.SubtypeId;

            return Config.EngineSubtypes.Contains(subtype) || 
                Config.PropellerSubtypes.Contains(subtype) ||
                subtype == "NPSExtractionCrudeOutput" ||
                subtype == "NPSProductionCrudeInput" ||
                subtype == "NPSProductionFuelInput";
        }
        //Utility method to remove or hide terminal controls for gas tanks based on block subtype, used to prevent player interaction with certain tanks
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
        //Utility method to remove or hide terminal actions for gas tanks based on block subtype, used to prevent player interaction with certain tanks
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
