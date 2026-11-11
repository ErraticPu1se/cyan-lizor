using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        public Vector2? RivalTarget { get; set; }
        public bool RivalStance { get; set; }
        public bool RivalBiting { get; set; }
        public bool BiteToward(Vector2 target)
        {
            if(State!=PetMotionState.Crawling || !RivalTarget.HasValue || float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsInfinity(target.x) || float.IsInfinity(target.y)) return false;
            var direction=(target-Position).normalized;
            if(direction.sqrMagnitude<.01f) return false;
            // Lizard's attack bends the body: front +8, middle/rear -6. No Violence/Grab call.
            for(int i=0;i<chunks.Length;i++)
                ReferenceContext.Set(chunks[i],"vel",Vector2.ClampMagnitude((Vector2)ReferenceContext.Get(chunks[i],"vel")+direction*(i==0?8:-6),14));
            return true;
        }
        public void ScuffleImpulse(Vector2 impulse)
        {
            if(State!=PetMotionState.Crawling) return;
            foreach(var chunk in chunks) Set(chunk,"vel",Vector2.ClampMagnitude((Vector2)Get(chunk,"vel")+Vector2.ClampMagnitude(impulse,5),12));
        }
    }
}
