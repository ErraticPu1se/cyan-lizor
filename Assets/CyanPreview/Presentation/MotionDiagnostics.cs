using System;
using System.IO;
using UnityEngine;
namespace CyanPreview
{
    // One bounded trace per overlay process; no settings, simulation or random-state writes.
    public sealed class MotionDiagnostics : IDisposable
    {
        [Serializable] sealed class Row
        { public string key,name; public float time,scale; public int frame; public PetMotion.MotionSample motion; }
        public const int MaximumSamples=12000;
        const long MaximumBytes=32*1024*1024;
        StreamWriter writer;
        int samples;
        long bytes;
        bool stopped;
        public string TracePath { get; private set; }
        public MotionDiagnostics(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                TracePath=Path.Combine(directory,"motion-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".jsonl");
                writer=new StreamWriter(new FileStream(TracePath,FileMode.CreateNew,FileAccess.Write,FileShare.Read));
                Debug.Log("MOTION_TRACE "+TracePath);
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) { Stop(e); }
        }
        public void Observe(DesktopPet pet)
        {
            if(stopped) { pet.Motion.MotionObserver=null; return; }
            if(pet.Motion.MotionObserver==null) pet.Motion.MotionObserver=s=>Record(pet.Config.key,pet.Config.name,pet.Config.scale,s);
        }
        public void Record(string key,string name,float scale,PetMotion.MotionSample sample)
        {
            if(stopped) return;
            try
            {
                string line=JsonUtility.ToJson(new Row {key=key,name=name,scale=scale,time=Time.realtimeSinceStartup,frame=Time.frameCount,motion=sample});
                int size=System.Text.Encoding.UTF8.GetByteCount(line)+2;
                if(samples>=MaximumSamples || bytes+size>MaximumBytes) { Dispose(); return; }
                writer.WriteLine(line); bytes+=size; samples++;
                if(samples%40==0) writer.Flush();
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) { Stop(e); }
        }
        void Stop(Exception error) { Debug.LogWarning("Motion trace stopped: "+error.Message); Dispose(); }
        public void Dispose()
        {
            stopped=true;
            try { writer?.Dispose(); } catch(IOException) {} writer=null;
        }
    }
}
