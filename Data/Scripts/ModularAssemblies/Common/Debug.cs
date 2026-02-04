using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NavalPowerSystems.Common
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class NPSDebug : MySessionComponentBase
    {
        public override void Load()
        {
            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
            {
                MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
            }
        }

        protected override void Unloadd()
        {
            MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/nps debugfill", StringComparison.OrdinalIgnoreCase))
                return;

            sendToOthers = false;

            string[] parts = messageText.Split(' ');
            if (parts.Length < 3)
            {
                MyAPIGateway.Utilities.ShowMessage("NPS Debug Fill", "Usage: /nps debugFill [Oil|Fuel|Diesel]");
                return;
            }

            string input = parts[2].ToLower();
            string type = null;
            if (type != null)
            {
                if (input == "oil")
                    type = "CrudeOil";
                else if (input == "fuel")
                    type = "FuelOil";
                else if (input == "diesel")
                    type = "DieselFuel";

                ExecuteDebugFill(type);
            }
            else
            {
                MyAPIGateway.Utilities.ShowMessage("NPS Debug Fill", "Invalid command. Options: Oil, Fuel, Diesel");
            };
            
            
        }

        private void ExecuteDebugFill(string type)
        {
            IMyCubeGrid targetGrid = GetTargetGrid();
            if (targetGrid == null)
            {
                MyAPIGateway.Utilities.ShowNotification("No grid found!", 2000, MyFontEnum.Red);
                return;
            }

            string targetGasId = "";
            switch (type)
            {
                case "oil": targetGasId = "CrudeOil"; break;
                case "fuel": targetGasId = "FuelOil"; break;
                case "diesel": targetGasId = "DieselFuel"; break;
                default:
                    MyAPIGateway.Utilities.ShowMessage("NPS Debug", "Unknown type. Use oil, fuel, or diesel.");
                    return;
            }

            FillTanks(targetGrid, targetGasId);
        }

        private void FillTanks(IMyCubeGrid grid, string gasSubtype)
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => b.FatBlock is IMyGasTank);
            int filledCount = 0;

            foreach (var slim in blocks)
            {
                var tank = slim.FatBlock as IMyGasTank;
                // Check the tank's definition to see if it holds the right gas
                // Note: You may need to check tank.BlockDefinition.Context or use the GasProperties
                if (tank.BlockDefinition.SubtypeName.Contains(gasSubtype) || tank.DetailedInfo.Contains(gasSubtype))
                {
                    tank.ChangeFillRatio(1.0f);
                    filledCount++;
                }
            }

            MyAPIGateway.Utilities.ShowNotification($"NPS: Filled {filledCount} tanks with {gasSubtype}.", 3000, MyFontEnum.Green);
        }

        private IMyCubeGrid GetTargetGrid()
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var start = camMatrix.Translation;
            var end = start + (camMatrix.Forward * 50);

            IHitInfo hit;
            if (MyAPIGateway.Physics.CastRay(start, end, out hit))
            {
                return hit.Element as IMyCubeGrid;
            }
            return null;
        }
    }
}
