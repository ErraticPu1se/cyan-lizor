using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview
{
    public sealed partial class PetMotion
    {
        public Vector2? RivalTarget { get; set; }
        public bool RivalBiting { get; set; }
        public void ScuffleImpulse(Vector2 impulse)
        {
            if(State!=PetMotionState.Crawling) return;
            foreach(var chunk in chunks) Set(chunk,"vel",Vector2.ClampMagnitude((Vector2)Get(chunk,"vel")+Vector2.ClampMagnitude(impulse,5),12));
        }
    }
}
