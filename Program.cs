using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CredentialProvider.Redirect;

class Program
{
    static readonly Action<string> log = MakeLogger();

    static Action<string> MakeLogger()
    {
        string logPath = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDER_REDIRECT_LOG_PATH");
        var logFile = logPath == null ? null : new StreamWriter(logPath) { AutoFlush = true };
        return (line) =>
        {
            logFile?.WriteLine($"{DateTime.UtcNow.ToString("o")} {line}");
        };
    }

    static (int,string) GetOutputLine(string cmd, string args)
    {
        log($"Launching `{cmd}` `{args}`");
        try
        {
            using var process = Process.Start(new ProcessStartInfo(cmd, args)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            _ = process.StandardError.ReadToEnd();
            string line = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            log($"Found `{process.ExitCode}` `{line}`");
            return (process.ExitCode,line);
        }
        catch (Exception e)
        {
            log($"Exception `{e}`");
            return (-1, null);
        }
    }
    
    static string GetWindowsEnvVar(string varName)
    {
        (int exitCode, string value) = GetOutputLine("cmd.exe", $"/c echo %{varName}%");
        if (exitCode != 0)
        {
            Environment.Exit(exitCode);
        }
        return value;
    }

    static string NormalizePath(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return path;
        }
        else
        {
            char drive = char.ToLowerInvariant(path[0]);
            return "/mnt/" + drive + path.Trim().Substring(1).Replace(":", "").Replace("\\", "/");
        }
    }

    static string GetVSCredProvider()
    {
        string programFiles32 = GetWindowsEnvVar("ProgramFiles(x86)");
        if (programFiles32 == null)
        {
            return null;
        }

        //e.g. C:\Program Files\Microsoft Visual Studio\2022\Preview
        (int exitCode, string vsPath) = GetOutputLine(programFiles32 + "\\Microsoft Visual Studio\\Installer\\vswhere.exe", "-latest -prerelease -property installationPath");
        if (exitCode != 0 || string.IsNullOrWhiteSpace(vsPath))
        {
            return null;
        }

        // e.g. C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\CommonExtensions\Microsoft\NuGet\Plugins\CredentialProvider.Microsoft\CredentialProvider.Microsoft.exe
        return vsPath + "\\Common7\\IDE\\CommonExtensions\\Microsoft\\NuGet\\Plugins\\CredentialProvider.Microsoft\\CredentialProvider.Microsoft.exe";
    }

    static string GetActualCredProviderCmd()
    {
        string windowsCredProviderCmd = Environment.GetEnvironmentVariable("NUGET_CREDENTIALPROVIDER_REDIRECT_TARGET");

        if (windowsCredProviderCmd == null)
        {
            windowsCredProviderCmd = GetVSCredProvider();
        }
        
        string userProfile = GetWindowsEnvVar("USERPROFILE");
        if (windowsCredProviderCmd == null)
        {
            windowsCredProviderCmd = userProfile + "\\.nuget\\plugins\\netfx\\CredentialProvider.Microsoft\\CredentialProvider.Microsoft.exe";
            windowsCredProviderCmd = NormalizePath(windowsCredProviderCmd);
            
            if (!File.Exists(windowsCredProviderCmd))
            {
                windowsCredProviderCmd = null;
            }
        }

        if (windowsCredProviderCmd == null)
        {
            windowsCredProviderCmd = userProfile + "\\.nuget\\plugins\\netcore\\CredentialProvider.Microsoft\\CredentialProvider.Microsoft.exe";
            windowsCredProviderCmd = NormalizePath(windowsCredProviderCmd);
            
            if (!File.Exists(windowsCredProviderCmd))
            {
                windowsCredProviderCmd = null;
            }
        }

        return windowsCredProviderCmd;
    }

    static int Main(string[] args)
    {
        string credProviderPath = GetActualCredProviderCmd();

        string credArgs = string.Empty;
        foreach (string arg in args)
        {
            credArgs += $"\"{arg}\" ";
        }

        using Process credProviderProcess = new Process()
        {
            StartInfo = new ProcessStartInfo(credProviderPath, credArgs)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                WindowStyle = ProcessWindowStyle.Hidden,
            },
            EnableRaisingEvents = true,
        };

        log($"Starting `{credProviderProcess.StartInfo.FileName}` `{credProviderProcess.StartInfo.Arguments}'");

        Action<string> winToBridge = (line) => log($"[TARGET -> BRIDGE]       {line}");

        Action<string> bridgeToWin = (line) =>
        {
            log($"[BRIDGE -> TARGET] START {line}");
            credProviderProcess.StandardInput.Write(line);
            credProviderProcess.StandardInput.Write("\r");
            credProviderProcess.StandardInput.Write("\n");
            credProviderProcess.StandardInput.Flush();
            log($"[BRIDGE -> TARGET] DONE  {line}");
        };

        Action<string> sourceToBridge = (line) => log($"[SOURCE -> BRIDGE]       {line}");

        Action<string> bridgeToTarget = (line) =>
        {
            log($"[BRIDGE -> SOURCE] START {line}");
            Console.Out.Write(line);
            Console.Out.Write('\n');
            Console.Out.Flush();
            log($"[BRIDGE -> SOURCE] DONE  {line}");
        };

        credProviderProcess.OutputDataReceived += (_sender, e) =>
        {
            string line = e.Data;
            if (line == null) return;
            winToBridge(line);
            bridgeToTarget(line);
        };

        credProviderProcess.ErrorDataReceived += (_sender, e) =>
        {
            string line = e.Data;
            if (line == null) return;
            log($"[TARGET STDERR] {line}");
        };

        credProviderProcess.Exited += (_sender, e) =>
        {
            log($"[{credProviderPath}] unexpectedly exited {credProviderProcess.ExitCode}.");
            Environment.Exit(credProviderProcess.ExitCode);
        };


        try
        {
            credProviderProcess.Start();
            credProviderProcess.BeginOutputReadLine();
            credProviderProcess.BeginErrorReadLine();

            var reader = new Thread(() =>
            {
                string line;
                while (null != (line = Console.ReadLine()))
                {
                    sourceToBridge(line);
                    bridgeToWin(line);
                }

                log($"SOURCE -> Bridge closed.");

                credProviderProcess.StandardInput.Close();
                log($"Bridge -> TARGET closed.");
            });

            reader.Start();

            credProviderProcess.WaitForExit();

            log($"[{credProviderPath}] exited {credProviderProcess.ExitCode}.");

            reader.Join();
            log($"[bridge thread exited]");

            return credProviderProcess.ExitCode;
        }
        catch (Exception e)
        {
            log(e.ToString());

            try
            {
                credProviderProcess.StandardInput.Close();
            }
            catch {}
            
            try
            {
                if (!credProviderProcess.WaitForExit(5000))
                {
                    credProviderProcess.Kill();
                }
            }
            catch { }

            return -1;
        }
    }
}
