using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class SocialFollowingChecks
    {
        static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
        public static void Verify() { Run(); }
        static readonly IPetSurfaces surface=new BackgroundSurfaces(new Rect(-1500,-800,3000,1600));
        static PetBehavior Listener(float sympathy=1,float energy=1)
        {
            var b=new PetBehavior(42);
            Set(b,"sympathy",sympathy); Set(b,"energy",energy); Set(b.Social,"sympathy",sympathy);
            b.Social.Notice("caller",new Vector2(200,0),1,75,"Caller","voice");
            for(int i=0;i<45;i++) b.Update(Vector2.zero,surface);
            Check(b.SoundInvestigation && !b.FollowingSocial,"Call did not begin with location investigation");
            b.Social.TrackVisual("caller",new Vector2(240,0),50,.025f,false);
            b.Update(new Vector2(125,0),surface);
            return b;
        }
        public static void Run()
        {
            foreach(float distance in new[]{0f,1f,4f,100f})
            {
                var active=new PetBehavior(42); active.Target(Vector2.right*1000);
                Set(active,"lastPosition",(Vector2?)Vector2.zero); Set(active,"fatigue",.3f);
                active.Update(Vector2.right*distance,surface);
                float expected=.3f+.000225f+Mathf.Min(4,distance)*.000325f;
                Check(Mathf.Abs(active.Fatigue-expected)<.000001f,"Fatigue gain or travel cap changed at distance="+distance);
            }
            var resting=new PetBehavior(42) {Roam=false}; Set(resting,"fatigue",.5f);
            resting.Update(Vector2.zero,surface);
            Check(Mathf.Abs(resting.Fatigue-.4988f)<.000001f,"Rest recovery rate changed");
            for(int i=0;i<500;i++) resting.Update(Vector2.zero,surface);
            Check(resting.Fatigue==0,"Fatigue recovered below zero");
            resting.Target(Vector2.right*1000); Set(resting,"fatigue",.99999f);
            resting.Update(Vector2.zero,surface); Check(resting.Fatigue==1,"Fatigue exceeded one");
            var moderate=Listener(); Set(moderate,"fatigue",.5f); Set(moderate,"restScore",.8f);
            moderate.Social.TrackVisual("caller",Vector2.right*300,50,.025f,false);
            moderate.Update(Vector2.right*126,surface);
            Check(moderate.FollowingSocial,"Following still applies fatigue twice to its decision score");
            foreach(float dt in new[]{.0125f,.025f,.1f})
            {
                var low=new PetCuriosity(0); var high=new PetCuriosity(1);
                low.Notice("same",Vector2.right*200,1,75,"Caller","voice"); high.Notice("same",Vector2.right*200,1,75,"Caller","voice");
                for(int i=0;i<Mathf.RoundToInt(7/dt);i++) { low.Advance(dt); high.Advance(dt); }
                Check(!low.Point.HasValue && high.Point.HasValue && high.Utility>0,"Sympathy did not extend interest at dt="+dt);
                Check(!high.Notice("same",Vector2.zero,1,75,"Caller","voice"),"Repeated call reset lifetime");
                high.Advance(float.NaN); high.Advance(float.PositiveInfinity); high.Advance(-1);
                Check(high.Point.HasValue,"Invalid elapsed time expired interest");
                for(int i=0;i<Mathf.RoundToInt(6/dt);i++) high.Advance(dt);
                Check(!high.Point.HasValue,"High sympathy caused permanent interest");
            }
            int[] followingTicks=new int[2];
            for(int profile=0;profile<2;profile++)
            {
                var follower=Listener(profile,1);
                for(int i=0;i<500 && follower.FollowingSocial;i++)
                {
                    follower.Social.TrackVisual("caller",new Vector2(300+i,0),50,.025f,false);
                    follower.Update(new Vector2(125+i,0),surface);
                    followingTicks[profile]++;
                }
            }
            Debug.Log("FOLLOWING_DURATION low="+followingTicks[0]*.025f+" high="+followingTicks[1]*.025f);
            Check(followingTicks[1]>followingTicks[0],"Sympathy changed interest but not actual following duration");
            var b=Listener(); Check(b.FollowingSocial,"Visible caller did not gain bounded following after arrival");
            b.Social.TrackVisual("caller",new Vector2(300,0),50,.025f,false);
            var moving=b.Update(new Vector2(125,0),surface);
            Check(moving.HasValue && Mathf.Abs(moving.Value.x-225)<.01f,"Follower failed to update visible caller position with spacing");
            b.Social.TrackVisual("caller",new Vector2(900,0),50,0,false);
            var hidden=b.Update(new Vector2(126,0),surface);
            Check(hidden.HasValue && hidden.Value==moving.Value && !b.Social.Visible,"Unseen caller telepathically moved destination");
            b.Update(new Vector2(225,0),surface); Check(!b.FollowingSocial,"Lost caller was followed beyond last known position");
            b=Listener(); b.Social.TrackVisual("caller",new Vector2(1000,0),50,.025f,true);
            b.Update(new Vector2(125,0),surface);
            Check(!b.FollowingSocial && !b.SoundInvestigation && b.Social.Point.HasValue && !b.Social.ApproachAllowed,"Held caller did not cancel pursuit while retaining glance");
            b=Listener(); b.Roam=false; Check(!b.Update(new Vector2(125,0),surface).HasValue && !b.FollowingSocial,"Disabled roaming kept following");
            b=Listener(); b.Target(Vector2.up*200); Check(!b.FollowingSocial && b.Update(new Vector2(125,0),surface)==Vector2.up*200,"Click did not override following");
            b=Listener(); b.TaskTarget=Vector2.down*100; Check(b.Update(new Vector2(125,0),surface)==b.TaskTarget && !b.FollowingSocial,"Feeding/fighting task did not override following");
            b=Listener(); b.Social.Clear(); b.Update(new Vector2(125,0),surface); Check(!b.SoundInvestigation,"Removed caller left stale pursuit");
            b=Listener(); Set(b,"fatigue",1f); Set(b,"restScore",.9f); b.Update(new Vector2(125,0),surface); Check(!b.FollowingSocial,"Exhausted pet continued following");
            var slow=Listener(1,0); var fast=Listener(1,1);
            for(int i=0;i<20;i++)
            {
                foreach(var pet in new[]{slow,fast}) { pet.Social.TrackVisual("caller",new Vector2(300+i,0),50,.025f,false); pet.Update(new Vector2(125+i,0),surface); }
            }
            Check(fast.RunSpeed>slow.RunSpeed,"Energy did not affect willingness to move with caller");
            b=Listener();
            for(int i=0;i<600;i++) { b.Social.TrackVisual("caller",new Vector2(300+i,0),50,.025f,false); b.Update(new Vector2(125+i,0),surface); }
            Check(!b.FollowingSocial,"Continuous visibility refreshed following forever");
            b=Listener(); for(int i=0;i<140;i++) { b.Social.TrackVisual("caller",new Vector2(300+i,0),50,.025f,false); b.Update(new Vector2(125,0),surface); }
            Check(!b.FollowingSocial,"Blocked follower did not give up");
            Debug.Log("SOCIAL_PERSONALITY_VISIBILITY_FOLLOWING_EXPIRY_PRIORITY_SPACING_CHECK_PASSED");
        }
    }
}
