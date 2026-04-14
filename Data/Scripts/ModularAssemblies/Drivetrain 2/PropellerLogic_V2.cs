using EmptyKeys.UserInterface.Controls;
using NavalPowerSystems.Drivetrain;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
            "placeholder"
    )]
    public class PropellerLogic_V2 : MyGameLogicComponent
    {
        public IMyCubeBlock PropellerBlock;
        public IMyFunctionalBlock PropellerFunctional;
        public IMyTerminalBlock PropellerTerminal;
        public MyEntitySubpart PropellerSubpart;
        public IMyCubeGrid PropellerGrid;
        public Matrix PropellerInitialMatrix;
        public PropellerNode PropellerNode;
        public PropellerStats_V2 PropellerStats;

        public double IncomingThrust;

        //Animation Information
        public bool ShaftListDirty = true;
        public double IncomingRPM = 0;
        public float CurrentAngle = 0f;
        public bool AnimCCW = false;
        public float DistanceToCamera = 0f;
        public List<IMySlimBlock> Driveshafts = new List<IMySlimBlock>();
        public Dictionary<MyEntitySubpart, Matrix> DriveshaftMatrices = new Dictionary<MyEntitySubpart, Matrix>();

        private bool IsCRP;
        
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;

        public void SetNode(PropellerNode node)
        {
            PropellerNode = node;
        }
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            PropellerFunctional = (IMyFunctionalBlock)Entity;
            PropellerBlock = (MyCubeBlock)Entity;
            PropellerTerminal = (IMyTerminalBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            PropellerGrid = PropellerBlock.CubeGrid;
            Entity.TryGetSubpart("Propeller", out PropellerSubpart);
            if (PropellerSubpart != null)
            {
                PropellerInitialMatrix = PropellerSubpart.PositionComp.LocalMatrixRef;
            }

            NeedsUpdate = 
                MyEntityUpdateEnum.EACH_FRAME
                | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            if (ShaftListDirty)
                RebuildDriveshaftTree();
            ApplyThrust();
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

            if (PropellerSubpart != null)
            {
                Matrix rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(CurrentAngle));
                Matrix finalMatrix = rotationMatrix * PropellerInitialMatrix;
                PropellerSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
            }

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

            var dist = Vector3D.Distance(PropellerBlock.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
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

        private void ApplyThrust()
        {
            var grid = PropellerGrid as MyCubeGrid;
            if (grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                return;
            if (!PropellerBlock.IsWorking || IncomingThrust < 1000) 
                return;

            Vector3D thrustVector = PropellerBlock.WorldMatrix.Backward * (float)IncomingThrust;
            var BlockPos = PropellerBlock.PositionComp.WorldVolume.Center;
            grid.Physics.AddForce(
            MyPhysicsForceType.APPLY_WORLD_FORCE,
            thrustVector,
            BlockPos,
            null
            );
        }
    }
}
