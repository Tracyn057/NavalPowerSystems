using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain_V2
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
        "NPS_Rudder_Small_CenteredV1",
        "NPS_Rudder_Small_OffsetLeftV1",
        "NPS_Rudder_Small_OffsetRightV1",
        "NPS_Rudder_Small_CenteredV2",
        "NPS_Rudder_Small_OffsetLeftV2",
        "NPS_Rudder_Small_OffsetRightV2"
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

            UpdateSyncBeforeFrame();

            if (!ActionsInitialized)
                CreateActions();
            if (!ControlsInitialized)
                CreateControls();

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
            RudderMyGrid.OnGridChanged += RudderGrid_OnGridChanged;
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

        private void Terminal_BrakeRight_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
        {
            BrakeRight = obj.Value;
            UpdateControls();
        }

        private void Terminal_BrakeLeft_ValueChanged(MySync<bool, SyncDirection.BothWays> obj)
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
                var Control_BrakeRight = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Rudder_TerminalControl_BrakeRight");
                Control_BrakeRight.Title = MyStringId.GetOrCompute("Brake Right");
                Control_BrakeRight.Tooltip = MyStringId.GetOrCompute("Rudder tilt override full Right.");
                Control_BrakeRight.OnText = MySpaceTexts.SwitchText_On;
                Control_BrakeRight.OffText = MySpaceTexts.SwitchText_Off;
                Control_BrakeRight.Visible = Control_Visible;
                Control_BrakeRight.SupportsMultipleBlocks = true;
                Control_BrakeRight.Getter = Control_Terminal_BrakeRight_Getter;
                Control_BrakeRight.Setter = Control_Terminal_BrakeRight_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_BrakeRight);
            }
            {
                var Control_BrakeLeft = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyFunctionalBlock>("NPS_Rudder_TerminalControl_BrakeLeft");
                Control_BrakeLeft.Title = MyStringId.GetOrCompute("Brake Left");
                Control_BrakeLeft.Tooltip = MyStringId.GetOrCompute("Rudder tilt override full Left.");
                Control_BrakeLeft.OnText = MySpaceTexts.SwitchText_On;
                Control_BrakeLeft.OffText = MySpaceTexts.SwitchText_Off;
                Control_BrakeLeft.Visible = Control_Visible;
                Control_BrakeLeft.SupportsMultipleBlocks= true;
                Control_BrakeLeft.Getter = Control_Terminal_BrakeLeft_Getter;
                Control_BrakeLeft.Setter = Control_Terminal_BrakeLeft_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_BrakeLeft);
            }
        }

        static RudderLogic_V2 GetLogic(IMyTerminalBlock rudder) =>
                rudder?.GameLogic?.GetAs<RudderLogic_V2>();
        static bool Control_Visible(IMyTerminalBlock rudder)
        {
            return GetLogic(rudder) != null;
        }

        static bool Control_Terminal_BrakeRight_Getter(IMyTerminalBlock rudder)
        {
            var logic = GetLogic(rudder);
            return (logic == null ? false : logic.Terminal_BrakeRight);
        }

        static void Control_Terminal_BrakeRight_Setter(IMyTerminalBlock rudder, bool value)
        {
            var logic = GetLogic(rudder);
            if (logic != null)
                logic.Terminal_BrakeRight.ValidateAndSet(value);
        }

        static bool Control_Terminal_BrakeLeft_Getter(IMyTerminalBlock rudder)
        {
            var logic = GetLogic(rudder);
            return (logic == null ? false : logic.Terminal_BrakeLeft);
        }

        static void Control_Terminal_BrakeLeft_Setter(IMyTerminalBlock rudder, bool value)
        {
            var logic = GetLogic(rudder);
            if (logic != null)
                logic.Terminal_BrakeLeft.ValidateAndSet(value);
        }

        private void CreateActions()
        {  
            if (ActionsInitialized)
                return;
            ActionsInitialized = true;

            {
                var Action_BrakeRight = MyAPIGateway.TerminalControls.CreateAction<IMyTerminalAction>("NPS_Rudder_TerminalAction_BrakeRight");
                Action_BrakeRight.Name = new StringBuilder("Brake Right");
                Action_BrakeRight.ValidForGroups = true;
                Action_BrakeRight.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
                Action_BrakeRight.Action = Control_Terminal_BrakeRight_Action;
                Action_BrakeRight.Writer = Control_Terminal_BrakeRight_Writer;
                MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(Action_BrakeRight);
            }
            {
                var Action_BrakeLeft = MyAPIGateway.TerminalControls.CreateAction<IMyFunctionalBlock>("NPS_Rudder_TerminalAction_BrakeLeft");
                Action_BrakeLeft.Name = new StringBuilder("Brake Left");
                Action_BrakeLeft.ValidForGroups = true;
                Action_BrakeLeft.Icon = @"Textures\GUI\Icons\Actions\Toggle.dds";
                Action_BrakeLeft.Action = Control_Terminal_BrakeLeft_Action;
                Action_BrakeLeft.Writer = Control_Terminal_BrakeLeft_Writer;
                MyAPIGateway.TerminalControls.AddAction<IMyFunctionalBlock>(Action_BrakeLeft);
            }
        }

        static void Control_Terminal_BrakeRight_Action(IMyTerminalBlock rudder)
        {
            var logic = GetLogic(rudder);
            if (logic != null)
                logic.Terminal_BrakeRight.ValidateAndSet(!logic.Terminal_BrakeRight.Value);
        }

        static void Control_Terminal_BrakeRight_Writer(IMyTerminalBlock rudder, StringBuilder writer)
        {
            var logic = GetLogic(rudder);
            if (logic != null)
                if (logic.Terminal_BrakeRight)
                {
                    writer.Append("Brake \nON");
                }
                else
                {
                    writer.Append("Brake \nOFF");
                }
        }

        static void Control_Terminal_BrakeLeft_Action(IMyTerminalBlock rudder)
        {
            var logic = GetLogic(rudder);
            if (logic != null)
                logic.Terminal_BrakeLeft.ValidateAndSet(!logic.Terminal_BrakeLeft.Value);
        }

        static void Control_Terminal_BrakeLeft_Writer(IMyTerminalBlock rudder, StringBuilder writer)
        {
            var logic = GetLogic(rudder);
            if (logic != null)
                if (logic.Terminal_BrakeLeft)
                {
                    writer.Append("Brake \nON");
                }
                else
                {
                    writer.Append("Brake \nOFF");
                }
        }
    }
}
