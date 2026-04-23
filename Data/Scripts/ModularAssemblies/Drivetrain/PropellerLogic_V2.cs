
using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
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
using VRageMath;

namespace NavalPowerSystems.Drivetrain
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_FunctionalBlock), false,
    "NPS_Propeller_4m3b",
    "NPS_Propeller_4m4b",
    "NPS_Propeller_4m5b"
    )]
    public class PropellerLogic_V2 : MyGameLogicComponent, IMyEventProxy, IDrivetrain
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public IMyCubeBlock PropellerBlock;
        private IMyFunctionalBlock PropellerFunctional;
        private IMyTerminalBlock PropellerTerminal;
        private MyEntitySubpart PropellerSubpart;
        private IDrivetrainNode PropellerNode => this as IDrivetrainNode;
        private IMyCubeGrid PropellerGrid;
        private Matrix PropellerSubpartInitialMatrix;
        public PropellerStats PropellerStats;
        private bool ControlsInitialized = false;
        private bool ActionsInitialized = false;
        MySync<float, SyncDirection.BothWays> Terminal_PitchRatio;
        public float PitchRatio = 0f;

        private float CurrentAngle;
        public bool ShouldAnimate = false;
        private HashSet<IMyCubeBlock> ConnectedParts = new HashSet<IMyCubeBlock>();
        private List<DrivetrainPacket> PacketInbox = new List<DrivetrainPacket>();
        public void ReceivePacket(DrivetrainPacket packet) => PacketInbox.Add(packet);
        public long GetNodeID() => PropellerBlock.EntityId;
        public string GetNodeSubtype() => PropellerFunctional.BlockDefinition.SubtypeId;
        public double GetLoadWeight() => 1;
        private double InputLoad = 0;
        private double OutputRPM = 0;
        private double OutputTorque = 0;
        public double OutputThrust = 0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            PropellerFunctional = (IMyFunctionalBlock)Entity;
            PropellerBlock = (MyCubeBlock)Entity;
            PropellerTerminal = (IMyTerminalBlock)Entity;

            PropellerTerminal.AppendingCustomInfo += AppendCustomInfo;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            PropellerGrid = PropellerBlock.CubeGrid;
            PropellerStats = Drivetrain_Config.PropellerSettings[PropellerBlock.BlockDefinition.SubtypeId];
            Entity.TryGetSubpart("Propeller", out PropellerSubpart);
            if (PropellerSubpart != null)
            {
                PropellerSubpartInitialMatrix = PropellerSubpart.PositionComp.LocalMatrixRef;
            }

            if (PropellerStats.IsCRP)
            {
                Terminal_PitchRatio.SetLocalValue(PitchRatio);
                Terminal_PitchRatio.ValueChanged += Terminal_PitchRatio_ValueChanged;
            }
            else
                PitchRatio = 1.1f;


            NeedsUpdate =
                MyEntityUpdateEnum.EACH_FRAME
                | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();
            //Clean slate before information gathering
            InputLoad = 0;
            OutputRPM = 0;
            OutputTorque = 0;

            //Gather load information
            double velocity = PropellerBlock.CubeGrid.Physics?.LinearVelocity.Length() ?? 0;
            double RPS = Math.Max(OutputRPM / 60, 0.1);
            double advanceRatio = (RPS > 0.1) ? velocity / (RPS * PropellerStats.Diameter) : 0;
            //Static Load
            double staticLoad = 0.01 * 
            double currentTorque = PropellerStats.TorqueCoefficient * PitchRatio * MathHelper.Clamp(1 - (advanceRatio / PitchRatio), 0.1, 1);
            double torqueDemand = currentTorque * PitchRatio * Math.Pow(RPS, 2) * Math.Pow(PropellerStats.Diameter, 5);
            InputLoad = torqueDemand + (PropellerStats.Diameter * 0.1);

            //Calculate output force
            OutputThrust = currentTorque * 1024 * Math.Pow(RPS, 2) * Math.Pow(PropellerStats.Diameter, 4);
            if (Math.Abs(OutputThrust) > 100)
            {
                Vector3D thrustVector = PropellerBlock.WorldMatrix.Backward * OutputThrust;
                var thrustPos = PropellerBlock.PositionComp.WorldVolume.Center;
                PropellerGrid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, thrustVector, thrustPos, null);
            }

            //Animate
            if (PropellerSubpart != null && OutputRPM != 0 && ShouldAnimate)
            {
                float degreesPerTick = (float)OutputRPM * 360f / 3600f; // convert RPM → degrees/tick at 60 Hz
                CurrentAngle += degreesPerTick;
                CurrentAngle %= 360f;

                Matrix rotationMatrix = Matrix.CreateRotationZ(MathHelper.ToRadians(-CurrentAngle));
                Matrix finalMatrix = rotationMatrix * PropellerSubpartInitialMatrix;
                PropellerSubpart.PositionComp.SetLocalMatrix(ref finalMatrix);
            }
        }
        public void CleanAssembly()
        {
            ConnectedParts.Clear();
            foreach (IMyCubeBlock neighbor in ModularApi.GetConnectedBlocks(PropellerBlock, "Drivetrain_Definition_V2", false))
            {
                ConnectedParts.Add(neighbor);
            }
        }

        private void AppendCustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            info.AppendLine($"Current Torque: {OutputThrust:0.00}");
            info.AppendLine($"Current RPM: {OutputRPM:0.00}");
        }

        private void Terminal_PitchRatio_ValueChanged(MySync<float, SyncDirection.BothWays> obj)
        {
            PitchRatio = obj.Value;
            //UpdateControls();
        }

        private static void UpdateControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);

            foreach (IMyTerminalControl control in controls)
            {
                switch (control.Id)
                {
                    case "NPS_Propeller_TerminalControl_PitchRatio":
                        {
                            control.UpdateVisual();
                            break;
                        }
                }
            }
        }
    }
}
