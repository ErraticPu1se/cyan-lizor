using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class ContactBiteChecks
    {
        static void Check(bool value,string message) { if(!value) throw new Exception(message); }
        static void Pose(DesktopPet pet,params Vector2[] positions)
        {
            var chunks=(Array)Get(pet.Pet.Lizard,"bodyChunks");
            for(int i=0;i<3;i++) { Set(chunks.GetValue(i),"pos",positions[i]/pet.Config.scale); Set(chunks.GetValue(i),"lastPos",positions[i]/pet.Config.scale); Set(chunks.GetValue(i),"vel",Vector2.zero); }
        }
        static float Overlap(DesktopPet a,DesktopPet b)
        {
            float total=0;
            for(int i=0;i<3;i++) for(int j=0;j<3;j++) total+=Mathf.Max(0,a.Motion.BodyRadius(i)*a.Config.scale+b.Motion.BodyRadius(j)*b.Config.scale-Vector2.Distance(a.Motion.BodyPosition(i)*a.Config.scale,b.Motion.BodyPosition(j)*b.Config.scale));
            return total;
        }
        public static void Run(ReferenceContext context)
        {
            var tuning=new BiteTuning(25,0,90,1.4f,1,12);
            var aJaw=new PetBite(1,tuning); var bJaw=new PetBite(2,tuning);
            for(int i=0;i<10;i++) aJaw.Advance(.025f,true,true,true,true);
            Check(aJaw.Jaw>0 && bJaw.Jaw==0,"Pet jaws share state");
            var left=new PetBite(1,tuning); var right=new PetBite(1,tuning); bool simultaneous=false;
            for(int i=0;i<80;i++) { var x=left.Advance(.025f,true,true,true,true); var y=right.Advance(.025f,true,true,true,true); simultaneous|=x==BiteDecision.Hit && y==BiteDecision.Hit; }
            Check(simultaneous,"Mutual contact cannot bite simultaneously");
            var miss=new PetBite(3,tuning); bool snapped=false;
            for(int i=0;i<100;i++) { var result=miss.Advance(.025f,true,true,true,false); Check(result!=BiteDecision.Hit,"Non-contact produced a hit"); snapped|=result==BiteDecision.Miss; }
            Check(snapped,"Failed attempts never produced a missed snap");
            var jaw=new PetBite(4,tuning); float before=jaw.Jaw;
            jaw.Advance(float.NaN,true,true,true,true); jaw.Advance(0,true,true,true,true); Check(jaw.Jaw==before,"Invalid time advanced jaw");
            for(int i=0;i<50;i++) Check(jaw.Advance(.025f,true,false,true,true)==BiteDecision.None,"Unavailable/carrying pet bit");
            jaw.ReceiveHit(); Check(jaw.Recovering,"Hit did not trigger local recovery");
            for(int i=0;i<6;i++) Check(jaw.Advance(.025f,true,true,true,true)!=BiteDecision.Hit,"Recovering pet attacked");
            jaw.Cancel(); Check(jaw.Jaw==0 && !jaw.Snapping,"Cancelled jaw remained open/snapping");

            var sequence=new PetBite(7,tuning);
            for(int i=0;i<20;i++) Check(sequence.Advance(.025f,true,true,true,false)==BiteDecision.None,"Bit before preparation completed");
            Check(sequence.Threatening && !sequence.Lunging,"Threat phase missing");
            Check(sequence.EngagementRange>tuning.LungeRange+20,"Crouch has no range margin");
            sequence.Advance(.025f,true,true,true,false,false);
            Check(!sequence.Threatening && !sequence.Lunging,"Misaligned wind-up did not cancel");
            Check(sequence.EngagementRange==tuning.LungeRange,"Cancelled threat retained enlarged range");
            for(int i=0;i<45 && !sequence.Lunging;i++) sequence.Advance(.025f,true,true,true,false);
            Check(sequence.Lunging && sequence.Propelling,"Prepared lunge did not launch");
            sequence.Advance(.025f,false,true,true,true);
            Check(!sequence.Lunging && !sequence.Propelling,"Interrupted lunge retained propulsion");
            sequence.ReceiveHit();
            for(int i=0;i<30;i++) Check(sequence.Advance(.025f,true,true,true,true)==BiteDecision.None,"Recovery attacked again");
            Check(sequence.Recovering,"Post-lunge recovery ended prematurely");

            var close=new PetBite(11,tuning); bool closeHit=false;
            for(int i=0;i<24;i++)
            {
                var result=close.Advance(.025f,true,true,false,true,false);
                closeHit|=result==BiteDecision.Hit;
                Check(!close.Lunging && !close.Propelling,"Close contact unnecessarily launched a lunge");
            }
            Check(closeHit,"Body contact required head-facing alignment or a lunge");
            var losingContact=new PetBite(12,tuning);
            for(int i=0;i<10;i++) losingContact.Advance(.025f,true,true,false,true,false);
            for(int i=0;i<30;i++) Check(losingContact.Advance(.025f,true,true,false,false,false)!=BiteDecision.Hit,"Lost mouth contact still hit");
            var zeroChance=new PetBite(13,new BiteTuning(25,0,90,1.4f,0,12)); bool closeMiss=false;
            for(int i=0;i<30;i++) { var r=zeroChance.Advance(.025f,true,true,true,true); Check(r!=BiteDecision.Hit,"Zero-chance bite hit"); closeMiss|=r==BiteDecision.Miss; }
            Check(closeMiss && !zeroChance.Lunging,"Close-range failed snaps never resolved");

            var root=new GameObject("Contact bite integration"); var host=root.AddComponent<DesktopApp>();
            var native=new WindowsDesktop(System.Diagnostics.Process.GetCurrentProcess().Id);
            Set(host,"native",native); Set(host,"settings",new DesktopSettings());
            var area=new Rect(-native.WorkArea.width/2,-native.WorkArea.height/2,native.WorkArea.width,native.WorkArea.height);
            var pets=(List<DesktopPet>)Get(host,"pets");
            try
            {
                var config=new DesktopSettings {id="9320",scale=1,sound=false,roam=false,jumps=false};
                var a=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(config,"a"),area,0); pets.Add(a);
                config.scale=2; var b=new DesktopPet(context,root.transform,DesktopPetConfiguration.From(config,"b"),area,1); pets.Add(b);
                Pose(a,Vector2.zero,new Vector2(-17,0),new Vector2(-34,0));
                Pose(b,new Vector2(100,0),new Vector2(70,0),new Vector2(25,0));
                Call(host,"ConfigureRival",a,b,true,false); Check(!a.Motion.Behavior.HoldPosition,"Approach stopped before mouth contact");
                Call(host,"ConfigureRival",a,b,true,true); Check(a.Motion.Behavior.HoldPosition,"Contact stance kept walking through victim");
                Check(PetContacts.BiteContact(a,b,out var near)==2 && near,"Bite failed to detect scaled rear body chunk");
                Check(PetContacts.TargetPoint(a,b)==new Vector2(25,0),"Body target ignored rear chunk beside the mouth");
                Call(host,"ConfigureRival",a,b,true,true);
                Check(a.Motion.Behavior.TaskTarget==a.Local(new Vector2(25,0)),"Movement still targets distant head instead of contacted body");
                Pose(a,Vector2.zero,new Vector2(17,0),new Vector2(34,0));
                Check(PetContacts.BiteContact(a,b,out near)==-1,"Backwards-facing bite hit");
                Pose(a,Vector2.zero,new Vector2(-17,0),new Vector2(-34,0));
                Pose(b,Vector2.zero,new Vector2(-17,0),new Vector2(-34,0));
                float initial=Overlap(a,b);
                for(int i=0;i<30;i++) PetContacts.Resolve(a,b,.025f);
                Check(Overlap(a,b)<initial*.1f,"Coincident scaled bodies did not separate");
                var position=a.Motion.Position; PetContacts.Resolve(a,b,float.NaN); PetContacts.Resolve(a,b,0); Check(a.Motion.Position==position,"Invalid/paused collision step moved pet");
                a.Motion.BeginGrab(a.Motion.Position); position=a.Motion.Position;
                PetContacts.Resolve(a,b,.1f); Check(a.Motion.Position==position && PetContacts.BiteContact(b,a,out near)==-1,"Held pet collided or received a bite");
                a.Motion.EndGrab(); Call(a.Motion,"ChangeState",PetMotionState.Crawling);
                Pose(a,Vector2.zero,new Vector2(-17,0),new Vector2(-34,0)); Pose(b,new Vector2(100,0),new Vector2(70,0),new Vector2(25,0));
                a.Motion.RivalTarget=a.Local(b.WorldPosition);
                var food=(SharedFood)Get(host,"food"); food.Begin(); food.Place(Vector2.zero); food.Take("b");
                Call(host,"ApplyBite",a,b,BiteDecision.Miss,-1); Check(food.Holder=="b","Miss dropped carrier food");
                var state=context.Blank("LizardState"); Set(state,"health",1f); Set(Get(b.Pet.Lizard,"abstractPhysicalObject"),"state",state);
                var headBefore=b.Motion.BodyVelocity(0);
                Call(host,"ApplyBite",a,b,BiteDecision.Hit,2);
                Check(food.Available && food.Holder==null,"Confirmed bite did not release victim's morsel");
                Check(b.Motion.BodyVelocity(2)!=Vector2.zero && b.Motion.BodyVelocity(0)==headBefore,"Hit impulse affected the whole victim instead of contacted chunk");
                Check((float)Get(state,"health")==1 && !(bool)Get(b.Pet.Lizard,"dead"),"Contact bite injured or killed pet");
                Check(food.TakeCursor(),"Dropped morsel unavailable for cursor drag"); Call(host,"ApplyBite",a,b,BiteDecision.Hit,2); Check(food.Owner==FoodOwner.Cursor,"Bite stole cursor-held food");
                food.Drop(Vector2.zero); food.Take("a"); Call(host,"ApplyBite",a,b,BiteDecision.Hit,2); Check(food.Holder=="a","Victim hit dropped another pet's food");
                food.Drop(Vector2.zero); food.Take("b"); food.EnterDen("b"); Call(host,"ApplyBite",a,b,BiteDecision.Hit,2); Check(food.Owner==FoodOwner.Den,"Bite dropped committed den food");
                foreach(var pet in pets) for(int i=0;i<3;i++) Check(!float.IsNaN(pet.Motion.BodyPosition(i).x) && !float.IsInfinity(pet.Motion.BodyVelocity(i).y),"Non-finite body after contacts");
                // A carrier's pickup must not reset the other pet's active combat task.
                food.Cancel(); a.Feeding.Cancel(); b.Feeding.Cancel();
                a.Motion.RivalTarget=new Vector2(200,50);
                a.Motion.Behavior.TaskTarget=a.Motion.RivalTarget; a.Motion.Behavior.HoldPosition=true;
                a.Motion.VoiceIntensity=.23f;
                a.Bite.Cancel(); a.Bite.Advance(.025f,true,true,true,false);
                float jawBeforePickup=a.Bite.Jaw;
                Call(host,"ResetInterest",a);
                Check(a.Motion.Behavior.TaskTarget==a.Motion.RivalTarget && a.Motion.Behavior.HoldPosition,"Pickup cleanup reset rival movement");
                b.Motion.RivalTarget=null;
                b.Feeding.Begin(); b.Feeding.Place(b.Motion.Position,b.Surfaces.Bounds,b.Motion.HeadToTailLength);
                food.Begin(); food.Place(b.WorldPosition);
                a.FoodRevision=b.FoodRevision=food.Revision;
                Call(host,"AdvanceFeeding",.025f);
                Check(food.Holder=="b","Pickup isolation fixture did not acquire food");
                Check(a.Motion.Behavior.TaskTarget==a.Motion.RivalTarget && a.Motion.Behavior.HoldPosition && a.Bite.Jaw==jawBeforePickup,"Carrier pickup interrupted rival bite preparation");
                Call(host,"AdvanceFeeding",.025f);
                Check(a.Motion.Behavior.TaskTarget==a.Motion.RivalTarget && a.Motion.VoiceIntensity==.23f,"Food follower overwrote combat or voice state");
                a.Audio.Enabled=true; b.Audio.Enabled=true;
                var otherVoice=Get(a.Audio,"voice"); var otherClip=Get(otherVoice,"clip");
                float otherCooldown=(float)Get(a.Audio,"affectionCooldown");
                Check(!ReferenceEquals(otherVoice,Get(b.Audio,"voice")) && !ReferenceEquals(a.Pet.Graphics,b.Pet.Graphics),"Pets share playback or graphics state");
                Check(b.Audio.FoodPickupCall(),"Carrier affection fixture did not play");
                Check(ReferenceEquals(otherClip,Get(otherVoice,"clip")) && (float)Get(a.Audio,"affectionCooldown")==otherCooldown && a.Motion.VoiceIntensity==.23f,"Carrier affection leaked into rival");
                a.Audio.Enabled=false; b.Audio.Enabled=false;
                a.Motion.RivalTarget=null;
                Call(host,"AdvanceFeeding",.025f);
                Check(a.FollowingFood && a.Motion.Behavior.TaskTarget.HasValue,"Food following did not resume after combat ended");
                food.Drop(new Vector2(area.xMax-100,area.yMax-100));
                Call(host,"AdvanceFeeding",.025f);
                Check(a.Feeding.Active,"Combat cancellation prevented fresh loose-food interest");
                a.Feeding.Cancel(); b.Feeding.Cancel();
                // Exercise the real motion loop: preparation must produce a physical lunge.
                food.Cancel(); a.Bite.Cancel();
                Pose(a,Vector2.zero,new Vector2(-17,0),new Vector2(-34,0));
                Pose(b,new Vector2(100,0),new Vector2(130,0),new Vector2(160,0));
                for(int i=0;i<60;i++) a.Motion.Advance(.025f);
                var launchStart=a.Motion.Position;
                a.Motion.RivalTarget=a.Motion.Position+Vector2.right*100;
                a.Motion.Behavior.HoldPosition=true; a.Motion.RivalStance=false;
                a.Motion.RivalLungeDirection=Vector2.right; a.Motion.RivalPropelling=true;
                for(int i=0;i<8;i++) a.Motion.Advance(.025f);
                Check(a.Motion.Position.x>launchStart.x+8,"Lunge propulsion did not move the real body");
                a.Motion.RivalPropelling=false;
                a.Motion.BeginGrab(a.Motion.Position); var held=a.Motion.Position;
                a.Motion.RivalPropelling=true;
                for(int i=0;i<8;i++) a.Motion.Advance(.025f);
                Check(Vector2.Distance(held,a.Motion.Position)<2,"Lunge force moved a cursor-held pet");
                a.Motion.RivalPropelling=false;
                Debug.Log("BODY_COLLISION_CONTACT_INDEPENDENT_BITE_FOOD_DROP_CHECK_PASSED");
            }
            finally { foreach(var p in pets) p.Dispose(); pets.Clear(); native.Dispose(); UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
