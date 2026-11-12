using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    [Serializable] public sealed class DesktopPetConfiguration
    {
        public string key,id,name,species="cyan";
        public float scale=1.2f;
        public bool roam=true,jumps=true,sound=true,clickToExplore=true;
        public ClimbingMode climbingMode=ClimbingMode.EdgesAndBackgrounds;
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(key)||key.Length>64||key.IndexOf('|')>=0||!ReferenceBridge.TryParseId(id,out _,out _)||species!="cyan"||float.IsNaN(scale)||float.IsInfinity(scale)||scale<.5f||scale>2||!Enum.IsDefined(typeof(ClimbingMode),climbingMode)) throw new InvalidDataException("Invalid desktop pet configuration");
        }
        public static DesktopPetConfiguration From(DesktopSettings s,string key="primary",string name="Current pet") => new DesktopPetConfiguration {key=key,name=name,id=s.id,scale=s.scale,roam=s.roam,jumps=s.jumps,sound=s.sound,clickToExplore=s.clickToExplore,climbingMode=s.climbingMode};
        public static DesktopPetConfiguration From(PetPreset p) => new DesktopPetConfiguration {key=Guid.NewGuid().ToString("N"),name=p.name,id=p.appearanceId,species=p.species,scale=p.scale,roam=p.roam,jumps=p.jumps,sound=p.sound,clickToExplore=p.clickToExplore,climbingMode=p.climbingMode};
        public DesktopSettings MenuSettings() => new DesktopSettings {id=id,scale=scale,roam=roam,jumps=jumps,sound=sound,clickToExplore=clickToExplore,climbingMode=climbingMode};
        public void ApplyMenu(DesktopMenuAction action)
        { var s=MenuSettings(); s.ApplyMenu(action); scale=s.scale; roam=s.roam; jumps=s.jumps; sound=s.sound; clickToExplore=s.clickToExplore; climbingMode=s.climbingMode; }
    }
    public sealed class DesktopPet : IDisposable
    {
        public DesktopPetConfiguration Config { get; private set; }
        public readonly ReferenceBridge Pet;
        public readonly SpriteMeshView View;
        public readonly GameObject Root;
        public PetMotion Motion { get; private set; }
        public PetBite Bite { get; private set; }
        public PetInteraction Input { get; private set; }
        public DesktopSurfaces Surfaces { get; private set; }
        public DenRouteSurfaces Den { get; private set; }
        public CyanJumpEffects Effects { get; private set; }
        public CyanPetAudio Audio { get; private set; }
        public readonly PetFeeding Feeding=new PetFeeding();
        public int FoodRevision=-1;
        public bool FollowingFood;
        public float Attention;
        readonly ReferenceContext context;
        public Vector2 WorldPosition => Motion.Position*Config.scale;
        public Vector2 WorldMouth => Motion.MouthPosition*Config.scale;
        public Vector2 Local(Vector2 world) => world/Config.scale;
        public bool Contains(Vector2 world) => !Feeding.Frozen && Motion.Contains(Local(world));
        public DesktopPet(ReferenceContext context,Transform parent,DesktopPetConfiguration config,Rect worldBounds,int order)
        {
            this.context=context; Config=config; Root=new GameObject("Desktop pet "+config.key); Root.transform.SetParent(parent,false);
            Pet=context.CreatePet(config.species); View=new SpriteMeshView(Root.transform,order) {DesktopTransparency=true};
            try { Rebuild(config,worldBounds,null); }
            catch { Dispose(); throw; }
        }
        public void Rebuild(DesktopPetConfiguration config,Rect worldBounds,Vector2? worldPosition)
        {
            Input?.Cancel(); Audio?.Dispose(); Effects?.Dispose(); Feeding.Cancel(); Config=config;
            Root.transform.localScale=Vector3.one*config.scale;
            Surfaces=new DesktopSurfaces(new Rect(worldBounds.position/config.scale,worldBounds.size/config.scale)) {Mode=config.climbingMode};
            Den=new DenRouteSurfaces(Surfaces,Surfaces.Bounds);
            Pet.Generate(config.id); Pet.Pose(0,.7f);
            Bite=new PetBite(Pet.Seed ^ Config.key.GetHashCode(),Pet.Species.Biting,Pet.Personality.Aggression);
            Motion=new PetMotion(Pet,Den) {Roam=config.roam,AutonomousJumps=config.jumps};
            if(worldPosition.HasValue) { var p=Surfaces.Nearest(Local(worldPosition.Value),out var n)+n*14; Motion.ShiftWithSurface(p-Motion.Position); }
            Input=new PetInteraction(Motion) {ClickToExplore=config.clickToExplore};
            Effects=new CyanJumpEffects(Root.transform,Motion,Pet.EffectColor,Pet.Seed);
            try { Audio=new CyanPetAudio(Root.transform,context,Motion,Effects,Pet.Seed) {Enabled=config.sound}; }
            catch(Exception e) { Audio=null; Debug.LogWarning("Pet audio unavailable: "+e.Message); }
            FoodRevision=-1; FollowingFood=false;
        }
        public void Configure(DesktopPetConfiguration config,bool paused)
        {
            if(Surfaces.Mode!=config.climbingMode) { Surfaces.Mode=config.climbingMode; Motion.RefreshSurfaceSupport(); }
            Config=config; Motion.Roam=config.roam; Motion.AutonomousJumps=config.jumps && !FollowingFood && !Feeding.UsingRoute; Input.ClickToExplore=config.clickToExplore;
            if(Audio!=null) Audio.Enabled=config.sound&&!paused;
        }
        public void RefreshWindows(IEnumerable<DesktopWindow> snapshot)
        {
            if(Den.Active) { Surfaces.UpdateWindows(snapshot); return; }
            var delta=Surfaces.UpdateWindows(snapshot,Motion.Position,Motion.State==PetMotionState.Crawling&&Motion.GripCount>0&&!Input.CapturesPointer);
            if(Surfaces.SupportLost) Motion.ReleaseSupport();
            else if(delta.sqrMagnitude>0) { Motion.Behavior.Rest(); Motion.ShiftWithSurface(delta,false); }
        }
        public void Advance(float dt,bool paused,bool draw=true)
        {
            if(!paused) { Audio?.Advance(dt); if(!Feeding.Frozen) Motion.Advance(dt); Effects.Advance(dt); }
            if(draw) Draw();
        }
        public void Draw() { Pet.Draw(Motion.Interpolation); View.Refresh(Pet); }
        public void Dispose()
        { Input?.Cancel(); Audio?.Dispose(); Effects?.Dispose(); View?.Dispose(); Pet?.Dispose(); if(Root!=null) UnityEngine.Object.Destroy(Root); }
    }
}
