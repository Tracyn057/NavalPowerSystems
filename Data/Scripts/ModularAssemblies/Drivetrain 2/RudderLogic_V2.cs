using EmptyKeys.UserInterface.Controls;
using NavalPowerSystems.Communication;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Vector3 = VRageMath.Vector3;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
        "NPSDrivetrainRudderSmallCenteredV1",
        "NPSDrivetrainRudderSmallOffsetLeftV1",
        "NPSDrivetrainRudderSmallOffsetRightV1",
        "NPSDrivetrainRudderSmallCenteredV2",
        "NPSDrivetrainRudderSmallOffsetLeftV2",
        "NPSDrivetrainRudderSmallOffsetRightV2"
    )]
    internal class RudderLogic_V2 : MyGameLogicComponent, IMyEventProxy
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        private IMyCubeBlock RudderBlock;
        private IMyFunctionalBlock RudderFunctional;
        private MyEntitySubpart RudderSubpart;
        private RudderStats_V2 RudderStats;
        private Vector3D RudderPosition;
        private MatrixD RudderSubpartMatrix;
        private MyCubeGrid RudderMyGrid;
        private IMyCubeGrid RudderGrid;
        private IMyShipController RudderShipController;
        private PropellerLogic_V2 NearestPropellerLogic;
        private List<IMySlimBlock> BlocksInSphere = new List<IMySlimBlock>();
        private List<PropellerLogic_V2> PropsInSphere = new List<PropellerLogic_V2>();

        private bool BrakeRight = false;
        MySync<bool, SyncDirection.BothWays> Terminal_BrakeRight;
        private bool BrakeLeft = false;
        MySync<bool, SyncDirection.BothWays> Terminal_BrakeLeft;
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;

        private float DistanceToCamera = 0f;
        private float RudderMaxAngle = 35f;
        private float RudderCurrentAngle = 0f;
        private float GridMass = 0f;
        private float YawInput = 0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            RudderBlock = Entity as IMyCubeBlock;
            RudderFunctional = Entity as IMyFunctionalBlock;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (RudderBlock != null && RudderFunctional != null)
            {
                Entity.TryGetSubpart("Rudder", out RudderSubpart);
                if (RudderSubpart != null)
                {
                    RudderSubpartMatrix = RudderSubpart.PositionComp.LocalMatrixRef;
                }
                RudderStats = Drivetrain_Config.RudderSettings_V2[RudderBlock.BlockDefinition.SubtypeId];
                RudderGrid = RudderBlock.CubeGrid;
                RudderMyGrid = RudderBlock.CubeGrid as MyCubeGrid;
                if (RudderMyGrid == null)
                {
                    NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    return;
                }

                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderMyGrid);

                if (player?.Controller?.ControlledEntity != null)
                {
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;
                }
            }

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            RudderGrid.OnGridChanged += RudderGrid_OnGridChanged;
        }

        

        public override void UpdateAfterSimulation()
        {
            if (!RudderFunctional.IsWorking || RudderMyGrid.Physics == null || RudderMyGrid.Physics.IsStatic) return;

            RecalculateController();
            GetControlInput();
            RudderAnimation();
            ApplyRotationalForce();
            SoftRollGravityAlign();
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateDistanceToCamera();
            if (RudderShipController != null)
            {
                GridMass = RudderShipController.CalculateShipMass().TotalMass;
            }
        }

        private void UpdateSyncBeforeFrame()
        {
            Terminal_BrakeRight.SetLocalValue(BrakeRight);
            Terminal_BrakeRight.ValueChanged += Terminal_BrakeRight_ValueChanged;

            Terminal_BrakeLeft.SetLocalValue(BrakeLeft);
            Terminal_BrakeLeft.ValueChanged += Terminal_BrakeLeft_ValueChanged;
        }

        private void Terminal_BrakeRight_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            BrakeRight = obj.Value;
            UpdateControls();
        }

        private void Terminal_BrakeLeft_ValueChanged(MySync<bool, SyncDirection.FromServer> obj)
        {
            BrakeLeft = obj.Value;
            UpdateControls();
        }

        public static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Rudder_TerminalControl_BrakeRight":
                    case "NPS_Rudder_TerminalControl_BrakeLeft":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }

        public void RecalculateController()
        {
            if (RudderShipController == null || !RudderShipController.IsWorking || !RudderShipController.IsMainCockpit)
            {
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(RudderMyGrid);
                RudderShipController = null;

                if (player?.Controller?.ControlledEntity != null)
                    RudderShipController = player.Controller.ControlledEntity as IMyShipController;
            }
            RudderPosition = RudderBlock.PositionComp.WorldVolume.Center;
        }

        public void UpdateDistanceToCamera()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            var dist = Vector3D.Distance(RudderBlock.WorldMatrix.Translation, MyAPIGateway.Session.Camera.WorldMatrix.Translation);
            DistanceToCamera = (float)dist;
        }

        private void GetControlInput()
        {
            if (RudderShipController == null) return;

            if (BrakeLeft)
            {
                YawInput = -1f;
                return;
            }
            else if (BrakeRight)
            {
                YawInput = 1f;
                return;
            }

            YawInput = MathHelper.Clamp(RudderShipController.RotationIndicator.X, -1, 1);
        }

        private void ApplyRotationalForce()
        {
            if (YawInput > 0.05f || YawInput < -0.05f)
            {
                Vector3D steeringVector = RudderSubpart.PositionComp.WorldMatrixRef.Backward * YawInput;
                Vector3D dragCounterVector = RudderShipController.PositionComp.WorldMatrixRef.Forward * YawInput;

                MatrixD subpartWorldMatrix = RudderSubpart.PositionComp.WorldMatrixRef;
                var propWash = GetPropWash();
                var velocity = RudderGrid.Physics.LinearVelocity.Length();
                var maxAuthorityVelocity = 25f;
                double velocityAuthority;

                if (velocity == 0)
                    velocityAuthority = 0f;
                else
                    velocityAuthority = Math.Pow(velocity, 2) / Math.Pow(maxAuthorityVelocity, 2);

                var rudderLiftForce = 0.5 * 1024 * Math.Pow(MathHelper.Clamp(velocity + propWash, 0f, 25f), 2) * RudderStats.SufaceArea * Math.Sin(MathHelper.ToRadians(RudderCurrentAngle));
                var rudderDragForce = Math.Abs(rudderLiftForce * Math.Sin(MathHelper.ToRadians(RudderCurrentAngle)));

                RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, steeringVector * rudderLiftForce * velocityAuthority, RudderPosition, null);
                RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, dragCounterVector * rudderDragForce * velocityAuthority, RudderGrid.Physics.CenterOfMassWorld, null);
            }
        }

        private void SoftRollGravityAlign()
        {
            var gridAngularVelocity = RudderGrid.Physics.AngularVelocity;
            var dampenAggressiveness = 1.0f;
            var gravity = RudderGrid.NaturalGravity;
            if (gravity == Vector3.Zero || gravity == null || RudderGrid.Physics.IsStatic)
                return;

            var rollVector = Vector3.Zero;

            if (gridAngularVelocity.LengthSquared() > Math.Pow(dampenAggressiveness, 2) && rollVector == Vector3.Zero)
            {
                var rollError = Vector3.Dot(RudderShipController.WorldMatrix.Right, -gravity);
                var rollVelocity = Vector3.Dot(gridAngularVelocity, RudderShipController.WorldMatrix.Forward);
                if (Math.Abs(rollVelocity) < dampenAggressiveness) rollVelocity = 0f;
                rollVector = new Vector3(0f, 0f, rollVelocity);

                var forceStrength = GridMass * 0.15f;
                var forceDampen = GridMass * 0.05;

                var forceMagnitude = (rollError * forceStrength) - (rollVelocity * forceDampen);
                var forceToApply = RudderShipController.WorldMatrix.Right * forceMagnitude;

                var applicationPoint = RudderMyGrid.Physics.CenterOfMassWorld + (RudderShipController.WorldMatrix.Down * 10);

                RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceToApply, applicationPoint, null);
                RudderMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -forceToApply, RudderMyGrid.Physics.CenterOfMassWorld, null);
            }
        }

        private void RudderAnimation()
        {
            if (MyAPIGateway.Utilities.IsDedicated || RudderSubpart == null || DistanceToCamera >= 500f)
                return;

            var angleStep = 0.1f;

            if (YawInput > 0.05f)
                RudderCurrentAngle += angleStep;
            else if (YawInput < -0.05f)
                RudderCurrentAngle -= angleStep;
            else
            {
                var tempAngle = RudderCurrentAngle;
                RudderCurrentAngle = MathHelper.Lerp(tempAngle, 0f, angleStep);

                if (Math.Abs(RudderCurrentAngle) < 0.01f) 
                    RudderCurrentAngle = 0f;
            }
                
            RudderCurrentAngle = MathHelper.Clamp(RudderCurrentAngle, -RudderMaxAngle, RudderMaxAngle);
            Matrix rotationMatrix = Matrix.CreateRotationY(MathHelper.ToRadians(RudderCurrentAngle));
            Matrix finalMatrix = rotationMatrix * RudderSubpartMatrix;
            RudderSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
        }

        private void RudderGrid_OnGridChanged(MyCubeGrid obj)
        {
            GetNearestProp();
        }

        private void GetNearestProp()
        {
            PropsInSphere.Clear();
            BlocksInSphere.Clear();
            BoundingSphereD propCheckSphere = new BoundingSphereD(RudderPosition, 10);
            BlocksInSphere = RudderGrid.GetBlocksInsideSphere(ref propCheckSphere);

            foreach (var block in BlocksInSphere)
            {
                if (block.FatBlock != null)
                {
                    var subtype = block.FatBlock.BlockDefinition.SubtypeId;
                    if (Config.PropellerSubtypes.Contains(subtype))
                    {
                        var logic = block.FatBlock.GameLogic?.GetAs<PropellerLogic_V2>();
                        PropsInSphere.Add(logic);
                    }
                }
            }

            if (PropsInSphere.Count == 1)
            {
                NearestPropellerLogic = PropsInSphere[0];
            }
            else if (PropsInSphere.Count > 1)
            {
                double closestDistance = double.MaxValue;
                PropellerLogic_V2 closestProp = null;

                foreach (var prop in PropsInSphere)
                {
                    var distance = Vector3D.Distance(RudderPosition, prop.PropellerBlock.PositionComp.WorldVolume.Center);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestProp = prop;
                    }
                }

                NearestPropellerLogic = closestProp;
            }
        }

        private float GetPropWash()
        {
            float discArea = (float)(Math.PI * Math.Pow(NearestPropellerLogic.PropellerStats.Diameter/2, 2));
            if (discArea <= 0 || NearestPropellerLogic.IncomingThrust <= 0) return 0f;

            return (float)Math.Sqrt(2 * NearestPropellerLogic.IncomingThrust / (1024 * discArea));
        }

        private void CreateControls()
        {
            if (ControlsInitialized)
                return;
            ControlsInitialized = true;

            {
                var NPS_Rudder_BrakeRight = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Rudder_TerminalControl_BrakeRight");
                NPS_Rudder_BrakeRight.Title = MyStringId.GetOrCompute("Brake Right");
                NPS_Rudder_BrakeRight.OnText = MyStringId.GetOrCompute("On");
                NPS_Rudder_BrakeRight.OffText = MyStringId.GetOrCompute("Off");
                NPS_Rudder_BrakeRight.Getter = (block) => BrakeRight;
                NPS_Rudder_BrakeRight.Setter = (block, value) => BrakeRight = value;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(NPS_Rudder_BrakeRight);
            }
            {
                var NPS_Rudder_BrakeLeft = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Rudder_TerminalControl_BrakeLeft");
                NPS_Rudder_BrakeLeft.Title = MyStringId.GetOrCompute("Brake Left");
                NPS_Rudder_BrakeLeft.OnText = MyStringId.GetOrCompute("On");
                NPS_Rudder_BrakeLeft.OffText = MyStringId.GetOrCompute("Off");
                NPS_Rudder_BrakeLeft.Getter = (block) => BrakeLeft;
                NPS_Rudder_BrakeLeft.Setter = (block, value) => BrakeLeft = value;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(NPS_Rudder_BrakeLeft);
            }
        }

        private void CreateActions()
        {  
            if (ActionsInitialized)
                return;
            ActionsInitialized = true;

            {
                var NPS_Rudder_ToggleBrakeRight = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>("NPS_Rudder_TerminalAction_BrakeRight");
                NPS_Rudder_ToggleBrakeRight.Name = MyStringId.GetOrCompute("Toggle Brake Right");
                NPS_Rudder_ToggleBrakeRight.Action = (block) => BrakeRight = !BrakeRight;
                MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(NPS_Rudder_ToggleBrakeRight);
            }
            {
                var NPS_Rudder_ToggleBrakeLeft = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>("NPS_Rudder_TerminalAction_BrakeLeft");
                NPS_Rudder_ToggleBrakeLeft.Name = MyStringId.GetOrCompute("Toggle Brake Left");
                NPS_Rudder_ToggleBrakeLeft.Action = (block) => BrakeLeft = !BrakeLeft;
                MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(NPS_Rudder_ToggleBrakeLeft);
            }
        }
    }
}
