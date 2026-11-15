using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        [Serializable] public sealed class MotionSample
        {
            public string state,intent;
            public bool resting,hasGoal,hasLook,hold,recovery;
            public Vector2 goal,look,head,graphicsLook;
            public float bodyTurn,neckTurn,runSpeed,voice;
            public int grips;
            public Vector2[] before=new Vector2[3],after=new Vector2[3],velocity=new Vector2[3],normal=new Vector2[3],
                support=new Vector2[3],drive=new Vector2[3],idle=new Vector2[3],restTarget=new Vector2[3],
                boundary=new Vector2[3],beforeConnections=new Vector2[3],connection=new Vector2[3];
        }
        // No sampling or allocation in ordinary builds unless a diagnostic consumer subscribes.
        public Action<MotionSample> MotionObserver { get; set; }
        MotionSample motionSample;
        void BeginMotionTrace()
        {
            motionSample=MotionObserver==null?null:new MotionSample();
            if(motionSample!=null) for(int i=0;i<3;i++) motionSample.before[i]=BodyPosition(i);
        }
        void EndMotionTrace()
        {
            if(motionSample==null) return;
            var s=motionSample;
            s.state=State.ToString(); s.intent=Behavior.Intent.ToString(); s.resting=resting;
            s.hasGoal=hasDestination; s.goal=destination; s.hasLook=lookTarget.HasValue; s.look=lookTarget.GetValueOrDefault();
            s.hold=Behavior.HoldPosition; s.recovery=Recovery.Active; s.bodyTurn=idleBodyTurn; s.neckTurn=idleNeckTurn;
            s.runSpeed=Behavior.RunSpeed; s.voice=VoiceIntensity; s.grips=GripCount; s.head=HeadPosition;
            s.graphicsLook=(Vector2)Get(graphics,"lookPos");
            for(int i=0;i<3;i++) { s.after[i]=BodyPosition(i); s.velocity[i]=(Vector2)Get(chunks[i],"vel"); }
            MotionObserver?.Invoke(s); motionSample=null;
        }
    }
}
