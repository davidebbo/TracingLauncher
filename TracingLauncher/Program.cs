using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TracingLauncher
{
    class Program
    {
        private const int FlushPeriod = 1000;

        static TextWriter _logWriter;
        private static Timer _timer;
        private static string _logPrefix;

        private static StringBuilder _infoStringBuilder = new StringBuilder();
        private static StringBuilder _errorStringBuilder = new StringBuilder();
        private static DateTime _infoLogTime = DateTime.MinValue;
        private static DateTime _errorLogTime = DateTime.MinValue;

        static int Main(string[] args)
        {
            bool loggingEnabled = Environment.GetEnvironmentVariable("TRACINGLAUNCHER_ENABLE_LOGGING") == "1";
            string roleInstance = Environment.GetEnvironmentVariable("RoleInstance") ?? Environment.MachineName;

            string dir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            string logFilePath = Path.Combine(dir, $"TracingLauncherLogs_{roleInstance}.log");

            using (_logWriter = loggingEnabled ? new StreamWriter(logFilePath) : TextWriter.Null)
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
            if (args.Length < 2)
            {
                Console.WriteLine("Syntax: TracingLauncher.exe LogStringPrefixRegex SomeApp.exe args");
                return 1;
            }

            _logPrefix = args[0];
            string app = args[1];
            string arguments = args[2];

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

            // Periodically flush the logs received from stdout and stderr
            _timer = new Timer(FlushPeriod);
            _timer.Elapsed += FlushLogs;
            _timer.Start();

            Task outputTask = ProcessLinesFromReader(process.StandardOutput, TraceInformation);
            Task errorTask = ProcessLinesFromReader(process.StandardError, TraceError);

            await Task.WhenAll(outputTask, errorTask);

            process.WaitForExit();

            _timer.Stop();
            FlushInformation();
            FlushError();

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

        static void TraceInformation(string line)
        {
            lock (_infoStringBuilder)
            {
                if (Regex.IsMatch(line, _logPrefix))
                {
                    FlushInformation();
                }

                _infoStringBuilder.AppendLine(line);
                _infoLogTime = DateTime.UtcNow;
            }
        }

        static void TraceError(string line)
        {
            lock (_errorStringBuilder)
            {
                if (Regex.IsMatch(line, _logPrefix))
                {
                    FlushError();
                }

                _errorStringBuilder.AppendLine(line);
                _errorLogTime = DateTime.UtcNow;
            }
        }

        // Flush logs received on stdout
        static void FlushInformation()
        {
            if (_infoStringBuilder.Length > 0)
            {
                Trace.TraceInformation(_infoStringBuilder.ToString());
                _infoStringBuilder.Clear();
            }
        }

        // Flush logs received on stderr
        static void FlushError()
        {
            if (_errorStringBuilder.Length > 0)
            {
                Trace.TraceInformation(_errorStringBuilder.ToString());
                _errorStringBuilder.Clear();
            }
        }

        // Flush logs received on stdout and stderr
        private static void FlushLogs(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            lock (_infoStringBuilder)
            {
                if ((DateTime.UtcNow - _infoLogTime).Ticks > FlushPeriod)
                {
                    FlushInformation();
                }
            }

            lock (_errorStringBuilder)
            {
                if ((DateTime.UtcNow - _errorLogTime).Ticks > FlushPeriod)
                {
                    FlushError();
                }
            }
        }
    }
}
