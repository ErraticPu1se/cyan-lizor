using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    // Opt-in observation only: never writes pet, motion, food or preference state.
    public sealed class PickupDiagnostics : IDisposable
    {
        [Serializable] public sealed class PetSample
        {
            public string key,name,motion,intent,feeding,contacts,rival;
            public string socialLook;
            public bool socialAligned;
            public int targetChunk=-1;
            public bool hold,hasTarget,hasDrive,threat,lunge,recovering;
            public Vector2 position,velocity,target,drive,frameDelta;
            public Vector2[] bodyPositions,bodyVelocities;
            public float jaw,voice;
            public int cooldown,grips;
        }
        [Serializable] sealed class Sample
        {
            public string phase,food,holder;
            public int frame;
            public float time;
            public PetSample[] pets;
        }
        readonly Transform parent;
        readonly bool socialOnly;
        readonly Dictionary<string,TextMesh> labels=new Dictionary<string,TextMesh>();
        readonly Dictionary<string,Vector2> starts=new Dictionary<string,Vector2>();
        readonly Queue<string> history=new Queue<string>();
        readonly Font font;
        readonly string directory;
        StreamWriter writer;
        float sampleWait,remaining;
        int lines,traceBytes;
        Rect viewport;
        bool traceFull;
        bool sample,disposed,failed;
        bool labelsVisible=true;
        public bool LabelsVisible
        {
            get => labelsVisible;
            set
            {
                if(labelsVisible==value) return;
                labelsVisible=value;
                if(!value) foreach(var label in labels.Values) if(label!=null) label.gameObject.SetActive(false);
            }
        }
        public string TracePath { get; private set; }
        public const int MaximumLines=5000;
        public const int MaximumBytes=8*1024*1024;
        public PickupDiagnostics(Transform parent,string directory,bool socialOnly=false)
        { this.socialOnly=socialOnly; this.parent=parent; this.directory=directory; font=Resources.GetBuiltinResource<Font>("Arial.ttf"); }
        public void BeginFrame(float seconds,List<DesktopPet> pets)
        {
            if(disposed || failed) return;
            if(pets.Count>0) viewport=new Rect(pets[0].Surfaces.Bounds.position*pets[0].Config.scale,pets[0].Surfaces.Bounds.size*pets[0].Config.scale);
            starts.Clear(); foreach(var p in pets) starts[p.Config.key]=p.WorldPosition;
            sampleWait-=Mathf.Clamp(seconds,0,.25f); sample=sampleWait<=0;
            if(sample) sampleWait=.1f;
            remaining=Mathf.Max(0,remaining-Mathf.Clamp(seconds,0,.25f));
        }
        public static PetSample Read(DesktopPet p,List<DesktopPet> pets,Vector2 start)
        {
            var m=p.Motion; var target=m.Behavior.TaskTarget; var drive=m.DiagnosticDriveTarget;
            var positions=new Vector2[m.BodyCount]; var velocities=new Vector2[m.BodyCount];
            for(int i=0;i<m.BodyCount;i++) { positions[i]=m.BodyPosition(i)*p.Config.scale; velocities[i]=m.BodyVelocity(i)*p.Config.scale; }
            var contacts=""; string rival=null; int targetChunk=-1; float closest=4;
            foreach(var other in pets) if(other!=p)
            {
                if(m.RivalTarget.HasValue)
                    for(int i=0;i<other.Motion.BodyCount;i++)
                    {
                        float distance=(other.Motion.BodyPosition(i)*other.Config.scale-m.RivalTarget.Value*p.Config.scale).sqrMagnitude;
                        if(distance<closest) { closest=distance; rival=other.Config.key; targetChunk=i; }
                    }
                int chunk=PetContacts.BiteContact(p,other,out _);
                if(chunk>=0) contacts+=(contacts.Length==0?"":",")+other.Config.key+":"+chunk;
            }
            return new PetSample {key=p.Config.key,name=p.Config.name,motion=m.State.ToString(),intent=m.Behavior.Intent.ToString(),
                socialAligned=m.SocialHeadAligned,
                socialLook=m.SocialGaze.HasValue && m.GazeTarget==m.SocialGaze.Value ? m.Behavior.Social.SourceName+" ("+m.Behavior.Social.Cue+")" : null,
                feeding=p.Feeding.Stage.ToString(),contacts=contacts,rival=rival,targetChunk=targetChunk,hold=m.Behavior.HoldPosition,hasTarget=target.HasValue,
                bodyPositions=positions,bodyVelocities=velocities,hasDrive=drive.HasValue,position=p.WorldPosition,velocity=m.BodyVelocity(0)*p.Config.scale,
                target=target.GetValueOrDefault()*p.Config.scale,drive=drive.GetValueOrDefault()*p.Config.scale,
                frameDelta=p.WorldPosition-start,jaw=p.Bite.Jaw,voice=m.VoiceIntensity,cooldown=p.Bite.CooldownTicks,
                grips=m.GripCount,threat=p.Bite.Threatening,lunge=p.Bite.Lunging,recovering=p.Bite.Recovering};
        }
        public void Record(string phase,List<DesktopPet> pets,SharedFood food)
        {
            if(disposed || failed || !sample || (socialOnly && (!labelsVisible || phase!="after-motion"))) return;
            try
            {
                var values=new PetSample[pets.Count];
                for(int i=0;i<pets.Count;i++) values[i]=Read(pets[i],pets,starts.TryGetValue(pets[i].Config.key,out var start)?start:pets[i].WorldPosition);
                if(socialOnly) { UpdateLabels(values); return; }
                string json=JsonUtility.ToJson(new Sample {phase=phase,frame=Time.frameCount,time=Time.realtimeSinceStartup,food=food.Owner.ToString(),holder=food.Holder,pets=values});
                if(writer!=null && remaining>0) Write(json);
                history.Enqueue(json); while(history.Count>100) history.Dequeue();
                if(phase=="after-motion") { UpdateLabels(values); writer?.Flush(); }
            }
            catch(Exception e) { Fail(e); }
        }
        public void Pickup(List<DesktopPet> pets,SharedFood food)
        {
            if(disposed || failed || socialOnly) return;
            try
            {
                if(writer==null)
                {
                    Directory.CreateDirectory(directory);
                    TracePath=Path.Combine(directory,"pickup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff")+".jsonl");
                    writer=new StreamWriter(new FileStream(TracePath,FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite));
                    Debug.Log("PICKUP_DIAGNOSTICS_TRACE "+TracePath);
                }
                if(remaining<=0) foreach(var previous in history) Write(previous);
                remaining=12; sample=true; Record("pickup",pets,food); writer.Flush();
            }
            catch(Exception e) { Fail(e); }
        }
        void Write(string line)
        {
            int bytes=System.Text.Encoding.UTF8.GetByteCount(line)+2;
            if(lines>=MaximumLines || traceBytes+bytes>MaximumBytes) { traceFull=true; return; }
            writer.WriteLine(line); lines++; traceBytes+=bytes;
        }
        void UpdateLabels(PetSample[] pets)
        {
            if(!labelsVisible) return;
            var live=new HashSet<string>(); var placed=new List<Rect>();
            foreach(var p in pets)
            {
                live.Add(p.key);
                if(!labels.TryGetValue(p.key,out var label))
                {
                    var obj=new GameObject("Diagnostic "+p.key); obj.transform.SetParent(parent,false);
                    label=obj.AddComponent<TextMesh>(); label.font=font; label.fontSize=32; label.characterSize=1;
                    label.anchor=TextAnchor.LowerCenter; label.alignment=TextAlignment.Center;
                    label.color=new Color(.65f,1,1,1); var renderer=obj.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial=font.material; renderer.sortingOrder=30000; labels.Add(p.key,label);
                }
                label.gameObject.SetActive(true);
                string action=p.recovering?"RECOVER":p.lunge?"LUNGE":p.threat?"THREAT":p.intent;
                label.text=p.name+" | "+p.motion+" / "+p.feeding+"\n"+action+" HOLD:"+p.hold+" jaw:"+p.jaw.ToString("0.0")+" cd:"+p.cooldown+
                    "\ntarget:"+(p.hasTarget?p.target.ToString("F0"):"none")+" body:"+p.targetChunk+" contact:"+(p.contacts.Length==0?"no":"YES")+" voice:"+p.voice.ToString("0.00")+
                    "\nmove:"+p.frameDelta.magnitude.ToString("0.0")+"px/frame grip:"+p.grips+(traceFull || lines>=MaximumLines?" TRACE FULL":"");
                if(socialOnly)
                {
                    label.text=string.IsNullOrEmpty(p.socialLook)?"":(p.socialAligned?"Looking at: ":"Noticed: ")+p.socialLook;
                    label.gameObject.SetActive(label.text.Length>0);
                    if(label.text.Length==0) continue;
                }
                else if(!string.IsNullOrEmpty(p.socialLook)) label.text+="\n"+(p.socialAligned?"Looking at: ":"Noticed: ")+p.socialLook;
                label.transform.localScale=Vector3.one;
                var size=label.GetComponent<Renderer>().bounds.size;
                float scale=Mathf.Min((socialOnly?18:72)/Mathf.Max(1,size.y),360/Mathf.Max(1,size.x));
                label.transform.localScale=Vector3.one*scale;
                float width=size.x*scale, height=size.y*scale;
                float x=Mathf.Clamp(p.position.x,viewport.xMin+width/2,viewport.xMax-width/2);
                float y=Mathf.Clamp(p.position.y+42,viewport.yMin,viewport.yMax-height);
                var box=new Rect(x-width/2,y,width,height);
                foreach(var prior in placed) if(box.Overlaps(prior)) box.y=Mathf.Max(viewport.yMin,prior.yMin-height-4);
                placed.Add(box); label.transform.localPosition=new Vector3(x,box.y,-2);
            }
            var removed=new List<string>(); foreach(var entry in labels) if(!live.Contains(entry.Key)) removed.Add(entry.Key);
            foreach(var key in removed) { ReferenceContext.Release(labels[key].gameObject); labels.Remove(key); }
        }
        void Fail(Exception error)
        { failed=true; Debug.LogWarning("Pickup diagnostics disabled: "+error.Message); try { writer?.Dispose(); } catch(IOException) {} writer=null; }
        public void Dispose()
        {
            if(disposed) return; disposed=true;
            try { writer?.Dispose(); } catch(IOException) {}
            foreach(var label in labels.Values) if(label!=null) ReferenceContext.Release(label.gameObject);
            labels.Clear(); history.Clear(); starts.Clear();
        }
    }
}
