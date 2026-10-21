using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

// BepInEx 5 preloader contract. This runs before the game assembly is loaded, so the
// game's first save lookup is redirected before it can inspect the Windows profile.
public static class SavePathPatcher
{
    public static IEnumerable<string> TargetDLLs
    {
        get { return new string[] { "Assembly-CSharp.dll" }; }
    }

    public static void Patch(AssemblyDefinition assembly)
    {
        TypeDefinition continuity = null;
        foreach (TypeDefinition type in assembly.MainModule.Types)
            if (type.FullName == "Continuity") { continuity = type; break; }
        if (continuity == null) throw new InvalidOperationException("Continuity type not found.");

        MethodDefinition method = null;
        foreach (MethodDefinition candidate in continuity.Methods)
            if (candidate.Name == "GetSaveDataPath" && candidate.Parameters.Count == 1 && candidate.Parameters[0].ParameterType.MetadataType == MetadataType.String)
            { method = candidate; break; }
        if (method == null) throw new InvalidOperationException("Continuity.GetSaveDataPath(string) not found.");

        ModuleDefinition module = assembly.MainModule;
        MethodInfo getEnvironmentVariable = typeof(Environment).GetMethod("GetEnvironmentVariable", new Type[] { typeof(string) });
        MethodInfo isNullOrEmpty = typeof(string).GetMethod("IsNullOrEmpty", new Type[] { typeof(string) });
        MethodInfo getCurrentDomain = typeof(AppDomain).GetProperty("CurrentDomain").GetGetMethod();
        MethodInfo getBaseDirectory = typeof(AppDomain).GetProperty("BaseDirectory").GetGetMethod();
        MethodInfo combine = typeof(Path).GetMethod("Combine", new Type[] { typeof(string), typeof(string) });
        MethodInfo createDirectory = typeof(Directory).GetMethod("CreateDirectory", new Type[] { typeof(string) });

        VariableDefinition root = new VariableDefinition(module.TypeSystem.String);
        method.Body.Variables.Clear();
        method.Body.Variables.Add(root);
        method.Body.ExceptionHandlers.Clear();
        method.Body.Instructions.Clear();
        method.Body.InitLocals = true;
        method.Body.MaxStackSize = 2;

        ILProcessor il = method.Body.GetILProcessor();
        Instruction useRoot = Instruction.Create(OpCodes.Nop);
        il.Append(Instruction.Create(OpCodes.Ldstr, "NITW_COOP_SAVE_ROOT"));
        il.Append(Instruction.Create(OpCodes.Call, module.ImportReference(getEnvironmentVariable)));
        il.Append(Instruction.Create(OpCodes.Stloc, root));
        il.Append(Instruction.Create(OpCodes.Ldloc, root));
        il.Append(Instruction.Create(OpCodes.Call, module.ImportReference(isNullOrEmpty)));
        il.Append(Instruction.Create(OpCodes.Brfalse, useRoot));

        il.Append(Instruction.Create(OpCodes.Call, module.ImportReference(getCurrentDomain)));
        il.Append(Instruction.Create(OpCodes.Callvirt, module.ImportReference(getBaseDirectory)));
        il.Append(Instruction.Create(OpCodes.Ldstr, "BepInEx\\config\\NightInTheWoodsCoop\\Saves"));
        il.Append(Instruction.Create(OpCodes.Call, module.ImportReference(combine)));
        il.Append(Instruction.Create(OpCodes.Stloc, root));

        il.Append(useRoot);
        il.Append(Instruction.Create(OpCodes.Ldloc, root));
        il.Append(Instruction.Create(OpCodes.Call, module.ImportReference(createDirectory)));
        il.Append(Instruction.Create(OpCodes.Pop));
        il.Append(Instruction.Create(OpCodes.Ldloc, root));
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Call, module.ImportReference(combine)));
        il.Append(Instruction.Create(OpCodes.Ret));
    }
}

