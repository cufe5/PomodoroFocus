# 番茄专注（PomodoroFocus）· Apple 设计语言（HIG & apple.com）视觉升级规格书

> 版本：v2.0-AppleRedesign（2026-11-20）　作者：`architect`　执行：`coder`　审核：`reviewer`
>
> **铁律约束（违反任意一条即视为失败）**：
> 1. **严禁执行任何 `git add / commit / push / tag / checkout / reset`** —— 所有修改仅保留在本地工作区，供用户直接预览。
> 2. **严禁修改或覆盖已入库的 `docs/screenshots/*.png`** —— 新生成的 10 张状态截图必须输出到 `%TEMP%\PomodoroFocus_apple_shots`（及 UITests 默认的 `%TEMP%\PomodoroFocus.UITests\<timestamp>\shots`）。
> 3. **工具链与零依赖约束不变**：`.NET Framework 4.8 (x64) + WPF + C# 5 (<LangVersion>5</LangVersion>)`，零 NuGet、不引入外部字体文件、全量文件保持 **UTF-8 with BOM**、`Release|x64` 与 `Debug|x64` **0 错误 0 警告**。
> 4. **100% 保持现有功能与自动化测试契约**：不得删减任何现有功能，不得更改任何 `AutomationProperties.AutomationId`、命令行参数、主题 Key（`midnight / mist / sunset / forest`）或 10 张截图的文件名。

---

## 1. 现有 10 张截图（`docs/screenshots/01..10.png`）与 Apple 设计语言差距诊断

通过逐张检视现有 `docs/screenshots/01..10.png` 并对照 **Apple Human Interface Guidelines (macOS Sequoia / Sonoma & iOS 18)** 与 **apple.com** 视觉规范，提炼出以下 8 大核心差距：

| 维度 | 现有视觉表现（`docs/screenshots/01..10.png`） | Apple 设计语言目标（Apple HIG / apple.com） |
|---|---|---|
| **1. 强调色与阶段色混淆** | 全局交互强调色 `Accent` 与专注珊瑚红绑定（浅色主题 `mist` 图 08 甚至用刺眼的猩红 `#E5484D` 渲染所有开关、滑块、图标和选中框，像报错界面）。 | **解耦系统交互色与计时阶段色**：通用交互（新建按钮、链接、选中框、滑块填充、焦点环）采用 **Apple System Blue（浅色 `#0071E3` / 深色 `#0A84FF`）**；开关开启态采用 **Apple System Green（浅色 `#34C759` / 深色 `#30D158`）**；计时阶段色采用 **Apple System Semantic Colors**（专注 System Red `#FF3B30`/`#FF453A`、短休 System Mint `#00C7BE`/`#63E6E2`、长休 System Indigo `#5856D6`/`#5E5CE6`）。 |
| **2. 背景与材质（Vibrancy）** | 图 01/04/06 左上和右下有面积过大、饱和度偏高的霓虹径向光斑（`Alpha=0.22` + `#244F46E5`），偏电竞/Web 暗黑风；浅色底 `#F3F5F9` 偏冷蓝灰。 | **克制纯净的 Apple 画布与分层材质**：浅色画布采用 apple.com 标志性 **`#F5F5F7`** + 纯白 `#FFFFFF` 悬浮卡片 + 极柔和漫射阴影（`Opacity=0.04`）；深色画布采用 Apple OLED/macOS 深空黑 **`#0D0D0F`** + `#1C1C1E` 二级分组卡片 + `#2C2C2E` 三级抬升控件面；背景氛围光晕降至极克制的 **`0.07..0.10`**（若隐若现的桌面色彩透射 Vibrancy）。 |
| **3. 侧边栏（Source List）** | 图 01..08 侧边栏顶部有一条横穿分隔线（`BorderThickness="0,1,1,0"`），选中项左侧有一根 `3×18` 竖条（典型 Win11 WinUI 风格，非 Apple），导航图标单调灰白。 | **macOS 全高 Source List 侧边栏**：去除侧边栏顶部分隔横线（仅保留右侧 `1px Stroke.Subtle` 垂直细线），**彻底移除左侧 3px 竖条**；导航项改为 macOS 圆角矩形选中底（`CornerRadius=8`、`Height=36`），每个导航项配 **24×24 圆角方形 SF 风格彩色图标徽章（Squircle Icon Badge，Radius=6）**，选中时图标底块实心点亮为对应 Apple 语义色+白色图标。 |
| **4. 分段控件（SegmentedControl）** | 图 01/04/06 分段控件是大号胶囊（`CornerRadius=20`），滑块用半透明彩色底+彩色字，像网页标签。 | **原生 macOS / iOS `NSSegmentedControl` 质感**：外框 `CornerRadius=9`、内边距 `2.5`、底槽 `Segment.Track`（`#14767680` / `#2E767680`）；**滑动指示块（Thumb）** 为 `CornerRadius=7` 的实体浮块（浅色纯白 `#FFFFFF`、深色抬升灰 `#3A3A3C`）配 **Apple 微阴影（`Blur=6, Depth=1.5`）与 `0.5px` 顶边高光**；文字一律高对比 `Text.Primary`。 |
| **5. 开关与滑块（Switch & Slider）** | 图 07/08 `ToggleSwitch` 关闭态是透明空心粗框+小灰点；`AppSlider` 拇指带彩色描边圈。 | **iOS/macOS 实心胶囊开关与白瓷滑块**：`ToggleSwitch`（`42×24`）关闭态为实心浅灰/深灰槽（`#E9E9EB` / `#39393D`，无空心框），开启态为 **Apple Green**（`#34C759` / `#30D158`），内部 `20×20` 纯白圆点带微阴影；`AppSlider` 拇指为 `18×18` 纯白实心瓷感圆球（无彩色粗边框，仅柔和阴影）。 |
| **6. 计时圆环与右侧列表** | 图 01..03 圆环外围 24px 光晕+粗刻度略显厚重；右侧“今日专注记录”每条都是独立深灰厚框盒子，层次琐碎。 | **Apple Watch / iOS 健身圆环级精致度 + Inset Grouped 列表**：圆环轨道与弧线 `10.5px`、外圈 60 刻度纤细化（主刻度 `1.2px`、次刻度 `0.75px`），大数字升级为 **`80pt` Light** 紧凑排版；右侧“今日专注记录”与统计页“历史记录”改为 **Apple Inset Grouped 列表**（同一卡片容器内以 `1px Stroke.Subtle` 缩进细线分隔，清爽克制）。 |
| **7. 环境音磁贴与设置页** | 图 07 激活的音轨卡片有 `1.5px` 亮色霓虹外框；图 08 设置页分组标题无彩色圆角块，主题预览卡红框刺眼。 | **iOS 控制中心 / HomePod 磁贴 + macOS 系统设置**：音轨卡片激活时采用柔和毛玻璃底 + **`40×40` 圆形图标底块实心点亮为音轨 Tint 色、内部 Lucide 图标反白 `#FFFFFF`**（控制中心经典交互），外框仅 `1px` 低透光描边；设置页每个分组标题左侧配 `26×26` Apple 彩色圆角方形图标徽章。 |
| **8. 迷你悬浮窗（Dynamic Island / HUD）** | 图 10 森林主题下迷你窗有突兀的粉红 `1.5px` 外框，底部进度条贴死底边被圆角裁切。 | **灵动岛 / macOS HUD 胶囊美学**：外框 `CornerRadius=20`、深邃玻璃黑/实色底 + `1px Stroke.Strong` 高光细边（不染色），底部进度条改为 **内缩胶囊条**（`Height=3.5`、`CornerRadius=2`、`Margin="16,0,16,7"`），精致悬浮。 |

