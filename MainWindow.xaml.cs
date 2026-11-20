using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace trophy_alert
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => ApplyScale();
        }

        /// <summary>预览窗体同样按屏幕等比放大，免得在 2K/4K 屏上显得过小。</summary>
        private void ApplyScale()
        {
            double scale = TrophyToast.DisplayScale;
            if (scale <= 1.0001) return;

            var root = Content as FrameworkElement;
            if (root == null) return;

            root.LayoutTransform = new ScaleTransform(scale, scale);
            Width = Width * scale;
            Height = Height * scale;
        }

        /// <summary>
        /// 手动触发：读进度文件里时间戳最新的解锁记录，按它的成就 ID 弹一次。
        /// 自动弹窗由 ProgressWatcher 负责。
        /// </summary>
        private void OnReadProgressClick(object sender, RoutedEventArgs e)
        {
            TrophyPaths.Invalidate();
            string progress = TrophyPaths.ProgressFile;

            try
            {
                if (!File.Exists(progress))
                {
                    MessageBox.Show(this, "找不到进度文件：\n" + progress, "读取进度",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int id = ProgressWatcher.ReadLatestUnlockedId(progress);
                if (id < 0)
                {
                    MessageBox.Show(this, "进度文件里没有解锁记录：\n" + progress, "读取进度",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                TrophyAlert.Show(id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "读取失败：" + ex.Message, "读取进度",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
