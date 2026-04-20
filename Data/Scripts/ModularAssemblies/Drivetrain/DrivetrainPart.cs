using NavalPowerSystems.Communication;
using Sandbox.Game.EntityComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Network;
using static NavalPowerSystems.Drivetrain_V2.Drivetrain_Config;

namespace NavalPowerSystems.Drivetrain_V2
{
    public abstract class DrivetrainPart<T> : MyGameLogicComponent, IMyEventProxy
        where T : IMyCubeBlock
    {
        protected static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public static readonly Guid SettingsGuid = new Guid("ff61eeb4-2728-4deb-9a8e-76b0a8ca1f93");
        internal T Block;
        internal int AssemblyId;
        internal const float PhysicsStep = MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
        internal DrivetrainSystem_V2 MemberSystem;
        internal DrivetrainRole Role;
        internal MyResourceSourceComponent Source;
        internal MyResourceSinkComponent Sink;
    }
}
