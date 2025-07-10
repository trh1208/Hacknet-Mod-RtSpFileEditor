using BepInEx;
using HarmonyLib;
using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Action;
using Pathfinder.Administrator;
using Pathfinder.Util;
using Pathfinder.Daemon;
using Pathfinder.GUI;
using Pathfinder.Mission;
using Pathfinder.Util.XML;
using System;
using System.IO;
using System.Collections.Generic;
using Pathfinder;

using BepInEx.Hacknet;

namespace HacknetPluginTemplate
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class HacknetPluginTemplate : HacknetPlugin
    {
        public const string ModGUID = "com.RtSpFileEditor.test";
        public const string ModName = "RtSpFileEditor";
        public const string ModVer = "114514";

        public override bool Load()
        {
            // 注册原有功能
            ActionManager.RegisterAction<CreateFileAction>("CreateFileAction");

            // 注册新增功能
            GoalManager.RegisterGoal<RealFileExistsGoal>("RealFileExists");
            GoalManager.RegisterGoal<RealFileNotExistsGoal>("RealFileNotExists");
            ActionManager.RegisterAction<TerminateGameAction>("TerminateGame");
            GoalManager.RegisterGoal<FileContentMatchGoal>("FileContentMatch");
            ActionManager.RegisterAction<RunExternalFileAction>("RunExternalFile");
            Console.WriteLine("");
            Console.WriteLine("[RtSpFileEditor] Registe Complete.");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("//////////////////////////////////// RtSpFileEditor //////////////////////////////////");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("/////////////////////////////////// ThanksForUsing ////////////////////////////////////");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("////////////////////////////////// WELCOME /////////////////////////////////////");
            Console.WriteLine("");
            Console.ResetColor();
            return true;
        }
    }
    /* ================= 文件内容正则匹配目标检测 ================= */
    public class FileContentMatchGoal : PathfinderGoal
    {
        [XMLStorage]
        public string FilePath;  // 目标文件的绝对路径

        [XMLStorage]
        public string Pattern;   // 要匹配的正则表达式

        [XMLStorage]
        public bool RequireMatch = true;  // true=需要匹配，false=需要不匹配

        public override bool isComplete(List<string> additionalDetails = null)
        {
            string resolvedPath = ResolvePath(FilePath);

            if (!File.Exists(resolvedPath))
            {
                return !RequireMatch; // 文件不存在时的检测逻辑
            }

            var regex = new System.Text.RegularExpressions.Regex(
                Pattern,
                System.Text.RegularExpressions.RegexOptions.None
            );

            bool matchFound = CheckFileContent(resolvedPath, regex);

            // 纯检测逻辑，无返回操作
            return RequireMatch ? matchFound : !matchFound;
        }

        private bool CheckFileContent(string filePath, System.Text.RegularExpressions.Regex regex)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (regex.IsMatch(line))
                    {
                        return true; // 检测到匹配立即返回
                    }
                }
            }
            return false;
        }

        private string ResolvePath(string path)
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        }
    }

    /* ================= 外部文件执行动作 ================= */
    public class RunExternalFileAction : Pathfinder.Action.DelayablePathfinderAction
    {
        [XMLStorage]
        public string FilePath;  // 要执行的文件路径

        [XMLStorage]
        public string Arguments = "";  // 传递给程序的参数

        [XMLStorage]
        public bool UseShellExecute = true;  // 是否使用操作系统shell执行

        [XMLStorage]
        public bool WaitForExit = false;  // 是否等待程序退出

        public override void Trigger(OS os)
        {
            try
            {
                string resolvedPath = ResolvePath(FilePath);

                if (!File.Exists(resolvedPath))
                {
                    os.write($"Error: File not found at {resolvedPath}");
                    return;
                }

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = resolvedPath,
                        Arguments = Arguments,
                        UseShellExecute = UseShellExecute,
                        CreateNoWindow = !UseShellExecute
                    }
                };

                os.write($"Starting external program: {Path.GetFileName(resolvedPath)}");
                process.Start();

                if (WaitForExit)
                {
                    os.write("Waiting for program to exit...");
                    process.WaitForExit();
                    os.write($"Program exited with code: {process.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                os.write($"Failed to run external file: {ex.Message}");
            }
        }

        private string ResolvePath(string path)
        {
            // 处理环境变量
            string resolved = Environment.ExpandEnvironmentVariables(path);

            // 如果是相对路径，转换为绝对路径
            if (!Path.IsPathRooted(resolved))
            {
                resolved = Path.GetFullPath(resolved);
            }

            return resolved;
        }
    }


    /* ================= 文件创建动作 ================= */
    public class CreateFileAction : Pathfinder.Action.DelayablePathfinderAction
    {
        [XMLStorage]
        public string FileName;

        [XMLStorage]
        public string FileDirectory;

        [XMLStorage]
        public string MinSize;

        [XMLStorage]
        public string MaxSize;

        private int minSizeBytes = 1 * 1024;
        private int maxSizeBytes = 10 * 1024;
        private string resolvedPath;

        private static readonly Random Rnd = new Random();

        public override void Trigger(OS os)
        {
            try
            {
                // 确保目录存在
                string dir = System.IO.Path.GetDirectoryName(resolvedPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                int fileSize = Rnd.Next(minSizeBytes, maxSizeBytes + 1);
                byte[] fileData = new byte[fileSize];
                Rnd.NextBytes(fileData);

                File.WriteAllBytes(resolvedPath, fileData);

                os.write($"Created file: {resolvedPath}");
                os.write($"Size: {fileSize} bytes");
            }
            catch (Exception ex)
            {
                os.write($"File creation failed: {ex.Message}");
            }
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);

            if (!string.IsNullOrEmpty(MinSize))
                minSizeBytes = ParseSize(MinSize);

            if (!string.IsNullOrEmpty(MaxSize))
                maxSizeBytes = ParseSize(MaxSize);

            if (minSizeBytes <= 0) minSizeBytes = 1024;
            if (maxSizeBytes <= 0) maxSizeBytes = 10240;
            if (minSizeBytes > maxSizeBytes)
                (minSizeBytes, maxSizeBytes) = (maxSizeBytes, minSizeBytes);

            resolvedPath = ResolveFilePath(FileName, FileDirectory);
        }

        private int ParseSize(string sizeStr)
        {
            if (sizeStr.EndsWith("KB", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(sizeStr.Substring(0, sizeStr.Length - 2), out int kbValue))
                return kbValue * 1024;

            if (int.TryParse(sizeStr, out int intValue))
                return intValue;

            return 1024;
        }

        private string ResolveFilePath(string fileName, string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return System.IO.Path.Combine(Environment.CurrentDirectory, fileName);

            if (System.IO.Path.IsPathRooted(directory))
                return System.IO.Path.Combine(directory, fileName);

            return System.IO.Path.Combine(Environment.CurrentDirectory, directory, fileName);
        }
    }

    /* ===== 修复后的文件存在检测目标 ===== */
    public class RealFileExistsGoal : PathfinderGoal
    {
        [XMLStorage]
        public string FilePath; // 保留原有属性

        public override bool isComplete(List<string> additionalDetails = null)
        {
            // 新增空值校验
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Console.WriteLine("[ERROR] FilePath is null or empty in RealFileExistsGoal");
                return false; // 路径无效视为文件不存在
            }

            try
            {
                string resolvedPath = Environment.ExpandEnvironmentVariables(FilePath);
                resolvedPath = Path.GetFullPath(resolvedPath); // 规范化路径
                return File.Exists(resolvedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] File check failed: {ex.Message}");
                return false;
            }
        }
    }

    /* ===== 修复后的文件不存在检测目标 ===== */
    public class RealFileNotExistsGoal : PathfinderGoal
    {
        [XMLStorage]
        public string FilePath;

        public override bool isComplete(List<string> additionalDetails = null)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Console.WriteLine("[ERROR] FilePath is null in RealFileNotExistsGoal");
                return true; // 路径无效视为文件不存在
            }

            try
            {
                string resolvedPath = Environment.ExpandEnvironmentVariables(FilePath);
                resolvedPath = Path.GetFullPath(resolvedPath);
                return !File.Exists(resolvedPath);
            }
            catch
            {
                return true;
            }
        }
    }


    /* ================= 游戏终止动作 ================= */
    public class TerminateGameAction : Pathfinder.Action.DelayablePathfinderAction
    {
        [XMLStorage]
        public new float Delay = 0f; // 使用new关键字解决隐藏警告

        public override void Trigger(OS os)
        {
            if (Delay > 0f)
            {
                os.delayer.Post(ActionDelayer.Wait(Delay), Terminate);
            }
            else
            {
                Terminate();
            }
        }

        private void Terminate()
        {
            Environment.Exit(0);
        }
    }
}
