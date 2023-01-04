using System;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using LoGiC.NET.Protections;
using SharpConfigParser;

namespace LoGiC.NET
{
    class Program
    {
        public static ModuleDefMD Module { get; set; }

        public static string FileExtension { get; set; }

        public static bool DontRename { get; set; }

        public static bool ForceWinForms { get; set; }

        public static string FilePath { get; set; }

        public static MemoryStream Stream = new MemoryStream();

        static void Main(string[] args)
        {
            //Console.WriteLine("- Drag & drop your file:");
            //string path = Console.ReadLine().Replace("\"", string.Empty);
            
            if (args.Length == 0 || args.Contains("?"))
            {             
                string helpMsg = "Usage: LoGiC.NET [.NET binary] optionA optionB optionN\n" +
                "?                 - Show this message\n" +
                "Renamer            - rename types, methods and their parameters, properties, fields and events to random strings.\n" +
                "AntiTamper         - take the MD5 of the target, put it as a byte, and write it at the EndOfFile.Then a method is injected in the GlobalType, also knwon as < 'Module' >.\n" +
                "JunkDefs           - add random junk defs to make the code harder to decrypt.\n" +
                "StringEncryption   - string encryption.\n" +
                "AntiDe4dot         - prevents usage of De4Dot (an open source .NET deobfuscator and unpacker).\n" +
                "ControlFlow        - rearranges the instructions in a method to make the flow of control more difficult to follow.\n" +
                "IntEncoding        - encodes the integers within different methods.\n" +
                "ProxyAdder         - The intensity of the proxy calls. The more the intensity is, the more proxy calls will be added.\n" +
                "InvalidMetadata    - add addtional jusnk methods and nasted types.\n";
                Console.WriteLine(helpMsg);
                return;                
            }
            string path = args[0];
            if (!File.Exists(path))
            {
                Console.WriteLine("Unable to locate file in: " + path);
                return;
            }

            path = Path.GetFullPath(path);
            Console.WriteLine("- Preparing obfuscation...");
            if (!File.Exists("config.txt"))
            {
                Console.WriteLine("Config file not found, continuing without it.");
                goto obfuscation;
            }

            Parser p = new Parser() { ConfigFile = "config.txt" };
            try { ForceWinForms = bool.Parse(p.Read("ForceWinFormsCompatibility").ReadResponse().ReplaceSpaces()); } catch { }
            try { DontRename = bool.Parse(p.Read("DontRename").ReadResponse().ReplaceSpaces()); } catch { }
            try { ProxyAdder.Intensity = int.Parse(p.Read("ProxyCallsIntensity").ReadResponse().ReplaceSpaces()); } catch { }

            Console.WriteLine("\n- ForceWinForms: " + ForceWinForms);
            Console.WriteLine("- DontRename: " + DontRename);
            Console.WriteLine("- ProxyCallsIntensity: " + ProxyAdder.Intensity + "\n");

            obfuscation:
            Module = ModuleDefMD.Load(path);
            FileExtension = Path.GetExtension(path);

            Protection[] protections = new Protection[]
            {
                new Renamer(),
                new AntiTamper(),
                new JunkDefs(),
                new StringEncryption(),
                new AntiDe4dot(),
                new ControlFlow(),
                new IntEncoding(),
                new ProxyAdder(),
                new InvalidMetadata()
            };

            foreach (Protection protection in protections)
            {
                protection.Name = protection.Name.Replace("-", "").ReplaceSpaces();
                if (args.Contains(protection.Name, StringComparer.OrdinalIgnoreCase)) 
                {
                    Console.WriteLine("- Executing protection: " + protection.Name);
                    protection.Execute();
                }
                else
                {
                    Console.WriteLine("- Skipping protection: " + protection.Name);
                }
            }

            Console.WriteLine("- Skipping Watermarking...");
            //Watermark.AddAttribute();

            
            FilePath = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + "protected_" + Path.GetFileName(path);
            Console.WriteLine("- Saving file: " + FilePath);
            Module.Write(Stream, new ModuleWriterOptions(Module) { Logger = DummyLogger.NoThrowInstance });

            Console.WriteLine("- Stripping DOS header...");
            StripDOSHeader.Execute();

            // Save stream to file
            File.WriteAllBytes(FilePath, Stream.ToArray());

            if (AntiTamper.Tampered)
                AntiTamper.Inject(FilePath);

            //Console.WriteLine("- Done! Press any key to exit...");
            //Console.ReadKey();
        }
    }
}
