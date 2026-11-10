using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ReleVoltage
{
    internal static class Program
    {
        private static Process pythonProcess;
        private static TaskbarVoltage taskbarVoltage;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            taskbarVoltage = new TaskbarVoltage();
            taskbarVoltage.FormClosed += (s, e) =>
            {
                if (pythonProcess != null)
                {
                    if (!pythonProcess.HasExited)
                        pythonProcess.Kill();

                    pythonProcess.Dispose();
                }
            };

            taskbarVoltage.StartMonitoring();
            start();
            Application.Run(taskbarVoltage);
        }

        private static void start()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "-u rele.py",
                WorkingDirectory = Application.StartupPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            pythonProcess = new Process { StartInfo = psi };

            pythonProcess.OutputDataReceived += (s, args) =>
            {
                if (args.Data == null) return;

                if (args.Data.StartsWith("VOLTAGE:"))
                {
                    string voltage = args.Data.Replace("VOLTAGE:", "");


                    taskbarVoltage.SetVoltage(voltage);

                }
            };

            pythonProcess.ErrorDataReceived += (s, args) =>
            {
                if (args.Data != null)
                    Debug.WriteLine(args.Data);
            };

            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();
        }
    }
}
