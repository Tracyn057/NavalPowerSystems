using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace NavalPowerSystems.Debug
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class DebugTanks : MySessionComponentBase
    {
        public override void LoadData()
        {
            if (MyAPIGateway.Session.IsServer || MyAPIGateway.Session.Player != null)
            {
                MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
            }
        }

        protected override void UnloadData()
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
            if (input != null)
            {
                if (input == "oil")
                    type = "Crude";
                else if (input == "fuel")
                    type = "Fuel";
                else if (input == "diesel")
                    type = "Diesel";

                ExecuteFillTanks(GetTargetGrid(), type);
            }
            else
            {
                MyAPIGateway.Utilities.ShowMessage("NPS Debug Fill", "Invalid command. Options: Oil, Fuel, Diesel");
            };
            
            
        }

        private void ExecuteFillTanks(IMyCubeGrid grid, string gasSubtype)
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => b.FatBlock is IMyGasTank);
            int filledCount = 0;

            foreach (var slim in blocks)
            {
                var tank = slim.FatBlock as IMyGasTank;

                if (tank.BlockDefinition.SubtypeName.Contains(gasSubtype))
                {
                    FillTanks(tank);
                    filledCount++;
                }
            }

            MyAPIGateway.Utilities.ShowNotification($"NPS: Filled {filledCount} tanks with {gasSubtype}.", 3000, "Green");
        }

        public static void FillTanks(IMyGasTank tank)
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
            tank.ChangeFilledRatio(1.0f, true);
        }

        private IMyCubeGrid GetTargetGrid()
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var start = camMatrix.Translation;
            var end = start + (camMatrix.Forward * 50);

            IHitInfo hit;
            if (MyAPIGateway.Physics.CastRay(start, end, out hit))
            {
                return hit.HitEntity as IMyCubeGrid;
            }
            return null;
        }
    }
}
