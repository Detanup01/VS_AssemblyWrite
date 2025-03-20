using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace VS_AssemblyWrite;

internal class VLib
{
    public static void TryBypass()
    {
        var dllName = "VintagestoryLib.dll";
        if (string.IsNullOrEmpty(dllName))
        {
            Console.WriteLine("Cannot find VintagestoryLib.dll!");
            return;
        }

        if (File.Exists(dllName + ".old"))
            File.Delete(dllName + ".old");
        File.Move(dllName, dllName + ".old");
        Bypass(dllName);
    }

    static MethodReference concat_Reference;
    static MethodReference ReadAllLines_Ref;

    //vintagestory.at
    //System.IO.File.ReadAllLines("urls.txt")[0]
    public static void Bypass(string path)
    {
        var def = new DefaultAssemblyResolver();
        def.AddSearchDirectory(path);
        def.AddSearchDirectory("G:\\Games\\Vintagestory");
        def.AddSearchDirectory("G:\\Games\\Vintagestory\\Lib");
        var reader = new ReaderParameters()
        { 
            AssemblyResolver = def
        };
        var asm = AssemblyDefinition.ReadAssembly(path + ".old", reader);

        concat_Reference = asm.MainModule.ImportReference(typeof(string).GetMethod("Concat", [typeof(string), typeof(string), typeof(string)]));
        ReadAllLines_Ref = asm.MainModule.ImportReference(typeof(File).GetMethod("ReadAllLines", [typeof(string)]));
        var types = asm.MainModule.Types.ToList();
        var ClientSettings = types.FirstOrDefault(x => x.Name == "ClientSettings");
        var SessionManager = types.FirstOrDefault(x=>x.Name == "SessionManager" && x.Methods.Any(y=>y.Name == "IsCachedSessionKeyValid"));
        SessionManagerBypass(ref SessionManager, ref ClientSettings);
        URLStuff(ref types);
        for (int i = 0; i < types.Count; i++)
        {
            var typeDefinition = types[i];
            var methods = typeDefinition.GetMethods().ToList();
            for (int j = 0; j < methods.Count; j++)
            {
                var methodDefinition = methods.ElementAt(j);
                LDFTN_Stupidity(ref typeDefinition, ref methodDefinition, 0);
            }
        }
        asm.Write(path);
    }


    public static void ReplaceURL(ref TypeDefinition? type, ref MethodDefinition method)
    {
        Console.WriteLine(method.Name);
        var instructions = method.Body.Instructions.Where(x => x.OpCode == OpCodes.Ldstr && ((string)x.Operand).Contains("vintagestory.at")).ToList();
        if (!instructions.Any())
            return;
        for (int i = 0; i < instructions.Count; i++)
        {
            var inst = instructions.ElementAt(i);

            if (inst == null)
                continue;

            // we skip skins
            if (((string)inst.Operand).Contains("skins"))
                return;

            // we skip wiki
            if (((string)inst.Operand).Contains("wiki"))
                return;

            var proc = method.Body.GetILProcessor();
            int index = method.Body.Instructions.IndexOf(inst);
            Console.WriteLine($"{index} , inst: {inst}");
            string url_end = ((string)inst.Operand).Replace("vintagestory.at", "|").Split("|")[1];
            inst.Operand = "http://";
            proc.InsertAfter(index, Instruction.Create(OpCodes.Ldstr, "urls.txt"));
            index++;
            proc.InsertAfter(index, Instruction.Create(OpCodes.Call, ReadAllLines_Ref));
            index++;
            proc.InsertAfter(index, Instruction.Create(OpCodes.Ldc_I4_0));
            index++;
            proc.InsertAfter(index, Instruction.Create(OpCodes.Ldelem_Ref));
            index++;
            proc.InsertAfter(index, Instruction.Create(OpCodes.Ldstr, url_end));
            index++;
            proc.InsertAfter(index, Instruction.Create(OpCodes.Call, concat_Reference));
            index++;
        }
    }

    public static void URLStuff(ref List<TypeDefinition> types)
    {
        for (int i = 0; i < types.Count; i++)
        {
            var typeDefinition = types[i];
            var methods = typeDefinition.GetMethods().Where(x => x.HasBody && x.Body.Instructions.Any(x => x.OpCode == OpCodes.Ldstr && ((string)x.Operand).Contains("vintagestory.at"))).ToList();
            if (!methods.Any())
                continue;
            Console.WriteLine(methods.Count);
            for (int j = 0; j < methods.Count; j++)
            {
                var methodDefinition = methods.ElementAt(j);
                Console.WriteLine(methodDefinition.Name);
                ReplaceURL(ref typeDefinition, ref methodDefinition);
            }
        }
    }

