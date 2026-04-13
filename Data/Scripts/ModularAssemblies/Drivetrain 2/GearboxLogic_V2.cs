using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using static Sandbox.Game.Components.MyRenderComponentThrust;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "placeholder"
    )]
    public class GearboxLogic_V2 : MyGameLogicComponent
    {
        public IMyCubeBlock GearboxBlock;
        public IMyFunctionalBlock GearboxFunctional;
        public IMyTerminalBlock GearboxTerminal;
        public GearboxNode GearboxNode;

        //Animation Information
        public bool ShaftListDirty = true;
        public double IncomingRPM = 0;
        public float CurrentAngle = 0f;
        public bool AnimCCW = false;
        public float DistanceToCamera = 0f;
        public List<IMySlimBlock> Driveshafts = new List<IMySlimBlock>();
        public Dictionary<MyEntitySubpart, Matrix> DriveshaftMatrices = new Dictionary<MyEntitySubpart, Matrix>();

        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;

        public void SetNode(GearboxNode node)
        {
            GearboxNode = node;
        }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            GearboxFunctional = (IMyFunctionalBlock)Entity;
            GearboxBlock = (MyCubeBlock)Entity;
            GearboxTerminal = (IMyTerminalBlock)Entity;

            NeedsUpdate =
                MyEntityUpdateEnum.BEFORE_NEXT_FRAME
                | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (ShaftListDirty)
                RebuildDriveshaftTree();
            UpdateAnimation();
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateDistanceToCamera();
        }

        private void UpdateAnimation()
        {
            if (DistanceToCamera > 500f || IncomingRPM == 0 || DriveshaftMatrices.Count == 0) return;
            float degreesPerTick = (float)IncomingRPM * 360f / 3600f; // convert RPM → degrees/tick at 60 Hz
            CurrentAngle += degreesPerTick;
            CurrentAngle %= 360f;

            if (!AnimCCW)
                CurrentAngle = -CurrentAngle;

            foreach (var subShaft in DriveshaftMatrices)
            {
                var subpart = subShaft.Key;
                var initialMatrix = subShaft.Value;

                Matrix rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(CurrentAngle));
                Matrix finalMatrix = rotationMatrix * initialMatrix;
                subpart.PositionComp.SetLocalMatrix(ref finalMatrix);
            }
        }

        public void UpdateDistanceToCamera()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(GearboxBlock.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            DistanceToCamera = (float)dist;
        }

        private void RebuildDriveshaftTree()
        {
            if (Driveshafts.Count == 0) return;

            foreach (var shaft in Driveshafts)
            {
                var fat = shaft.FatBlock as MyCubeBlock;
                if (fat == null)
                    continue;

                MyEntitySubpart subpart;

                if (!fat.TryGetSubpart("Driveshaft", out subpart))
                    continue;
                if (!DriveshaftMatrices.ContainsKey(subpart))
                    DriveshaftMatrices.Add(subpart, subpart.PositionComp.LocalMatrixRef);
            }
            ShaftListDirty = false;
        }
    }
}
