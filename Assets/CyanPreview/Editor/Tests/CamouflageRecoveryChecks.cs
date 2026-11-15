using System;
using System.Linq;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
 public static class CamouflageRecoveryChecks
 {
  public static void Verify() { using(var c=new ReferenceContext(System.IO.Path.GetFullPath("local-data"))) Run(c); }
  static Color[] Colors(ReferenceBridge pet) => pet.OrderedSprites.SelectMany(s=>s.GetType().Name=="TriangleMesh" && (bool)Get(s,"customColor") ? (Color[])Get(s,"verticeColors") : new[]{(Color)Get(s,"color")}).ToArray();
  static void CheckHeadCycle(ReferenceContext context)
  {
   using(var pet=context.CreatePet("white")) using(var other=context.CreatePet("white")) using(var cyan=context.CreatePet("cyan"))
   {
    pet.Generate("24556"); other.Generate("24556"); cyan.Generate("9320");
    var surface=new BackgroundSurfaces(new Rect(-1000,-1000,2000,2000));
    var motion=new PetMotion(pet,surface,42) {Roam=false};
    var second=new PetMotion(other,surface,42) {Roam=false};
    var cyanMotion=new PetMotion(cyan,surface,42) {Roam=false};
    float cyanBlink=(float)Get(cyan.Graphics,"blink");
    float min=1,max=0; var sample=new Color(.15f,.25f,.35f);
    for(int frame=0;frame<480;frame++)
    {
     motion.Advance(.025f); second.Advance(.025f); cyanMotion.Advance(.025f);
     if((float)Get(pet.Graphics,"blink")!=(float)Get(other.Graphics,"blink")) throw new Exception("White head cycle leaked between pets");
     var color=(Color)Call(pet.Graphics,"HeadColor",1f); min=Mathf.Min(min,color.r); max=Mathf.Max(max,color.r);
     other.ApplyCamouflage(sample,1);
     if(Vector4.Distance((Color)Call(other.Graphics,"HeadColor",1f),sample)>.0001f) throw new Exception("Full camouflage lost its head color");
    }
    if(max-min<.7f) throw new Exception("White head remains stuck dark");
    if((float)Get(cyan.Graphics,"blink")!=cyanBlink || (float)Get(cyan.Graphics,"headColorSetter")!=1) throw new Exception("White cycle changed Cyan");
    Set(pet.Graphics,"headColorSetter",1f); motion.Advance(.025f);
    if(Mathf.Abs((float)Get(pet.Graphics,"headColorSetter")-.8f)>.0001f) throw new Exception("White override did not decay");
    pet.Pose(0,.7f); float first=(float)Get(pet.Graphics,"blink"); pet.Pose(2,.7f);
    if((float)Get(pet.Graphics,"blink")<=first) throw new Exception("White preview cycle did not advance");
   }
   Debug.Log("WHITE_HEAD_COLOR_CYCLE_CHECK_PASSED");
  }
  public static void Run(ReferenceContext context)
  {
   CheckHeadCycle(context);
   foreach(string id in new[]{"24556","9320"}) foreach(float voice in new[]{0f,.001f,.1f,1f})
   using(var pet=context.CreatePet("white"))
   {
    pet.Generate(id);
    var motion=new PetMotion(pet,new BackgroundSurfaces(new Rect(-1000,-1000,2000,2000)),42) {Roam=false};
    motion.MoveTo(Vector2.right*400); Color[] last=null; float worst=0;
    for(int frame=0;frame<=160;frame++)
    {
     motion.VoiceIntensity=frame<80?voice:0; motion.Advance(.025f);
     pet.ApplyCamouflage(new Color(.15f,.25f,.35f),Mathf.Max(0,1-frame/80f)); pet.Draw();
     var colors=Colors(pet);
     if(last!=null) for(int i=0;i<colors.Length;i++)
     {
      float d=Mathf.Max(Mathf.Abs(colors[i].r-last[i].r),Mathf.Abs(colors[i].g-last[i].g),Mathf.Abs(colors[i].b-last[i].b));
      worst=Mathf.Max(worst,d);
      if(d>.06f) throw new Exception("White marking snapped: ID "+id+" voice="+voice+" frame="+frame+" color="+i+" delta="+d);
     }
     last=colors;
    }
    // At the end, the rendered colors must converge to the same pose with no vocal contribution.
    Set(pet.Graphics,"voiceVisualization",0f); Set(pet.Graphics,"lastVoiceVisualization",0f);
    Set(pet.Graphics,"voiceVisualizationIntensity",0f); Set(pet.Graphics,"lastVoiceVisualizationIntensity",0f); pet.Draw();
    var normal=Colors(pet);
    for(int i=0;i<normal.Length;i++) if(Vector4.Distance(last[i],normal[i])>.002f) throw new Exception("White did not return to normal marking color");
    Debug.Log("WHITE_RECOVERY_COLOR_CONTINUITY id="+id+" voice="+voice+" maxStep="+worst);
   }
   Debug.Log("WHITE_VOICE_END_HEAD_MARKINGS_RECOVERY_CHECK_PASSED");
  }
 }
}
