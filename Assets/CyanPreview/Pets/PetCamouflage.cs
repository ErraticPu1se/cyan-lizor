using UnityEngine;
namespace CyanPreview
{
    // One independent sample per settled idle episode; animation tremors do not rearm it.
    public sealed class PetCamouflage
    {
        float stillTime;
        bool attempted, sampled, anchored;
        Vector2 anchor;
        public float Amount {get;private set;}
        Color targetColor=UnityEngine.Color.white;
        public Color Color {get;private set;}=UnityEngine.Color.white;
        public bool Advance(float dt,bool idle,Vector2 position)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0) return false;
            dt=Mathf.Min(dt,.1f);
            if(!anchored) { anchor=position; anchored=true; }
            if(!idle || Vector2.Distance(anchor,position)>3)
            { anchor=position; stillTime=0; attempted=sampled=false; }
            else stillTime+=dt;
            Color=UnityEngine.Color.Lerp(Color,targetColor,1-Mathf.Exp(-dt*.9f));
            Amount=Mathf.MoveTowards(Amount,idle&&sampled?1:0,dt*(idle?1f/4f:1f/2f));
            if(idle && stillTime>=.75f && !attempted) { attempted=true; return true; }
            return false;
        }
        public void Accept(Color value) { targetColor=value; sampled=true; }
        public static Color Combine(Color a,Color b,Color c) => a==b?a:b==c?b:c==a?a:(a+b+c)/3;
    }
}
