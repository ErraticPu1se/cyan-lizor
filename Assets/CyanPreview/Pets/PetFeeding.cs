using System;
using UnityEngine;
namespace CyanPreview
{
    public enum FeedingStage { None, Placing, Investigating, Approaching, Carrying, Entering, Eating, Emerging }
    // Per-pet task state; the host supplies surfaces, input, rendering, and elapsed time.
    public sealed class PetFeeding
    {
        public FeedingStage Stage { get; private set; }
        public Vector2 Food { get; private set; }
        public Vector2 Entrance { get; private set; }
        public Vector2 InspectionPoint { get; private set; }
        public Vector2 Den { get; private set; }
        public bool ExitLeft { get; private set; }
        public bool UsingRoute => Stage==FeedingStage.Entering || Stage==FeedingStage.Eating || Stage==FeedingStage.Emerging;
        public Vector2? Target => Stage==FeedingStage.Approaching ? Food : Stage==FeedingStage.Carrying || Stage==FeedingStage.Emerging ? Entrance : Stage==FeedingStage.Entering ? Den : (Vector2?)null;
        public bool Active => Stage!=FeedingStage.None;
        public bool Frozen => Stage==FeedingStage.Eating;
        public bool FoodVisible => Stage==FeedingStage.Placing || Stage==FeedingStage.Investigating || Stage==FeedingStage.Approaching || Stage==FeedingStage.Carrying || Stage==FeedingStage.Entering;
        public string Status => Stage==FeedingStage.Placing ? "Place food with a click · Escape cancels" : Stage.ToString();
        Rect bounds;
        float elapsed;
        public bool Begin()
        { if(Active) return false; Change(FeedingStage.Placing); return true; }
        public void Preview(Vector2 cursor,IPetSurfaces surfaces)
        { if(Stage==FeedingStage.Placing) Food=surfaces.Nearest(cursor,out var normal)+normal*10; }
        public bool Place(Vector2 position,Rect area)
        {
            if(Stage!=FeedingStage.Placing || !Valid(area) || !Finite(position.x) || !Finite(position.y)) return false;
            bounds=area; Food=Clamp(position,area); InspectionPoint=Clamp(Food+Vector2.right*75,area); Change(FeedingStage.Investigating); return true;
        }
        public Vector2 ChooseInspectionPoint(Vector2 observer,IPetSurfaces surfaces)
        {
            var left=surfaces.Advance(Food,-75); var right=surfaces.Advance(Food,75);
            InspectionPoint=Vector2.Distance(observer,left)<Vector2.Distance(observer,right)?left:right;
            return InspectionPoint;
        }
        // Reset is used only when the host also rebuilds motion (display/size change or disposal).
        public void Cancel() { Change(FeedingStage.None); }
        public void RequestReturn()
        { if(UsingRoute) Change(FeedingStage.Emerging); else Cancel(); }
        public void Advance(float seconds,Vector2 head,Vector2 mouth,bool grabbed,Rect area,bool grounded=true, Rect? occupied=null)
        {
            if(!Active || Stage==FeedingStage.Placing || !Finite(seconds) || seconds<=0) return;
            if(!Valid(area)) { Cancel(); return; }
            if(area!=bounds)
            {
                // The host rebuilds motion on a display/scale change; never leave a pet hidden at an obsolete entrance.
                Cancel(); return;
            }
            if(grabbed)
            {
                if(Stage==FeedingStage.Carrying) { Food=Clamp(mouth,area); Change(FeedingStage.Approaching); }
                return;
            }
            elapsed+=Mathf.Min(seconds,.1f);
            if(Stage==FeedingStage.Investigating)
            {
                if(elapsed>=5 || (elapsed>=1.5f && Vector2.Distance(head,InspectionPoint)<50)) Change(FeedingStage.Approaching);
            }
            else if(Stage==FeedingStage.Approaching)
            {
                if(Vector2.Distance(head,Food)<35)
                {
                    ExitLeft=head.x<area.center.x;
                    float inset=Mathf.Min(200,area.width*.35f);
                    Entrance=new Vector2(ExitLeft?area.xMin+inset:area.xMax-inset,area.yMin+14);
                    Den=new Vector2(ExitLeft?area.xMin-260:area.xMax+260,area.yMin+14);
                    Change(FeedingStage.Carrying);
                }
                else if(elapsed>60) Cancel();
            }
            else if(Stage==FeedingStage.Carrying)
            {
                Food=mouth;
                if(grounded && Vector2.Distance(head,Entrance)<28) Change(FeedingStage.Entering);
                else if(elapsed>60) Cancel();
            }
            else if(Stage==FeedingStage.Entering)
            {
                Food=mouth;
                if(occupied.HasValue && (ExitLeft?occupied.Value.xMax<area.xMin:occupied.Value.xMin>area.xMax)
                    && Vector2.Distance(head,Den)<30) Change(FeedingStage.Eating);
                else if(elapsed>60) RequestReturn();
            }
            else if(Stage==FeedingStage.Eating)
            { if(elapsed>=3) Change(FeedingStage.Emerging); }
            else if(Stage==FeedingStage.Emerging)
            {
                if(occupied.HasValue && occupied.Value.xMin>=area.xMin && occupied.Value.xMax<=area.xMax
                    && Vector2.Distance(head,Entrance)<30) Cancel();
            }
        }
        void Change(FeedingStage stage) { Stage=stage; elapsed=0; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Valid(Rect area) => Finite(area.x) && Finite(area.y) && Finite(area.width) && Finite(area.height) && area.width>=60 && area.height>=60;
        static Vector2 Clamp(Vector2 point,Rect area) => new Vector2(Mathf.Clamp(point.x,area.xMin+10,area.xMax-10),Mathf.Clamp(point.y,area.yMin+10,area.yMax-10));
    }
}
