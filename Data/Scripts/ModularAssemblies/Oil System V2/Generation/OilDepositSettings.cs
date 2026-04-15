using ProtoBuf;

namespace BlackGold.Generation
{
    [ProtoContract(UseProtoMembersOnly = true)]
    public class OilWell
    {
        [ProtoMember(1)]
        public long OilWellId;

        [ProtoMember(2)]
        public Vector3D OilWellLocation;

        [ProtoMember(3)]
        public float OilWellDepth;

        [ProtoMember(4)]
        public double OilWellCapacity;

        [ProtoMember(5)]
        public float OilWellTapProgress;
        
        [ProtoMember(6)]
        public WellState OilWellState;
    }

    public enum WellState
    {
        Scanned,
        Inactive,
        Active,
        Depleted
    }
}