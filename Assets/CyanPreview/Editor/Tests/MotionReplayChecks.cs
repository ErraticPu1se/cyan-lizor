using System;
using System.Text;
using System.Globalization;
using UnityEngine;
namespace CyanPreview.Editor
{
    public static class MotionReplayChecks
    {
        public static void Verify()
        {
            var data=new StringBuilder();
            using(var context=new ReferenceContext(System.IO.Path.GetFullPath("local-data")))
            foreach(var species in new[]{"cyan","white"}) using(var pet=context.CreatePet(species))
            {
                pet.Generate(species=="cyan"?"9320":"2455");
                var surfaces=new DesktopSurfaces(new Rect(-500,-400,1000,800));
                surfaces.UpdateWindows(new[]{new DesktopWindow(1,new Rect(-100,-200,200,400))});
                var motion=new PetMotion(pet,surfaces,42);
                for(int frame=0;frame<800;frame++)
                {
                    if(frame==100) motion.ExploreClick(new Vector2(120,80));
                    if(frame==300) motion.BeginGrab(motion.Position);
                    if(frame>300 && frame<350) motion.DragTo(new Vector2(frame-300,180));
                    if(frame==350) motion.EndGrab();
                    motion.ObserveCursor(frame<250?(Vector2?)new Vector2(80,90):null);
                    motion.Advance(.025f);
                    data.Append((int)motion.State).Append(':');
                    for(int i=0;i<3;i++) Append(data,motion.BodyPosition(i));
                    Append(data,motion.HeadPosition);
                }
            }
            using(var hash=System.Security.Cryptography.SHA256.Create())
                Debug.Log("MOTION_REPLAY_SHA256="+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()))));
        }
        static void Append(StringBuilder text,Vector2 p)
        { text.Append(p.x.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(p.y.ToString("R",CultureInfo.InvariantCulture)).Append(';'); }
    }
}
