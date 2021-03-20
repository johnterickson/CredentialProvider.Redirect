using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Plugins;

namespace CredentialProvider.WSL2
{
    class Program
    {
        static int Main(string[] args)
        {
            string lotsOfSpaces = " ";
            while (lotsOfSpaces.Length < 1000)
            {
                lotsOfSpaces = lotsOfSpaces + lotsOfSpaces;
            }

            string windowsCredProviderPath;
            {
                // var getUserProfile = Process.Start(new ProcessStartInfo("cmd.exe", "/c echo %USERPROFILE%")
                // {
                //     RedirectStandardInput = true,
                //     RedirectStandardOutput = true,
                //     RedirectStandardError = true,
                //     UseShellExecute = false,
                //     WindowStyle = ProcessWindowStyle.Hidden,
                // });

                // _ = getUserProfile.StandardError.ReadToEnd();
                // string userProfile = getUserProfile.StandardOutput.ReadToEnd();
                // getUserProfile.WaitForExit();

                // if (getUserProfile.ExitCode != 0)
                // {
                //     return getUserProfile.ExitCode;
                // }

                string userProfile = @"C:\Users\jerick";

                windowsCredProviderPath = userProfile + "\\.nuget\\plugins\\netfx\\CredentialProvider.Microsoft\\CredentialProvider.Microsoft.exe";

                // char drive = char.ToLowerInvariant(windowsCredProviderPath[0]);

                // windowsCredProviderPath = "/mnt/" + drive + windowsCredProviderPath.Trim().Substring(1).Replace(":","").Replace("\\","/");
            }
            
            string winArgs = string.Empty;
            foreach(string arg in args)
            {
                winArgs += $"\"{arg}\" ";
            }

            // using(var argsToWin = new StreamWriter("args.txt"))
            // {
            //     await argsToWin.WriteLineAsync(winArgs);
            // }

            Process windowsCredProvider = new Process()
            {
                StartInfo = new ProcessStartInfo(windowsCredProviderPath, winArgs)
                {
                    // RedirectStandardInput = true,
                    // RedirectStandardOutput = true,
                    // RedirectStandardError = true,
                    UseShellExecute = false,
                    // StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    // StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    // StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    WindowStyle = ProcessWindowStyle.Hidden,
                }
            };

            // var sync = new object();

            var logFile = new StreamWriter("log.txt");
            logFile.AutoFlush = true;

            Action<string> log = (line) => {
                // lock(sync)
                {
                    logFile.WriteLine($"{DateTime.UtcNow.ToString("o")} {line}");
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                }
            };

            // AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs e) => {
            //     log(e.ToString());
            // };

            // Action<string> winToBridge = (line) => {
            //     log($"[WIN -> BRIDGE] {line}");
            // };

            // Action<string> bridgeToWin = (line) => {
            //     // lock(sync)
            //     {
            //         log($"[BRIDGE -> WIN] {line}");
            //         windowsCredProvider.StandardInput.Write(line);
            //         windowsCredProvider.StandardInput.Write("\n");
            //         // windowsCredProvider.StandardInput.Write(lotsOfSpaces);
            //         windowsCredProvider.StandardInput.Flush();
            //         windowsCredProvider.StandardInput.BaseStream.Flush();
            //     }
            // };

            // Action<string> wslToBridge = (line) => {
            //     // lock(sync)
            //     {
            //         log($"[WSL -> BRIDGE] {line}");
            //     }
            // };

            // Action<string> bridgeToWsl = (line) => {
            //     // lock(sync)
            //     {
            //         log($"[BRIDGE -> WSL] {line}");
            //         Console.Out.Write(line);
            //         Console.Out.Write('\n');
            //         // Console.Out.Write(lotsOfSpaces);
            //         Console.Out.Flush();
            //     }
            // };

            // windowsCredProvider.OutputDataReceived += (_sender, e) => {
            //     string line = e.Data;
            //     if (line == null) return;
            //     winToBridge(line);
            //     bridgeToWsl(line);
            // };

            // windowsCredProvider.ErrorDataReceived += (_sender, e) => {
            //     string line = e.Data;
            //     if (line == null) return;
            //     // lock(sync)
            //     {
            //         log($"[WIN STDERR] {line}");
            //     }
            // };

            windowsCredProvider.Start();
            // windowsCredProvider.BeginOutputReadLine();
            // windowsCredProvider.BeginErrorReadLine();
            
            // var reader = new Thread(() => {
            //     string line;
            //     while(null != (line = Console.ReadLine()))
            //     {
            //         wslToBridge(line);
                    
            //         // if (line.Contains("MonitorNuGetProcessExit"))
            //         // {
            //         //     var request = JsonSerializationUtilities.Deserialize<Message>(line);
            //         //     var payload = new MonitorNuGetProcessExitResponse(MessageResponseCode.Success);
            //         //     var responseMessage = MessageUtilities.Create(request.RequestId, MessageType.Response, MessageMethod.MonitorNuGetProcessExit, payload);

            //         //     StringBuilder sb = new StringBuilder();
            //         //     using (StringWriter sw = new StringWriter(sb))
            //         //     using (JsonWriter writer = new JsonTextWriter(sw))
            //         //     {
            //         //         JsonSerializationUtilities.Serialize(writer, responseMessage);
            //         //     }
            //         //     sb.AppendLine();
            //         //     bridgeToWsl(sb.ToString().Trim());
            //         // }
            //         // else
            //         {
            //             bridgeToWin(line);
            //         }
            //     }

            //     log($"WSL -> Bridge closed.");

            //     windowsCredProvider.StandardInput.Close();
            //     log($"Bridge -> WIN closed.");
            // });
            
            // reader.Start();

            windowsCredProvider.WaitForExit();

            log($"[{windowsCredProviderPath}] exited {windowsCredProvider.ExitCode}.");

            // reader.Join();
            // log($"[bridge thread exited]");

            return windowsCredProvider.ExitCode;
        }
    }
}
