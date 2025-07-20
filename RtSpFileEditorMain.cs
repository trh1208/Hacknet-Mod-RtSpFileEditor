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

using Hacknet.Extensions;
using BepInEx.Hacknet;


using System.Diagnostics;
//去你妈的报错，老子就不信全给你引用一遍，还能报缺少库！！！！





namespace RtSpFileEditor
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    //RNM!!!!!!!!!!!!CNMD,SB上古VS，这么大个RtSpFileEditor，你丫输出的名字还是你那个默认的HacknetPluginTemplate.dll
    //微软，整个世界就是一个巨大的不安全的NuGet包是吧！BYD之前好歹只是提示第三方包不安全，现在自己的包都不安全了是吧。
    //编译为什么要检查那个该死的包安全性啊Fvvvvvvvvvvvvvvvk！
    public class HacknetPluginTemplate : HacknetPlugin
    {
        public const string ModGUID = "com.RtSpFileEditor.test";
        public const string ModName = "RtSpFileEditor";
        public const string ModVer = "1.0.0";

        public override bool Load()
        {


            // 注册新增功能

            ActionManager.RegisterAction<CreateFileAction>("CreateFileAction");
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
    /* ================= 游戏终止动作 (使用正确Hacknet API) ================= */
    //如果有人看到了下面的这么一大坨注释，不要问我为什么不删，下面是备份，是bug最少的版本，要改可以从这里直接改。
    //小贴士：不要在提交的时候覆盖仓库，不然我就烧了你！！！！！
    /*

    public class TerminateGameAction : DelayablePathfinderAction
    {
        [XMLStorage]
        public new float Delay = 0f;

        [XMLStorage]
        public bool SaveBeforeExit = false;
        public override void Trigger(OS os)
        {
            if (Delay <= 0f)
            {
                SafeTerminate(os);
            }
            else
            {
                // 完全正确的延迟执行方式 - 使用 Hacknet 中实际存在的 API
                os.delayer.Post(
                    ActionDelayer.Wait(Delay), // 创建等待条件
                    () => SafeTerminate(os)    // 条件满足后执行的动作
                );
            }
        }
        private void SafeTerminate(OS os)
        {
            try
            {
                if (SaveBeforeExit)
                {
                    TrySaveGame(os);
                }

                // 强制终止游戏进程
                ForceKillGameProcess();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TerminateGame] Critical error: {ex.Message}");
                ForceKillGameProcess();
            }
        }
        private void TrySaveGame(OS os)
        {
            try
            {
                // 使用OS.cs中实际存在的保存方法
                os.saveGame();
                Console.WriteLine("[TerminateGame] Game saved successfully before exit");
            }
            catch (Exception saveEx)
            {
                Console.WriteLine($"[TerminateGame] Save failed: {saveEx.Message}");
            }
        }
        private void ForceKillGameProcess()
        {
            try
            {
                // 获取当前进程并强制终止
                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.Kill();
            }
            catch (Exception killEx)
            {
                Console.WriteLine($"[TerminateGame] Process kill failed: {killEx.Message}");
                Environment.Exit(0); // 作为后备方案
            }
        }
    }
    */

    public class TerminateGameAction : DelayablePathfinderAction
    {
        [XMLStorage]
        public new float Delay = 0f;

        [XMLStorage]
        public bool SaveBeforeExit = false;

        public override void Trigger(OS os)
        {
            // 检查是否为特殊测试账户
            if (os.defaultUser.name == "__ExtensionTest")
            {
                HandleExtensionTestTermination(os);
                return;
            }

            // 普通账户的正常终止逻辑
            if (Delay <= 0f)
            {
                SafeTerminate(os);
            }
            else
            {
                // 使用 Hacknet 中实际存在的 API 进行延迟
                os.delayer.Post(
                    ActionDelayer.Wait(Delay),
                    () => SafeTerminate(os)
                );
            }
        }

        // 处理特殊测试账户的终止逻辑
        private void HandleExtensionTestTermination(OS os)
        {
            os.write("==============================================");
            os.write("Extension Test Mode: Skipping game termination");
            os.write("==============================================");

            if (SaveBeforeExit)
            {
                os.write("Saving game state for extension test user...");
                TrySaveGame(os);
            }
            else
            {
                os.write("SaveBeforeExit=false, skipping save operation");
            }

            os.write("Continuing game execution for testing purposes");
        }

        private void SafeTerminate(OS os)
        {
            try
            {
                if (SaveBeforeExit)
                {
                    TrySaveGame(os);
                }

                ForceKillGameProcess();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TerminateGame] Critical error: {ex.Message}");
                ForceKillGameProcess();
            }
        }

        private void TrySaveGame(OS os)
        {
            try
            {
                // 使用 Hacknet 内置的保存方法
                os.saveGame();
                Console.WriteLine("[TerminateGame] Game saved successfully before exit");
            }
            catch (Exception saveEx)
            {
                Console.WriteLine($"[TerminateGame] Save failed: {saveEx.Message}");
            }
        }

        private void ForceKillGameProcess()
        {
            try
            {
                // 获取当前进程并强制终止
                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.Kill();
            }
            catch (Exception killEx)
            {
                Console.WriteLine($"[TerminateGame] Process kill failed: {killEx.Message}");
                Environment.Exit(0); // 后备退出方案
            }
        }
    }

    //下面的这个大括号不要动！！！！！
    //这是整个程序的末端，是闭合的！！！！
    //你再删了，把你那个B AI写的混合农家肥复制过来的时候，把这玩意删了，我就让你飞起来！！！！

}