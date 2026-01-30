using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
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
                MyAPIGateway.Utilities.ShowNotification($"Processing {tank} Failed", 2000, "Red");
                return;
            }
            float capacity = tank.Capacity;
            if (capacity <= 0)
            {
                MyAPIGateway.Utilities.ShowNotification($"Processing {tank} Failed at {capacity}", 2000, "Red");
                return;
            }

            double currentGas = tank.Capacity * tank.FilledRatio;
            double newGas = currentGas + amountLiters;
            double newRatio = Math.Min(1.0, newGas / tank.Capacity);

            tank.ChangeFilledRatio(newRatio, true);
        }
    }
}
