using System;
using System.IO;

namespace trophy_alert
{
    /// <summary>
    /// 定位奖杯数据目录。不同区域版本的奖杯集 ID 不同（港版/亚洲版是 NPWR07394_00，
    /// 美版、日版、欧版各是别的名字），所以不写死：扫 user\trophy 下所有 NPWR* 目录，
    /// 取进度文件最近被写入的那个；一个都没有时退回港版/亚洲版的默认目录。
    /// </summary>
    public static class TrophyPaths
    {
        /// <summary>港版 / 亚洲版（CUSA03873）的奖杯集 ID，作为默认与兜底</summary>
        public const string DefaultSetId = "NPWR07394_00";

        private static readonly object Gate = new object();
        private static bool _resolved;
        private static string _setDir;
        private static string _progressFile;

        /// <summary>程序所在目录，即游戏根目录</summary>
        public static string GameRoot
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>user\trophy</summary>
        public static string TrophyRoot
        {
            get { return Path.Combine(GameRoot, "user", "trophy"); }
        }

        /// <summary>当前使用的奖杯集目录；目录还没建时给出默认路径（不保证存在）</summary>
        public static string SetDir
        {
            get
            {
                lock (Gate)
                {
                    if (!_resolved) DoResolve();
                    return _setDir ?? Path.Combine(TrophyRoot, DefaultSetId);
                }
            }
        }

        /// <summary>当前要读的进度文件；文件还没出现时给出默认路径</summary>
        public static string ProgressFile
        {
            get
            {
                lock (Gate)
                {
                    if (!_resolved) DoResolve();
                    if (_progressFile != null) return _progressFile;
                    return Path.Combine(_setDir ?? Path.Combine(TrophyRoot, DefaultSetId),
                                        "progress_user1.txt");
                }
            }
        }

        /// <summary>Icons 目录（TROPxxx.PNG 放在这里）</summary>
        public static string IconsDir
        {
            get { return Path.Combine(SetDir, "Icons"); }
        }

        /// <summary>目录结构变了之后丢掉缓存，下次访问重新扫一遍</summary>
        public static void Invalidate()
        {
            lock (Gate)
            {
                _resolved = false;
                _setDir = null;
                _progressFile = null;
            }
        }

        /// <summary>
        /// 扫 user\trophy\NPWR*：有进度文件的目录优先，其中写时间最新的胜出；
        /// 都没有进度文件时，看目录本身的写入时间。
        /// </summary>
        private static void DoResolve()
        {
            _resolved = true;
            _setDir = null;
            _progressFile = null;

            try
            {
                if (!Directory.Exists(TrophyRoot)) return;

                string bestDir = null;
                string bestFile = null;
                long bestScore = long.MinValue;
                bool bestHasProgress = false;

                foreach (string dir in Directory.GetDirectories(TrophyRoot, "NPWR*"))
                {
                    string newest = NewestProgressFile(dir);
                    bool hasProgress = newest != null;
                    long score = hasProgress ? SafeWriteTimeTicks(newest) : SafeDirTicks(dir);

                    if (bestDir == null
                        || (hasProgress && !bestHasProgress)
                        || (hasProgress == bestHasProgress && score > bestScore))
                    {
                        bestDir = dir;
                        bestFile = newest;
                        bestScore = score;
                        bestHasProgress = hasProgress;
                    }
                }

                _setDir = bestDir;
                _progressFile = bestFile;
            }
            catch
            {
                // 读不到就当还没建，等下一次重扫
            }
        }

        private static string NewestProgressFile(string dir)
        {
            string newest = null;
            long newestTicks = long.MinValue;

            try
            {
                foreach (string f in Directory.GetFiles(dir, "progress_user*.txt"))
                {
                    long ticks = SafeWriteTimeTicks(f);
                    if (ticks > newestTicks)
                    {
                        newestTicks = ticks;
                        newest = f;
                    }
                }
            }
            catch
            {
            }
            return newest;
        }

        private static long SafeWriteTimeTicks(string path)
        {
            try { return File.GetLastWriteTimeUtc(path).Ticks; }
            catch { return long.MinValue; }
        }

        private static long SafeDirTicks(string dir)
        {
            try { return Directory.GetLastWriteTimeUtc(dir).Ticks; }
            catch { return long.MinValue; }
        }
    }
}
