using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CyanPreview
{
    [Serializable]
    public sealed class PetPreset
    {
        public string key, name, species = "cyan", appearanceId;
        public float scale = DesktopSettings.DefaultScale;
        public ClimbingMode climbingMode;
        public bool sound, roam, jumps, clickToExplore;
        public static PetPreset Capture(string appearance, CompanionPreferences settings) => new PetPreset {
            key=Guid.NewGuid().ToString("N"), name="My cyan", appearanceId=appearance,
            scale=settings.scale, climbingMode=settings.climbingMode, sound=settings.sound,
            roam=settings.roam, jumps=settings.jumps, clickToExplore=settings.clickToExplore };
        public void Validate()
        {
            if(!Guid.TryParseExact(key,"N",out _) || string.IsNullOrWhiteSpace(name) || name.Length>40 || name.Any(char.IsControl)
                || !ReferenceBridge.TryParseId(appearanceId,out _,out _) || float.IsNaN(scale) || float.IsInfinity(scale)
                || scale<.5f || scale>2 || !Enum.IsDefined(typeof(ClimbingMode),climbingMode))
                throw new InvalidDataException("Invalid saved pet.");
            LizardSpecies.Find(species);
        }
    }
    [Serializable]
    public sealed class PetPresetCollection
    {
        public int version=1;
        public string selected;
        public List<PetPreset> pets=new List<PetPreset>();
        public PetPreset Current => pets.Find(p=>p.key==selected);
        public PetPresetCollection Copy() => JsonUtility.FromJson<PetPresetCollection>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if(version!=1 || pets==null || pets.Count==0 || pets.Count>100 || pets.Any(p=>p==null))
                throw new InvalidDataException("Unsupported or empty saved pets file.");
            foreach(var pet in pets) pet.Validate();
            if(pets.Select(p=>p.key).Distinct().Count()!=pets.Count || Current==null)
                throw new InvalidDataException("Saved pet identity is missing or duplicated.");
        }
    }
    public sealed class PetPresetStore
    {
        readonly string path;
        public PetPresetCollection Data { get; private set; }
        public string Warning { get; private set; }
        public PetPresetStore(string path, PetPreset legacy)
        {
            this.path=path;
            if(File.Exists(path))
            {
                try { Data=Read(path); return; }
                catch(Exception e) when(e is ArgumentException || e is IOException || e is NotSupportedException)
                {
                    // Recover only from a validated backup; never overwrite unreadable user data with defaults.
                    try { Data=Read(path+".bak"); Warning="Recovered saved pets from the previous good save."; }
                    catch { throw new InvalidDataException("Saved pets could not be loaded. Your files are unchanged. "+e.Message,e); }
                    File.Copy(path,path+".damaged-"+DateTime.UtcNow.Ticks);
                    File.Copy(path+".bak",path,true);
                    return;
                }
            }
            if(File.Exists(path+".bak")) { Data=Read(path+".bak"); File.Copy(path+".bak",path); return; }
            legacy.Validate();
            Commit(new PetPresetCollection {selected=legacy.key,pets=new List<PetPreset>{legacy}});
        }
        static PetPresetCollection Read(string file)
        {
            if(new FileInfo(file).Length>1024*1024) throw new InvalidDataException("Saved pets file is too large.");
            var result=JsonUtility.FromJson<PetPresetCollection>(File.ReadAllText(file));
            if(result==null) throw new InvalidDataException("Missing saved pets.");
            result.Validate(); return result;
        }
        public void Commit(PetPresetCollection next)
        {
            next.Validate();
            string json=JsonUtility.ToJson(next,true);
            if(Data!=null && json==JsonUtility.ToJson(Data,true)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp";
            using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var writer=new StreamWriter(stream)) { writer.Write(json); writer.Flush(); stream.Flush(true); }
            if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
            Data=next.Copy();
        }
        public void SaveCurrent(PetPreset values)
        {
            var next=Data.Copy(); values.key=next.Current.key; values.name=next.Current.name;
            next.pets[next.pets.FindIndex(p=>p.key==next.selected)]=values; Commit(next);
        }
        public void Duplicate(string name)
        {
            var next=Data.Copy(); var pet=JsonUtility.FromJson<PetPreset>(JsonUtility.ToJson(next.Current));
            pet.key=Guid.NewGuid().ToString("N"); pet.name=(name??"").Trim(); next.pets.Add(pet); next.selected=pet.key; Commit(next);
        }
        public void Rename(string name)
        { var next=Data.Copy(); next.Current.name=(name??"").Trim(); Commit(next); }
        public PetPresetCollection WithoutCurrent()
        {
            if(Data.pets.Count==1) throw new InvalidOperationException("Keep at least one saved pet.");
            var next=Data.Copy(); next.pets.RemoveAll(p=>p.key==next.selected); next.selected=next.pets[0].key; return next;
        }
    }
}