---

## 2. Apple Design Tokens 体系（4 套主题完整色板）

### 2.1 新增与升级的 Token 键名契约
在保留原有全部 Token 键名（确保 100% 向后兼容）的基础上，**所有 4 个 `Themes/Tokens.<theme>.xaml` 必须统一新增以下 5 个 Apple 专用画刷资源**：

- `Switch.On`（`SolidColorBrush`）：开关开启态底色（Apple System Green）
- `Switch.Off`（`SolidColorBrush`）：开关关闭态实心轨道色
- `Segment.Track`（`SolidColorBrush`）：分段控件外框凹槽底色
- `Segment.Thumb`（`SolidColorBrush`）：分段控件滑动实体浮块色
- `Sidebar.ItemSelected`（`SolidColorBrush`）：侧边栏 Source List 选中项背景色

### 2.2 四套主题精确色值表（请 `coder` 逐项更新 `Tokens.*.xaml`）

| Token Key | `midnight`（深空暗夜 · Space Black，默认） | `mist`（苹果浅色 · Cupertino Light） | `sunset`（暖金暗夜 · Warm Amber） | `forest`（翠林暗夜 · Alpine Green） |
|---|---|---|---|---|
| `IsDark` (`sys:Boolean`) | `True` | `False` | `True` | `True` |
| `Bg.Glow1.Alpha` (`sys:Double`) | `0.09` | `0.06` | `0.10` | `0.09` |
| `Shadow.Opacity` (`sys:Double`) | `0.42` | `0.10` | `0.42` | `0.42` |
| `Card.ShadowOpacity` (`sys:Double`) | `0.0` | `0.04` | `0.0` | `0.0` |
| `Glow.Opacity` (`sys:Double`) | `0.38` | `0.24` | `0.38` | `0.38` |
| `Bg.Base` / `.Color` | `#FF0D0D0F` | `#FFF5F5F7` | `#FF141012` | `#FF0C1310` |
| `Bg.Glow2` / `.Color` | `#120A84FF` | `#0C0071E3` | `#16FF9F0A` | `#1430D158` |
| `Sidebar.Bg` | `#CC141417` | `#D9EBEBF0` | `#CC1B1518` | `#CC111A16` |
| `Sidebar.ItemSelected` | `#FF26262A` | `#FFE2E2E8` | `#FF2D2327` | `#FF1E2C26` |
| `Surface.1`（卡片主面） | `#FF1C1C1E` | `#FFFFFFFF` | `#FF1F191C` | `#FF15201B` |
| `Surface.2`（次级填充/列表内嵌） | `#FF262629` | `#FFF5F5F7` | `#FF2A2226` | `#FF1D2B25` |
| `Surface.3`（输入框/胶囊槽） | `#FF323236` | `#FFEAEAEF` | `#FF362C31` | `#FF263730` |
| `Surface.Solid` / `.Color` | `#FF1E1E22` | `#FFFFFFFF` | `#FF241C20` | `#FF17231E` |
| `Overlay`（模态遮罩） | `#88000000` | `#4D1D1D1F` | `#88000000` | `#88000000` |
| `Stroke.Subtle`（发丝分隔线） | `#16FFFFFF` | `#12000000` | `#17FFEFE6` | `#16E8FFF2` |
| `Stroke.Strong` / `.Color` | `#29FFFFFF` | `#22000000` | `#2BFFEFE6` | `#29E8FFF2` |
| `Stroke.Highlight`（顶边微高光） | `#1FFFFFFF` | `#FFFFFFFF` | `#21FFF5EE` | `#1FEFFFF6` |
| `Text.Primary` / `.Color` | `#FFF5F5F7` | `#FF1D1D1F` | `#FFFFF7F2` | `#FFEEF7F2` |
| `Text.Secondary` | `#FFA1A1A6` | `#FF6E6E73` | `#FFC4B2AA` | `#FF9AB0A5` |
| `Text.Tertiary` / `.Color` | `#FF6E6E73` | `#FF86868B` | `#FF8A7770` | `#FF657A70` |
| `Text.Disabled` | `#FF48484A` | `#FFC7C7CC` | `#FF574945` | `#FF41524A` |
| **`Accent` / `.Color`（系统交互色）** | **`#FF0A84FF`** (Apple Blue) | **`#FF0071E3`** (Apple Blue) | **`#FFFF9F0A`** (Apple Amber) | **`#FF30D158`** (Apple Green) |
| `Accent.Hover` | `#FF3399FF` | `#FF0077ED` | `#FFFFB340` | `#FF4AE070` |
| `Accent.Pressed` | `#FF006EE6` | `#FF005BB5` | `#FFE68A00` | `#FF24B347` |
| `Accent.Soft` | `#240A84FF` | `#180071E3` | `#24FF9F0A` | `#2430D158` |
| `Text.OnAccent` | `#FFFFFFFF` | `#FFFFFFFF` | `#FF1D1200` | `#FF04240D` |
| **`Switch.On`（苹果绿开关）** | **`#FF30D158`** | **`#FF34C759`** | **`#FFFF9F0A`** | **`#FF30D158`** |
| **`Switch.Off`** | **`#FF39393D`** | **`#FFE5E5EA`** | **`#FF42363B`** | **`#FF2C3E36`** |
| **`Segment.Track`** | **`#FF252528`** | **`#FFE8E8ED`** | **`#FF2C2327`** | **`#FF1D2A24`** |
| **`Segment.Thumb`** | **`#FF424247`** | **`#FFFFFFFF`** | **`#FF47393F`** | **`#FF2F443B`** |
| `Phase.Focus.Start / End` | `#FFFF453A` / `#FFFF6961` | `#FFFF3B30` / `#FFFF6259` | `#FFFF5E3A` / `#FFFF9F0A` | `#FFFF453A` / `#FFFF7A59` |
| `Phase.Short.Start / End` | `#FF30D158` / `#FF63E6E2` | `#FF34C759` / `#FF00C7BE` | `#FF30D158` / `#FF63E6E2` | `#FF30D158` / `#FF66F2A8` |
| `Phase.Long.Start / End` | `#FF5E5CE6` / `#FFBF5AF2` | `#FF5856D6` / `#FFAF52DE` | `#FF7D7AFF` / `#FFDA8FFF` | `#FF0A84FF` / `#FF5E5CE6` |
| `Success` | `#FF30D158` | `#FF34C759` | `#FF30D158` | `#FF30D158` |
| `Warning` | `#FFFF9F0A` | `#FFFF9500` | `#FFFFD60A` | `#FFFF9F0A` |
| `Danger` | `#FFFF453A` | `#FFFF3B30` | `#FFFF453A` | `#FFFF453A` |
| `Priority.High / Medium / Low` | `#FFFF453A` / `#FFFF9F0A` / `#FF0A84FF` | `#FFFF3B30` / `#FFFF9500` / `#FF007AFF` | `#FFFF453A` / `#FFFF9F0A` / `#FF64D2FF` | `#FFFF453A` / `#FFFF9F0A` / `#FF0A84FF` |
| `Ring.Track` | `#18FFFFFF` | `#12000000` | `#18FFF0E8` | `#18E8FFF2` |
| `Ring.Tick` | `#2EFFFFFF` | `#28000000` | `#2EFFF0E8` | `#2EE8FFF2` |
| `Chart.Grid` | `#12FFFFFF` | `#10000000` | `#14FFF0E8` | `#12E8FFF2` |
| `Heat.L0..L4` | `L0`: `#12FFFFFF`；`L1..L4`: `#4030D158` / `#7830D158` / `#B830D158` / `#FF30D158`（浅色 `mist` 用 `#10000000` 与 `#34C759` 四级透明度；`sunset` 用 `#FF9F0A` 四级透明度） ||||
| `DwmBorder` / `.Color` | `#FF28282C` | `#FFD8D8DC` | `#FF33282C` | `#FF22302A` |

