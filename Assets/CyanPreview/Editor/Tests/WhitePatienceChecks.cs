using System;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class WhitePatienceChecks
    {
        static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
        static int FirstRoam(PetBehavior b,IPetSurfaces surface)
        { for(int i=0;i<12000;i++) { b.Update(Vector2.zero,surface); if(b.Intent==PetIntent.Roaming) return i; } return -1; }
        public static void Run()
        {
            var surface=new BackgroundSurfaces(new Rect(-1000,-1000,2000,2000));
            foreach(int seed in new[]{1,42,73})
            {
                int cyan=FirstRoam(new PetBehavior(seed),surface),white=FirstRoam(new PetBehavior(seed,null,1),surface);
                Check(cyan>0 && white>cyan*3,"White did not rest substantially longer");
                Debug.Log("WHITE_REST_COMPARISON seed="+seed+" cyanSeconds="+cyan*.025f+" whiteSeconds="+white*.025f);
            }
            var quiet=new PetBehavior(42,null,1);
            for(int i=0;i<800;i++) { quiet.ObserveOwner(Vector2.right*140); quiet.Update(Vector2.zero,surface); Check(quiet.Intent==PetIntent.Idle,"Stationary cursor forced White to walk"); }
            var curious=new PetBehavior(42,null,1); bool inspected=false;
            for(int i=0;i<400;i++)
            {
                curious.ObserveOwner(new Vector2(140+i%2*8,30)); curious.Update(Vector2.zero,surface);
                if(curious.Intent==PetIntent.Investigating) { inspected=true; break; }
            }
            Check(inspected,"Sustained nearby motion could not outweigh White rest");
            curious.Rest();
            for(int i=0;i<400;i++) { curious.ObserveOwner(new Vector2(140+i%2*8,30)); curious.Update(Vector2.zero,surface); Check(curious.Intent==PetIntent.Idle,"Cursor curiosity ignored cooldown"); }
            var disabled=new PetBehavior(42,null,1) {Roam=false};
            for(int i=0;i<400;i++) { disabled.ObserveOwner(new Vector2(140+i%2*8,30)); Check(!disabled.Update(Vector2.zero,surface).HasValue,"Disabled roaming still followed cursor"); }
            var faint=new PetBehavior(42,null,1); faint.Social.Notice("other",Vector2.right*180,.2f);
            for(int i=0;i<180;i++) Check(!faint.Update(Vector2.zero,surface).HasValue,"Weak glance forced movement");
            var call=new PetBehavior(42,null,1); call.Social.Notice("other",Vector2.right*180,1,cue:"voice"); bool heard=false;
            for(int i=0;i<100;i++) { call.Update(Vector2.zero,surface); heard|=call.SoundInvestigation; }
            Check(heard,"Strong voice could not interrupt White rest");
            var click=new PetBehavior(42,null,1); click.Target(Vector2.right*200); click.Update(Vector2.zero,surface);
            Check(click.Intent==PetIntent.Investigating,"Explicit click lost priority");
            var c=new PetCamouflage();
            for(int i=0;i<40;i++) if(c.Advance(.025f,true,Vector2.zero)) c.Accept(Color.red);
            Check(c.Amount<.1f,"Camouflage appeared immediately");
            for(int i=0;i<80;i++) c.Advance(.025f,true,Vector2.zero);
            Check(c.Amount>.45f && c.Amount<.65f,"Fade is not approximately halfway after two seconds");
            for(int i=0;i<80;i++) c.Advance(.025f,true,Vector2.zero);
            Check(c.Amount>.99f,"Four-second camouflage did not finish");
            var before=c.Color; c.Accept(Color.blue); Check(c.Color==before,"New sample snapped the color");
            c.Advance(.025f,false,Vector2.zero); Check(c.Amount>.9f && c.Amount<1,"Movement reveal snapped or stalled");
            for(int i=0;i<39;i++) c.Advance(.025f,false,Vector2.zero);
            Check(c.Amount>.49f && c.Amount<.51f,"Reveal is not halfway after one second");
            for(int i=0;i<40;i++) c.Advance(.025f,false,Vector2.zero);
            Check(c.Amount<.001f,"Two-second recovery did not restore original coloring");
            Debug.Log("WHITE_PATIENCE_CURSOR_COOLDOWN_SOCIAL_CLICK_FOUR_SECOND_CAMOUFLAGE_CHECK_PASSED");
        }
    }
}
