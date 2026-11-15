namespace CyanPreview
{
    // Defaults preserve the existing 40 Hz movement. Override values in a species definition.
    public sealed class MotionTuning
    {
        public AttentionTuning Attention { get; }
        public MotionTuning(AttentionTuning attention=null) { Attention=attention??new AttentionTuning(); }
    }
    public sealed class AttentionTuning
    {
        public readonly float DeliberateSmoothing, SmallAdjustmentSmoothing;
        public readonly float DeliberateDegreesPerTick, SmallAdjustmentDegreesPerTick;
        public readonly float BodySmoothing, DeliberateBodyDegreesPerTick, IdleBodyDegreesPerTick;
        public AttentionTuning(float deliberateSmoothing=.65f,float smallAdjustmentSmoothing=.1f,
            float deliberateDegreesPerTick=30,float smallAdjustmentDegreesPerTick=4,
            float bodySmoothing=.08f,float deliberateBodyDegreesPerTick=2,float idleBodyDegreesPerTick=.75f)
        {
            DeliberateSmoothing=deliberateSmoothing; SmallAdjustmentSmoothing=smallAdjustmentSmoothing;
            DeliberateDegreesPerTick=deliberateDegreesPerTick; SmallAdjustmentDegreesPerTick=smallAdjustmentDegreesPerTick;
            BodySmoothing=bodySmoothing; DeliberateBodyDegreesPerTick=deliberateBodyDegreesPerTick; IdleBodyDegreesPerTick=idleBodyDegreesPerTick;
        }
    }
}
