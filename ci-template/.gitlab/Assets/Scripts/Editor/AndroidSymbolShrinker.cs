using System;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

public class AndroidSymbolShrinker
{
    private static string LastSymbolToShrinkLocation = nameof(LastSymbolToShrinkLocation);

    class ProcessResult
    {
        internal int ExitCode { get; }
        internal string StdOut { get; }
        internal string StdErr { get; }

        internal bool Failure => ExitCode != 0;
        internal ProcessResult(int exitCode, string stdOut, string stdErr)
        {
            ExitCode = exitCode;
            StdOut = stdOut;
            StdErr = stdErr;
        }

        public override string ToString()
        {
            return $"Exit Code: {ExitCode}\nStdOut:\n{StdOut}\nStdErr:\n{StdErr}";
        }
    }

    private static void Log(string message)
    {
        UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
    }

    private static ProcessResult RunProcess(string workingDirectory, string fileName, string args, bool useBashCommand)
    {
        if (useBashCommand) { return RunProcessCommand(workingDirectory, args); }

        Log($"Executing {fileName} {args} (Working Directory: {workingDirectory}");
        Process process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = args;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.CreateNoWindow = true;
        var output = new StringBuilder();
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
            }
        });

        var error = new StringBuilder();
        process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                error.AppendLine(e.Data);
            }
        });

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        Log($"{fileName} exited with {process.ExitCode}");
        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }
    
    static ProcessResult RunProcessCommand(string workingDirectory, string args)
    {
        string command = $"-c '7z {args}'";
        string fileName = "/bin/bash";
        if (Application.platform == RuntimePlatform.WindowsEditor)
            fileName = "bash";
        
        ProcessStartInfo procStartInfo = new ProcessStartInfo(fileName, command);
        procStartInfo.RedirectStandardOutput = true;
        procStartInfo.RedirectStandardError = true;
        procStartInfo.UseShellExecute = false;
        procStartInfo.CreateNoWindow = true;
        procStartInfo.WorkingDirectory = workingDirectory;

        System.Diagnostics.Process process = new System.Diagnostics.Process();
        
        var output = new StringBuilder();
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                output.AppendLine(e.Data);
            }
        });

        var error = new StringBuilder();
        process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                error.AppendLine(e.Data);
            }
        });
        
        process.StartInfo = procStartInfo;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        
        Log($"{command} exited with {process.ExitCode}");
        return new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
    }
    
    private static void Cleanup(string path)
    {
        if (Directory.Exists(path))
        {
            Log($"Delete {path}");
            Directory.Delete(path, true);
        }
        if (File.Exists(path))
        {
            Log($"Delete {path}");
            File.Delete(path);
        }
    }

    [MenuItem("Android Symbols/Shrink")]
    public static void ShrinkSymbols()
    {
        var location = EditorPrefs.GetString(LastSymbolToShrinkLocation, Path.Combine(Application.dataPath, ".."));
        location = EditorUtility.OpenFilePanel(
            "Open Android Symbol Package to shrink",
             location, "*.zip");

        if (string.IsNullOrEmpty(location))
            return;

        ShrinkSymbols(location);
    }

    public static void ShrinkSymbols(string location)
    {
        var targetDirectory = Path.GetDirectoryName(location);
        var intermediatePath = Path.Combine(targetDirectory, "TempShrink");
        var newZip = Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(location) + ".small.zip");
        EditorPrefs.SetString(LastSymbolToShrinkLocation, targetDirectory);

        var zipFileName = Path.GetFullPath(Path.Combine(EditorApplication.applicationContentsPath, "Tools", "7z"));
        if (Application.platform == RuntimePlatform.WindowsEditor)
            zipFileName += ".exe";

        bool useBashCommand = true;
        if (!File.Exists(zipFileName))
        {
            var exception = new Exception($"Failed to locate {zipFileName}");
            Debug.LogException(exception);
            useBashCommand = true;
        }

        Cleanup(intermediatePath);
        Cleanup(newZip);
        var result = RunProcess(targetDirectory, zipFileName, $"x -o\"{intermediatePath}\" \"{location}\"", useBashCommand);
        if (result.Failure)
            throw new Exception(result.ToString());

        EditorUtility.DisplayProgressBar("Shrinking symbols", "Deleting/Renaming/Compressing symbol files", 0.5f);
        var files = Directory.GetFiles(intermediatePath, "*.*", SearchOption.AllDirectories);
        var symSo = ".sym.so";
        foreach (var file in files)
        {
            if (file.EndsWith(".dbg.so"))
                Cleanup(file);
            if (file.EndsWith(symSo))
            {
                var fileSO = file.Substring(0, file.Length - symSo.Length) + ".so";
                Log($"Rename {file} --> {fileSO}");
                File.Move(file, fileSO);
            }
        }

        result = RunProcess(intermediatePath, zipFileName, $"a -tzip \"{newZip}\"", useBashCommand);
        EditorUtility.ClearProgressBar();
        if (result.Failure)
            throw new Exception(result.ToString());

        Cleanup(intermediatePath);

        Log($"New small symbol package: {newZip}");
        EditorUtility.RevealInFinder(newZip);   
    }
}