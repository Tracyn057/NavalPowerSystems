using VRage.Game.Components;
using Sandbox.Common.ObjectBuilders;
using VRage.ObjectBuilders;
using VRage.ModAPI;
using SpaceEngineers.Game.ModAPI;
using Sandbox.Game.EntityComponents;
using VRage.Utils;
using VRageMath;

namespace Humanoid.AirIntake
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_AirVent), false, "AirIntakeJetSmall")]
    internal class AirIntake : MyGameLogicComponent
    {
	private const float MinOutput = 30f; 	// Min gas output of a vent at 0 m/s
	private const float MaxOutput = 250f; 	// Max gas output of a vent at MaxSpeed250
	private const float MaxSpeed = 50f; 	// Speed at which the vent reaches maximum output

        IMyAirVent vent;
        float maxPower; //Don't edit
	MyResourceSourceComponent source;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            vent = Entity as IMyAirVent;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (vent != null && vent.CubeGrid.Physics != null)
            {
                source = Entity.Components.Get<MyResourceSourceComponent>();

                if (source != null)
                {
		    source.SetMaxOutput(MinOutput);
                }
		
		vent.Depressurize = true;

		NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
            }
        }

	public override void UpdateAfterSimulation10()
	{
		if (vent == null || vent.CubeGrid.Physics == null || !vent.IsWorking || vent.CanPressurize || source == null)
		return;

		var LinearSpeed = vent.CubeGrid.Physics.LinearVelocity;
		source.SetMaxOutput(MathHelper.Clamp(Vector3.Dot((LinearSpeed / MaxSpeed) * MaxOutput, vent.CubeGrid.PositionComp.WorldMatrixRef.Backward), MinOutput, MaxOutput));
	}

        public override void Close()
        {
		if (vent != null)
		vent = null;

		if (source != null)
		source = null;
        }
    }
}