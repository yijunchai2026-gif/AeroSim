using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;

// Instruments an explicit QA COPY, never the delivered application.
public static class Instrument
{
    public static void Main(string[] args)
    {
        var target=Path.GetFullPath(args[0]);var qa=Path.GetFullPath(args[1]);
        if(!target.Contains("AeroSim-UIQA.app"))throw new Exception("Refusing to instrument non-QA application.");
        var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(Path.GetDirectoryName(target));
        var parameters=new ReaderParameters{InMemory=true,AssemblyResolver=resolver};
        using(var assembly=AssemblyDefinition.ReadAssembly(target,parameters))
        using(var plugin=AssemblyDefinition.ReadAssembly(qa,parameters))
        {
            if(assembly.MainModule.GetType("AeroSim.Domain.AircraftCatalog")==null)
                throw new Exception("This is not the delivered six-aircraft build.");
            var bootstrap=assembly.MainModule.GetType("AeroSim.AppBootstrap").Methods.Single(m=>m.Name=="CreateApplication");
            var entry=plugin.MainModule.GetType("AeroSim.Qa.QaBootstrap").Methods.Single(m=>m.Name=="Initialize");
            var imported=assembly.MainModule.ImportReference(entry);var il=bootstrap.Body.GetILProcessor();
            foreach(var ret in bootstrap.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())
                il.InsertBefore(ret,il.Create(OpCodes.Call,imported));
            assembly.Write(target+".instrumented");
        }
        File.Copy(target+".instrumented",target,true);
        // Generated metadata in the QA build copy, not product source/configuration.
        var metadata=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(target)),"ScriptingAssemblies.json");
        var json=File.ReadAllText(metadata);
        json=Regex.Replace(json,"\"names\":\\[(.*?)\\]","\"names\":[$1,\"LayoutQa.dll\"]");
        json=Regex.Replace(json,"\"types\":\\[(.*?)\\]","\"types\":[$1,16]");
        File.WriteAllText(metadata,json);
        Console.WriteLine("QA copy instrumented; original build is untouched.");
    }
}
