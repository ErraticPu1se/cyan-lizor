using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        public Vector2? DiagnosticDriveTarget => hasDestination ? destination : (Vector2?)null;
        public Vector2? RivalTarget { get; set; }
        public float RivalJaw { get; set; }
        public int BodyCount => chunks.Length;
        public Vector2 BodyPosition(int index) => (Vector2)Get(chunks[index],"pos");
        public Vector2 BodyVelocity(int index) => (Vector2)Get(chunks[index],"vel");
        public float BodyRadius(int index) => (float)Get(chunks[index],"rad");
        public float BodyMass(int index) => (float)Get(chunks[index],"mass");
        public Vector2 BitePoint => Position+(Position-BodyPosition(1)).normalized*Species.Biting.Forward;
        public void ContactCorrection(int index,Vector2 delta,Vector2 velocityDelta)
        {
            if(State==PetMotionState.Grabbed) return;
            var p=BodyPosition(index); var next=surfaces.Constrain(p+delta,BodyRadius(index),out _);
            TranslatePart(chunks[index],next-p);
            Set(chunks[index],"vel",Vector2.ClampMagnitude(BodyVelocity(index)+velocityDelta,18));
            resting=false;
        }
        public void BiteHoming(Vector2 target,float strength)
        {
            if(State!=PetMotionState.Crawling) return;
            var direction=(target-Position).normalized;
            for(int i=0;i<chunks.Length;i++) Set(chunks[i],"vel",Vector2.ClampMagnitude(BodyVelocity(i)+direction*strength*(i==0?3:-1),14));
        }
        public bool RivalStance { get; set; }
        public bool RivalLunging { get; set; }
        public bool RivalPropelling { get; set; }
        public bool RivalRecovering { get; set; }
        public Vector2 RivalLungeDirection { get; set; }
        public bool FacingRival(Vector2 target) => Vector2.Dot((Position-BodyPosition(1)).normalized,(target-Position).normalized)>.65f;
        public bool RivalBiting { get; set; }
        public bool BiteToward(Vector2 target)
        {
            if(State!=PetMotionState.Crawling || !RivalTarget.HasValue || float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsInfinity(target.x) || float.IsInfinity(target.y)) return false;
            var direction=(target-Position).normalized;
            if(direction.sqrMagnitude<.01f) return false;
            // Lizard's attack bends the body: front +8, middle/rear -6. No Violence/Grab call.
            for(int i=0;i<chunks.Length;i++)
                ReferenceContext.Set(chunks[i],"vel",Vector2.ClampMagnitude((Vector2)ReferenceContext.Get(chunks[i],"vel")+direction*(i==0?8:-6),14));
            return true;
        }
        public void ScuffleImpulse(Vector2 impulse)
        {
            if(State!=PetMotionState.Crawling) return;
            foreach(var chunk in chunks) Set(chunk,"vel",Vector2.ClampMagnitude((Vector2)Get(chunk,"vel")+Vector2.ClampMagnitude(impulse,5),12));
        }
    }
}