> **关键设计收益**：
> 1. 浅色模式（`mist`，图 08）将彻底蜕变为 **纯正 macOS Sequoia / apple.com 浅色质感**：`#F5F5F7` 苹果灰底 + `#FFFFFF` 纯白卡片 + `#0071E3` 苹果蓝按钮/选中框 + `#34C759` 苹果绿开关 + `#1D1D1F` 深邃墨黑字。
> 2. 深色模式（`midnight`，图 01..07）将蜕变为 **macOS 深空黑分层质感**：`#0D0D0F` 底 + `#1C1C1E` 卡片 + `#0A84FF` 苹果蓝通用控件 + `#FF453A` 苹果红专注环。
> 3. 专注热力图（`Heat.L1..L4`）采用 Apple Fitness / GitHub 经典的 **Apple 翡翠绿 `#30D158` / `#34C759`**（日落主题用琥珀金），视觉清晰清爽。

---

## 3. 字体、圆角、材质与核心控件升级规格

### 3.1 SF Pro 风格字体节奏（`Themes/Typography.xaml`）
利用本机已安装的 `Segoe UI Variable Display`（大字紧凑光学字重）与 `Segoe UI Variable Text, Microsoft YaHei UI`（正文高可读光学字重）贴近 Apple SF Pro Display / SF Pro Text：
- **`Text.Timer`**：字号 `76` → **`80`**，`FontWeight="Light"`，`Typography.NumeralAlignment="Tabular"`（在 FocusViewModel 中 `< 1 小时` 为 `80`，`≥ 1 小时` 7 字符为 `62`）。
- **`Text.TimerMini`**：字号 `30` → **`32`**，`FontWeight="Light"`，tabular。
- **`Text.PageTitle`**：字号 `28`，`FontWeight="Bold"`（Apple Large Title 风格，扎实醒目）。
- **`Text.KPI`**：字号 `26`，`FontWeight="Bold"`，tabular。
- **`Text.SectionTitle`**：字号 `16`，`FontWeight="SemiBold"`。
- **`Text.CardTitle`**：字号 `14.5`，`FontWeight="SemiBold"`。
- **`Text.BodyStrong`**：字号 `13.5`，`FontWeight="SemiBold"`。
- **`Text.Body`**：字号 `13.5`，`FontWeight="Normal"`，`LineHeight="20"`。
- **`TextStyle.Secondary`**：字号 `12.5`，`Foreground="{DynamicResource Text.Secondary}"`。
- **`Text.Caption`**：字号 `11.5`，`Foreground="{DynamicResource Text.Tertiary}"`。

