using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace trophy_alert
{
    /// <summary>
    /// exe 同目录下的 config.json 配置。读不到或写错时用默认值。
    /// </summary>
    public static class AppConfig
    {
        public const string FileName = "trophy_alert_config.json";

        /// <summary>是否显示预览窗体（默认后台运行，不显示）</summary>
        public static bool ShowPreviewWindow { get; private set; }

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        static AppConfig()
        {
            ShowPreviewWindow = false;
        }

        public static void Load()
        {
            ShowPreviewWindow = false;

            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return;

                string text = File.ReadAllText(path, Encoding.UTF8);
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);

                var serializer = new JavaScriptSerializer();
                var dict = serializer.Deserialize<Dictionary<string, object>>(text);
                if (dict == null) return;

                object value;
                if (dict.TryGetValue("showPreviewWindow", out value))
                {
                    bool parsed;
                    if (TryGetBool(value, out parsed)) ShowPreviewWindow = parsed;
                }
            }
            catch
            {
                // 配置异常就按默认：不显示预览窗体
            }
        }

        private static bool TryGetBool(object value, out bool result)
        {
            result = false;

            if (value is bool)
            {
                result = (bool)value;
                return true;
            }

            string s = value as string;
            if (s == null) return false;

            s = s.Trim();
            if (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1")
            {
                result = true;
                return true;
            }
            if (s.Equals("false", StringComparison.OrdinalIgnoreCase) || s == "0")
            {
                result = false;
                return true;
            }
            return false;
        }
    }
}
