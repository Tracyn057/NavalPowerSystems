using NavalPowerSystems.Communication;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using static NavalPowerSystems.Config;


namespace NavalPowerSystems.IntegratedElectrics
{

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false,
    "transforer subtype",
    "vfd subtype"
    )]
    public class TransformerVFDLogic : MyGameLogicComponent
    {
        private IMyCubeBlock Block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Block = Entity as IMyCubeBlock;

            if (Block != null)
            {
                ElectricalManager.OnBlockAdded(Block);
            }
        }

        public override void Close()
        {
            if (Block != null)
            {
                ElectricalManager.OnBlockRemoved(Block);
            }
            base.Close();
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeBlock), false, 
    "switchboard subtype"
    )]
    public class SwitchboardLogic : MyGameLogicComponent
    {
        private MyResourceSinkComponent Sink;
        private IMyCubeBlock Block;
        public float RequestedMW { get; set; } = 0f;
        public float MaxOutputMW { get; set; } = 0f;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Block = Entity as IMyCubeBlock;
            if (Block == null) return;

            ElectricalManager.OnBlockAdded(Block);

            Sink = new MyResourceSinkComponent();

            //Sink.Init(
            //    MyStringHash.GetOrCompute("Electricity"),
            //    () => CalculateRequiredPower()
            //);

            if (!Entity.Components.Contains(typeof(MyResourceSinkComponent)))
            {
                Entity.Components.Add(Sink);
            }
        }

        public override void Close()
        {
            if (Block != null)
            {
                ElectricalManager.OnBlockRemoved(Block);
            }
            base.Close();
        }

        private float CalculateRequiredPower()
        {
            //var system = ElectricalManager.Instance?.GetSystem(Block.CubeGrid.EntityId);

            //return system != null ? (float)system.CurrentDemandMW : 0f;
            return 0f;
        }
    }
}