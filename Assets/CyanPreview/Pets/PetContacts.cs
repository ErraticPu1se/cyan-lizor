using UnityEngine;
namespace CyanPreview
{
    // All cross-pet geometry uses desktop world units; scale conversion happens at the boundary.
    public static class PetContacts
    {
        public static bool Available(DesktopPet p) => !p.Feeding.UsingRoute && p.Motion.State!=PetMotionState.Grabbed && !p.Input.CapturesPointer;
        public static int BiteContact(DesktopPet attacker,DesktopPet defender,out bool near)
        {
            near=false; var tune=attacker.Pet.Species.Biting;
            if(tune==null || !Available(attacker) || !Available(defender)) return -1;
            var head=attacker.WorldPosition; var forward=(attacker.Motion.Position-attacker.Motion.BodyPosition(1)).normalized;
            var mouth=attacker.Motion.BitePoint*attacker.Config.scale;
            int best=-1; float closest=float.MaxValue;
            for(int i=0;i<defender.Motion.BodyCount;i++)
            {
                var target=defender.Motion.BodyPosition(i)*defender.Config.scale;
                if(Vector2.Distance(head,target)<=tune.AttemptRadius*attacker.Config.scale) near=true;
                float radius=Mathf.Max(8,defender.Motion.BodyRadius(i))*defender.Config.scale+tune.RadiusBonus*attacker.Config.scale;
                float distance=(target-mouth).sqrMagnitude;
                if(Vector2.Dot(target-head,forward)>0 && distance<=radius*radius && distance<closest) { best=i; closest=distance; }
            }
            return best;
        }
        public static Vector2 TargetPoint(DesktopPet attacker,DesktopPet defender)
        {
            int contact=BiteContact(attacker,defender,out _);
            if(contact>=0) return defender.Motion.BodyPosition(contact)*defender.Config.scale;
            int best=0; float distance=float.MaxValue;
            for(int i=0;i<defender.Motion.BodyCount;i++)
            {
                var point=defender.Motion.BodyPosition(i)*defender.Config.scale;
                float gap=Vector2.Distance(attacker.WorldPosition,point)-defender.Motion.BodyRadius(i)*defender.Config.scale;
                if(gap<distance) { best=i; distance=gap; }
            }
            return defender.Motion.BodyPosition(best)*defender.Config.scale;
        }
        public static void Resolve(DesktopPet a,DesktopPet b,float seconds)
        {
            if(a==b || !Available(a) || !Available(b) || float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0) return;
            float limit=4*Mathf.Clamp01(seconds/.025f);
            for(int pass=0;pass<3;pass++)
            for(int i=0;i<a.Motion.BodyCount;i++) for(int j=0;j<b.Motion.BodyCount;j++)
            {
                var pa=a.Motion.BodyPosition(i)*a.Config.scale; var pb=b.Motion.BodyPosition(j)*b.Config.scale;
                float radius=a.Motion.BodyRadius(i)*a.Config.scale+b.Motion.BodyRadius(j)*b.Config.scale;
                var delta=pb-pa; float distance=delta.magnitude;
                if(distance>=radius) continue;
                var normal=distance>.001f?delta/distance:Vector2.right;
                float ma=a.Motion.BodyMass(i)*a.Config.scale*a.Config.scale, mb=b.Motion.BodyMass(j)*b.Config.scale*b.Config.scale;
                float shareA=mb/(ma+mb), shareB=1-shareA;
                float correction=Mathf.Min(limit,radius-distance);
                float closing=Mathf.Min(0,Vector2.Dot(b.Motion.BodyVelocity(j)*b.Config.scale-a.Motion.BodyVelocity(i)*a.Config.scale,normal));
                a.Motion.ContactCorrection(i,-normal*correction*shareA/a.Config.scale,normal*closing*shareA/a.Config.scale);
                b.Motion.ContactCorrection(j,normal*correction*shareB/b.Config.scale,-normal*closing*shareB/b.Config.scale);
            }
        }
    }
}
