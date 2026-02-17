using ProtoBuf;

namespace Humanoid.GimbalJetThruster
{
    [ProtoContract(UseProtoMembersOnly = true)]
    public class GimbalJetThrusterSettings
    {
        [ProtoMember(1)]
        public bool OverrideThrottle;

        [ProtoMember(2)]
        public bool EnableAngularDampening;

        [ProtoMember(3)]
        public bool KeepThrottle;

        [ProtoMember(4)]
        public float ThrustLimiter;

        [ProtoMember(5)]
        public bool OverrideControl;

        [ProtoMember(6)]
        public float OverrideLR;

        [ProtoMember(7)]
        public float OverrideUD;

        [ProtoMember(8)]
        public float SliderOverrideThrottle;
    }
}
