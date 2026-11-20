using System.Collections.Generic;
using UnityEngine;
namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        readonly List<DesktopPetConfiguration> extraPets=new List<DesktopPetConfiguration>();
        const string AttentionLabelsKey="Lizor.ShowAttentionLabels";
        bool showAttentionLabels=true;
        bool primaryPresent=true, allowFighting, muteAll;
        DesktopSettings DesktopConfiguration()
        {
            var settings=PrimaryConfiguration(); var roster=new List<DesktopPetConfiguration>();
            if(primaryPresent) roster.Add(DesktopPetConfiguration.From(settings,"primary",primaryName??presets?.Data.Current.name??"Current pet"));
            roster.AddRange(extraPets);
            // Stopping the last pet still needs a valid shutdown message.
            if(roster.Count==0) roster.Add(DesktopPetConfiguration.From(settings));
            settings.showAttentionLabels=false; settings.pets=roster.ToArray(); settings.allowFighting=allowFighting; settings.muteAll=muteAll;
            return settings;
        }
        void RemoveActivePet(string key)
        {
            if(key=="primary") primaryPresent=false; else extraPets.RemoveAll(p=>p.key==key);
            if(!primaryPresent && extraPets.Count==0) desktop.Stop();
            if(selectedPetKey==key) SelectLivePet(primaryPresent?"primary":extraPets.Count>0?extraPets[0].key:null);
        }
    }
}

