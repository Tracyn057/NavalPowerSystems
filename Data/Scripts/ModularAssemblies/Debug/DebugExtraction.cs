using NavalPowerSystems.Extraction;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.Debug
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class DebugExtraction : MySessionComponentBase
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
            if (!messageText.StartsWith("/nps debugextract", StringComparison.OrdinalIgnoreCase))
                return;

            sendToOthers = false;

            string[] parts = messageText.Split(' ');
            if (parts.Length < 3)
            {
                MyAPIGateway.Utilities.ShowMessage("NPS Debug Extraction", "Usage: /nps debugExtract [Land|Sea]");
                return;
            }

            string input = parts[2].ToLower();
            if (input != null)
            {
                SetDebug(input);
            }
            else
            {
                MyAPIGateway.Utilities.ShowMessage("NPS Debug Extraction", "Usage: /nps debugExtract [Land|Sea]");
            };
        }

        private void SetDebug(string input)
        {
            var logic = GetTargetDerrick();
            if (logic == null)
            {
                MyAPIGateway.Utilities.ShowMessage("NPS Debug Extraction", "No Derrick found.");
                return;
            }
            if (input == "land")
                logic._isDebug = !logic._isDebug;
            else if (input == "sea")
                logic._isDebugOcean = !logic._isDebugOcean;
        }

        private DerrickLogic GetTargetDerrick()
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var start = camMatrix.Translation;
            var end = start + (camMatrix.Forward * 50);

            IHitInfo hit;
            if (MyAPIGateway.Physics.CastRay(start, end, out hit))
            {
                var grid = hit.HitEntity as IMyCubeGrid;
                if (grid != null)
                {
                    Vector3I blockPos = grid.WorldToGridInteger(hit.Position + (camMatrix.Forward * 0.1));
                    IMySlimBlock slimBlock = grid.GetCubeBlock(blockPos);

                    if (slimBlock?.FatBlock != null)
                    {
                        return slimBlock.FatBlock.GameLogic?.GetAs<DerrickLogic>();
                    }
                }
            }
            return null;
        }
    }
}