### 3.2 通用控件模板升级（`Themes/Controls.xaml` & `Controls/*.cs`）

#### 3.2.1 `SegmentedControl.cs`（macOS / iOS 悬浮滑块分段控件 — **P0 核心升级**）
修改 `src/PomodoroFocus.App/Controls/SegmentedControl.cs`：
1. **外槽（Outer Track）**：
   - `CornerRadius = new CornerRadius(9)`（大号 `SegmentHeight >= 34` 时 `10`），`Padding = new Thickness(3)`，`BorderThickness = new Thickness(0)`（无外框线，纯靠凹槽色区分）；
   - `SetResourceReference(BackgroundProperty, "Segment.Track")`。
2. **滑动实体指示块（`_indicator` Thumb）**：
   - `CornerRadius = new CornerRadius(7)`；
   - **无论 `UsePhaseAccent` 为何值**，滑块背景统一使用 `SetResourceReference(BackgroundProperty, "Segment.Thumb")`，并加上 `BorderBrush = Stroke.Highlight`、`BorderThickness = new Thickness(0, 0.5, 0, 0)` 以及 Apple 悬浮微阴影：
     ```csharp
     _indicator.Effect = new System.Windows.Media.Effects.DropShadowEffect
     {
         Color = Colors.Black,
         BlurRadius = 6,
         ShadowDepth = 1.5,
         Direction = 270,
         Opacity = 0.16
     };
     ```
3. **文字颜色与字重**：
   - 选中项文字：`Text.Primary`（当 `UsePhaseAccent == true` 时，可在选中文字左侧或阶段胶囊体现阶段色，或者选中项文字统一用 `Text.Primary` `FontWeights.SemiBold`，未选中项用 `Text.Secondary` `FontWeights.Normal`）。

#### 3.2.2 `ToggleSwitch`（`Themes/Controls.xaml` — iOS / macOS 实心胶囊开关 — **P0 核心升级**）
修改 `Themes/Controls.xaml` 中的 `ToggleSwitch`（`42 × 24`）：
- **轨道 `Track`**：`Width="42" Height="24" CornerRadius="12" BorderThickness="0"`，默认 `Background="{DynamicResource Switch.Off}"`。
- **圆钮 `Thumb`**：`Width="20" Height="20" Margin="2,0,0,0" Fill="#FFFFFFFF"`，附带微阴影：
  `<DropShadowEffect BlurRadius="4" ShadowDepth="1" Direction="270" Opacity="0.22" Color="#000000" />`。
- **`IsChecked == True` 触发器**：
  - `Track.Background` 设为 `{DynamicResource Switch.On}`（Apple 翠绿 `#34C759` / `#30D158`）；
  - `ThumbOffset.X` 在 150ms 内平滑动画到 `18`。
