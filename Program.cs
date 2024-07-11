using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CommandLine;
using CommandLine.Text;

namespace LineCount
{
    class Program
    {
        enum CountLineStat
        {
            CLS_NONE,
            CLS_COMMENT,
            CLS_MACRO,
            CLS_NEST_MACRO,
        };

        public class Options
        {
            [Value(0, HelpText = "answer if module A is referenced by module B(refModules), and where are the reference points")]
            public string filePath { get; set; } = ".";

            [Option('s', "skipMacro", Required = false, HelpText ="skip code surrounded by macros like #if XXXX ... #endif")]
            public string skipMacroString { get; set; } = "";

            [Option('e', "exclusive", Required = false, HelpText ="exclusive dirs")]
            public string exclusiveDirString { get; set; } = "";

            [Option('m', "modules", Required = true, HelpText ="modules A dirs")]
            public string moduleDirString { get; set; } = "";            

            [Option('r', "ref-modules", Required = true, HelpText ="ref modules B dirs")]
            public string refModuleDirString { get; set; } = "";              
            
            //[Option('r', "recursive", Required = false, HelpText = "recursive counting in dir")]
            //public bool bRecursive { get; set; } = true;

            [Option('v', "verbos", Required = false, HelpText = "showing detail result")]
            public bool bVerbos { get; set; } = false;

            [Option('d', "debug", Required = false, HelpText = "debug tool")]
            public bool bDebug { get; set; } = false;

        }
        static void DBG(string format, int lineNum, string line)
        { 
            if(bVerbos && bDebug)
                Console.WriteLine(format, lineNum, line);
        }

        static bool bVerbos = false;
        static bool bByFile = false;
        static bool bDebug = false;

        static string[] fileExts = { ".cpp", ".h", ".c", ".hpp", ".inl"};
        static string[] exclusiveDirs = { };
        static string[] moduleDirs = { };
        static string[] refModuleDirs = { };

        static HashSet<string> skipMacros = new HashSet<string>();

        static Dictionary<string, string> RefHeaderDict = new Dictionary<string, string>(); //文件名和文件路径的映射
        static Dictionary<string, string> RefMap = new Dictionary<string, string>(); //引用关系key引用value，都是路径。

        //统计一个头文件都被哪些源文件引用了。 
        static Dictionary<string, List<string>> headersRefDict = new Dictionary<string, List<string>>();

