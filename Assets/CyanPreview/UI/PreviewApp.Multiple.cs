using System.Collections.Generic;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        readonly List<DesktopPetConfiguration> extraPets=new List<DesktopPetConfiguration>();
        bool primaryPresent=true, allowFighting, showActivePets=true;
        DesktopSettings DesktopConfiguration()
        {
            var settings=PrimaryConfiguration(); var roster=new List<DesktopPetConfiguration>();
            if(primaryPresent) roster.Add(DesktopPetConfiguration.From(settings,"primary",presets?.Data.Current.name??"Current pet"));
            roster.AddRange(extraPets);
            // Stopping the last pet still needs a valid shutdown message.
            if(roster.Count==0) roster.Add(DesktopPetConfiguration.From(settings));
            settings.pets=roster.ToArray(); settings.allowFighting=allowFighting; return settings;
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
            }
            GUILayout.EndVertical();
        }
        void DrawActivePet(string key,string label)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label,theme.Small);
            if(GUILayout.Button("Remove",GUILayout.Width(68))) RemoveActivePet(key);
            GUILayout.EndHorizontal();
        }
    }
}
