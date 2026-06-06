using System.Runtime.InteropServices;

namespace TEST_101
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [STAThread]
        static void Main(string[] args)
        {
            // 有命令行参数 → 终端运行模式
            if (args.Length > 0)
            {
                ConsoleRunner.TryRun(args);
                return;
            }

            // 无参数 → 正常启动 WinForms 窗体，隐藏多余控制台窗口
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
                ShowWindow(handle, 0); // SW_HIDE

            ApplicationConfiguration.Initialize();
            Application.Run(new ModbusForm());
        }
    }
}