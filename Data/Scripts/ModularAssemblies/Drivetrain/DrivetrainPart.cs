using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;

namespace NavalPowerSystems.Drivetrain
{
    public abstract class DrivetrainPart<T> : MyGameLogicComponent, IMyEventProxy, IDrivetrainPart
        where T : class, IMyCubeBlock
    {
        protected static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public static readonly Guid SettingsGuid = new Guid("ff61eeb4-2728-4deb-9a8e-76b0a8ca1f93");
        protected string _subtypeName;
        public string SubtypeName => _subtypeName ?? (_subtypeName = Block?.BlockDefinition.SubtypeName);
        public T Block => Entity as T;
        public IMyCubeGrid IMyGrid => Entity.Parent as IMyCubeGrid;
        public NavalGridManager MyGridManager => DrivetrainManager.Instance.GetGridManager(IMyGrid);
        public DrivetrainSystem MyDrivetrainSystem => DrivetrainManager.Instance.DrivetrainSystems[ModularApi.GetContainingAssembly(Block, "Drivetrain_Definition")];
        public IDrivetrainPart IPart;
        public int AssemblyId;
        public const float PhysicsStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

        public DrivetrainRole Role { get; set; }
        public float RPM_In { get; set; }
        public float RPM_Out { get; set; }
        public double Load_In { get; set; }
        public double Load_Out { get; set; }
        public double Torque_In { get; set; }
        public double Torque_Out { get; set; }
        public float EngagementMult { get; set; }

        public virtual float GetRatio() => 1f;
        public virtual double GetTorque() => 0f;
        public virtual double GetLoad() => 0f;
        public virtual DrivetrainRole GetRole() => Role;
        public virtual IMyCubeBlock GetMyCubeBlock() => Block;

        public bool CanWork => Block != null && Block.IsWorking;
    }

    public interface IDrivetrainPart
    {
        float RPM_In { get; set; }
        float RPM_Out { get; set; }
        double Load_In { get; set; }
        double Load_Out { get; set; }
        double Torque_In { get; set; }
        double Torque_Out { get; set; }
        float EngagementMult { get; set; }

        float GetRatio();
        double GetTorque();
        double GetLoad();
        DrivetrainRole GetRole();
        IMyCubeBlock GetMyCubeBlock();
    }

    public enum DrivetrainRole { Producer, Consumer, Transformer }
}