- 移除原有的空心透明边框与灰色小圆点。

#### 3.2.3 `AppSlider`（`Themes/Controls.xaml` — Apple 白瓷滑块）
- 轨道高 `4`、圆角 `2`，未填充底色改为 `{DynamicResource Switch.Off}`，已填充段保持 `{Binding Foreground, RelativeSource={RelativeSource AncestorType=Slider}}`。
- 滑块 `Knob`：`Width="18" Height="18"`，**纯白实心 `Fill="#FFFFFFFF"`**，边框改为极细微的半透明黑边 `Stroke="#20000000" StrokeThickness="0.5"`（去除原先粗重的彩色边框），配 `<DropShadowEffect BlurRadius="6" ShadowDepth="1.5" Direction="270" Opacity="0.24" Color="#000000" />`。

#### 3.2.4 `CardBorder` 与按钮（`Themes/Controls.xaml`）
- `CardBorder`：`CornerRadius="14"`（更接近 macOS Sequoia 卡片曲率），`Padding="18,16"`，`Background="{DynamicResource Surface.1}"`，`BorderBrush="{DynamicResource Stroke.Subtle}"`，`BorderThickness="1"`；浅色主题下 `Card.ShadowOpacity = 0.04`、`BlurRadius = 20`、`ShadowDepth = 3`。
- `Button.Primary`：默认 `ControlProps.CornerRadius` 设为 `8`（紧凑洗练的 macOS 按钮曲率），`FontSize="13.5"`，`Height="34"`。
- `Button.TimerPrimary`（专注主控圆钮）：`72 × 72`，背景 `{DynamicResource Phase.Gradient}`，顶部叠加一层 `1px` 半透明白高光内环（`<Border CornerRadius="999" BorderBrush="#38FFFFFF" BorderThickness="1" />`），阴影改为更柔和的向下漫射（`BlurRadius="22" ShadowDepth="4" Direction="270" Opacity="{DynamicResource Glow.Opacity}"`）。

---

## 4. 各核心视图 Apple 风格具体改动清单（按文件逐项落地）

### 4.1 全局壳层与 macOS Source List 侧边栏（`Views/MainWindow.xaml`）— **优先级 P0**
1. **去除侧边栏顶部分隔横线**：
   - 将第 222 行左侧导航栏 `Border` 的 `BorderThickness="0,1,1,0"` 改为 **`BorderThickness="0,0,1,0"`**，让侧边栏从上到下成为完整的 macOS 材质列。
2. **侧边栏分组小标题**：
   - 在 5 个导航按钮上方（`StackPanel` 顶部）加一行 `"专注工作台"`（`FontSize="11" FontWeight="SemiBold" Foreground="{DynamicResource Text.Tertiary}" Margin="18,2,0,6"`，`Visibility="{Binding IsCompactLayout, Converter={StaticResource InvBoolToVis}}"`）。
3. **macOS Source List 导航项（移除 Win11 左侧 3px 竖条 + 增加 SF 风格圆角方形彩色图标徽章）**：
   - `NavItemButton` 样式：`Height="38"`、`Margin="10,2"`、`CornerRadius="8"`；选中态 `Background` 改为 `{DynamicResource Sidebar.ItemSelected}`。
   - **删除** 5 个导航按钮里的 `<Border Width="3" Height="18" ... />` 左侧竖条！
   - 将每个导航按钮的图标包裹在 **`24 × 24`、`CornerRadius="6"` 的圆角方形徽章 `Border`** 中（字号 `13`），各导航项固定使用 Apple 系统语义色：
     - **专注**（`NavFocusButton`）：图标底 `#22FF453A`、图标色 `#FFFF453A`（选中时底 `#FFFF453A`、图标 `#FFFFFFFF`）；
     - **任务**（`NavTasksButton`）：图标底 `#220A84FF`、图标色 `#FF0A84FF`（选中时底 `#FF0A84FF`、图标 `#FFFFFFFF`）；
     - **统计**（`NavStatsButton`）：图标底 `#225E5CE6`、图标色 `#FF5E5CE6`（选中时底 `#FF5E5CE6`、图标 `#FFFFFFFF`）；
     - **环境音**（`NavAudioButton`）：图标底 `#2230D158`、图标色 `#FF30D158`（选中时底 `#FF30D158`、图标 `#FFFFFFFF`）；
     - **设置**（`NavSettingsButton`）：图标底 `#228E8E93`、图标色 `{DynamicResource Text.Secondary}`（选中时底 `#FF8E8E93`、图标 `#FFFFFFFF`）。
4. **模态对话框（`OverlayHost` 内的 `Modal A / B / C`）**：
   - 圆角 `CornerRadius="18"`，外阴影 `BlurRadius="48" ShadowDepth="14" Opacity="0.32"`；底部“取消 / 保存”按钮统一 `Height="34"`、`MinWidth="80"`。

