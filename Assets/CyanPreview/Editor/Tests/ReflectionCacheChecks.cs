using System;
using UnityEngine;
using static CyanPreview.ReferenceContext;
namespace CyanPreview.Editor
{
    public static class ReflectionCacheChecks
    {
        class Parent { private int inherited=7; public int Value {get;set;} }
        class Sample:Parent
        {
            public static int Shared=2;
            public string Pick(int x)=>"int";
            public string Pick(string x)=>"string";
            public string Pick()=>"empty";
            public void Fail() { throw new ArgumentException("fixture"); }
        }
        static void Check(bool value,string why) { if(!value) throw new Exception(why); }
        public static void Run()
        {
            var a=new Sample(); var b=new Sample();
            for(int i=0;i<3;i++)
            {
                Set(a,"Value",i); Check((int)Get(a,"Value")==i && (int)Get(b,"Value")==0,"Metadata cache retained instance state");
                Check((int)Get(a,"inherited")==7,"Inherited private lookup changed");
                Set(typeof(Sample),"Shared",i); Check((int)Get(typeof(Sample),"Shared")==i,"Static lookup changed");
                Check((string)Call(a,"Pick",1)=="int" && (string)Call(a,"Pick","a")=="string" && (string)Call(a,"Pick")=="empty","Overload cache reused incompatible arguments");
                bool failed=false; try { Call(a,"Fail"); } catch(InvalidOperationException e) { failed=e.InnerException is ArgumentException; }
                Check(failed,"Invocation failure lost original exception");
                failed=false; try { Get(a,"Missing"); } catch(MissingMemberException) { failed=true; } Check(failed,"Missing member stopped failing");
            }
            Debug.Log("REFLECTION_CACHE_INHERITANCE_INSTANCE_OVERLOAD_EXCEPTION_CHECK_PASSED");
        }
    }
}
