using System.Collections.Generic;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        readonly List<DesktopPetConfiguration> extraPets=new List<DesktopPetConfiguration>();
        bool primaryPresent=true, allowFighting, showActivePets=true;
        string scuffleFirst,scuffleSecond;
        int scuffleRequest;
        DesktopSettings DesktopConfiguration()
        {
            var settings=PrimaryConfiguration(); var roster=new List<DesktopPetConfiguration>();
            if(primaryPresent) roster.Add(DesktopPetConfiguration.From(settings,"primary",presets?.Data.Current.name??"Current pet"));
            roster.AddRange(extraPets);
            // Stopping the last pet still needs a valid shutdown message.
            if(roster.Count==0) roster.Add(DesktopPetConfiguration.From(settings));
            settings.pets=roster.ToArray(); settings.allowFighting=allowFighting;
            settings.scuffleFirst=scuffleFirst; settings.scuffleSecond=scuffleSecond; settings.scuffleRequest=scuffleRequest; return settings;
        }
        void RemoveActivePet(string key)
        {
            if(key=="primary") primaryPresent=false; else extraPets.RemoveAll(p=>p.key==key);
            if(!primaryPresent && extraPets.Count==0) desktop.Stop();
        }
        void DrawActivePets()
        {
            GUILayout.BeginVertical(theme.Card);
            int count=(primaryPresent?1:0)+extraPets.Count;
            if(Section("Desktop pets · "+(desktop.Active?count:0)+" / "+DesktopSettings.MaximumPets,ref showActivePets))
            {
                GUILayout.Label("Controls below edit your primary pet. Right-click any pet to change its own settings. Extra pets last for this session.",theme.Small);
                if(desktop.Active)
                {
                    if(primaryPresent) DrawActivePet("primary",presets?.Data.Current.name??"Current pet");
                    foreach(var p in extraPets.ToArray()) DrawActivePet(p.key,p.name+" · "+p.id);
                }
                if(presets!=null)
                {
                    GUI.enabled=Ready && desktop.Active && count<DesktopSettings.MaximumPets;
                    foreach(var p in presets.Data.pets.ToArray())
                        if(GUILayout.Button("Add · "+p.name)) extraPets.Add(DesktopPetConfiguration.From(p));
                    GUI.enabled=true;
                }
                allowFighting=GUILayout.Toggle(allowFighting,"Allow fighting (no injuries)");
                GUILayout.Label("Brief scuffles, then a rest. Grabbing a pet stops its fight.",theme.Small);
                DrawScuffleControls();
            }
            GUILayout.EndVertical();
        }
        void DrawScuffleControls()
        {
            if(!desktop.Active || !Ready) return;
            var roster=DesktopConfiguration().pets;
            if(roster.Length<2) { GUILayout.Label("Add a second pet to test a scuffle.",theme.Small); return; }
            if(!System.Array.Exists(roster,p=>p.key==scuffleFirst)) scuffleFirst=roster[0].key;
            if(!System.Array.Exists(roster,p=>p.key==scuffleSecond) || scuffleSecond==scuffleFirst)
                scuffleSecond=System.Array.Find(roster,p=>p.key!=scuffleFirst).key;
            GUILayout.Label("Test a scuffle · choose two pets",theme.Small);
            for(int slot=0;slot<2;slot++)
            {
                GUILayout.Label(slot==0?"First pet":"Second pet",theme.Small);
                foreach(var p in roster)
                {
                    string selected=slot==0?scuffleFirst:scuffleSecond;
                    if(GUILayout.Button(p.name+" · "+p.id,p.key==selected?theme.Selected:GUI.skin.button))
                    { if(slot==0) scuffleFirst=p.key; else scuffleSecond=p.key; }
                }
            }
            GUI.enabled=allowFighting && !trayState.Paused && scuffleFirst!=scuffleSecond;
            if(GUILayout.Button("Start a scuffle",theme.Primary)) { scuffleRequest++; }
            GUI.enabled=true;
            if(!allowFighting) GUILayout.Label("Enable Allow fighting first.",theme.Small);
            if(trayState.Paused) GUILayout.Label("Resume the pets first.",theme.Small);
            if(socialStatus!=null && socialStatus.request==scuffleRequest && !string.IsNullOrEmpty(socialStatus.message)) GUILayout.Label(socialStatus.message,theme.Small);
        }
        DesktopSocialStatus socialStatus;
        float nextSocialRead;
        void DrawActivePet(string key,string label)
        {
            if(Time.unscaledTime>=nextSocialRead) { nextSocialRead=Time.unscaledTime+.2f; socialStatus=desktop.ReadSocialStatus(); }
            GUILayout.BeginHorizontal(); GUILayout.Label(label,theme.Small);
            if(GUILayout.Button("Remove",GUILayout.Width(68))) RemoveActivePet(key);
            GUILayout.EndHorizontal();
            var state=socialStatus?.pets==null?null:System.Array.Find(socialStatus.pets,p=>p.key==key);
            var color=GUI.color;
            if(state!=null && allowFighting)
            {
                GUI.color=state.active?new Color(1,.72f,.3f):color;
                GUILayout.Label(state.state+" · tension "+Mathf.RoundToInt(state.tension*100)+"%"+(state.active?" · with "+state.rival:""),theme.Small);
            }
            else GUILayout.Label(allowFighting?"Waiting for status…":"Fighting off",theme.Small);
            GUI.color=color;
        }
    }
}
