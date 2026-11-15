using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class WhiteSpeciesChecks
    {
        static void Check(bool ok,string why) { if(!ok) throw new Exception(why); }
        public static void Verify() { using(var c=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(c); }
        public static void Run(ReferenceContext context)
        {
            using(var cyan=context.CreatePet()) using(var white=context.CreatePet("white")) using(var duplicate=context.CreatePet("white"))
            {
                cyan.Generate("9320"); var cyanSummary=cyan.Summary;
                var random=UnityEngine.Random.state;
                white.Generate("9320"); duplicate.Generate("9320");
                Check(white.Summary==duplicate.Summary && white.PetId!=duplicate.PetId,"White generation or independent identity failed");
                Check(UnityEngine.Random.state.Equals(random),"White generation leaked random state");
                Check((int)Get(Get(white.Lizard,"lizardParams"),"tailSegments")==5,"White used cyan tail geometry");
                Check(!white.Species.Movement.CanPropel && white.Species.Movement.ClimbPoles && white.Species.Movement.ClimbBackgrounds,"White abilities incorrect");
                var motion=new PetMotion(white,new BackgroundSurfaces(new Rect(-500,-300,1000,600)),42);
                Check(!motion.JumpTo(Vector2.right*100),"White gained cyan propulsion");
                var start=motion.Position;
                motion.Behavior.TaskTarget=Vector2.right*200;
                var root=new GameObject("White mixed species check");
                try
                {
                    using(var view=new SpriteMeshView(root.transform))
                    {
                        for(int i=0;i<160;i++) { motion.Advance(.025f); white.Draw(); view.Refresh(white); }
                        Check(Vector2.Distance(start,motion.Position)>20,"White did not crawl");
                        foreach(var filter in root.GetComponentsInChildren<MeshFilter>()) foreach(var v in filter.sharedMesh.vertices) Check(!float.IsNaN(v.x)&&!float.IsInfinity(v.y),"White produced invalid geometry");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                Check(context.LoadSound(white.Species.VoiceSound(white.Seed))!=null,"White voice missing");
                Check(context.LoadSound(white.Species.AffectionSound(white.Seed))!=null,"White affection missing");
                var preset=PetPreset.Capture("9320",new CompanionPreferences()); preset.species="white"; preset.Validate();
                Check(JsonUtility.FromJson<PetPreset>(JsonUtility.ToJson(preset)).species=="white","White preset lost species on serialization");
                bool rejected=false; try { LizardSpecies.Find("unknown"); } catch(NotSupportedException) { rejected=true; }
                Check(rejected,"Unknown species silently accepted");
                cyan.Generate("9320"); Check(cyan.Summary==cyanSummary,"White generation changed cyan appearance");
                var config=DesktopPetConfiguration.From(new DesktopSettings {id="9320",species="white"}); config.Validate(); Check(config.species=="white","Primary configuration lost species");
            }
            var desktopRoot=new GameObject("Mixed species desktop check");
            try
            {
                var bounds=new Rect(-500,-300,1000,600);
                using(var cyanPet=new DesktopPet(context,desktopRoot.transform,new DesktopPetConfiguration {key="cyan-check",id="9320",species="cyan",sound=false},bounds,0))
                using(var whitePet=new DesktopPet(context,desktopRoot.transform,new DesktopPetConfiguration {key="white-check",id="9320",species="white",sound=false},bounds,1))
                {
                    Check(whitePet.Audio!=null,"White desktop audio initialization failed");
                    Check(whitePet.Pet.Species.HeadLightRadius==0,"White inherited cyan head glow");
                    for(int i=0;i<80;i++) { cyanPet.Advance(.025f,false); whitePet.Advance(.025f,false); }
                    Check(!whitePet.Motion.JumpTo(Vector2.up*120),"Desktop White gained cyan jump");
                    whitePet.Rebuild(new DesktopPetConfiguration {key="white-check",id="7205",species="white",scale=2,sound=false},bounds,null);
                    whitePet.Advance(.025f,false); cyanPet.Advance(.025f,false);
                    Check(cyanPet.Pet.Species.Id=="cyan" && whitePet.Pet.Species.Id=="white","Mixed rebuild changed another species");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(desktopRoot); }
            Debug.Log("WHITE_APPEARANCE_CRAWL_NO_PROPULSION_MIXED_ID_RANDOM_VOICE_CHECK_PASSED");
        }
    }
}
