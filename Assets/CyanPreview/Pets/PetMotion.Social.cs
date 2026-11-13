using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        public Vector2? DiagnosticDriveTarget => hasDestination ? destination : (Vector2?)null;
        public Vector2? RivalTarget { get; set; }
        public bool RivalPursuingCarrier { get; set; }
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
            var shift=next-p;
            TranslatePart(chunks[index],shift);
            // Contact resolution runs after simulation: keep rendered interpolation in sync.
            var draw=(Vector2[,])Get(graphics,"drawPositions");
            draw[index,0]+=shift; draw[index,1]+=shift;
            if(index==0) TranslatePart(Get(graphics,"head"),shift);
            Set(chunks[index],"vel",Vector2.ClampMagnitude(BodyVelocity(index)+velocityDelta,18));
            resting=false;
        }
        public void BiteHoming(Vector2 target,float strength)
        {
            if(State!=PetMotionState.Crawling || GripCount==0 || !FacingRival(target)
                || float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsInfinity(target.x) || float.IsInfinity(target.y)
                || float.IsNaN(strength) || float.IsInfinity(strength) || strength<=0) return;
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
            if(State==PetMotionState.Grabbed || !RivalTarget.HasValue || float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsInfinity(target.x) || float.IsInfinity(target.y)) return false;
            var direction=(target-Position).normalized;
            if(direction.sqrMagnitude<.01f) return false;
            // Contact snaps can happen in flight without an extra propulsion impulse.
            if(State!=PetMotionState.Crawling) return true;
            // Lizard's attack bends the body: front +8, middle/rear -6. No Violence/Grab call.
            for(int i=0;i<chunks.Length;i++)
                ReferenceContext.Set(chunks[i],"vel",Vector2.ClampMagnitude((Vector2)ReferenceContext.Get(chunks[i],"vel")+direction*(i==0?8:-6),14));
            return true;
        }
        bool pursuitJump;
        float PursuitJumpDistance => Mathf.Max(190,Species.Biting.LungeRange+30);
        bool CanPlanPursuitJump(Vector2 target) => !Behavior.HoldPosition && !RivalLunging && !RivalRecovering
            && Vector2.Distance(Position,target)>PursuitJumpDistance && FacingRival(target);
        void RefreshPursuitCharge()
        {
            if(State!=PetMotionState.Charging || (!pursuitJump && !RivalPursuingCarrier)) return;
            pursuitJump=true;
            if(!RivalPursuingCarrier || !RivalTarget.HasValue || !AutonomousJumps || !CanPlanPursuitJump(RivalTarget.Value))
            { ChangeState(PetMotionState.Crawling); cooldown=Mathf.Max(cooldown,12); pursuitJump=false; return; }
            if(chargeTicks>1) return;
            // Recheck the moving target at takeoff rather than launching toward its old position.
            if(!PetJumpPlanner.TryPlanToTarget(Position,RivalTarget.Value,surfaces,capabilities,out var plan))
            { ChangeState(PetMotionState.Crawling); cooldown=Mathf.Max(cooldown,12); pursuitJump=false; return; }
            jumpTarget=plan.Landing; launchVelocity=plan.Velocity; jumpPower=plan.Power;
        }
        public void ScuffleImpulse(Vector2 impulse)
        {
            if(State!=PetMotionState.Crawling) return;
            foreach(var chunk in chunks) Set(chunk,"vel",Vector2.ClampMagnitude((Vector2)Get(chunk,"vel")+Vector2.ClampMagnitude(impulse,5),12));
        }
    }
}
