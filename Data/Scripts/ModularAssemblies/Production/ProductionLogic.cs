using Microsoft.CodeAnalysis;
using NavalPowerSystems.Common;
using NavalPowerSystems.Communication;
using NavalPowerSystems.Extraction;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.EntityComponents.Blocks;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.Production
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "NPSProductionOilCracker", "NPSProductionFuelRefinery")]
    public class ProductionLogic : MyGameLogicComponent
    {
        private IMyTerminalBlock _refinery;
        private IMyGasTank _inputTank;
        private MyObjectBuilder_Ore _dummyItem;
        private float _ratio = 0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            _refinery = Entity as IMyTerminalBlock;
            if (_refinery == null) return;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _inputTank = _refinery as IMyGasTank;
            if (_inputTank == null)
            {
                MyAPIGateway.Utilities.ShowNotification("Error: Production block is missing required components!", 2000, MyFontEnum.Red);
                return;
            }

            if (_refinery != null)
            {
                if (_refinery.BlockDefinition.SubtypeName.Contains("OilCracker"))
                {
                    _dummyItem = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>("DummyItemFuel");
                    _ratio = Config.crudeFuelOilRatio;
                }
                else if (_refinery.BlockDefinition.SubtypeName.Contains("FuelRefinery"))
                {
                    _dummyItem = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Ore>("DummyItemDiesel");
                    _ratio = Config.fuelOilDieselRatio;
                }
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation100()
        {
            ProcessConversion();
        }

        private void ProcessConversion()
        {
            var inventory = _refinery.GetInventory(0);
            if (_inputTank == null || inventory == null)
            {
                MyAPIGateway.Utilities.ShowNotification("Error: Production block is missing required components!", 2000, MyFontEnum.Red);
                return;
            }

            float gasToRemove = Config.baseRefineRate * 1.6f;
            VRage.MyFixedPoint itemsToAdd = (VRage.MyFixedPoint)Config.baseRefineRate * 1.6f * _ratio;

            if (_inputTank.FilledRatio < gasToRemove / _inputTank.Capacity)
            {
                MyAPIGateway.Utilities.ShowNotification("Not enough input resource to refine!", 2000, MyFontEnum.Red);
                return;
            }
            Utilities.ChangeTankLevel(_inputTank, -gasToRemove);
            Utilities.AddNewItem(inventory, _dummyItem, itemsToAdd);
        }

    }
}