### 4.2 专注主视图与 Apple Watch 级圆环（`Views/FocusView.xaml` & `Controls/CircularTimerRing.cs`）— **优先级 P0**
1. **`CircularTimerRing.cs` 精细化**：
   - 第 188 行环境柔光环 `glowPen`：线宽从 `24.0` 收窄至 **`16.0`**，`BreathFactor` 范围从 `0.06..0.14` 收敛为更克制的 **`0.03..0.08`**（Idle `0.02`），消除圆环外围的浑浊感。
   - 第 193–227 行外圈 60 刻度：主刻度线宽从 `1.5` 改为 **`1.2`**（长 `4.5`），次刻度线宽从 `1.0` 改为 **`0.75`**（长 `2.2`），`innerR = r + 12.0`，呈现瑞士腕表/Apple Watch 表盘般的精密纤细刻度。
   - 第 230/236 行轨道与主进度弧：线宽设为 **`10.5`**；弧端发光点 `tipGlow` 半径从 `12.0` 收至 **`9.0`**，内核白点半径 `4.0`。
2. **`FocusView.xaml` 右侧辅助栏升级为 Apple Widget & Inset Grouped List**：
   - 右侧“今日专注记录”卡片（Card 2）：将每条记录的 `<Border CornerRadius="10" Background="{DynamicResource Surface.2}" Padding="10,8" Margin="0,0,0,6">` 改为 **清爽的底线分隔行**：`<Border BorderBrush="{DynamicResource Stroke.Subtle}" BorderThickness="0,0,0,1" Padding="4,9">`（无厚重灰底块，仅左侧 6px 阶段色圆点 + 标题/时间段 + 右侧 `25 分钟` 标签），视觉通透度大幅提升。
   - 顶部分段控件 `ModeSegmentControl`：`Width="348"`、`SegmentHeight="30"`。

### 4.3 任务管理视图（`Views/TasksView.xaml` — Apple Reminders 提醒事项风格）— **优先级 P1**
1. **快速添加栏（Quick Add Bar）**：
   - 背景改为 `{DynamicResource Surface.1}`、`CornerRadius="12"`、`Height="46"`；右侧“添加”按钮 `Height="32"`、`CornerRadius="8"`。
2. **任务卡片行（`RowCard`）**：
   - 左侧优先级竖条从 `Width="4" Height="28"` 精致化为 **`Width="3" Height="20" CornerRadius="1.5"`**；
   - 圆形完成勾选框（`20 × 20`）：未完成时边框色直接按任务优先级着色（高优先级用 `Priority.High`、中用 `Priority.Medium`、低用 `Priority.Low`、无优先级用 `Stroke.Strong`），完美复刻 **macOS / iOS Reminders 提醒事项** 的彩色圆环勾选框！

### 4.4 专注统计视图与图表色板（`Views/StatsView.xaml` & `Controls/Charts/*.cs`）— **优先级 P1**
1. **`DistributionDonutChart.cs` 调色板升级为 Apple HIG System Chart Palette**：
   - 将 `Palette` 静态数组更新为：
     ```csharp
     private static readonly Color[] Palette = new Color[]
     {
         Color.FromRgb(0x0A, 0x84, 0xFF), // Apple System Blue
         Color.FromRgb(0xFF, 0x45, 0x3A), // Apple System Red
         Color.FromRgb(0x30, 0xD1, 0x58), // Apple System Green
         Color.FromRgb(0x5E, 0x5C, 0xE6), // Apple System Indigo
         Color.FromRgb(0xFF, 0x9F, 0x0A), // Apple System Orange
         Color.FromRgb(0x64, 0xD2, 0xFF)  // Apple System Teal
     };
     ```
2. **`StatsView.xaml` 历史记录明细**：
   - 同样改为 Apple Inset Grouped 行样式（`<Border BorderBrush="{DynamicResource Stroke.Subtle}" BorderThickness="0,0,0,1" Padding="6,8">`），去除每行沉闷的深灰底块。

### 4.5 环境音面板（`Views/AmbientAudioView.xaml` & `ViewModels/AmbientAudioViewModel.cs` — iOS 控制中心磁贴风格）— **优先级 P1**
1. **音轨 Tint 色升级为 Apple System Vibrancy Palette**（`AmbientAudioViewModel.cs` 第 163–174 行）：
   - `rain` 细雨：`#0A84FF`（Apple Blue）
   - `ocean` 海浪：`#32ADE6`（Apple Cyan）
   - `forest` 林间鸟鸣：`#30D158`（Apple Green）
   - `cafe` 咖啡馆：`#AC8E68`（Apple Warm Taupe）
   - `fire` 篝火：`#FF9F0A`（Apple Orange）
   - `wind` 微风：`#5E5CE6`（Apple Indigo）
   - `white` 白噪音：`#8E8E93`（Apple Gray）
   - `brown` 棕噪音：`#BF5AF2`（Apple Purple）
   - `clock` 时钟滴答：`#FF375F`（Apple Pink）
   - `TintBorderBrush` 透明度从 `0xB3`（70%）收敛为 **`0x55`（33%）**；`ActiveBgBrush` 渐变透明度收敛为 **`0x1E → 0x0A`**，温和优雅。
