using UnityEngine;
namespace CyanPreview
{
    // Desktop adaptation: intimidation changes willingness to stay, never health or bite damage.
    public sealed class PetConflictResponse
    {
        readonly float dominance,nervous;
        float pressure,retreat,cooldown;
        public bool HasYielded { get; private set; }
        public bool Retreating => retreat>0;
        public Vector2? Destination { get; set; }
        public PetConflictResponse(float dominance,float nervous)
        { this.dominance=Trait(dominance); this.nervous=Trait(nervous); }
        static float Trait(float value) => float.IsNaN(value)||float.IsInfinity(value)?.5f:Mathf.Clamp01(value);
        public void Advance(float dt)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0) return;
            dt=Mathf.Min(dt,.1f); retreat=Mathf.Max(0,retreat-dt); cooldown=Mathf.Max(0,cooldown-dt);
        }
        public void NoticeHit(float opponentDominance)
        {
            if(HasYielded || cooldown>0) return;
            pressure+=.7f+nervous*.9f+Mathf.Max(0,Trait(opponentDominance)-dominance)*.6f;
        }
        public void ObserveThreat(float dt,float opponentDominance,bool threatening,bool canRetreat)
        {
            if(HasYielded || cooldown>0 || float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0) return;
            dt=Mathf.Min(dt,.1f);
            pressure=Mathf.Max(0,pressure+(threatening?.15f+.65f*nervous+.4f*Mathf.Max(0,Trait(opponentDominance)-dominance):-.3f)*dt);
            if(canRetreat && pressure>1.6f+.8f*dominance)
            { HasYielded=true; retreat=1.5f+nervous; cooldown=retreat+8; Destination=null; }
        }
        public void Cancel() { pressure=retreat=0; HasYielded=false; Destination=null; }
    }
}
