using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LoGiC.NET.Utils;

namespace LoGiC.NET.Protections
{
    public class AntiTamper : Protection
    {
        public AntiTamper()
        {
            Name = "Anti-Tamper";
        }

        // Thanks to the EOF Anti-Tamper project by Xenocode on GitHub!

        public static bool Tampered { get; set; }

        public static void Inject(string filePath)
        {
            using (MD5 hash = MD5.Create())
            {
                //We get the md5 as byte, of the target
                byte[] bytes = hash.ComputeHash(File.ReadAllBytes(filePath));
                //Append md5 in the end
                using (FileStream fs = new FileStream(filePath, FileMode.Append))
                    fs.Write(bytes, 0, bytes.Length);
            }
        }

        public override void Execute()
        {
            //We declare our Module, here we want to load the TamperClass class
            ModuleDefMD typeModule = ModuleDefMD.Load(typeof(TamperClass).Module);
            //We declare TamperClass as a TypeDef using it's Metadata token (needed)
            TypeDef typeDef = typeModule.ResolveTypeDef(MDToken.ToRID(typeof(TamperClass).MetadataToken));
            //We use confuserEX InjectHelper class to inject TamperClass class into our target, under <Module>
            IEnumerable<IDnlibDef> members = InjectHelper.Inject(typeDef, Program.Module.GlobalType, Program.Module);
            MethodDef init = (MethodDef)members.Single(method => method.Name == "NoTampering");
            init.GetRenamed();
            //We find or create the .cctor method in <Module>, aka GlobalType, if it doesn't exist yet
            Program.Module.GlobalType.FindOrCreateStaticConstructor().Body.Instructions.Insert(0,
                Instruction.Create(OpCodes.Call, init));

            foreach (MethodDef method in Program.Module.GlobalType.Methods)
                if (method.Name.Equals(".ctor"))
                {
                    Program.Module.GlobalType.Remove(method);
                    break;
                }

            Tampered = true;
        }
    }
}