    public static void LDFTN_Stupidity(ref TypeDefinition? type, ref MethodDefinition method, int deep = 0)
    {
        if (!method.HasBody)
            return;
        if (deep > 3)
            return;
        var shit_fuck = method.Body.Instructions.Where(x=>x.OpCode == OpCodes.Ldftn).ToList();
        if (shit_fuck.Count == 0)
            return;
        deep++;
        for (int i = 0; i < shit_fuck.Count; i++)
        {
            var inst = shit_fuck[i];
            var op = inst.Operand;

            if (op.GetType().Name == "MethodDefinition")
            {
                var def = (MethodDefinition)op;
                ReplaceURL(ref type, ref def);
                Console.WriteLine(type.FullName + "\n" + def.FullName + " deep bef: " + deep);
                
                LDFTN_Stupidity(ref type, ref def, deep);
            }
        }
        
    }



    /*
    public bool IsCachedSessionKeyValid()
    {
        return ClientSettings.UserEmail != "" && ClientSettings.Sessionkey != "" && ClientSettings.SessionSignature != "" && ClientSettings.PlayerName != "" && ClientSettings.PlayerUID != "";
    }
    */
    public static void SessionManagerBypass(ref TypeDefinition? SessionManager, ref TypeDefinition? ClientSettings)
    {
        if (SessionManager == null)
            return;
        if (ClientSettings == null)
            return;
        Console.WriteLine("All found!");
        var IsCachedSessionKeyValid_Method = SessionManager.Methods.FirstOrDefault(x=>x.Name == "IsCachedSessionKeyValid");
        if (IsCachedSessionKeyValid_Method == null)
            return;
        if (!IsCachedSessionKeyValid_Method.HasBody)
            return;
        var il = IsCachedSessionKeyValid_Method.Body.GetILProcessor();
        if (il == null)
            return;
        var get_userEmail = ClientSettings.Methods.First(x => x.Name == "get_UserEmail");
        var op_Inequality = new MethodReference("op_Inequality", IsCachedSessionKeyValid_Method.ReturnType, get_userEmail.ReturnType)
        { 
            Parameters = { new ParameterDefinition(get_userEmail.ReturnType), new ParameterDefinition(get_userEmail.ReturnType) },
            HasThis = false
        };
        il.Clear();
        var ld20 = il.Create(OpCodes.Ldc_I4_0);
        il.Append(il.Create(OpCodes.Call, ClientSettings.Methods.First(x=>x.Name == "get_UserEmail")));
        il.Append(il.Create(OpCodes.Ldstr, ""));
        il.Append(il.Create(OpCodes.Call, op_Inequality));
        il.Append(il.Create(OpCodes.Brfalse_S, ld20));
        il.Append(il.Create(OpCodes.Call, ClientSettings.Methods.First(x => x.Name == "get_Sessionkey")));
        il.Append(il.Create(OpCodes.Ldstr, ""));
        il.Append(il.Create(OpCodes.Call, op_Inequality));
        il.Append(il.Create(OpCodes.Brfalse_S, ld20));
        il.Append(il.Create(OpCodes.Call, ClientSettings.Methods.First(x => x.Name == "get_SessionSignature")));
        il.Append(il.Create(OpCodes.Ldstr, ""));
        il.Append(il.Create(OpCodes.Call, op_Inequality));
        il.Append(il.Create(OpCodes.Brfalse_S, ld20));
        il.Append(il.Create(OpCodes.Call, ClientSettings.Methods.First(x => x.Name == "get_PlayerName")));
        il.Append(il.Create(OpCodes.Ldstr, ""));
        il.Append(il.Create(OpCodes.Call, op_Inequality));
        il.Append(il.Create(OpCodes.Brfalse_S, ld20));
        il.Append(il.Create(OpCodes.Call, ClientSettings.Methods.First(x => x.Name == "get_PlayerUID")));
        il.Append(il.Create(OpCodes.Ldstr, ""));
        il.Append(il.Create(OpCodes.Call, op_Inequality));
        il.Append(il.Create(OpCodes.Ret));
        il.Append(ld20);
        il.Append(il.Create(OpCodes.Ret));
    }
}
