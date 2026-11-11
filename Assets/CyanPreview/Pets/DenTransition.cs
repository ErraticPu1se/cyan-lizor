using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    // Distance along one path, rather than translating a rigid sprite: head leads, each segment follows.
    public sealed class DenPath
    {
        readonly List<Vector2> points=new List<Vector2>();
        readonly List<float> distances=new List<float>();
        public float Start { get; set; }
        public float Length => distances[distances.Count-1];
        public void Add(Vector2 point)
        {
            if(points.Count>0 && Vector2.Distance(points[points.Count-1],point)<.001f) return;
            float distance=points.Count==0?0:Length+Vector2.Distance(points[points.Count-1],point);
            points.Add(point); distances.Add(distance);
        }
        public void Curve(Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {
            for(int i=1;i<=24;i++)
            { float t=i/24f,u=1-t; Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d); }
        }
        public Vector2 At(float distance)
        {
            if(points.Count<2) throw new InvalidOperationException("Den path needs two points.");
            if(distance<0) return points[0]+(points[1]-points[0]).normalized*distance;
            for(int i=1;i<points.Count;i++) if(distance<=distances[i])
                return Vector2.Lerp(points[i-1],points[i],Mathf.InverseLerp(distances[i-1],distances[i],distance));
            int last=points.Count-1;
            return points[last]+(points[last]-points[last-1]).normalized*(distance-Length);
        }
        public static DenPath Emerge(Vector2 entrance,Rect bounds)
        {
            float direction=entrance.x<=bounds.center.x?1:-1;
            var side=Vector2.right*direction;
            var path=new DenPath(); var under=entrance+Vector2.down*32;
            path.Add(entrance+Vector2.down*180); path.Add(under);
            var bend=entrance+side*36+Vector2.up*3;
            path.Curve(under,entrance+Vector2.up*20,entrance+side*16+Vector2.up*3,bend);
            path.Add(entrance+side*Mathf.Min(140,bounds.width*.3f)+Vector2.up*3);
            return path;
        }
    }
    public sealed partial class PetMotion
    {
        public DenPath BeginDenEntry(Vector2 entrance)
        {
            var path=new DenPath(); var tail=(Array)Get(graphics,"tail");
            for(int i=tail.Length-1;i>=0;i--) path.Add((Vector2)Get(tail.GetValue(i),"pos"));
            for(int i=2;i>=0;i--) path.Add((Vector2)Get(chunks[i],"pos"));
            path.Start=path.Length;
            var forward=(Position-(Vector2)Get(chunks[1],"pos")).normalized;
            var under=entrance+Vector2.down*32;
            path.Curve(Position,Position+forward*18,entrance+Vector2.up*20,under);
            path.Add(entrance+Vector2.down*180); return path;
        }
        public void PoseDen(DenPath path,float progress)
        {
            float distance=Mathf.Lerp(path.Start,path.Length,Mathf.SmoothStep(0,1,progress));
            Action<object,Vector2> place=(part,position)=> { Set(part,"pos",position); Set(part,"lastPos",position); Set(part,"vel",Vector2.zero); };
            var draw=(Vector2[,])Get(graphics,"drawPositions");
            for(int i=0;i<3;i++) { var position=path.At(distance-i*17); place(chunks[i],position); draw[i,0]=draw[i,1]=position; }
            place(Get(graphics,"head"),path.At(distance+11.4f));
            var tail=(Array)Get(graphics,"tail"); float behind=34;
            for(int i=0;i<tail.Length;i++)
            {
                var part=tail.GetValue(i); behind+=(float)Get(part,"connectionRad");
                place(part,path.At(distance-behind)); Set(part,"stretched",1f);
            }
            Set(tail.GetValue(0),"connectedPoint",(Vector2?)draw[2,0]);
            var limbs=(Array)Get(graphics,"limbs");
            for(int i=0;i<4;i++)
            {
                float anchor=distance-(i<2?0:34);
                var tangent=(path.At(anchor+1)-path.At(anchor-1)).normalized;
                var normal=new Vector2(-tangent.y,tangent.x);
                var position=path.At(anchor-5)+normal*(i%2==0?1:-1)*(8+Mathf.Sin(progress*24+i)*3);
                place(feet[i],position); place(limbs.GetValue(i),position);
                planted[i]=seeking[i]=false; gripAge[i]=0;
            }
            foreach(string field in new[]{"depthRotation","lastDepthRotation","headDepthRotation","lastHeadDepthRotation"}) Set(graphics,field,0f);
            Set(graphics,"lookPos",path.At(distance+30));
            var random=UnityEngine.Random.state;
            try { foreach(var cosmetic in (IEnumerable)Get(graphics,"cosmetics")) Call(cosmetic,"Update"); }
            finally { UnityEngine.Random.state=random; }
        }
        public void FinishDenTransition()
        {
            lookTarget=null; Behavior.TaskTarget=null; Behavior.Rest(); accumulator=0; resting=false;
            ReleaseSupport();
        }
    }
}
