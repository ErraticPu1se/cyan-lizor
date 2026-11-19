using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class RoamingContinuityChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        public static void Verify()
        { using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(context); }
        public static void Run(ReferenceContext context)
        {
            var surface=new BackgroundSurfaces(new Rect(-2000,-1200,4000,2400));
            foreach(int seed in new[]{1,42,73})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320"); var m=new PetMotion(pet,surface,seed) {Roam=true,AutonomousJumps=false};
                bool started=false,stopped=false,slowed=false; float travel=0,previousSpeed=0; int moving=0;
                for(int tick=0;tick<5000;tick++)
                {
                    var before=m.Position; bool expectingRoam=moving==20;
                    m.Advance(.025f);
                    var b=m.Behavior;
                    if(expectingRoam) Check(b.Intent==PetIntent.Roaming,"Sight cue stopped roaming");
                    if(b.Intent==PetIntent.Roaming)
                    {
                        started=true; moving++; travel+=Vector2.Distance(before,m.Position);
                        if(moving==20) b.Social.Notice("other",m.Position+Vector2.up*80,1,m.BodyLength*1.5f,"Other","sight");
                    }
                    if(b.RoamStopping)
                    {
                        slowed=true; Check(b.RunSpeed<=previousSpeed+.001f,"Roam stopping accelerated");
                        Check(m.DiagnosticDriveTarget.HasValue,"Stopping dropped movement goal before slowing");
                    }
                    if(started && b.Intent==PetIntent.Idle) { stopped=true; break; }
                    previousSpeed=b.RunSpeed;
                }
                Debug.Log("ROAM_BOUT seed="+seed+" ticks="+moving+" bodyLengths="+travel/m.BodyLength);
                Check(started && stopped && slowed,"Roam did not complete through gradual settling");
                Check(travel>m.BodyLength*3 && moving>100,"Roaming still ends after a short shuffle");
                for(int i=0;i<60;i++) { m.Advance(.025f); Check(m.Behavior.Intent==PetIntent.Idle,"Rest immediately changed to "+m.Behavior.Intent+" cue="+m.Behavior.Social.Cue); }
                m.MoveTo(m.Position+Vector2.right*200); m.Advance(.025f);
                Check(m.Behavior.Intent==PetIntent.Investigating,"Click failed to interrupt rest");
            }
            var blocked=new PetBehavior(42); Set(blocked,"restlessness",1f);
            bool entered=false,finished=false;
            for(int i=0;i<500;i++) { blocked.Update(Vector2.zero,surface); entered|=blocked.Intent==PetIntent.Roaming; if(entered&&blocked.Intent==PetIntent.Idle) {finished=true;break;} }
            Check(finished,"Blocked roaming kept its commitment indefinitely");
            var interrupt=new PetBehavior(42); Set(interrupt,"restlessness",1f);
            for(int i=0;i<100 && interrupt.Intent!=PetIntent.Roaming;i++) interrupt.Update(Vector2.zero,surface);
            interrupt.Target(Vector2.right*300); interrupt.Update(Vector2.zero,surface);
            Check(interrupt.Intent==PetIntent.Investigating && !interrupt.RoamStopping,"Click could not interrupt roaming");
            interrupt.TaskTarget=Vector2.up*300; Check(interrupt.Update(Vector2.zero,surface)==interrupt.TaskTarget,"Task lost priority");
            interrupt.HoldPosition=true; Check(!interrupt.Update(Vector2.zero,surface).HasValue && interrupt.RunSpeed==0,"Hold did not stop movement");
            Debug.Log("ROAM_CONTINUITY_SOCIAL_RESILIENCE_SLOW_STOP_BLOCKED_INTERRUPTS_PASSED");
        }
    }
}
