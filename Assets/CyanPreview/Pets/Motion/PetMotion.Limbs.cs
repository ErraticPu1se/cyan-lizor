using System;
using System.Collections;
using UnityEngine;
using static CyanPreview.ReferenceContext;

namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        void UpdateLimbs(Vector2 tangent,Vector2 normal)
        {
            for (int i = 0; i < 4; i++)
            {
                var shoulder = (Vector2)Get(chunks[i < 2 ? 0 : 2], "pos");
                bool flying = State == PetMotionState.Airborne;
                float reach = (float)Get(((Array)Get(graphics, "limbs")).GetValue(i), "jointDist");
                // LizardLimb releases a grip when the shoulder passes it and the
                // limb reaches its limit; its partner's grip delays the next step.
                var localForward = i < 2 ? (resting ? (Position-BodyPosition(1)).normalized : tangent) : ((Vector2)Get(chunks[1], "pos") - shoulder).normalized;
                if (localForward.sqrMagnitude < .01f) localForward = tangent;
                float extension = Vector2.Distance(anchors[i], shoulder);
                bool trailing = Vector2.Dot(shoulder - anchors[i], localForward) > 0;
                bool release = !resting && !Recovery.Active && planted[i] && trailing && extension > reach - 2 && (gripAge[i ^ 1] > 2 || extension > reach + 3);
                bool lostSupport=(planted[i] || seeking[i]) && Vector2.Distance(anchors[i],surfaces.Nearest(anchors[i],out _))>2;
                if (release || flying || lostSupport || extension > (resting ? reach-1 : reach+3)) { planted[i] = seeking[i] = false; gripAge[i] = 0; }
                if (!planted[i] && !seeking[i])
                {
                    var spread = normal == Vector2.zero ? new Vector2(-localForward.y, localForward.x) * (i % 2 == 0 ? 14 : -14) : Vector2.zero;
                    var preferred=shoulder + localForward * (Recovery.Active ? 0 : i % 2 == 0 ? 16 : 10) + spread;
                    seeking[i]=TryFindFoothold(shoulder,preferred,reach,out anchors[i]);
                }
                Vector2 hunt = planted[i] || seeking[i] ? anchors[i] : shoulder+Vector2.ClampMagnitude(anchors[i]-shoulder,Mathf.Max(0,reach-1));
                bool reachingToCatch = false;
                Vector2 catchNormal = Vector2.zero;
                SurfaceSupport caughtSupport=default;
                if (flying)
                {
                    hunt = shoulder - localForward * 8 + normal * (i % 2 == 0 ? 10 : -10);
                    if (!propelled && FallCatch.Ready && surfaces is IEdgeCatchSurfaces catchSurfaces
                        && catchSurfaces.TryCatchSupport(shoulder,reach,out var support) && capabilities.Supports(support.Kind))
                    { caughtSupport=support; hunt=anchors[i]=support.Point; catchNormal=support.Normal; reachingToCatch=true; }
                }
                // LizardLimb increases hunt speed by its connection velocity when reaching.
                Set(feet[i], "huntSpeed", 6f + (reachingToCatch ? ((Vector2)Get(chunks[i < 2 ? 0 : 2], "vel")).magnitude : 0));
                Set(feet[i], "absoluteHuntPos", hunt);
                Call(feet[i], "Update");
                Call(feet[i], "ConnectToPoint", shoulder, reach, false, 0f, Get(chunks[i < 2 ? 0 : 2], "vel"), 0f, 0f);
                if (reachingToCatch && Vector2.Distance((Vector2)Get(feet[i], "pos"), hunt) < 2
                    && Vector2.Distance(shoulder, hunt) <= reach)
                {
                    float speed = ((Vector2)Get(chunks[1], "vel")).magnitude;
                    ChangeState(PetMotionState.Crawling);
                    if(caughtSupport.Kind==SurfaceKind.Pole) poleGrip=caughtSupport;
                    Recovery.Begin(speed,catchNormal,hunt);
                    foreach (var chunk in chunks) Set(chunk,"vel",(Vector2)Get(chunk,"vel") * .5f);
                    Behavior.Rest(); resting = false;
                    planted[i] = true; seeking[i] = false;
                }
                if (!flying && seeking[i] && Vector2.Distance(shoulder, anchors[i]) <= reach && Vector2.Distance((Vector2)Get(feet[i], "pos"), anchors[i]) < 2)
                {
                    if(!planted[i] && hasDestination && State==PetMotionState.Crawling && !Recovery.Active && BodyVelocity(i<2?0:2).sqrMagnitude>.04f) FootstepRevision++;
                    planted[i] = true; seeking[i] = false;
                }
                if (planted[i]) gripAge[i]++;
            }
        }
        object CreateFoot(int index)
        {
            var state = UnityEngine.Random.state;
            try { return pet.Context.New("Limb", graphics, chunks[index < 2 ? 0 : 2], index, 2.5f, .7f, .99f, 6f, .6f); }
            finally { UnityEngine.Random.state = state; }
        }
        bool TryFindFoothold(Vector2 shoulder,Vector2 preferred,float reach,out Vector2 anchor)
        {
            // Project progressively shorter steps onto real support, leaving room for idle sway.
            for(int step=4;step>=0;step--)
            {
                var point=Vector2.Lerp(shoulder,preferred,step*.25f);
                anchor=GroundSupport(point,out _);
                if(capabilities.Supports(surfaces.SupportAt(anchor).Kind) && Vector2.Distance(shoulder,anchor)<=Mathf.Max(0,reach-3)) return true;
            }
            anchor=GroundSupport(shoulder,out _);
            return capabilities.Supports(surfaces.SupportAt(anchor).Kind) && Vector2.Distance(shoulder,anchor)<=Mathf.Max(0,reach-1);
        }
    }
}
