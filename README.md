# trophy\_alert · 奥丁领域奖杯提示插件

《奥丁领域：里普特拉西尔》PC 移植版（[odin-sphere-leifthrasir-pc](https://github.com/saulob/odin-sphere-leifthrasir-pc)）的配套小工具：解锁奖杯的瞬间，在屏幕右下角滑出一条类似 Steam 成就提示的横条，同时播一声音效。

移植版的奖杯是纯本地的，只记在 `user\trophy\` 里、在启动器的 Trophies 页列表显示，游戏过程中没有任何即时提示。这个插件补的就是这一下。

- 产物是单文件 `start.exe`，扔进移植版目录双击即可，不需要装任何运行库（Win10 1903+ / Win11 自带 .NET Framework 4.8）
- 双击后它自己把 `odin_sphere_leifthrasir.exe` 拉起来；游戏退出，插件跟着退出，不留后台进程
- 只读游戏写出来的进度文本，不改游戏文件、不改存档、不进游戏内存

## 截图

正常解锁时（176×70，贴主屏右下角）：

![另一个成就](screenshots/toast-2.png)

2 倍缩放（4K 屏上的实际观感，与 1080p 下等比一致）：

![4K 缩放](screenshots/scaling-2x.png)

## 特性

- **右下角弹窗**：从下往上滑入、淡入，停留 3.6 秒后淡出；贴主屏工作区右下角，任务栏在侧面或顶部也能正确贴边
- **音效**：mp3 以资源形式内嵌在 exe 里，首次播放时释放到临时目录
- **文案与图标**：标题/描述按成就 ID 取自内嵌的奖杯表；图标优先用 `user\trophy\<集ID>\Icons\TROP{ID}.PNG`，取不到就回退内置的白金杯图标
- **自动识别区域版本**：不写死奖杯集 ID，扫 `user\trophy\NPWR*` 挑进度文件最近写入的那个，港版/亚洲版、美版、日版、欧版都能直接用
- **按时间戳取最新**：进度文件里行的顺序不代表解锁顺序（游戏会插着写），取时间戳最大的那条；同一时间戳取靠后那行。同一批写入多个成就时只弹最后一个，不会连着一串
- **容错**：进度文件、`user` 目录甚至整个奖杯目录还不存在时也不会失去监听——watcher 挂到最贴近的已存在目录上，目录逐级出现后自动收敛过去，另有 1 秒轮询兜底
- **进程联动**：启动时拉起游戏（已在运行则跳过）；500 毫秒轮询游戏进程，游戏一关自己也退
- **隐藏形态**：两个窗口都不显示在任务栏，不进 Alt+Tab
- **DPI/分辨率自适应**：以 1080p 为基准，按主屏逻辑宽度等比缩放（上限 2 倍），4K\@200% 与 4K\@100% 都是同样的观感比例

## 环境

- Windows 10 1903 或更高 / Windows 11，64 位
- .NET Framework 4.8（Win10 1903+ 与 Win11 系统自带，无需安装）
- 一份能正常运行的移植版本体

## 安装与使用

1. 把 `start.exe` 和 `trophy_alert_config.json` 拷到移植版目录，与 `odin_sphere_leifthrasir.exe` 同层
2. 双击 `start.exe` 进游戏（之后不用再管它）
3. 解锁奖杯时右下角自动弹提示；退出游戏时插件同步退出

删掉这两个文件即卸载，游戏文件与存档一个字节都不会被动到。

## 配置

`trophy_alert_config.json` 与 `start.exe` 放同一目录：

```json
{
  "showPreviewWindow": false,
  "comment": "showPreviewWindow = true 时启动会显示预览窗体；false（默认）时程序只在后台监听进度文件，不占任务栏"
}
```

| 键                   | 默认      | 说明                                                    |
| ------------------- | ------- | ----------------------------------------------------- |
| `showPreviewWindow` | `false` | 为 `true` 时启动会多开一个小预览窗，上面有「弹最新解锁的成就」按钮，方便在没解锁任何东西时测试效果 |

文件缺失、JSON 写错或键名写错都会回退到默认值，不会影响启动。

## 它是怎么工作的

1. **定位数据**：`TrophyPaths` 扫 `user\trophy\NPWR*`，取进度文件写入时间最新的目录；一个都没有时退回 `NPWR07394_00`（港版/亚洲版）。结果按需缓存，目录结构变化时失效重扫。
2. **监听**：`ProgressWatcher` 把 `FileSystemWatcher` 挂到最贴近进度文件、且当前存在的目录上（文件不存在时逐级上溯），只接受路径形如 `user\trophy\NPWR*\progress_user*.txt` 的事件，避免 Icons、Xml 之类的写入误触发。文件事件做 500 毫秒防抖合并，另有 1 秒定时器负责目录收敛、文件出现补弹和 watcher 重建。
3. **解析**：打开文件时用共享读写 + 最多 8 次重试（60 毫秒间隔），逐行匹配 `unlocked <id> <时间戳>`，取时间戳最大的一条。
4. **出图**：`TrophyAlert` 用 ID 查 `TrophyDatabase` 拿到名称与描述，用 `TrophyPaths.IconsDir` 拼出图标路径，交给 `TrophyToast`。
5. **弹窗**：`TrophyToast` 是 176×70 的无边框透明窗口，`Root` 上套 `TranslateTransform` 做滑入、窗口自身做透明度淡入淡出；`DisplayScale` 按主屏逻辑宽度算缩放，窗口尺寸与内容一起按 `ScaleTransform` 放大。
6. **生命周期**：`App.OnStartup` 读配置、按配置建预览窗、设 `ShutdownMode`、拉起游戏、启动监听；`DispatcherTimer` 每 500 毫秒检查游戏进程，游戏没了就退出。

## 构建

用 Visual Studio 2022（需勾选「.NET 桌面开发」和 .NET Framework 4.8 目标包）打开 `trophy_alert.slnx`，或者命令行：

```powershell
MSBuild.exe trophy_alert.csproj /p:Configuration=Release
```

**注意**：`trophy_alert.csproj` 里 Debug/Release 的 `<OutputPath>` 写的是作者本机的游戏目录，请改成你自己的移植版目录，或改回 `bin\Release\`。

如果新增了 `Assets` 下的资源条目，经典 csproj 的增量构建可能不重新打包，需要用 `/t:Rebuild`。

## 目录结构

```
trophy_alert/
├─ App.xaml(.cs)             启动流程：读配置、建预览窗、拉起游戏、启动监听、游戏退出即退出
├─ MainWindow.xaml(.cs)      可选的预览窗，一个「弹最新解锁的成就」按钮
├─ TrophyToast.xaml(.cs)     弹窗本体：滑入、淡出、音效、按屏幕等比缩放
├─ TrophyAlert.cs            出图入口：ID → 文案 + 图标 → 弹窗
├─ TrophyDatabase.cs         读内嵌的 Assets/trophies.xml（XDocument 懒加载）
├─ TrophyPaths.cs            定位奖杯集目录 / 进度文件 / Icons，自动识别区域
├─ ProgressWatcher.cs        文件监听、防抖、目录收敛、解析最新 unlocked
├─ AppConfig.cs              读 trophy_alert_config.json
├─ Assets/                   toast_bg.png、trophy_icon.png、trophy.mp3、trophies.xml
├─ app.ico                   exe 图标
├─ trophy_alert_config.json  默认配置，随构建拷到输出目录
└─ screenshots/              README 用的演示图
```

## 数据与素材说明

- `Assets/trophies.xml`：48 条奖杯的名称与描述，由作者从自己游戏包里的 `TROP_10.XML` 提取并转为简体，作为资源编译进 exe
- `Assets/trophy_icon.png`（白金杯）、`Assets/toast_bg.png`（底板）、`Assets/trophy.mp3`（音效）：取自游戏本体与作者自己的 PS4 奖杯弹窗截图
- 上述素材的版权归 Vanillaware / Atlus 等原权利人所有，本仓库不包含任何游戏本体文件。若要长期公开分发，建议把这几项换成自制的占位素材

## 已知限制

- 只面向移植版的数据格式（`user\trophy\<NPWR>\progress_user*.txt`），不是通用的 PS4 奖杯库读取工具
- 弹窗固定贴**主屏**右下角；多显示器时不会跑到游戏所在那块屏
- 尺寸以 1080p 为基准等比放大，上限 2 倍；缩放比例在首次弹出时确定，运行期间改分辨率或系统缩放需要重启
- 独占全屏（exclusive fullscreen）可能盖住置顶窗口；移植版只提供窗口与无边框全屏，所以正常可见
- 同一批写入的多个成就只弹时间戳最新的那个，这是有意的设计

## 协议

代码采用 MIT 协议，详见 [LICENSE](LICENSE)。`Assets/` 下的游戏素材不在协议范围内。

本插件为个人兴趣作品，与 Vanillaware、Atlus、SEGA、索尼互动娱乐及 odin-sphere-leifthrasir-pc 项目的作者均无关，也未获得其背书。
