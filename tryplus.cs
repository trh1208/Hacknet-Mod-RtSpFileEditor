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

namespace RtSpFileEditor
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class RtSpFileEditor : HacknetPlugin
    {
        public const string ModGUID = "com.RtSpFileEditor.test";
        public const string ModName = "RtSpFileEditor";
        public const string ModVer = "1.0.0";

        // Store current extension root directory
        public static string CurrentExtensionRoot { get; private set; }

        // BepInEx config file
        public static ConfigFile Config { get; private set; }

        // Config entries
        public static ConfigEntry<bool> DebugLogging { get; private set; }
        public static ConfigEntry<string> CustomExtensionPath { get; private set; }

        // Unified path resolution method - based on current extension root
        public static string ResolvePath(string inputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                return CurrentExtensionRoot ?? AppDomain.CurrentDomain.BaseDirectory;

            string resolved = Environment.ExpandEnvironmentVariables(inputPath).Replace('/', '\\').Trim();
            resolved = resolved.Trim('\\', '/');

            // If it's an absolute path, return directly
            if (Path.IsPathRooted(resolved) && resolved.Contains(":"))
                return Path.GetFullPath(resolved);

            // Use current extension root as base
            string baseDir = CurrentExtensionRoot;
            if (string.IsNullOrEmpty(baseDir))
                baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string fullPath = Path.Combine(baseDir, resolved);

            if (DebugLogging?.Value == true)
            {
                Console.WriteLine($"[RtSpFileEditor] ResolvePath: input='{inputPath}', base='{baseDir}' => '{fullPath}'");
            }

            return Path.GetFullPath(fullPath);
        }

        // Infer extension root from config file path
        private static string GetExtensionRootFromConfigPath(string configPath)
        {
            try
            {
                if (string.IsNullOrEmpty(configPath))
                    return null;

                Console.WriteLine($"[RtSpFileEditor] Config file path: {configPath}");

                // Config file is at: Extensions\RTSP-CORE1\Plugins\Configs\RtSpFileEditor.cfg
                // We need to go up three levels to get extension root
                DirectoryInfo configFileInfo = new FileInfo(configPath).Directory;
                if (configFileInfo != null)
                {
                    // Configs -> Plugins -> RTSP-CORE1 -> Extensions
                    // Actually we need: RTSP-CORE1 directory (extension root)

                    // Go up to Configs directory's parent (Plugins)
                    DirectoryInfo pluginsDir = configFileInfo.Parent;
                    if (pluginsDir != null)
                    {
                        // Go up to extension root directory (RTSP-CORE1)
                        DirectoryInfo extensionRootDir = pluginsDir.Parent;
                        if (extensionRootDir != null)
                        {
                            string extensionRoot = extensionRootDir.FullName;
                            Console.WriteLine($"[RtSpFileEditor] Extracted extension root from config: {extensionRoot}");

                            // Verify path
                            if (Directory.Exists(extensionRoot))
                            {
                                // Check if it's an extension directory
                                if (IsExtensionDirectory(extensionRoot))
                                {
                                    return extensionRoot;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RtSpFileEditor] Error getting extension root from config: {ex.Message}");
            }

            return null;
        }

        // Check if directory is an extension directory
        private static bool IsExtensionDirectory(string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return false;

                // Check for Info.xml file
                string infoFile = Path.Combine(directory, "Info.xml");
                if (File.Exists(infoFile))
                {
                    Console.WriteLine($"[RtSpFileEditor] Found Info.xml at: {infoFile}");
                    return true;
                }

                // Check for common extension directory structure
                string[] typicalSubDirs = { "Content", "Actions", "Daemons", "Themes", "Plugins", "Missions" };
                int foundCount = 0;
                foreach (string subDir in typicalSubDirs)
                {
                    if (Directory.Exists(Path.Combine(directory, subDir)))
                    {
                        foundCount++;
                    }
                }

                // If at least 2 typical extension subdirectories are found, consider it an extension directory
                if (foundCount >= 2)
                {
                    Console.WriteLine($"[RtSpFileEditor] Found {foundCount} typical extension subdirectories");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RtSpFileEditor] Error checking extension directory: {ex.Message}");
            }

            return false;
        }

        // Initialize config and get extension root
        private static void InitializeConfigAndPath()
        {
            try
            {
                // Create config directory
                string configDir = Path.Combine(Paths.ConfigPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    Console.WriteLine($"[RtSpFileEditor] Created config directory: {configDir}");
                }

                // Create config file
                string configFilePath = Path.Combine(configDir, "RtSpFileEditor.cfg");
                Console.WriteLine($"[RtSpFileEditor] Creating config file at: {configFilePath}");

                Config = new ConfigFile(configFilePath, true);

                // Add config entries
                DebugLogging = Config.Bind(
                    "Settings",
                    "DebugLogging",
                    false,
                    "Enable debug logging for path resolution"
                );

                CustomExtensionPath = Config.Bind(
                    "Settings",
                    "CustomExtensionPath",
                    "",
                    "Custom extension root path (leave empty for auto-detection)"
                );

                // Method 1: Use custom path (if user set it)
                if (!string.IsNullOrEmpty(CustomExtensionPath.Value) && Directory.Exists(CustomExtensionPath.Value))
                {
                    CurrentExtensionRoot = CustomExtensionPath.Value;
                    Console.WriteLine($"[RtSpFileEditor] Using custom extension path from config: {CurrentExtensionRoot}");
                    return;
                }

                // Method 2: Infer from config file path
                CurrentExtensionRoot = GetExtensionRootFromConfigPath(configFilePath);
                if (!string.IsNullOrEmpty(CurrentExtensionRoot))
                {
                    Console.WriteLine($"[RtSpFileEditor] Using auto-detected extension root: {CurrentExtensionRoot}");

                    // Save detected path to config for next time
                    if (string.IsNullOrEmpty(CustomExtensionPath.Value))
                    {
                        CustomExtensionPath.Value = CurrentExtensionRoot;
                        Config.Save();
                    }
                    return;
                }

                // Method 3: Infer from plugin Assembly location
                try
                {
                    string pluginPath = typeof(RtSpFileEditor).Assembly.Location;
                    Console.WriteLine($"[RtSpFileEditor] Plugin path: {pluginPath}");

                    if (File.Exists(pluginPath))
                    {
                        DirectoryInfo pluginsDir = new FileInfo(pluginPath).Directory;
                        if (pluginsDir?.Parent != null)
                        {
                            // Plugin is usually at: Extensions\ExtensionName\Plugins\
                            // So go up two levels to Extensions directory
                            DirectoryInfo extensionRootDir = pluginsDir.Parent.Parent;
                            if (extensionRootDir != null && Directory.Exists(extensionRootDir.FullName))
                            {
                                string potentialRoot = extensionRootDir.FullName;
                                if (IsExtensionDirectory(potentialRoot))
                                {
                                    CurrentExtensionRoot = potentialRoot;
                                    Console.WriteLine($"[RtSpFileEditor] Using extension root from plugin location: {CurrentExtensionRoot}");
                                    return;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RtSpFileEditor] Error getting path from plugin location: {ex.Message}");
                }

                // Method 4: Use game root directory as fallback
                CurrentExtensionRoot = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"[RtSpFileEditor] WARNING: Could not determine extension root. Using game directory: {CurrentExtensionRoot}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RtSpFileEditor] CRITICAL ERROR in InitializeConfigAndPath: {ex.Message}");
                Console.WriteLine($"[RtSpFileEditor] Stack trace: {ex.StackTrace}");

                // Make sure we have at least a valid root directory
                CurrentExtensionRoot = AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public override bool Load()
        {
            Console.WriteLine("[RtSpFileEditor] Starting load process...");

            // Initialize config and get extension root
            InitializeConfigAndPath();

            Console.WriteLine($"[RtSpFileEditor] Extension root set to: {CurrentExtensionRoot}");

            // Verify extension root directory
            if (string.IsNullOrEmpty(CurrentExtensionRoot) || !Directory.Exists(CurrentExtensionRoot))
            {
                Console.WriteLine($"[RtSpFileEditor] ERROR: Invalid extension root: {CurrentExtensionRoot}");
                Console.WriteLine($"[RtSpFileEditor] Creating directory: {CurrentExtensionRoot}");
                try
                {
                    Directory.CreateDirectory(CurrentExtensionRoot);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RtSpFileEditor] Failed to create directory: {ex.Message}");
                }
            }

            // Create a test file to verify permissions
            try
            {
                string testFile = Path.Combine(CurrentExtensionRoot, "_RtSpFileEditor_Test.txt");
                File.WriteAllText(testFile, $"RtSpFileEditor test - {DateTime.Now}");
                Console.WriteLine($"[RtSpFileEditor] Test file created: {testFile}");

                // Read test file to verify
                if (File.Exists(testFile))
                {
                    string content = File.ReadAllText(testFile);
                    Console.WriteLine($"[RtSpFileEditor] Test file content: {content}");

                    // Delete test file
                    File.Delete(testFile);
                    Console.WriteLine($"[RtSpFileEditor] Test file deleted");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RtSpFileEditor] Warning: Could not create test file: {ex.Message}");
            }

            // Register new features
            ActionManager.RegisterAction<CreateFileAction>("CreateFileAction");
            GoalManager.RegisterGoal<RealFileExistsGoal>("RealFileExists");
            GoalManager.RegisterGoal<RealFileNotExistsGoal>("RealFileNotExists");
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

        // Provide manual method to set extension root
        public static void SetExtensionRoot(string extensionRoot)
        {
            if (!string.IsNullOrEmpty(extensionRoot))
            {
                CurrentExtensionRoot = extensionRoot;
                Console.WriteLine($"[RtSpFileEditor] Extension root manually set to: {CurrentExtensionRoot}");

                // Update config file
                if (CustomExtensionPath != null)
                {
                    CustomExtensionPath.Value = extensionRoot;
                    Config?.Save();
                }
            }
        }
    }

    /* ================= File Content Regex Match Goal Detection ================= */
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
            string resolvedPath = RtSpFileEditor.ResolvePath(FilePath);

            if (!File.Exists(resolvedPath))
            {
                return !RequireMatch;
            }

            var regex = new System.Text.RegularExpressions.Regex(
                Pattern,
                System.Text.RegularExpressions.RegexOptions.None
            );

            bool matchFound = CheckFileContent(resolvedPath, regex);

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
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /* ================= External File Execution Action ================= */
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
    }

    /* ================= File Creation Action ================= */
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
                string dir = Path.GetDirectoryName(resolvedPath);
                if (!Directory.Exists(dir))
                {
                    os.write($"Creating directory: {dir}");
                    Directory.CreateDirectory(dir);
                }

                int fileSize = Rnd.Next(minSizeBytes, maxSizeBytes + 1);
                byte[] fileData = new byte[fileSize];
                Rnd.NextBytes(fileData);

                os.write($"Creating file at: {resolvedPath}");
                File.WriteAllBytes(resolvedPath, fileData);

                os.write($"Created file: {resolvedPath}");
                os.write($"Size: {fileSize} bytes");
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

            resolvedPath = ResolveFilePath(FileName, FileDirectory);
            Console.WriteLine($"[CreateFileAction] Resolved file path: {resolvedPath}");
        }

        private string ResolveFilePath(string fileName, string directory)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("FileName cannot be null or empty");

            if (string.IsNullOrWhiteSpace(directory))
                return RtSpFileEditor.ResolvePath(fileName);

            string combined = Path.Combine(directory.Trim('\\', '/'), fileName);
            return RtSpFileEditor.ResolvePath(combined);
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

    /* ===== File Existence Detection Goal ===== */
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
                string resolvedPath = RtSpFileEditor.ResolvePath(FilePath);
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

    /* ===== File Non-Existence Detection Goal ===== */
    public class RealFileNotExistsGoal : PathfinderGoal
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

    /* ================= Game Termination Action ================= */
    public class TerminateGameAction : DelayablePathfinderAction
    {
        [XMLStorage]
        public new float Delay = 0f;

        [XMLStorage]
        public bool SaveBeforeExit = false;

        public override void Trigger(OS os)
        {
            if (os.defaultUser.name == "__ExtensionTest")
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
