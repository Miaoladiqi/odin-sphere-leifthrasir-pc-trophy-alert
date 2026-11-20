using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace trophy_alert
{
    /// <summary>
    /// 监听 user\trophy\NPWR*\progress_user*.txt（奖杯集目录按最近写入自动识别）：
    /// 文件一被写入就弹提示，内容取文件里时间戳最新的那条 unlocked 记录的成就 ID。
    /// 目录或文件还不存在时（首次游玩、游戏还没建）不会报错，
    /// 而是监听上层目录并在目录逐级出现后自动挂到正确位置。
    /// </summary>
    public static class ProgressWatcher
    {
        /// <summary>写文件抖动合并窗口</summary>
        private const int DebounceMs = 500;
        /// <summary>目录/文件出现后重新挂监听的检查周期</summary>
        private const int ScopeCheckMs = 1000;

        private static readonly Regex UnlockedLine =
            new Regex(@"^\s*unlocked\s+(\d+)\s+(\d+)\s*$", RegexOptions.IgnoreCase);

        private static FileSystemWatcher _watcher;
        private static string _watchDir;
        private static DispatcherTimer _debounce;
        private static DispatcherTimer _scope;
        private static string _baseDir;
        /// <summary>最近一次触发弹窗的事件文件（后台线程写，UI 线程读）</summary>
        private static string _pendingPath;
        /// <summary>是否已知进度文件存在（启动时为 false 时，之后出现会补弹一次）</summary>
        private static bool _fileKnown;

        public static void Start()
        {
            try
            {
                var app = Application.Current;
                if (app == null) return;

                _baseDir = TrophyPaths.GameRoot;
                _fileKnown = File.Exists(TrophyPaths.ProgressFile);
                Attach();

                _debounce = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher);
                _debounce.Interval = TimeSpan.FromMilliseconds(DebounceMs);
                _debounce.Tick += (s, e) =>
                {
                    _debounce.Stop();
                    HandleChange();
                };

                // 目录/文件可能还不存在，定期重新评估该监听的层级
                _scope = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher);
                _scope.Interval = TimeSpan.FromMilliseconds(ScopeCheckMs);
                _scope.Tick += (s, e) => EnsureScope();
                _scope.Start();
            }
            catch
            {
                // 监听起不来不影响弹窗程序和游戏联动
            }
        }

        public static void Stop()
        {
            try
            {
                if (_scope != null) _scope.Stop();
                if (_debounce != null) _debounce.Stop();
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                    _watchDir = null;
                }
            }
            catch
            {
            }
        }

        // ---------------- 监听挂载 ----------------

        /// <summary>把 watcher 挂到最贴近进度文件、且当前已存在的目录上。</summary>
        private static void Attach()
        {
            try
            {
                string dir = ResolveWatchDir();
                if (dir == null) return;
                if (_watcher != null && string.Equals(_watchDir, dir, StringComparison.OrdinalIgnoreCase))
                    return;

                if (_watcher != null)
                {
                    try
                    {
                        _watcher.EnableRaisingEvents = false;
                        _watcher.Dispose();
                    }
                    catch
                    {
                    }
                    _watcher = null;
                }

                var w = new FileSystemWatcher(dir);
                w.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
                                 | NotifyFilters.CreationTime | NotifyFilters.DirectoryName;
                w.Filter = "*";
                w.IncludeSubdirectories = false;
                w.InternalBufferSize = 64 * 1024;
                w.Changed += OnFsEvent;
                w.Created += OnFsEvent;
                w.Deleted += OnFsEvent;
                w.Renamed += OnFsEvent;
                w.Error += OnFsError;
                w.EnableRaisingEvents = true;

                _watcher = w;
                _watchDir = dir;
            }
            catch
            {
                // 下一轮检查再试
            }
        }

        /// <summary>从当前奖杯集目录往上找第一个已存在的目录。</summary>
        private static string ResolveWatchDir()
        {
            string dir = TrophyPaths.SetDir;
            while (dir != null && !Directory.Exists(dir))
            {
                if (string.Equals(dir, _baseDir, StringComparison.OrdinalIgnoreCase)) break;
                dir = Path.GetDirectoryName(dir);
            }
            return Directory.Exists(dir) ? dir : null;
        }

        /// <summary>目录结构变化后再看一次：该挂哪、文件是否已经出现。</summary>
        private static void EnsureScope()
        {
            try
            {
                TrophyPaths.Invalidate();   // 可能新出现/切换了奖杯集目录
                Attach();

                bool exists = File.Exists(TrophyPaths.ProgressFile);
                if (exists && !_fileKnown)
                {
                    _fileKnown = true;
                    _pendingPath = null;    // 文件刚出现，按一次更新处理
                    Kick();
                }
                else if (!exists)
                {
                    _fileKnown = false;
                }
            }
            catch
            {
            }
        }

        // ---------------- 事件 ----------------

        private static void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            if (IsTargetFile(e.FullPath))
            {
                _fileKnown = true;
                _pendingPath = e.FullPath;
                Kick();
                return;
            }

            // 别的条目变化（例如奖杯集目录被创建），安排一次监听层级重估
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(EnsureScope));
        }

        private static void OnFsError(object sender, ErrorEventArgs e)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                _watchDir = null;   // 强制重建
                EnsureScope();
                Kick();
            }));
        }

        /// <summary>
        /// 是否是某个奖杯集目录下的进度文件：user\trophy\NPWRxxxxx_00\progress_user*.txt。
        /// 只看路径形状，不比对某一个写死的目录名，换区的奖杯集也能命中。
        /// </summary>
        private static bool IsTargetFile(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return false;

            string name = Path.GetFileName(fullPath);
            if (name == null) return false;
            if (!name.StartsWith("progress_user", StringComparison.OrdinalIgnoreCase)) return false;
            if (!name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return false;

            string dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir)) return false;

            string root = TrophyPaths.TrophyRoot;
            string prefix = root + Path.DirectorySeparatorChar;
            if (!dir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

            // 只接受直接位于 user\trophy 下的 NPWR 目录，避免更深层级同名文件误触发
            string setName = dir.Substring(prefix.Length);
            return setName.Length > 0
                && setName.StartsWith("NPWR", StringComparison.OrdinalIgnoreCase)
                && setName.IndexOf(Path.DirectorySeparatorChar) < 0;
        }

        /// <summary>文件系统事件在后台线程，切到 UI 线程做防抖。</summary>
        private static void Kick()
        {
            var app = Application.Current;
            if (app == null || _debounce == null) return;

            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                _debounce.Stop();
                _debounce.Start();
            }));
        }

        private static void HandleChange()
        {
            try
            {
                string path = _pendingPath;
                _pendingPath = null;

                TrophyPaths.Invalidate();
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    path = TrophyPaths.ProgressFile;

                int id = ReadLatestUnlockedId(path);
                if (id >= 0) TrophyAlert.Show(id);
            }
            catch
            {
                // 单次读失败忽略，下次变化再处理
            }
        }

        /// <summary>
        /// 读出文件里时间戳最新的一条 unlocked 记录的成就 ID，没有则返回 -1。
        /// 文件里的行顺序不代表解锁先后（游戏会插着写），所以按时间戳比大小。
        /// 时间戳相同时取靠后的那一行。文件可能不存在，也可能正被游戏写着，
        /// 因此共享读写打开并重试几次。
        /// </summary>
        public static int ReadLatestUnlockedId(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                try
                {
                    if (!File.Exists(path)) return -1;

                    int bestId = -1;
                    long bestTs = -1;

                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                   FileShare.ReadWrite | FileShare.Delete))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            var m = UnlockedLine.Match(line);
                            if (!m.Success) continue;

                            long ts = long.Parse(m.Groups[2].Value);
                            if (ts >= bestTs)
                            {
                                bestTs = ts;
                                bestId = int.Parse(m.Groups[1].Value);
                            }
                        }
                    }
                    return bestId;
                }
                catch (IOException)
                {
                    Thread.Sleep(60);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(60);
                }
                catch (FormatException)
                {
                    return -1;
                }
            }
            return -1;
        }
    }
}
