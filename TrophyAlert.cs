using System.IO;

namespace trophy_alert
{
    /// <summary>
    /// 弹窗出口：按成就 ID 从 TrophyDatabase 取文案、从 Icons 取图标，然后交给 TrophyToast。
    /// </summary>
    public static class TrophyAlert
    {
        /// <summary>Icons\TROPxxx.PNG 的完整路径（三位补零）</summary>
        public static string IconPathFor(int id)
        {
            return Path.Combine(TrophyPaths.IconsDir,
                                string.Format(@"TROP{0:D3}.PNG", id));
        }

        /// <summary>按成就 ID 弹一次：文案取成就表，图标取 Icons 目录。</summary>
        public static void Show(int id)
        {
            var info = TrophyDatabase.Get(id);

            string title = info != null && !string.IsNullOrEmpty(info.Name)
                ? info.Name
                : TrophyDatabase.GameTitle;
            string detail = info != null ? info.Detail : "";

            TrophyToast.Pop(title, detail, IconPathFor(id));
        }
    }
}