2. **iOS 控制中心式“实心点亮图标圆章”**（`AmbientAudioView.xaml` 第 244–276 行 & `FocusView.xaml` 快捷环境音按钮）：
   - 当音轨 `IsActive == True` 时，`40×40` 圆形图标徽章的 `Background` 设为实心 **`{Binding TintBrush}`**，内部的 Lucide `Path.Stroke` 设为纯白 **`#FFFFFFFF`**！未激活时圆形底为 `{DynamicResource Surface.2}`、图标描边为 `{DynamicResource Text.Secondary}`。这一下就能产生极其鲜明的 Apple HomeKit / Control Center 磁贴质感。

### 4.6 设置中心（`Views/SettingsView.xaml` & `ViewModels/SettingsViewModel.cs` — macOS System Settings 风格）— **优先级 P0**
1. **分组标题配 macOS 26×26 圆角方形彩色图标徽章**（`SettingsView.xaml`）：
   - 为“外观、计时、自动化、提醒与声音、快捷键、系统、数据”每个分组标题左侧配置 `Width="26" Height="26" CornerRadius="6"` 的实心彩色图标块 + `14px #FFFFFF` 图标：
     - 外观：`#0A84FF`（Apple Blue）+ `Icon.Theme`
     - 计时：`#FF453A`（Apple Red）+ `Icon.NavFocus`
     - 自动化：`#5E5CE6`（Apple Indigo）+ `Icon.Play`
     - 提醒与声音：`#FF9F0A`（Apple Orange）+ `Icon.Bell`
     - 快捷键：`#32ADE6`（Apple Cyan）+ `Icon.Keyboard`
     - 系统：`#30D158`（Apple Green）+ `Icon.Power`
     - 数据：`#8E8E93`（Apple Gray）+ `Icon.Folder`
2. **主题预览卡色值同步**（`SettingsViewModel.cs` 第 87–93 行）：
   - 更新 4 张卡片的预览色值以匹配新 Apple 色板（保持 `Key` 与 `AutomationId` 完全不变）：
     - `midnight`（深邃夜空）：`BgHex="#0D0D0F"`, `CardHex="#1C1C1E"`, `AccentHex="#0A84FF"`, `TextHex="#F5F5F7"`
     - `mist`（晨雾浅色）：`BgHex="#F5F5F7"`, `CardHex="#FFFFFF"`, `AccentHex="#0071E3"`, `TextHex="#1D1D1F"`
     - `sunset`（暖阳日落）：`BgHex="#141012"`, `CardHex="#1F191C"`, `AccentHex="#FF9F0A"`, `TextHex="#FFF7F2"`
     - `forest`（静谧森林）：`BgHex="#0C1310"`, `CardHex="#15201B"`, `AccentHex="#30D158"`, `TextHex="#EEF7F2"`

### 4.7 迷你悬浮窗与 Toast 卡片（`Views/MiniTimerWindow.xaml` & `Views/ToastWindow.xaml`）— **优先级 P1**
1. **`MiniTimerWindow.xaml`（灵动岛 / macOS HUD 风格）**：
   - `RootCard`：`CornerRadius="20"`，`BorderBrush="{DynamicResource Stroke.Strong}"`，`BorderThickness="1"`（移除突兀的粉红粗外框 `Phase.MiniBorderBrush`），`Clip` 同步改为 `RadiusX="20" RadiusY="20"`。
   - 底部进度条（Row 2）：外层 `RowDefinition Height="10"`，内部进度轨道设 `Height="3.5" CornerRadius="1.75" Margin="16,0,16,6"`，指示条 `CornerRadius="1.75"` —— 让进度条像 iOS 底部 Home Indicator 一样优雅悬浮在胶囊内部，绝不触碰两侧圆角边界。
2. **`ToastWindow.xaml`**：
   - 去除顶部横穿的 3px 彩条，改为 `CornerRadius="16"` 的纯净 macOS 通知中心横幅（左侧 `36×36` 圆角方形阶段色图标徽章 + 标题正文 + 右侧胶囊按钮）。

---

## 5. 需修改的文件清单（共 17 个文件，范围清晰可控）

