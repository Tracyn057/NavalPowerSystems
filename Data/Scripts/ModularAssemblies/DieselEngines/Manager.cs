using Jakaria.API;
using NavalPowerSystems.Communication;
using ProtoBuf.Meta;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace NavalPowerSystems.DieselEngines
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ExtractionManager : MySessionComponentBase
    {
        public static ExtractionManager Instance = new ExtractionManager();
        private int _ticks;
        public ModularDefinition ExtractionDefinition;
        public static List<ExtractionLogic> ActiveRigs = new List<ExtractionLogic>();
        public static Dictionary<int, ExtractionSystem> ExtractionSystems = new Dictionary<int, ExtractionSystem>();
        private static ModularDefinitionApi ModularApi => NavalPowerSystems.ModularDefinition.ModularApi;

        public void Load()
        {
            Instance = this;
        }

        public void Unload()
        {
            Instance = null;
            ActiveRigs.Clear();
        }

        public void UpdateTick()
        {
            if (_ticks % 100 == 0)
                Update100();

            _ticks++;
        }

        private void Update100()
        {
            var systems = ModularApi.GetAllAssemblies();
            foreach (var ExtractionSystem in ExtractionSystems.Values.ToList())
                if (!systems.Contains(ExtractionSystem.AssemblyId))
                    ExtractionSystems.Remove(ExtractionSystem.AssemblyId);
        }

        public void OnPartAdd(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!ExtractionSystems.ContainsKey(assemblyId))
                ExtractionSystems.Add(assemblyId, new ExtractionSystem(assemblyId));

            ExtractionSystems[assemblyId].AddPart(block);
            ValidateRig(assemblyId, block);
        }

        private bool GetWaterDepth(Vector3D headPos)
        {
            var headDepth = WaterModAPI.GetDepth(headPos);
            if (headDepth == null)
                return false;
            if (headDepth > Config.minWaterDepth)
                return true;
            return false;
        }

        private void ValidateRig(int assemblyId, IMyCubeBlock block)
        {
            bool hasRig = false;
            bool hasHead = false;
            bool hasOutput = false;
            bool hasRod = false;
            bool isRig = false;


            foreach (IMyCubeBlock part in ModularApi.GetMemberParts(assemblyId))
            {
                string subtype = part.BlockDefinition.SubtypeId;
                if (subtype.Contains("NPSExtractionCrudeOutput")) hasOutput = true;
                if (subtype.Contains("NPSExtractorOilDerrick")) hasRig = true;
                if (subtype.Contains("NPSExtractionDrillHead")) hasHead = true;
                if (subtype.Contains("NPSExtractionDrillRod")) hasRod = true;
            }

            if (hasRig && hasRod && hasHead && hasOutput)
            {
                isRig = true;
                ModularApi.SetAssemblyProperty(assemblyId, "IsRig", isRig);
            }

            if (block.BlockDefinition.SubtypeName == "NPSExtractionDrillHead")
            {
                Vector3D pos = block.WorldMatrix.Translation;
                MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(pos);
                if (planet != null)
                {
                    float cacheYield = OilMap.GetOil(pos, planet);
                    bool isOcean = GetWaterDepth(pos);
                    ModularApi.SetAssemblyProperty(assemblyId, "OilYield", cacheYield);
                    ModularApi.SetAssemblyProperty(assemblyId, "IsOcean", isOcean);
                }
            }

        }

        public void OnPartRemove(int assemblyId, IMyCubeBlock block, bool isBasePart)
        {
            if (!ExtractionSystems.ContainsKey(assemblyId))
                return;

            if (!isBasePart)
                ExtractionSystems[assemblyId].RemovePart(block);
            ValidateRig(assemblyId, block);
        }

        public static void Register(ExtractionLogic assemblyId)
        {
            if (!ActiveRigs.Contains(assemblyId))
                ActiveRigs.Add(assemblyId);
        }

        public static void Unregister(ExtractionLogic assemblyId)
        {
            if (ActiveRigs.Contains(assemblyId))
                ActiveRigs.Remove(assemblyId);
        }
    }
}