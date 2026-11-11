using UnityEngine;
namespace CyanPreview
{
    public sealed partial class PreviewApp
    {
        void DrawFeeding()
        {
            GUILayout.BeginVertical(theme.Card);
            GUILayout.Label("Feeding prototype",theme.Heading);
            string status=desktop.Active?desktop.FeedingStatus:"Start your pet to offer food.";
            GUILayout.Label(string.IsNullOrEmpty(status)?"Offer a morsel. Your pet carries it to the edge, eats, and returns.":status,theme.Small);
            GUILayout.BeginHorizontal(); GUI.enabled=desktop.Active && !trayState.Paused;
            if(GUILayout.Button("Place food")) desktopFeedRequest++;
            GUI.enabled=desktop.Active;
            if(GUILayout.Button("Cancel feeding")) desktopCancelFeedRequest++;
            GUI.enabled=true; GUILayout.EndHorizontal(); GUILayout.EndVertical();
        }
    }
}
