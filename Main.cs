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
using RtSpFileEditor;
using System.Diagnostics;
using BepInEx.Configuration;
using Pathfinder.Command;
using System.Text.RegularExpressions;
using Hacknet.Gui;
using System.Reflection;
using Hacknet.PlatformAPI.Storage;
using static Pathfinder.Event.Menu.DrawMainMenuTitlesEvent;
using System.Security.Policy;
using static RtSpFileEditor.RtSpFileEditor;

namespace RtSpFileEditor;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class RtSpFileEditor : HacknetPlugin
{
    public const string ModGUID = "com.RtSpFileEditor.test";
    public const string ModName = "RtSpFileEditor";
    public const string ModVer = "1.0.0";

    public override bool Load()
    {
        if (ExtensionLoader.ActiveExtensionInfo != null)
        {
            // 统一为正斜杠，移除双斜杠逻辑
            CurrentExtRootPath = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace('\\', '/');
        }
        // 注册新特性
        ActionManager.RegisterAction<CreateFileAction>("CreateFileAction");
        GoalManager.RegisterGoal<RealFileExistsGoal>("RealFileExists");
        GoalManager.RegisterGoal<RealFileNotExistGoal>("RealFileNotExists");
        ActionManager.RegisterAction<TerminateGameAction>("TerminateGame");
        GoalManager.RegisterGoal<FileContentMatchGoal>("FileContentMatch");
        ActionManager.RegisterAction<RunExternalFileAction>("RunExternalFile");

        Console.WriteLine("");
        Console.WriteLine("[RtSpFileEditor] Register Complete.");
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

    public static string CurrentExtRootPath;

    // Resolve Path - 统一使用单斜杠
    public static string ResolvePath(string inputPath)
    {
        if (string.IsNullOrEmpty(inputPath))
            return CurrentExtRootPath?.Replace('\\', '/') ?? "";

        // 先将所有反斜杠转换为正斜杠
        string normalized = inputPath.Replace('\\', '/');

        if (normalized.StartsWith("/"))
        {
            // 根路径也统一为正斜杠
            string root = CurrentExtRootPath?.Replace('\\', '/') ?? "";
            return root + normalized;
        }
        else
        {
            return normalized;
        }
    }

    /* File Content Regex Match Goal */
    public class FileContentMatchGoal : PathfinderGoal
    {
        [XMLStorage]
        public string FilePath;

        [XMLStorage]
        public string Pattern;

        [XMLStorage]
        public bool RequireMatch = true;

        public override bool isComplete(List<string> additionalDetails = null)
        {
            string resolvedPath = ResolvePath(FilePath);
            if (!File.Exists(resolvedPath))
            {
                return !RequireMatch;
            }

            var Regex = new System.Text.RegularExpressions.Regex(
                Pattern,
                System.Text.RegularExpressions.RegexOptions.None
                );
            bool matched = CheckFileContent(resolvedPath, Regex);
            return RequireMatch ? matched : !matched;
        }

        private bool CheckFileContent(string filepath, Regex regex)
        {
            using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var rdr = new StreamReader(stream))
            {
                string line;
                while ((line = rdr.ReadLine()) != null)
                {
                    if (regex.IsMatch(line))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /* External File Execution Action */
    /* External File Execution Action */
    public class RunExternalFileAction : Pathfinder.Action.DelayablePathfinderAction
    {
        [XMLStorage]
        public string FilePath;

        [XMLStorage]
        public string Arguments = "";

        [XMLStorage]
        public bool UseShellExecute = true;

        [XMLStorage]
        public bool WaitForExit = false;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const int SW_FORCEMINIMIZE = 11;

        public override void Trigger(OS os)
        {
            string resolvedPath = RtSpFileEditor.ResolvePath(FilePath);

            try
            {
                if (!File.Exists(resolvedPath))
                {
                    os.write($"Error: File not found at {resolvedPath}");
                    return;
                }

                IntPtr gameWindowHandle = Process.GetCurrentProcess().MainWindowHandle;

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

                if (WaitForExit)
                {
                    // 需要等待程序退出：最小化游戏窗口，后台等待，结束后恢复
                    if (gameWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(gameWindowHandle, SW_MINIMIZE);
                    }

                    // 启动后台任务等待进程退出并维护窗口最小化
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            process.Start();

                            // 循环等待进程退出，同时每隔 0.2 秒强制最小化游戏窗口（防止用户恢复）
                            while (!process.HasExited)
                            {
                                System.Threading.Thread.Sleep(200);
                                if (gameWindowHandle != IntPtr.Zero)
                                {
                                    // 检查窗口是否最小化，如果不是则再次最小化
                                    // 使用 ShowWindow(SW_MINIMIZE) 即使已经最小化也没副作用
                                    ShowWindow(gameWindowHandle, SW_MINIMIZE);
                                }
                            }

                            // 进程已退出，恢复游戏窗口
                            if (gameWindowHandle != IntPtr.Zero)
                            {
                                // 通过 os.delayer 在主线程上执行恢复操作
                                os.delayer.Post(ActionDelayer.Wait(0f), () =>
                                {
                                    ShowWindow(gameWindowHandle, SW_RESTORE);
                                    SetForegroundWindow(gameWindowHandle);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // 后台异常：确保游戏窗口恢复
                            if (gameWindowHandle != IntPtr.Zero)
                            {
                                os.delayer.Post(ActionDelayer.Wait(0f), () =>
                                {
                                    ShowWindow(gameWindowHandle, SW_RESTORE);
                                    SetForegroundWindow(gameWindowHandle);
                                });
                            }
                            os.write($"Background task error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    // 不需要等待：直接启动进程，不恢复游戏焦点（让外部程序窗口在前）
                    process.Start();
                    // 不执行任何焦点恢复，让外部程序窗口自然获得焦点并覆盖游戏窗口
                }
            }
            catch (Exception ex)
            {
                os.write($"Failed to run external file: {ex.Message}");
            }
        }
    }

    /* File Creation Action */
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
                string dir = ResolvePath(FileDirectory);
                if (!Directory.Exists(dir))
                {
                    os.write($"Creating Dir at {dir}");
                    Directory.CreateDirectory(dir);
                }

                int filesize = Rnd.Next(minSizeBytes, maxSizeBytes + 1);
                byte[] data = new byte[filesize];
                Rnd.NextBytes(data);

                os.write($"Creating file at: {resolvedPath}");
                File.WriteAllBytes(resolvedPath, data);

                os.write($"Created file: {resolvedPath}");
                os.write($"Size: {filesize} bytes");
            }
            catch (Exception ex)
            {
                os.write($"File creation failed: {ex.Message}");
                os.write($"Stack trace: {ex.StackTrace}");
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
            resolvedPath = CombineResolvePath(FileName, FileDirectory);
            Console.WriteLine($"[CreateFileAction] Resolved file path: {resolvedPath}");
        }

        private string CombineResolvePath(string filename, string directory)
        {
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentException("FileName cannot be null or empty");

            string combined;
            if (string.IsNullOrEmpty(directory))
                combined = filename;
            else
                combined = Path.Combine(directory, filename);

            // 统一为正斜杠
            combined = combined.Replace('\\', '/');

            // 获取完整路径（包含 CurrentExtRootPath 并确保统一斜杠）
            return ResolvePath(combined);
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
    }

    /* File Existence Detection Goal */
    public class RealFileExistsGoal : PathfinderGoal
    {
        [XMLStorage]
        public string FilePath;

        public override bool isComplete(List<string> additionalDetails = null)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Console.WriteLine("[ERROR] FilePath is null or empty");
                return false;
            }
            try
            {
                string resolvedPath = ResolvePath(FilePath);
                Console.WriteLine($"[RealFileExistsGoal] Checking file: {resolvedPath}");
                return File.Exists(resolvedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] File check failed: {ex.Message}");
                return false;
            }
        }
    }

    /* File Non-Existence Detection Goal */
    public class RealFileNotExistGoal : PathfinderGoal
    {
        [XMLStorage]
        public string FilePath;

        public override bool isComplete(List<string> additionalDetails = null)
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                Console.WriteLine("[ERROR] FilePath is null or empty");
                return false;
            }
            try
            {
                string resolvedPath = RtSpFileEditor.ResolvePath(FilePath);
                Console.WriteLine($"[RealFileNotExistsGoal] Checking file: {resolvedPath}");
                return !File.Exists(resolvedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] File check failed: {ex.Message}");
                return false;
            }
        }
    }

    /* Game Termination Action */
    public class TerminateGameAction : DelayablePathfinderAction
    {
        [XMLStorage]
        public new float Delay = 0f;

        [XMLStorage]
        public bool SaveBeforeExit = false;

        public override void Trigger(OS os)
        {
            if (os.SaveUserAccountName.ToLower().Contains("extensiontest"))
            {
                HandleExtensionTestTermination(os);
                return;
            }

            if (Delay <= 0f)
            {
                SafeTerminate(os);
            }
            else
            {
                os.delayer.Post(
                    ActionDelayer.Wait(Delay),
                    () => SafeTerminate(os)
                );
            }
        }

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
                Process currentProcess = Process.GetCurrentProcess();
                currentProcess.Kill();
            }
            catch (Exception killEx)
            {
                Console.WriteLine($"[TerminateGame] Process kill failed: {killEx.Message}");
                Environment.Exit(0);
            }
        }
    }
}