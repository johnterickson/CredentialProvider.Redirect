﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Plugins;

namespace CredentialProvider.WSL2
{
    class Program
    {
        static readonly Action<string> log = MakeLogger();

        static Action<string> MakeLogger()
        {
            string logPath = Environment.GetEnvironmentVariable("NUGET_WSL_LOG_PATH");
            var logFile = logPath == null ? null : new StreamWriter(logPath) { AutoFlush = true };
            return (line) =>
            {
                logFile?.WriteLine($"{DateTime.UtcNow.ToString("o")} {line}");
            };
        }

        static string GetActualCredProviderPath()
        {
            string windowsCredProviderPath = Environment.GetEnvironmentVariable("NUGET_WSL_REDIRECT_TO_EXEC");
            if (windowsCredProviderPath == null)
            {
                var getUserProfile = Process.Start(new ProcessStartInfo("cmd.exe", "/c echo %USERPROFILE%")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });

                _ = getUserProfile.StandardError.ReadToEnd();
                string userProfile = getUserProfile.StandardOutput.ReadToEnd().Trim();
                getUserProfile.WaitForExit();

                if (getUserProfile.ExitCode != 0)
                {
                    Environment.Exit(getUserProfile.ExitCode);
                }

                windowsCredProviderPath = userProfile + "\\.nuget\\plugins\\netfx\\CredentialProvider.Microsoft\\CredentialProvider.Microsoft.exe";

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    char drive = char.ToLowerInvariant(windowsCredProviderPath[0]);
                    windowsCredProviderPath = "/mnt/" + drive + windowsCredProviderPath.Trim().Substring(1).Replace(":", "").Replace("\\", "/");
                }
            }

            return windowsCredProviderPath;
        }

        static int Main(string[] args)
        {
            string credProviderPath = GetActualCredProviderPath();

            string credArgs = string.Empty;
            foreach (string arg in args)
            {
                credArgs += $"\"{arg}\" ";
            }

            Process credProviderProcess = new Process()
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

            Action<string> winToBridge = (line) => log($"[WIN -> BRIDGE]       {line}");

            Action<string> bridgeToWin = (line) =>
            {
                log($"[BRIDGE -> WIN] START {line}");
                credProviderProcess.StandardInput.Write(line);
                credProviderProcess.StandardInput.Write("\r");
                credProviderProcess.StandardInput.Write("\n");
                credProviderProcess.StandardInput.Flush();
                log($"[BRIDGE -> WIN] DONE  {line}");
            };

            Action<string> wslToBridge = (line) => log($"[WSL -> BRIDGE]       {line}");

            Action<string> bridgeToWsl = (line) =>
            {
                log($"[BRIDGE -> WSL] START {line}");
                Console.Out.Write(line);
                Console.Out.Write('\n');
                Console.Out.Flush();
                log($"[BRIDGE -> WSL] DONE  {line}");
            };

            credProviderProcess.OutputDataReceived += (_sender, e) =>
            {
                string line = e.Data;
                if (line == null) return;
                winToBridge(line);
                bridgeToWsl(line);
            };

            credProviderProcess.ErrorDataReceived += (_sender, e) =>
            {
                string line = e.Data;
                if (line == null) return;
                log($"[WIN STDERR] {line}");
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
                        wslToBridge(line);
                        bridgeToWin(line);
                    }

                    log($"WSL -> Bridge closed.");

                    credProviderProcess.StandardInput.Close();
                    log($"Bridge -> WIN closed.");
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
}
