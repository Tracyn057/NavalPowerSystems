using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
    "NPS_Rudder_Small_CenteredV1",
    "NPS_Rudder_Small_OffsetLeftV1",
    "NPS_Rudder_Small_OffsetRightV1",
    "NPS_Rudder_Small_CenteredV2",
    "NPS_Rudder_Small_OffsetLeftV2",
    "NPS_Rudder_Small_OffsetRightV2"
    )]
    public class RudderLogic : DrivetrainPart<IMyFunctionalBlock>
    {
        private RudderSettings MyStats => Config_Rudder.RudderSettings[SubtypeName];
        private MyEntitySubpart MySubpart;
        private Matrix MySubpartMatrix;
        private static bool ControlsInitialized = false;
        private float CurrentAngle = 0f;

        private bool BrakeRight = false;
        MySync<bool, SyncDirection.BothWays> Terminal_BrakeRight;
        private bool BrakeLeft = false;
        MySync<bool, SyncDirection.BothWays> Terminal_BrakeLeft;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            MyGridManager.RegisterRudder(this);
            Entity.TryGetSubpart("Rudder", out MySubpart);
            MySubpartMatrix = MySubpart.PositionComp.LocalMatrixRef;

            if (!ControlsInitialized)
            {
                CreateActions();
                CreateControls();
                ControlsInitialized = true;
            }

            Terminal_BrakeRight.SetLocalValue(BrakeRight);
            Terminal_BrakeRight.ValueChanged += Terminal_BrakeRight_ValueChanged;

            Terminal_BrakeLeft.SetLocalValue(BrakeLeft);
            Terminal_BrakeLeft.ValueChanged += Terminal_BrakeLeft_ValueChanged;

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();

            RudderAnimation();
            ApplyRotationalForce();
            SoftRollGravityAlign();
        }

        private float GetControlInput()
        {
            if (BrakeLeft)
                return -1f;
            else if (BrakeRight)
                return 1f;

            return MyGridManager.YawInput;
        }

        private void ApplyRotationalForce()
        {
            var yaw = GetControlInput();
            if (yaw > 0.05f || yaw < -0.05f)
            {
                Vector3D steeringVector = MySubpart.PositionComp.WorldMatrixRef.Backward * yaw;
                Vector3D dragCounterVector = MyGridManager.GridMatrixRef.Forward * yaw;

                MatrixD subpartWorldMatrix = MySubpart.PositionComp.WorldMatrixRef;
                var propWash = MyGridManager.GridAverageThrust;
                var velocity = IMyGrid.Physics.LinearVelocity.Length();
                var maxAuthorityVelocity = 25f;
                double velocityAuthority;

                if (velocity == 0)
                    velocityAuthority = 0f;
                else
                    velocityAuthority = Math.Pow(velocity, 2) / Math.Pow(maxAuthorityVelocity, 2);

                var rudderLiftForce = 0.5 * 1024 * Math.Pow(MathHelper.Clamp(velocity + propWash, 0f, 25f), 2) * MyStats.SufaceArea * Math.Sin(MathHelper.ToRadians(CurrentAngle));
                var rudderDragForce = Math.Abs(rudderLiftForce * Math.Sin(MathHelper.ToRadians(CurrentAngle)));

                IMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, steeringVector * rudderLiftForce * velocityAuthority, Block.PositionComp.WorldVolume.Center, null);
                IMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, dragCounterVector * rudderDragForce * velocityAuthority, IMyGrid.Physics.CenterOfMassWorld, null);
            }
        }

        private void SoftRollGravityAlign()
        {
            var gridAngularVelocity = IMyGrid.Physics.AngularVelocity;
            var dampenAggressiveness = 1.0f;
            var gravity = IMyGrid.NaturalGravity;
            if (gravity == Vector3.Zero || gravity == null || IMyGrid.Physics.IsStatic)
                return;

            var rollVector = Vector3.Zero;

            if (gridAngularVelocity.LengthSquared() > Math.Pow(dampenAggressiveness, 2) && rollVector == Vector3.Zero)
            {
                var rollError = Vector3.Dot(MyGridManager.GridMatrixRef.Right, -gravity);
                var rollVelocity = Vector3.Dot(gridAngularVelocity, MyGridManager.GridMatrixRef.Forward);
                if (Math.Abs(rollVelocity) < dampenAggressiveness) rollVelocity = 0f;
                rollVector = new Vector3(0f, 0f, rollVelocity);

                var forceStrength = MyGridManager.MyGridMass * 0.15f;
                var forceDampen = MyGridManager.MyGridMass * 0.05;

                var forceMagnitude = (rollError * forceStrength) - (rollVelocity * forceDampen);
                var forceToApply = MyGridManager.GridMatrixRef.Right * forceMagnitude;

                var applicationPoint = IMyGrid.Physics.CenterOfMassWorld + (MyGridManager.GridMatrixRef.Down * 10);

                IMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, forceToApply, applicationPoint, null);
                IMyGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -forceToApply, IMyGrid.Physics.CenterOfMassWorld, null);
            }
        }

        private void RudderAnimation()
        {
            if (MyAPIGateway.Utilities.IsDedicated || MySubpart == null || MyGridManager.DistanceToCamera >= 750f)
                return;

            var yaw = GetControlInput();
            var angleStep = 0.1f;

            if (yaw > 0.05f)
                CurrentAngle += angleStep;
            else if (yaw < -0.05f)
                CurrentAngle -= angleStep;
            else
            {
                var tempAngle = CurrentAngle;
                CurrentAngle = MathHelper.Lerp(tempAngle, 0f, angleStep);

                if (Math.Abs(CurrentAngle) < 0.01f)
                    CurrentAngle = 0f;
            }

            CurrentAngle = MathHelper.Clamp(CurrentAngle, -35f, 35f);
            Matrix rotationMatrix = Matrix.CreateRotationY(MathHelper.ToRadians(CurrentAngle));
            Matrix finalMatrix = rotationMatrix * MySubpartMatrix;
            MySubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
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

        static void CreateControls()
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
                Control_BrakeLeft.SupportsMultipleBlocks = true;
                Control_BrakeLeft.Getter = Control_Terminal_BrakeLeft_Getter;
                Control_BrakeLeft.Setter = Control_Terminal_BrakeLeft_Setter;
                MyAPIGateway.TerminalControls.AddControl<IMyFunctionalBlock>(Control_BrakeLeft);
            }
        }

        static RudderLogic GetLogic(IMyTerminalBlock block) =>
                block?.GameLogic?.GetAs<RudderLogic>();
        static bool Control_Visible(IMyTerminalBlock block)
        {
            return GetLogic(block) != null;
        }

        static bool Control_Terminal_BrakeRight_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return (logic == null ? false : logic.Terminal_BrakeRight);
        }

        static void Control_Terminal_BrakeRight_Setter(IMyTerminalBlock block, bool value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_BrakeRight.ValidateAndSet(value);
        }

        static bool Control_Terminal_BrakeLeft_Getter(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            return (logic == null ? false : logic.Terminal_BrakeLeft);
        }

        static void Control_Terminal_BrakeLeft_Setter(IMyTerminalBlock block, bool value)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_BrakeLeft.ValidateAndSet(value);
        }

        static void CreateActions()
        {
            if (ControlsInitialized) return;

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

        static void Control_Terminal_BrakeRight_Action(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_BrakeRight.ValidateAndSet(!logic.Terminal_BrakeRight.Value);
        }

        static void Control_Terminal_BrakeRight_Writer(IMyTerminalBlock block, StringBuilder writer)
        {
            var logic = GetLogic(block);
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

        static void Control_Terminal_BrakeLeft_Action(IMyTerminalBlock block)
        {
            var logic = GetLogic(block);
            if (logic != null)
                logic.Terminal_BrakeLeft.ValidateAndSet(!logic.Terminal_BrakeLeft.Value);
        }

        static void Control_Terminal_BrakeLeft_Writer(IMyTerminalBlock block, StringBuilder writer)
        {
            var logic = GetLogic(block);
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

    public class Config_Rudder
    {
        public static readonly Dictionary<string, RudderSettings> RudderSettings = new Dictionary<string, RudderSettings>
        {
            {"NPS_Rudder_Small_CenteredV1", new RudderSettings { SufaceArea = 38.9f } },
            {"NPS_Rudder_Small_OffsetLeftV1", new RudderSettings { SufaceArea = 38.9f } },
            {"NPS_Rudder_Small_OffsetRightV1", new RudderSettings { SufaceArea = 38.9f } },
            {"NPS_Rudder_Small_CenteredV2", new RudderSettings { SufaceArea = 37.32f } },
            {"NPS_Rudder_Small_OffsetLeftV2", new RudderSettings { SufaceArea = 37.32f } },
            {"NPS_Rudder_Small_OffsetRightV2", new RudderSettings { SufaceArea = 37.32f } },
        };
    }

    public class RudderSettings
    {
        public float SufaceArea;
    }
}
