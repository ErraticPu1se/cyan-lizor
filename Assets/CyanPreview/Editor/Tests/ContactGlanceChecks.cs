using System;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class ContactGlanceChecks
    {
        static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
        static float Overlap(DesktopPet a,DesktopPet b)
        {
            float maximum=0;
            for(int i=0;i<3;i++) for(int j=0;j<3;j++) maximum=Mathf.Max(maximum,a.Motion.BodyRadius(i)*a.Config.scale+b.Motion.BodyRadius(j)*b.Config.scale-Vector2.Distance(a.Motion.BodyPosition(i)*a.Config.scale,b.Motion.BodyPosition(j)*b.Config.scale));
            return maximum;
        }
        public static void Verify() { using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(context); }
        public static void Run(ReferenceContext context)
        {
            foreach(bool fight in new[]{false,true})
            {
                var root=new GameObject("Collision independent of fighting"); var host=root.AddComponent<DesktopApp>();
                Set(host,"settings",new DesktopSettings {allowFighting=fight});
                var pets=(List<DesktopPet>)Get(host,"pets");
                try
                {
                    for(int i=0;i<2;i++) pets.Add(new DesktopPet(context,root.transform,DesktopPetConfiguration.From(new DesktopSettings {id="9320",scale=i==0?1:1.5f,sound=false,roam=false,jumps=false},"collision-"+i),new Rect(-700,-400,1400,800),i));
                    foreach(var pet in pets) pet.Motion.ShiftWithSurface(-pet.Motion.Position);
                    for(int tick=0;tick<160;tick++)
                    {
                        foreach(var pet in pets) pet.Advance(.025f,false,false);
                        Call(host,"ResolvePetContacts",.025f);
                        foreach(var pet in pets)
                        {
                            var draw=(Vector2[,])Get(pet.Pet.Graphics,"drawPositions");
                            for(int i=0;i<3;i++) Check(Vector2.Distance(draw[i,0],pet.Motion.BodyPosition(i))<.001f,"Collision correction not reflected in rendered body");
                            pet.Draw();
                        }
                    }
                    Debug.Log("PASSIVE_COLLISION fighting="+fight+" overlap="+Overlap(pets[0],pets[1]));
                    Check(Overlap(pets[0],pets[1])<1,"Passive bodies remain overlapped");
                    Check(!pets[0].Motion.RivalTarget.HasValue && !pets[1].Motion.RivalTarget.HasValue,"Collision started fighting");
                    pets[0].Motion.BeginGrab(pets[0].Motion.Position); var held=pets[0].Motion.Position;
                    pets[1].Motion.ShiftWithSurface(pets[1].Local(pets[0].WorldPosition)-pets[1].Motion.Position);
                    Call(host,"ResolvePetContacts",.025f); Check(pets[0].Motion.Position==held,"Collision pulled grabbed pet");
                }
                finally { foreach(var pet in pets) pet.Dispose(); pets.Clear(); UnityEngine.Object.DestroyImmediate(root); }
            }
            foreach(var target in new[]{new Vector2(200,0),new Vector2(0,200),new Vector2(-200,10),new Vector2(-200,-10)})
            using(var pet=context.CreatePet())
            {
                pet.Generate("9320"); var m=new PetMotion(pet,new BackgroundSurfaces(new Rect(-700,-400,1400,800)),42) {Roam=false,AutonomousJumps=false};
                for(int i=0;i<100;i++) m.Advance(.025f);
                m.Behavior.Social.Notice("other",target,1,m.BodyLength*1.5f,"Other");
                m.Advance(.025f);
                if(target.x<0) Check(!m.SocialHeadAligned,"Rear target falsely reported as looking at");
                bool aligned=false;
                for(int i=0;i<140;i++) { m.Advance(.025f); aligned|=m.SocialHeadAligned; }
                float angle=Vector2.Angle(m.HeadPosition-m.Position,target-m.Position);
                Debug.Log("SOCIAL_ACTUAL_HEAD target="+target+" angle="+angle);
                Check(aligned,"Social head never faced selected target");
                var before=m.Position;
                for(int i=0;i<100;i++) m.Advance(.025f);
                Check(Vector2.Distance(before,m.Position)<m.BodyLength,"Expired glance caused large return swing");
                Check(!m.SocialHeadAligned,"Expired glance still labelled looking at");
            }
            Debug.Log("PASSIVE_COLLISION_RENDER_SYNC_SOCIAL_REAL_HEAD_REAR_TURN_PASSED");
        }
    }
}
