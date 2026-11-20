using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace trophy_alert
{
    public partial class App : Application
    {
        private const string GameExeName = "odin_sphere_leifthrasir.exe";
        private const string GameProcName = "odin_sphere_leifthrasir";

        private DispatcherTimer _gameWatch;
        private bool _gameWasRunning;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppConfig.Load();

            string gamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GameExeName);

            if (AppConfig.ShowPreviewWindow)
            {
                // 有预览窗体时，关掉它就退出程序
                ShutdownMode = ShutdownMode.OnLastWindowClose;

                var main = new MainWindow();
                MainWindow = main;
                main.Show();
            }
            else
            {
                // 没有可见主窗口，只剩弹窗窗口，生命周期由自己控制
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // 既没有游戏也没有预览窗体，没必要常驻内存
                if (!File.Exists(gamePath))
                {
                    Shutdown();
                    return;
                }
            }

            LaunchGame();

            // 进度文件一变就弹奖杯提示
            ProgressWatcher.Start();

            // 游戏进程一旦消失，自己跟着退出
            _gameWatch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _gameWatch.Tick += (s, args) => CheckGameAlive();
            _gameWatch.Start();
        }

        /// <summary>
        /// 启动同目录下的游戏主程序；已在运行则不再重复拉起。
        /// </summary>
        private static void LaunchGame()
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string exe = Path.Combine(dir, GameExeName);

                if (!File.Exists(exe)) return;
                if (Process.GetProcessesByName(GameProcName).Length > 0) return;

                Process.Start(new ProcessStartInfo(exe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = dir
                });
            }
            catch
            {
                // 拉不起来游戏不影响自己的界面
            }
        }

        private void CheckGameAlive()
        {
            bool running;
            try
            {
                running = Process.GetProcessesByName(GameProcName).Length > 0;
            }
            catch
            {
                return;
            }

            if (running)
            {
                _gameWasRunning = true;
                return;
            }

            // 曾经见过游戏进程，现在没了 —— 退出
            if (_gameWasRunning)
            {
                _gameWatch.Stop();
                ProgressWatcher.Stop();
                Shutdown();
            }
        }
    }
}
