using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TracingLauncher
{
    class Program
    {
        static TextWriter _logWriter;

        static int Main(string[] args)
        {
            string dir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            string logFilePath = Path.Combine(dir, "TracingLauncherLogs.log");

            using (_logWriter = new StreamWriter(logFilePath))
            {
                _logWriter.WriteLine("Starting TracingLauncher");

                try
                {
                    return MainAsync(args).Result;
                }
                catch (Exception e)
                {
                    _logWriter.WriteLine(e.ToString());
                    Console.WriteLine(e.ToString());
                    return 1;
                }
            }
        }

        static async Task<int> MainAsync(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Syntax: TracingLauncher.exe SomeApp.exe args");
                return 1;
            }

            string app = args[0];
            string arguments = args[1];

            _logWriter.WriteLine($"App: {args[0]}");
            _logWriter.WriteLine($"Args: {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = app,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                ErrorDialog = false,
                Arguments = arguments
            };

            var process = new Process()
            {
                StartInfo = psi
            };

            process.Start();

            Task outputTask = ProcessLinesFromReader(process.StandardOutput, line => { Trace.TraceInformation(line); });
            Task errorTask = ProcessLinesFromReader(process.StandardError, line => { Trace.TraceError(line); });

            await Task.WhenAll(outputTask, errorTask);

            process.WaitForExit();

            return process.ExitCode;
        }

        static async Task ProcessLinesFromReader(TextReader reader, Action<string> action)
        {
            for (; ; )
            {
                string line = await reader.ReadLineAsync();
                if (line == null) return;

                action(line);
            }
        }
    }
}
