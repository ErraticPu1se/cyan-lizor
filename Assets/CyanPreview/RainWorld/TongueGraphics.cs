using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public static class TongueGraphics
    {
        public static void Apply(ReferenceBridge pet,PetTongue tongue)
        {
            if(tongue==null) return;
            var original=Get(pet.Lizard,"tongue");
            bool visible=tongue.Stage==TongueStage.Flying || tongue.Stage==TongueStage.Retracting;
            Set(original,"state",pet.Context.Static("LizardTongue+State",visible?(tongue.Stage==TongueStage.Flying?"LashingOut":"Retracting"):"Hidden"));
            var positions=Get(original,"graphPos") as Vector2[];
            if(positions==null) { positions=new Vector2[2]; Set(original,"graphPos",positions); }
            positions[0]=tongue.Tip; positions[1]=tongue.PreviousTip;
            var parts=(Array)Get(pet.Graphics,"tongue");
            float stretch=Mathf.Clamp01(Vector2.Distance(pet.HeadPosition,tongue.Tip)/PetTongue.Range);
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts.GetValue(i); float t=i/(float)(parts.Length-1), edge=Mathf.Abs(t-.5f)*2;
                Set(part,"pos",tongue.Points[i]); Set(part,"lastPos",tongue.PreviousPoints[i]);
                Set(part,"rad",(2+.75f*(1-stretch)+Mathf.Lerp(8,1,edge)*(1-Mathf.Sqrt(stretch)))/(1+t*.5f+(stretch+.5f)*3*(1-edge)));
            }
        }
    }
}