| 序号 | 文件相对路径 | 改动要点 |
|---|---|---|
| 1 | `src/PomodoroFocus.App/Themes/Tokens.midnight.xaml` | 更新为 Apple Space Black 深空黑 Token + 新增 5 个 `Switch/Segment/Sidebar` 资源 |
| 2 | `src/PomodoroFocus.App/Themes/Tokens.mist.xaml` | 更新为 Apple Cupertino Light (`#F5F5F7` + `#FFFFFF` + `#0071E3` + `#34C759`) Token |
| 3 | `src/PomodoroFocus.App/Themes/Tokens.sunset.xaml` | 更新为 Apple Warm Amber Token |
| 4 | `src/PomodoroFocus.App/Themes/Tokens.forest.xaml` | 更新为 Apple Alpine Green Token |
| 5 | `src/PomodoroFocus.App/Themes/Typography.xaml` | 升级 `Text.Timer` (80pt Light)、`Text.PageTitle` (28pt Bold)、`Text.KPI` (26pt Bold) 等 SF 风格字阶 |
| 6 | `src/PomodoroFocus.App/Themes/Controls.xaml` | 升级 `ToggleSwitch` (实心苹果绿开关)、`AppSlider` (纯白瓷感滑块)、`CardBorder` (14px 圆角)、`Button.Primary` (8px 圆角)、`Button.TimerPrimary` (高光内环) |
| 7 | `src/PomodoroFocus.App/Controls/SegmentedControl.cs` | 升级为 macOS/iOS 实体滑块分段控件（`Segment.Track` + `Segment.Thumb` + 微阴影 + `Text.Primary`） |
| 8 | `src/PomodoroFocus.App/Controls/CircularTimerRing.cs` | 纤细化 60 刻度（1.2px/0.75px）、收敛外围柔光环（16px, alpha 0.03..0.08）、10.5px 进度弧 |
| 9 | `src/PomodoroFocus.App/Controls/Charts/DistributionDonutChart.cs` | 升级为 Apple 6 色系统图表调色板 |
| 10 | `src/PomodoroFocus.App/Views/MainWindow.xaml` | macOS Source List 侧边栏（去顶线、去左 3px 竖条、加 24×24 圆角方形彩色图标徽章、分组小标题） |
| 11 | `src/PomodoroFocus.App/Views/FocusView.xaml` | 今日专注记录改为 Apple Inset Grouped 分隔线列表、快捷环境音激活态实心图标圆章 |
| 12 | `src/PomodoroFocus.App/ViewModels/FocusViewModel.cs` | `TimerFontSize` 在 `< 1 小时` 时返回 `80.0`（紧凑布局 `72.0`），`≥ 1 小时` 返回 `62.0` |
| 13 | `src/PomodoroFocus.App/Views/TasksView.xaml` | Apple Reminders 风格按优先级着色的圆形勾选框、3×20 纤细优先级标、精致化快速添加栏 |
| 14 | `src/PomodoroFocus.App/Views/StatsView.xaml` | 历史记录改为 Apple Inset Grouped 分隔线列表 |
| 15 | `src/PomodoroFocus.App/Views/AmbientAudioView.xaml` & `ViewModels/AmbientAudioViewModel.cs` | Apple 系统色音轨 Tint、iOS 控制中心式激活态实心彩色圆章+白图标、温和边框透明度 |
| 16 | `src/PomodoroFocus.App/Views/SettingsView.xaml` & `ViewModels/SettingsViewModel.cs` | macOS 系统设置 26×26 彩色圆角方形分组图标徽章、更新 4 张主题预览卡色值 |
| 17 | `src/PomodoroFocus.App/Views/MiniTimerWindow.xaml` & `Views/ToastWindow.xaml` | 灵动岛胶囊边框 + 内部悬浮圆角进度条（不贴边裁切）、macOS 通知横幅样式 |

---

## 6. 验证与视觉验收清单（供 `coder` 自检与 `reviewer` 终审）

1. **编译与自动化测试**：
   - 运行 `.\build.ps1 -UiTest`，确认 `Release|x64`（及 `Debug|x64`）**0 错误 0 警告**，18/18 单元测试与 `PomodoroFocus.UITests.exe` 全部通过（退出码 0）。
2. **生成 Apple 升级版 10 张截图到 `%TEMP%\PomodoroFocus_apple_shots`（严禁覆盖 `docs/screenshots`）**：
   ```powershell
   .\src\PomodoroFocus.App\bin\Release\PomodoroFocus.exe --data-dir "$env:TEMP\pf_apple_data" --seed-demo --screenshot-suite "$env:TEMP\PomodoroFocus_apple_shots"
   ```
3. **10 张截图 Apple 视觉验收要点**：
   - `01..03`（专注/短休/长休）：顶部分段控件具有纯正 macOS 实体滑块阴影质感；圆环刻度纤细精密、80pt 数字大气舒展；左侧栏为 macOS Source List 圆角方形彩色图标徽章（无左侧 3px 竖条、无顶部分隔横线）；右侧“今日专注记录”为清爽的细线分隔列表。
   - `04..05`（任务与新建弹窗）：按钮与新建任务主按钮为清爽的 **Apple Blue (`#0A84FF`)**；任务行勾选框按优先级呈现红/橙/蓝圆环；弹窗具有 macOS Sheet 圆角与柔和深阴影。
   - `06`（统计页）：环形图采用 Apple 6 色系统色板；热力图采用 Apple 翡翠绿多级色阶；历史记录为细线分隔列表；一屏完整显示无截断。
   - `07`（环境音）：开启的开关为 **Apple Green (`#30D158`)** 实心胶囊；激活的“细雨 / 篝火 / 时钟滴答”卡片左侧 `40×40` 圆章实心点亮为对应 Apple 语义色且内部图标反白。
   - `08`（设置页 · `mist` 苹果浅色主题）：`#F5F5F7` 苹果灰画布 + `#FFFFFF` 纯白卡片 + `#0071E3` 苹果蓝选中框 + `#34C759` 苹果绿开关 + 白瓷滑块 + 26×26 彩色圆角方形分组图标，彻底告别原先的刺眼猩红配黑框。
   - `09`（`sunset` 860×600 紧凑模式）：琥珀金交互色协调，所有控件零截断零越界。
   - `10`（`forest` 迷你悬浮窗）：20px 圆角玻璃黑胶囊 + 内缩悬浮圆角进度条，精致无裁切。
4. **Git 工作区检查**：
   - 运行 `git status --short`：确认 `docs/screenshots/` 下没有任何文件被修改，且没有任何新增 commit。
