using System;
using System.Collections.Generic;
using System.Windows;
using System.Xml.Linq;

namespace trophy_alert
{
    /// <summary>
    /// 成就定义表。数据来自 Assets\trophies.xml（打进 exe 的 Resource），
    /// 按成就 ID 取对应的 name / detail，代码里不写死任何文案。
    /// </summary>
    public static class TrophyDatabase
    {
        public sealed class TrophyInfo
        {
            public int Id;
            public string Name;
            public string Detail;
            /// <summary>奖杯类型：P 白金 / G 金 / S 银 / B 铜</summary>
            public string Type;
        }

        private static Dictionary<int, TrophyInfo> _map;
        private static bool _loaded;
        private static string _gameTitle = "";

        /// <summary>游戏名（trophyset/@title），作为找不到对应 ID 时的兜底标题</summary>
        public static string GameTitle
        {
            get { EnsureLoaded(); return _gameTitle; }
        }

        /// <summary>取某个 ID 的成就信息，不存在返回 null。</summary>
        public static TrophyInfo Get(int id)
        {
            EnsureLoaded();
            TrophyInfo info;
            return _map != null && _map.TryGetValue(id, out info) ? info : null;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            _map = new Dictionary<int, TrophyInfo>();

            try
            {
                var res = Application.GetResourceStream(
                    new Uri("Assets/trophies.xml", UriKind.Relative));
                if (res == null || res.Stream == null) return;

                XDocument doc;
                using (var fs = res.Stream)
                {
                    doc = XDocument.Load(fs);
                }

                var root = doc.Root;
                if (root == null) return;

                _gameTitle = Attr(root, "title");
                foreach (var el in root.Elements("trophy"))
                {
                    int id;
                    if (!int.TryParse(Attr(el, "id"), out id)) continue;

                    var info = new TrophyInfo
                    {
                        Id = id,
                        Name = Attr(el, "name"),
                        Detail = Attr(el, "detail"),
                        Type = Attr(el, "type")
                    };
                    _map[id] = info;
                }
            }
            catch
            {
                // 资源缺失或格式异常时保持空表，由调用方兜底
            }
        }

        private static string Attr(XElement el, string name)
        {
            var a = el.Attribute(name);
            return a == null ? "" : a.Value;
        }
    }
}