        static void FindRefInFile(string filePath, string headerName)
        {
            int lineNum = 0;
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    Stack<CountLineStat> countStatStack = new Stack<CountLineStat>();
                    Stack<string> macroNameStack = new Stack<string>();
                    macroNameStack.Push("");
                    string curMacroName = macroNameStack.Peek();
                    countStatStack.Push(CountLineStat.CLS_NONE);
                    CountLineStat curStat = countStatStack.Peek();

                    List<int> macroCountStack = new List<int>();
                    macroCountStack.Add(0);

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;

                        line = line.TrimStart();
                        if (line.Length == 0)
                        {
                            DBG("{0:0000} BL: {1}", lineNum, line);

                            continue;
                        }
                        if (line.StartsWith("//"))
                        {
                            DBG("{0:0000} CM: {1}", lineNum, line);
                        
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_COMMENT)
                        {
                            if (line.Contains("*/"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} CM: {1}", lineNum, line);
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                        {
                            bool bStepIn = line.StartsWith("#if");

                            if (line.StartsWith("#endif"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();

                                int innerMC = macroCountStack[0];
                                macroCountStack.RemoveAt(0);
                            }
                            
                            if (!bStepIn)
                            {
                                macroCountStack[0] += 1;
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                        }

                        if (line.StartsWith("/*"))
                        {
                            line = line.TrimEnd();
                            if (line.EndsWith("*/"))
                            {
                                DBG("{0:0000} CM: {1}", lineNum, line);
                                continue;
                            }
                            else if (line.Contains("*/"))
                            {
                                DBG("{0:0000} C-: {1}", lineNum, line);
                            }
                            else
                            {
                                countStatStack.Push(CountLineStat.CLS_COMMENT);
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} C-: {1}", lineNum, line);
                            continue;
                        }
                        if (line.StartsWith("#if"))
                        {
                            macroCountStack.Insert(0, 1);
                            if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                            {
                                countStatStack.Push(CountLineStat.CLS_NEST_MACRO);
                                curStat = countStatStack.Peek();
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                            else
                            {
                                bool isskipMacro = false;

                                foreach(string macro in skipMacros)
                                {
                                    if (macro.Length == 0)
                                        break;
                                    if (line.Contains(macro))
                                    {
                                        countStatStack.Push(CountLineStat.CLS_MACRO);
                                        curStat = countStatStack.Peek();
                                        DBG("{0:0000} MC: {1}", lineNum, line);
                                        isskipMacro = true; 
                                        break;
                                    }
                                }
                                if (isskipMacro)
                                    continue;
                            }
                        }
                        if (line.StartsWith("#elif") || line.StartsWith("#else"))
                        {
                            //do not handle it yet.
                        }

                        if (line.StartsWith("#include"))
                        {
                            if (!line.Contains(".generated.h"))
                            {
                                string[] arrstr = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (arrstr.Length > 1)
                                {
                                    string[] includeFileStr = arrstr[1].Split(new char[] { '/', '<', '>', '\"'}, StringSplitOptions.RemoveEmptyEntries);
                                    string includeFileName = includeFileStr[includeFileStr.Length - 1];

                                    //includeFileName = includeFileName.Substring(1, includeFileName.Length - 2); //trim <> or "" in filename
                                    if (!headersRefDict.ContainsKey(includeFileName))
                                    {
                                        headersRefDict.Add(includeFileName, new List<string>());
                                    }
                                    headersRefDict[includeFileName].Add(Path.GetFileName(filePath));
                                    DBG("{0:0000} IN: {1}", lineNum, line);
                                    continue;
                                }
                            }
                        }
                        DBG("{0:0000} CO: {1}", lineNum, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error：{ex.Message}");
            }
        }

        //1. 递归查找fileDir下所有.h结尾的文件，保存到RefHeaderDict中。
        static void FindAllHeaderInDirectory(string fileDir, bool bRecursive)
        {
            foreach (string exclusiveDir in exclusiveDirs)
            {
                if (exclusiveDir.Length == 0)
                    continue;

                string lowerDir = fileDir.ToLower();
                if (lowerDir.EndsWith(exclusiveDir))
                {
                    return;
                }
            }

            if (Directory.Exists(fileDir))
            {
                string[] filePaths = Directory.GetFiles(fileDir);
                foreach (string filePath in filePaths)
                {
                    string ext = Path.GetExtension(filePath);
                    if (ext is null)
                        continue;
                    ext = ext.ToLower();

                    string fileName = Path.GetFileName(filePath);

                    if (ext == ".h")
                    {
                        if (!RefHeaderDict.ContainsKey(fileName))
                        {
                            RefHeaderDict.Add(fileName, filePath);
                        }
                        else
                        {
                            Console.WriteLine("Error --- file exist in RefHeaderDict:", RefHeaderDict[fileName]);
                        }
                    }
                }

                if (bRecursive)
                {
                    // Recurse sub directories
                    string[] folders = Directory.GetDirectories(fileDir);
                    foreach (string folder in folders)
                    {
                        FindAllHeaderInDirectory(folder, bRecursive);
                    }
                }
            }
        }

        //2. 遍历模块B，对每个文件调用FindRefInFile()
        static void FindRefInDirectory(string fileDir, bool bRecursive)
        {
            if (Directory.Exists(fileDir))
            {
                string[] filePaths = Directory.GetFiles(fileDir);
                foreach (string filePath in filePaths)
                {
                    string ext = Path.GetExtension(filePath);
                    if (ext is null)
                        continue;
                    ext = ext.ToLower();

                    string fileName = Path.GetFileName(filePath);

                    if (ext == ".h" || ext == ".cpp" || ext == ".inl")
                    {
                        FindRefInFile(filePath);
                    }
                }

                if (bRecursive)
                {
                    // Recurse sub directories
                    string[] folders = Directory.GetDirectories(fileDir);
                    foreach (string folder in folders)
                    {
                        FindRefInDirectory(folder, bRecursive);
                    }
                }
            }
        }

        //3. 查看文件中每个Include是否包含特定的文件，并标记出来：
        static void FindRefInFile(string filePath)
        {
            int lineNum = 0;
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    Stack<CountLineStat> countStatStack = new Stack<CountLineStat>();
                    Stack<string> macroNameStack = new Stack<string>();
                    macroNameStack.Push("");
                    string curMacroName = macroNameStack.Peek();
                    countStatStack.Push(CountLineStat.CLS_NONE);
                    CountLineStat curStat = countStatStack.Peek();

                    List<int> macroCountStack = new List<int>();
                    macroCountStack.Add(0);

                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNum++;

                        line = line.TrimStart();
                        if (line.Length == 0)
                        {
                            DBG("{0:0000} BL: {1}", lineNum, line);

                            continue;
                        }
                        if (line.StartsWith("//"))
                        {
                            DBG("{0:0000} CM: {1}", lineNum, line);

                            continue;
                        }

                        if (curStat == CountLineStat.CLS_COMMENT)
                        {
                            if (line.Contains("*/"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} CM: {1}", lineNum, line);
                            continue;
                        }

                        if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                        {
                            bool bStepIn = line.StartsWith("#if");

                            if (line.StartsWith("#endif"))
                            {
                                countStatStack.Pop();
                                curStat = countStatStack.Peek();

                                int innerMC = macroCountStack[0];
                                macroCountStack.RemoveAt(0);
                            }

                            if (!bStepIn)
                            {
                                macroCountStack[0] += 1;
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                        }

                        if (line.StartsWith("/*"))
                        {
                            line = line.TrimEnd();
                            if (line.EndsWith("*/"))
                            {
                                DBG("{0:0000} CM: {1}", lineNum, line);
                                continue;
                            }
                            else if (line.Contains("*/"))
                            {
                                DBG("{0:0000} C-: {1}", lineNum, line);
                            }
                            else
                            {
                                countStatStack.Push(CountLineStat.CLS_COMMENT);
                                curStat = countStatStack.Peek();
                            }
                            DBG("{0:0000} C-: {1}", lineNum, line);
                            continue;
                        }
                        if (line.StartsWith("#if"))
                        {
                            macroCountStack.Insert(0, 1);
                            if (curStat == CountLineStat.CLS_MACRO || curStat == CountLineStat.CLS_NEST_MACRO)
                            {
                                countStatStack.Push(CountLineStat.CLS_NEST_MACRO);
                                curStat = countStatStack.Peek();
                                DBG("{0:0000} MC: {1}", lineNum, line);
                                continue;
                            }
                            else
                            {
                                bool isskipMacro = false;

                                foreach (string macro in skipMacros)
                                {
                                    if (macro.Length == 0)
                                        break;
                                    if (line.Contains(macro))
                                    {
                                        countStatStack.Push(CountLineStat.CLS_MACRO);
                                        curStat = countStatStack.Peek();
                                        DBG("{0:0000} MC: {1}", lineNum, line);
                                        isskipMacro = true;
                                        break;
                                    }
                                }
                                if (isskipMacro)
                                    continue;
                            }
                        }
                        if (line.StartsWith("#elif") || line.StartsWith("#else"))
                        {
                            //do not handle it yet.
                        }

                        if (line.StartsWith("#include"))
                        {
                            if (!line.Contains(".generated.h"))
                            {
                                string[] arrstr = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (arrstr.Length > 1)
                                {
                                    string[] includeFileStr = arrstr[1].Split(new char[] { '/', '<', '>', '\"' }, StringSplitOptions.RemoveEmptyEntries);
                                    string includeFileName = includeFileStr[includeFileStr.Length - 1];

                                    //includeFileName = includeFileName.Substring(1, includeFileName.Length - 2); //trim <> or "" in filename
                                    if (RefHeaderDict.ContainsKey(includeFileName))
                                    {
                                        RefMap.Add(filePath + ":" + lineNum.ToString(), RefHeaderDict[includeFileName]);
                                    }
                                    DBG("{0:0000} IN: {1}", lineNum, line);
                                    continue;
                                }
                            }
                        }
                        DBG("{0:0000} CO: {1}", lineNum, line);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error：{ex.Message}");
            }
        }


        //static void FindHeaderInDirectory(string fileDir, bool bRecursive)
        //{
        //    foreach (string exclusiveDir in exclusiveDirs)
        //    {
        //        if (exclusiveDir.Length == 0)
        //            continue;

        //        string lowerDir = fileDir.ToLower();
        //        if (lowerDir.EndsWith(exclusiveDir))
        //        {
        //            return;
        //        }
        //    }
        //    if (Directory.Exists(fileDir))
        //    {
        //        string[] filePaths = Directory.GetFiles(fileDir);
        //        foreach (string filePath in filePaths)
        //        {
        //            string ext = Path.GetExtension(filePath);
        //            if (ext is null)
        //                continue;
        //            ext = ext.ToLower();

        //            string fileName = Path.GetFileName(filePath);

        //            if (ext == ".h")
        //            {
        //                if (!headersRefDict.ContainsKey(fileName))
        //                {
        //                    headersRefDict.Add(fileName, new List<string>());
        //                }
        //            }
        //            foreach (string fileExt in fileExts)
        //            {
        //                if (ext == fileExt)
        //                {
        //                    Program.FindRefInFile(filePath, fileName);
        //                    break;
        //                }
        //            }
        //        }

        //        if (bRecursive)
        //        {
        //            // Recurse sub directories
        //            string[] folders = Directory.GetDirectories(fileDir);
        //            foreach (string folder in folders)
        //            {
        //                FindHeaderInDirectory(folder, bRecursive);
        //            }
        //        }
        //    }
        //}
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(Run)
                .WithNotParsed(HandleParseError);
            //if (bDebug)
            //{
            //    Console.ReadLine();
            //}
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            
        }
        static void Run(Options option)
        { 
            
            //string[] skipMacro = { "WITH_EDITOR", "0", "LOGTRACE_ENABLED", "WITH_EDITOR_ONLY_DATA", "UE_TRACE_ENABLED", "!UE_BUILD_SHIPPING", "VULKAN_HAS_DEBUGGING_ENABLED" };
            //string[] skipMacro = {"VULKAN_HAS_DEBUGGING_ENABLED" };
            //string[] skipMacro = { "0"};

            exclusiveDirs = option.exclusiveDirString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < exclusiveDirs.Length; i++)
            {
                exclusiveDirs[i] = exclusiveDirs[i].ToLower();
            }

            moduleDirs = option.moduleDirString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            refModuleDirs = option.refModuleDirString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            //1. 找到模块A中所有的.h文件
            for (int i = 0; i < moduleDirs.Length; i++)
            {
                FindAllHeaderInDirectory(moduleDirs[i], true);
            }

            //2. 到模块B下，查看每个文件是否包含模块A中的任何一个.h文件
            for (int i = 0; i < refModuleDirs.Length; i++)
            {
                FindRefInDirectory(refModuleDirs[i], true);
            }

            var sortedDict = from objDic in RefMap orderby objDic.Key descending select objDic;
            foreach (KeyValuePair<string, string> kv in sortedDict)
            {
                Console.WriteLine("{0} --#include-- {1}", kv.Key, kv.Value);
            }
            //Console.WriteLine("-------------finished------------");
            //string[] arrSkipMacros = option.skipMacroString.Split(new char [] {','}, StringSplitOptions.RemoveEmptyEntries);
            //foreach (string item in arrSkipMacros)
            //    skipMacros.Add(item);

            //string []fileDirInfos = option.filePath.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            //bool bRecursive = option.bRecursive;
            //bVerbos = option.bVerbos;
            //bDebug = option.bDebug;

            //foreach (string fileDirInfo in fileDirInfos)
            //{
            //    string fileDir = Path.GetFullPath(fileDirInfo);
            //    string shortName = fileDir;
            //    if (Directory.Exists(fileDir))
            //    {
            //        shortName = Path.GetFileName(fileDir);
            //        Program.FindHeaderInDirectory(fileDir, bRecursive);
            //    }
            //}

            //var sortedDict = from objDic in headersRefDict orderby objDic.Value.Count descending select objDic;
            //foreach (KeyValuePair<string, List<string>> kv in sortedDict)
            //{
            //    if (kv.Value.Count != 1)
            //        continue;
            //    string[] buf1 = kv.Key.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //    if (buf1.Length == 2 && buf1[1] == "inl")
            //        continue;

            //    string[] buf2 = kv.Value[0].Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            //    if (buf1[0] != buf2[0])
            //        continue;

            //    string refs = "";
            //    foreach (string item in kv.Value)
            //    {
            //        refs += item+",";
            //    }
            //    Console.WriteLine("{0}:{1}:{2}", kv.Key, kv.Value.Count, refs);
            //}

        }
    }
}
