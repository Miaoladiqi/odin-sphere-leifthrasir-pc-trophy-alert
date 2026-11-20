using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace trophy_alert
{
    /// <summary>
    /// 成就 / 奖杯解锁弹窗：右下角贴边，从下往上滑入，同时播放解锁音效，停留后淡出。
    /// </summary>
    public partial class TrophyToast : Window
    {
        private readonly DispatcherTimer _life;

        /// <summary>滑入位移（DIP），从下往上</summary>
        private const double SlideOffset = 18;
        /// <summary>距屏幕边缘留白：右下角贴边为 0</summary>
        private const double ScreenMarginRight = 0;
        private const double ScreenMarginBottom = 0;
        /// <summary>音效音量 0~1</summary>
        private const double SoundVolume = 1.0;

        /// <summary>基准尺寸：按 1080p（逻辑宽 1920）调好的 176×70</summary>
        private const double BaseWidth = 176;
        private const double BaseHeight = 70;
        /// <summary>基准屏幕逻辑宽度，即 1080p</summary>
        private const double ReferenceScreenWidth = 1920;
        /// <summary>放大上限，避免超宽屏上弹窗过大</summary>
        private const double MaxScale = 2.0;

        private static double _displayScale = -1;

        /// <summary>本次实际使用的滑入位移，随缩放放大</summary>
        private double _slideOffset = SlideOffset;

        /// <summary>
        /// 以 1080p 为基准的显示缩放：主屏逻辑宽度 / 1920，限制在 1.0 ~ 2.0。
        /// 逻辑宽度随系统缩放变化，所以 4K@200%（逻辑 1920）和 4K@100%（逻辑 3840）
        /// 都会得到 ≈320 物理像素宽，和 1080p 下的观感一致。
        /// </summary>
        public static double DisplayScale
        {
            get
            {
                if (_displayScale < 0)
                {
                    double width = SystemParameters.PrimaryScreenWidth;
                    double scale = width > 0 ? width / ReferenceScreenWidth : 1.0;

                    if (scale < 1.0) scale = 1.0;
                    if (scale > MaxScale) scale = MaxScale;
                    _displayScale = scale;
                }
                return _displayScale;
            }
        }

        private static MediaPlayer _sound;
        private static string _soundFile;
        private static bool _soundOpened;
        private static bool _soundPending;

        public TrophyToast()
        {
            InitializeComponent();

            _life = new DispatcherTimer();
            _life.Tick += (s, e) =>
            {
                _life.Stop();
                FadeOut();
            };
        }

        /// <summary>
        /// 弹出一次提示。iconPath 为空时使用内置的白金杯图标。
        /// </summary>
        public static TrophyToast Pop(string title, string subtitle, string iconPath = null,
                                      double staySeconds = 3.6)
        {
            var app = Application.Current;
            if (app != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(new Action(() => Pop(title, subtitle, iconPath, staySeconds)));
                return null;
            }

            var toast = new TrophyToast();
            toast.TitleText.Text = title;
            toast.SubtitleText.Text = subtitle;
            toast._life.Interval = TimeSpan.FromSeconds(staySeconds);

            // 按屏幕逻辑宽度等比放大，保持与 1080p 观感一致
            toast.ApplyScale(DisplayScale);

            if (!string.IsNullOrEmpty(iconPath))
                toast.SetIcon(iconPath);

            Rect area = SystemParameters.WorkArea;
            toast.Left = area.Right - toast.Width - ScreenMarginRight;
            toast.Top = area.Bottom - toast.Height - ScreenMarginBottom;

            toast.Show();
            toast.PlayEnter();
            PlaySound();
            return toast;
        }

        /// <summary>
        /// 等比放大整个弹窗：窗口尺寸和内容一起缩放，滑入位移同步放大。
        /// </summary>
        private void ApplyScale(double scale)
        {
            if (scale <= 1.0001) return;

            Width = BaseWidth * scale;
            Height = BaseHeight * scale;
            Root.LayoutTransform = new ScaleTransform(scale, scale);
            _slideOffset = SlideOffset * scale;
        }

        /// <summary>
        /// 用外部图片替换图标；文件读不进或不存在时保持内置图标。
        /// </summary>
        private void SetIcon(string path)
        {
            try
            {
                if (!File.Exists(path)) return;

                var bmp = new BitmapImage();
                using (var fs = File.OpenRead(path))
                {
                    bmp.BeginInit();
                    bmp.StreamSource = fs;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                }
                bmp.Freeze();

                TrophyImage.Source = bmp;
            }
            catch
            {
                // 换图标失败就用默认图标
            }
        }

        private void PlayEnter()
        {
            SlideTransform.Y = _slideOffset;

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var slide = new DoubleAnimation(_slideOffset, 0, TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = ease
            };
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slide);

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));

            _life.Start();
        }

        private void FadeOut()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(350));
            fade.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fade);
        }

        // ---------------- 音效 ----------------

        private static void PlaySound()
        {
            try
            {
                if (_sound == null)
                {
                    _sound = new MediaPlayer();
                    _sound.Volume = SoundVolume;
                    _sound.MediaOpened += (s, e) =>
                    {
                        _soundOpened = true;
                        TryStartSound();
                    };
                    _sound.MediaFailed += (s, e) => { _soundPending = false; };
                    _sound.Open(new Uri(GetSoundFile(), UriKind.Absolute));
                }

                _soundPending = true;
                TryStartSound();
            }
            catch
            {
                // 播不出来不影响弹窗
            }
        }

        private static void TryStartSound()
        {
            if (!_soundPending || _sound == null || !_soundOpened) return;

            try
            {
                _sound.Position = TimeSpan.Zero;
                _sound.Play();
                _soundPending = false;
            }
            catch
            {
                // 忽略
            }
        }

        /// <summary>
        /// 音效以 Resource 打进 exe，首次使用时释放到临时目录再交给 MediaPlayer。
        /// </summary>
        private static string GetSoundFile()
        {
            if (!string.IsNullOrEmpty(_soundFile) && File.Exists(_soundFile))
                return _soundFile;

            var res = Application.GetResourceStream(new Uri("Assets/trophy.mp3", UriKind.Relative));
            var path = Path.Combine(Path.GetTempPath(), "trophy_alert_toast.mp3");

            using (var fs = File.Create(path))
            {
                res.Stream.CopyTo(fs);
            }

            _soundFile = path;
            return path;
        }
    }
}
