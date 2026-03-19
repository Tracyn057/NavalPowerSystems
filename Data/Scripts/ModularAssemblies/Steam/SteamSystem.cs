using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static NavalPowerSystems.Config;

namespace NavalPowerSystems.Steam
{
    public class SteamSystem
    {
        private static ModularDefinitionApi ModularApi => ModularDefinition.ModularApi;
        public readonly int AssemblyId;
        public readonly IMyCubeGrid Grid;
        public List<IMyGasTank> Boilers = new List<IMyGasTank>();
        public List<IMyTerminalBlock> Turbines = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> UpdraftPreheaters = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> Economizers = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> SafetyValves = new List<IMyTerminalBlock>();
        public List<IMyTerminalBlock> UpdraftExhausts = new List<IMyTerminalBlock>();
        public List<IMyCubeBlock> SteamPipes = new List<IMyCubeBlock>();
        public List<IMyCubeBlock> UpdraftBlocks = new List<IMyCubeBlock>();
        public List<IMyCubeBlock> Condensers = new List<IMyCubeBlock>();
    }
}