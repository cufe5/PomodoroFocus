# 番茄专注（PomodoroFocus）· 规划与 UI/UX 设计规格书

> 版本：v1.0（2026-11-19）　作者：`architect`　读者：`coder`（实现）、`reviewer`（审核）
>
> 本文是**唯一事实来源**。`coder` 必须逐条实现，不得自行删减功能或替换技术栈；如确有实现障碍，在消息板 `@architect` 说明原因与替代方案，经确认后再改。
> 文中 **必须 / 禁止** 为硬性要求；**建议** 为推荐做法。

---

## 0. 产品一句话

一款面向个人与团队日常使用的 Windows 桌面番茄钟：**深色玻璃质感 + 随阶段流动的色彩**，专注计时、任务、统计、离线环境音、迷你悬浮窗、托盘与全局快捷键一应俱全，全部纯本地离线可用。

- 产品显示名：**番茄专注**（窗口标题、托盘提示、通知标题统一使用）
- 程序集 / 进程名：`PomodoroFocus.exe`
- 默认数据目录：`%LocalAppData%\PomodoroFocus\`

---

## 1. 本机环境实测与技术栈选型

### 1.1 实测结果（architect 于 2026-11-19 只读探测 + 隔离探针构建）

| 项目 | 实测结果 | 结论 |
|---|---|---|
| 操作系统 | Windows 11 25H2，Build 26200，x64 | 支持 DWM 圆角 / 暗色标题栏属性 |
| 显示器 | DISPLAY2 3840×2160 @175%（逻辑 2194×1234）；DISPLAY1 逻辑 1600×1000 | **主力 DPI 缩放 175%**，必须 PerMonitorV2 |
| .NET Framework | 4.8.09221（Release 533509） | ✅ 可用 |
| C# 编译器 | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 4.8.9221.0（**仅支持 C# 5**） | ✅ 可用，有语法约束 |
| MSBuild | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe` 4.8.9221.0 | ✅ 可用 |
| WPF 程序集 | `Framework64\v4.0.30319\WPF\` 下 PresentationFramework / PresentationCore / UIAutomationClient / UIAutomationTypes 齐全 | ✅ |
| Reference Assemblies | **未安装**（无 `Reference Assemblies\...\v4.8`） | 需在 csproj 中指向运行时目录（见 1.3 模板，已验证） |
| .NET SDK（dotnet CLI） | Host 9.0.5 + 运行时 3.1/9.0，**无任何 SDK** | ❌ 不能 `dotnet build` |
| Visual Studio / vswhere | 无 | ❌ |
| NuGet 包 | 沙箱无网络，且无 SDK | ❌ **禁止依赖任何 NuGet 包** |
| Node.js / npm | v24.14.1 / 11.11.0（全局仅 CLI 工具，无 electron） | Electron 需联网下载，构建不稳定 |
| Rust / cargo | 无 | ❌ Tauri 不可用 |
| Python | msys2 python 3.12 运行即报 `No module named 'encodings'` | ❌ 损坏 |
| WebView2 Runtime | 157.0.4341.60 | 运行时有，但 WebView2 SDK 需 NuGet → 不可用 |
| winmm 音频 | `waveOutGetNumDevs()` = 4 | ✅ 可用 waveOut 实时合成播放 |
| DWM | `DwmSetWindowAttribute` 返回 S_OK | ✅ |
| UI Automation | `AutomationElement.RootElement` 可用 | ✅ 可做 UIA 端到端测试 |
| 字体 | Segoe UI Variable（Display/Text/Small）、Segoe Fluent Icons、Microsoft YaHei UI、Cascadia、Bahnschrift | ✅ 已验证 WPF 中 `Segoe UI Variable Display` 的 **tabular 数字生效**（"11:11" 与 "00:00" 等宽 221.6px@100pt） |
| 沙箱写权限 | `%TEMP%` 可写；`%LocalAppData%`、`E:\wu\localTemp\gemini_downloads` **沙箱内不可写** | 测试数据走 `%TEMP%`；截图归档需 BypassSandbox（见 1.6） |

**隔离探针**：architect 用与 1.3 完全相同的 csproj 写法构建了一个最小 WPF 程序（WindowChrome、`Path` 弧形、`DropShadowEffect`、NotifyIcon、UIAutomationClient、DWM、winmm P/Invoke），**0 错误 0 警告**，运行退出码 0。探针文件已删除。

### 1.2 选型结论：**WPF + .NET Framework 4.8（x64）+ C# 5，纯 BCL 零第三方依赖**

| 候选 | 本机可构建性 | UI 上限 | 结论 |
|---|---|---|---|
| **WPF / .NET Framework 4.8** | ✅ 系统自带 MSBuild+csc，离线零报错 | 矢量渲染、渐变、模糊/阴影特效、Storyboard 动画、自绘控件（OnRender）、WindowChrome 自定义标题栏，足以做出一流现代 UI | **选用** |
| WinUI 3 / .NET 8 WPF | ❌ 无 SDK、无 NuGet | 高 | 不可用 |
| Electron / Tauri | ❌ 需联网下载 / 无 Rust | 高 | 构建不稳定 |
| PyQt | ❌ Python 损坏 | 中 | 不可用 |

理由：唯一能在本机**离线、稳定、零报错**构建的方案；WPF 的矢量合成与动画体系足以实现玻璃质感卡片、渐变进度环、细腻过渡；原生 Win32 互操作（托盘、全局热键、DWM、winmm）全部可用。

### 1.3 工具链硬约束（违反即视为构建失败）

1. **C# 5 语法限定**（`<LangVersion>5</LangVersion>`）。**禁止**：`$"..."` 插值、`?.` / `?[]`、`nameof`、`=>` 表达式体成员、自动属性初始化器 `{ get; set; } = x`、只读自动属性 `{ get; }`、`using static`、`out var`、元组 / 值元组、模式匹配 / `is T x`、`switch` 表达式、本地函数、`catch when`、`await` 写在 catch/finally、字典初始化器 `[k] = v`、`default` 字面量、`throw` 表达式、数字分隔符 `1_000`。**允许**：`async/await`、LINQ、lambda、匿名类型、`var`、集合/对象初始化器、泛型、`string.Format`。
2. **禁止 NuGet / 外部 DLL**。仅可引用框架程序集：`System, System.Core, System.Xml, System.Xaml, WindowsBase, PresentationCore, PresentationFramework, System.Drawing, System.Windows.Forms, System.Runtime.Serialization`（UI 测试项目另加 `UIAutomationClient, UIAutomationTypes`）。
3. **源码编码**：所有 `.cs/.xaml/.ps1/.md` 一律 **UTF-8 with BOM**；所有 csproj 设置 `<CodePage>65001</CodePage>`（双保险，防止中文字面量被按 GBK 编译成乱码）。
4. **旧式 csproj**：必须显式列出每一个 `<Compile>` 与 `<Page>`；新增文件忘记登记 = 构建遗漏。
5. **零警告**：所有项目 `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`、`<WarningLevel>4</WarningLevel>`。
6. 平台统一 `x64`，`Prefer32Bit=false`；解决方案只有 `Debug|x64`、`Release|x64` 两个配置。

**App 项目 csproj 模板（已在本机验证可构建，Core/Tests 照此改 OutputType 与引用）：**

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">x64</Platform>
    <ProjectGuid>{固定 GUID}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>PomodoroFocus.App</RootNamespace>
    <AssemblyName>PomodoroFocus</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <ProjectTypeGuids>{60dc8134-eba5-43b8-bcc9-bb4bc16c2548};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
    <LangVersion>5</LangVersion>
    <CodePage>65001</CodePage>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningLevel>4</WarningLevel>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Assets\AppIcon.ico</ApplicationIcon>
    <BypassFrameworkInstallChecks>true</BypassFrameworkInstallChecks>
    <_TargetFrameworkDirectories Condition="!Exists('$(MSBuildProgramFiles32)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8')">$(MSBuildToolsPath);$(MSBuildToolsPath)\WPF</_TargetFrameworkDirectories>
    <_FullFrameworkReferenceAssemblyPaths Condition="!Exists('$(MSBuildProgramFiles32)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8')">$(MSBuildToolsPath)</_FullFrameworkReferenceAssemblyPaths>
    <PlatformTarget>x64</PlatformTarget>
    <Prefer32Bit>false</Prefer32Bit>
    <OutputPath>bin\$(Configuration)\</OutputPath>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <DebugSymbols>true</DebugSymbols><DebugType>full</DebugType><Optimize>false</Optimize><DefineConstants>DEBUG;TRACE</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <DebugType>pdbonly</DebugType><Optimize>true</Optimize><DefineConstants>TRACE</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" /><Reference Include="System.Core" /><Reference Include="System.Xml" /><Reference Include="System.Xaml" />
    <Reference Include="WindowsBase" /><Reference Include="PresentationCore" /><Reference Include="PresentationFramework" />
    <Reference Include="System.Drawing" /><Reference Include="System.Windows.Forms" /><Reference Include="System.Runtime.Serialization" />
  </ItemGroup>
  <ItemGroup>
    <ApplicationDefinition Include="App.xaml"><Generator>MSBuild:Compile</Generator></ApplicationDefinition>
    <!-- 每个 XAML：<Page Include="Views\FocusView.xaml"><Generator>MSBuild:Compile</Generator></Page> -->
    <!-- 每个 CS：<Compile Include="..." />；XAML 后台代码加 <DependentUpon> -->
    <None Include="App.config" /><None Include="app.manifest" /><Resource Include="Assets\AppIcon.ico" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\PomodoroFocus.Core\PomodoroFocus.Core.csproj"><Project>{Core GUID}</Project><Name>PomodoroFocus.Core</Name></ProjectReference>
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

- Core 项目：`OutputType=Library`，**无** `ProjectTypeGuids`，只引用 `System, System.Core, System.Xml, System.Runtime.Serialization`（Core **禁止**引用任何 WPF/WinForms 程序集，保证可单测）。
- `App.config`：`<startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup>`。
- `app.manifest`：`<dpiAware>true/pm</dpiAware>` + `<dpiAwareness>PerMonitorV2</dpiAwareness>`，`supportedOS` 声明 Win10/11 GUID `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}`。

### 1.4 构建 / 测试 / 启动命令（写入 README.md 与 build.ps1）

```powershell
# 0) 进入项目目录
Set-Location e:\wu\workSpace\LearningProject\windows_pomodoro

# 1) 构建（Release|x64，要求 0 Error 0 Warning）
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" PomodoroFocus.sln /t:Build /p:Configuration=Release /p:Platform=x64 /m /nologo /v:minimal

# 2) 单元测试（退出码 0 = 全部通过）
.\tests\PomodoroFocus.Tests\bin\Release\PomodoroFocus.Tests.exe

# 3) UI 自动化端到端测试 + 截图（隔离数据目录，自动清理）
.\tests\PomodoroFocus.UITests\bin\Release\PomodoroFocus.UITests.exe --out "$env:TEMP\PomodoroFocus.UITests"

# 4) 启动应用（正常使用）
.\src\PomodoroFocus.App\bin\Release\PomodoroFocus.exe

# 一键脚本
.\build.ps1                 # 构建
.\build.ps1 -Test           # 构建 + 单元测试
.\build.ps1 -UiTest         # 构建 + 单元测试 + UI 测试
.\build.ps1 -Run            # 构建后启动
.\build.ps1 -Clean          # 删除所有 bin/ obj/
```

`build.ps1` 要求：`Set-StrictMode -Version 2`、`$ErrorActionPreference='Stop'`；任一步非 0 退出码立即 `exit` 该码；最后打印 `BUILD OK` / `TESTS OK`（ASCII 输出，避免 PowerShell 5 控制台中文乱码）。

### 1.5 目录现状与清理决策（已执行）

- 探测时目录内存在此前中止任务遗留的 `WindowsPomodoro` 脚手架：仅有占位主窗口（"UI Pending M2"）、未经验证的 Core 代码、约 190KB 针对不存在 UI 的 E2E 用例、`bin/ obj/` 构建产物以及 `_probe\a.tmp` 垃圾文件；其数据模型与本规格的契约不一致。
- **决策：清空重建。** architect 已删除目录内全部旧文件，当前目录仅含本 `DESIGN_SPEC.md`。`coder` 从零按第 4 章结构创建，**禁止**恢复或参考旧文件名 `WindowsPomodoro.*`。
- 文件卫生规则：仓库内只允许第 4.1 节列出的文件 + 构建产物 `bin/ obj/`；**禁止**提交测试截图、日志、临时数据、`*.tmp`、探针程序。

### 1.6 临时文件与截图位置（遵守全局规则）

| 用途 | 路径 | 说明 |
|---|---|---|
| 单元 / UI 测试的隔离数据目录 | `%TEMP%\PomodoroFocus.Tests\<guid>`、`%TEMP%\PomodoroFocus.UITests\<时间戳>\data` | 沙箱内可写；测试结束**自动删除**数据目录 |
| UI 测试截图（运行时） | `%TEMP%\PomodoroFocus.UITests\<时间戳>\shots\*.png` | 沙箱内可写 |
| 截图 / 评审产物归档 | `E:\wu\localTemp\gemini_downloads\windows_pomodoro_review\` | 全局规则指定目录；沙箱不可写，`reviewer` 用**单独一条** `BypassSandbox` 的 `Copy-Item` 命令从 `%TEMP%` 复制过去 |
| 正常运行数据 | `%LocalAppData%\PomodoroFocus\` | 沙箱内不可写 → 应用必须优雅降级（见 2.5.8） |

---

## 2. 功能规划（全部端到端真实可用，零占位按钮）

> 原则：界面上出现的**每一个**按钮、开关、菜单项都必须产生真实效果；若依赖条件不满足（如无音频设备、热键被占用），控件显示为禁用态并给出中文原因提示（ToolTip 或行内说明），**不得静默无效**。

### 2.1 计时核心

#### 2.1.1 模式与阶段
- **番茄模式（Pomodoro）**：阶段 `专注 Focus` → `短休息 ShortBreak` → … 每完成 N 个专注（默认 4）后进入 `长休息 LongBreak`。
- **正计时模式（Stopwatch）**：从 0 向上计时，手动“完成”时记录为一次专注（≥ 60 秒才记录），上限 4 小时自动完成。
- **自定义时长**：Idle 状态的专注阶段，点击计时数字打开“调整本次时长”浮层：快捷芯片 `15 / 25 / 45 / 60 / 90 分钟` + 数字步进器（1–180 分钟，步长 1，按住加速）。仅作用于本次专注，不改设置默认值；重置后恢复默认值。
- 顶部分段控件：`专注 | 短休息 | 长休息 | 正计时`。在 Running/Paused 且当前为专注且有效时长 ≥ 60 秒时切换，弹确认框（文案见 3.2）。

#### 2.1.2 状态机（Core `PomodoroEngine`，详细契约见 4.4）
状态 `Idle / Running / Paused` × 阶段 `Focus / ShortBreak / LongBreak` × 模式 `Pomodoro / Stopwatch`。

| 操作 | Idle | Running | Paused |
|---|---|---|---|
| 开始 Start | → Running | 无操作 | 无操作 |
| 暂停 Pause | 无操作 | → Paused | 无操作 |
| 继续 Resume | 无操作 | 无操作 | → Running |
| 切换 Toggle（空格/主按钮） | Start | Pause | Resume |
| 重置 Reset | 恢复默认时长 | → Idle 同阶段满时长；专注有效 ≥60s 记录 `Interrupted` | 同左 |
| 跳过 Skip | → 下一阶段 | → 下一阶段；专注有效 ≥60s 记录 `Interrupted` | 同左 |
| +5 分钟 | 计划 +5 | 结束时间 +5、计划 +5 | 剩余 +5、计划 +5 |

- **计时以墙钟为准**：Running 时保存 `PhaseEndUtc`，剩余 = End − Now；暂停保存剩余秒数。系统睡眠/卡顿不产生漂移。
- **自然完成**：Running 且 Now ≥ End → 记录 `Completed` 会话（结束时间 = End 而非 Now）；专注完成则 `CompletedFocusInCycle++`、绑定任务的已完成番茄 +1；切到下一阶段。
- **下一阶段规则**：专注之后 → `CompletedFocusInCycle >= LongBreakInterval ? LongBreak : ShortBreak`；任意休息之后 → Focus；长休息结束（完成或跳过）后 `CompletedFocusInCycle = 0`。
- **自动开始**：专注→休息看 `AutoStartBreaks`；休息→专注看 `AutoStartFocus`；Skip 同样遵循该规则；若完成时已超期 > 5 分钟（如电脑睡眠），下一阶段一律 Idle。
- **+5 分钟**上限：单阶段计划时长 ≤ 240 分钟；正计时模式隐藏该按钮。
- **正计时**：Skip 文案变为“完成”，有效 ≥60s 记录 `Completed`，否则丢弃；Reset 在有效 ≥60s 时先确认“放弃本次计时？”，丢弃不记录。
- **每日目标**：设置 `DailyGoal`（默认 8 个番茄）。今日番茄数 = 今日完成的番茄模式专注数 + Σ floor(正计时有效秒数 / (FocusMinutes×60))。
- **新一天起点** `DayStartHour`（0–6，默认 0）：日期归属 = (本地时间 − DayStartHour 小时).Date；会话按**开始时间**归日。
- **结束前 1 分钟提醒**（可选，默认关）：剩余 60 秒时播放一次轻滴答提示。
- **崩溃 / 重启恢复**：每次状态迁移即保存 `timer_state.json`；启动时若设置 `RestoreTimerOnLaunch`（默认开）：Running 且未到期 → 继续；已到期 → 以 End 时刻补记完成并进入下一阶段 Idle；Paused → 保持 Paused。

#### 2.1.3 完成时的反馈
1. 提示音（设置所选，见 2.4.3）；
2. 桌面通知（托盘气泡，Win11 显示为系统通知）+ 应用内完成卡片（ToastWindow，见 3.8.9）；
3. 主窗口可见时：进度环光晕闪烁 + 数字轻微弹跳 + 18 颗粒子从环外缘迸发（见 3.9）；
4. 达成每日目标时额外一次“今日目标达成”通知（每天仅一次）；
5. 绑定任务达到预估番茄数时通知“「任务名」已达到预估番茄数”，按钮 `标记完成 / 继续专注`。

### 2.2 任务管理
- 字段：标题（必填 1–80 字）、备注（≤500 字）、优先级（无/低/中/高）、标签（每项 ≤12 字，最多 5 个）、预估番茄（0–20，0 表示未预估）、已完成番茄（由会话自动累计，不可手改）、完成状态、创建/完成时间。
- **快速添加栏**：回车创建，支持内联语法（Core `QuickAddParser`，需单测）：
  - `#标签`（可多个）；`!高 / !中 / !低` 或 `!3 / !2 / !1`；`~4` 或 `*4` 设预估；其余文字为标题，首尾空白折叠。
  - 例：`写周报 #工作 !高 ~3` → 标题“写周报”、标签[工作]、高优先级、预估 3。
- 新建 / 编辑：模态对话框（3.8.10）。删除：立即删除 + 底部 Snackbar“已删除「xx」 撤销”（6 秒内可撤销）。会话记录保留并保留任务标题快照。
- 完成 / 取消完成：圆形勾选框，带弹跳动画；完成的任务若正绑定当前专注 → 自动解绑并提示。
- 筛选：`进行中 | 已完成 | 全部`；标签芯片多选过滤（OR 逻辑）；搜索（标题/备注/标签，不区分大小写，输入防抖 200ms）。
- 排序下拉：`智能排序`（默认：未完成在前 → 优先级降序 → 创建时间升序）/ `优先级` / `创建时间（新→旧）` / `剩余番茄（多→少）`。
- **绑定当前专注任务**：专注页任务选择器（3.8.3）或任务行的“开始专注”按钮（绑定 + 跳转专注页 + 若 Idle 且当前为专注阶段则立即开始）。绑定任务在列表中显示“专注中”徽标。

### 2.3 专注统计与可视化
- KPI 卡片（4 张）：**今日专注**（番茄数 + 时长）、**本周专注**（周一为一周起点，时长 + 较上周同期变化）、**连续专注天数**（截止今天或昨天、每天 ≥1 个完成专注即算）、**累计专注**（总时长 + 完成率 = Completed /(Completed+Interrupted) 专注数）。
- **专注趋势柱状图**：切换 `近 7 天 | 近 30 天`，每日专注分钟；虚线目标线 = DailyGoal × FocusMinutes；今日柱高亮；悬停提示。
- **专注时段分布**（24 小时面积折线）：所选范围（随趋势图的 7/30 天）内每小时专注分钟，跨小时会话按比例拆分；副标题给出“高效时段 09:00–11:00”（滑动 2 小时窗口最大值）。
- **分布环形图**：切换 `标签 | 任务`，Top 5 + “其他”；无标签计为“未分类”，无任务计为“未关联任务”。
- **专注热力图**：GitHub 风格，按周列 × 周一至周日行，列数随宽度自适应（12–53 周）；等级按当日番茄数 0 / 1–2 / 3–4 / 5–7 / ≥8 → L0–L4。
- **历史记录明细**：按日分组（“11月19日 周四 · 3 个番茄 · 1 小时 15 分”），行内容：时间段、类型徽标、任务标题、时长、结果（完成/中断）；筛选 `全部 | 专注 | 休息`；先显示最近 50 条，底部“加载更多”；行悬停出现删除按钮（Snackbar 撤销）。
- **导出 CSV**：SaveFileDialog（默认文件名 `番茄专注记录_20261119.csv`），UTF-8 BOM，列：`日期,开始时间,结束时间,类型,模式,结果,计划分钟,实际分钟,任务,标签`。
- 统计计算全部在 Core `StatsService`（纯函数，可单测），时长统计口径：**仅 Completed 专注**（两种模式）的有效秒数。

### 2.4 环境音与提示音（全部代码实时合成，零音频文件，纯离线）

#### 2.4.1 环境音列表（9 种，可多选叠加混音，每路独立音量）
| Id | 名称 | 图标（Lucide 路径见 3.5.2） | 卡片色调 Tint |
|---|---|---|---|
| `rain` | 细雨 | rain | `#60A5FA` |
| `ocean` | 海浪 | waves | `#22D3EE` |
| `forest` | 林间鸟鸣 | tree | `#4ADE80` |
| `cafe` | 咖啡馆 | coffee | `#D4A373` |
| `fire` | 篝火 | flame | `#FB923C` |
| `wind` | 微风 | wind | `#A5B4FC` |
| `white` | 白噪音 | wave | `#94A3B8` |
| `brown` | 棕噪音 | moon | `#C08457` |
| `clock` | 时钟滴答 | clock | `#F472B6` |

- 主音量 0–100；每路音量 0–100；开/关淡入淡出 600ms。
- 预设组合芯片：`雨夜书房`（rain 70 + fire 35 + clock 20）、`林间晨光`（forest 70 + wind 30）、`海边咖啡`（ocean 50 + cafe 55）、`深度专注`（brown 60）、`全部停止`。
- 定时关闭：`不限 / 15 / 30 / 60 / 90 分钟`，到时 3 秒淡出全部。
- 联动规则（设置项）：`仅在专注时播放`（默认开：进入休息淡出，回到专注 Running 淡入）、`暂停计时时静音`（默认开）。若两项均开且当前非专注 Running，点亮卡片只记录“已启用”，状态说明显示“将在专注开始时播放”。
- 无音频设备 / waveOutOpen 失败：页面顶部警示条“未检测到可用的音频输出设备”，卡片禁用。

#### 2.4.2 合成算法规格（Core `Audio/`，44100 Hz，立体声 float，左右声道独立噪声源以获得空间感）
- 随机数：`XorShift32`（可设种子，测试确定性）。白噪声 w ∈ [−1,1]。
- **粉红噪声**：Paul Kellet 精简滤波 `b0=0.99765b0+w·0.0990460; b1=0.96300b1+w·0.2965164; b2=0.57000b2+w·1.0526913; p=(b0+b1+b2+w·0.1848)·0.18`。
- **棕噪声**：`b=(b+0.02w)/1.02; out=b·3.5`。
- 滤波器：一阶低通/高通 + 状态变量滤波器 SVF（带通，参数 fc、Q）放在 `Filters.cs`。
- `rain`：粉红噪声经 400Hz 高通 ×0.5 + 雨滴（每声道泊松到达率 30/s；每滴 8–25ms 白噪声爆发、指数衰减，SVF 带通 fc 随机 2–6kHz、Q=2，幅度 0.1–0.5）+ 棕噪声 ×0.15 低频底噪。
- `ocean`：棕噪声 × 包络 e(t)=0.35+0.65·(0.5−0.5cos(2πt/T))，每个浪周期 T 随机 7–11s；低通截止随包络 400→1800Hz；右声道相位滞后 0.6s。
- `forest`：风底（粉红噪声 800Hz 低通 × (0.5+0.5·LFO 0.07Hz) ×0.4）+ 鸟鸣（每 2–7s 一串 2–5 个音符，单音 60–180ms，正弦扫频 f0∈[2500,4500]Hz → f0×[0.8,1.3]，30Hz 颤音 ±5%，5ms 起音、指数释放，随机声像 −0.6..0.6）。
- `cafe`：人声底噪（粉红噪声 250–1200Hz 带通 × 3 个 0.3–1.5Hz 随机 LFO 之和的幅度调制）+ 杯碟轻碰（每 3–9s，分音 2300/3700/5900Hz 幅度 1/0.6/0.3、τ=120ms，随机声像）+ 棕噪声 ×0.1。
- `fire`：棕噪声 300Hz 低通 ×0.5 + 噼啪（泊松 6/s，1–4ms 白噪声经 1.5kHz 高通，幅度 0.2–0.9）+ 爆裂（0.5/s，15ms 150Hz 衰减正弦）。
- `wind`：粉红噪声经 SVF 带通，中心频率由 0.05–0.13Hz LFO 在 300–900Hz 间扫动，幅度 LFO 叠加。
- `white`：w × 0.25；`brown`：上式；`clock`：每 1.0s 一次，“嘀/嗒”交替（2000Hz / 1600Hz 衰减正弦 6ms + 2ms 噪声咔嗒）。
- **电平标准**：每路在音量 100 时 RMS 目标 ≈ 0.12（约 −18 dBFS）；混音后软削波 `y = x / (1 + |x|·0.6)` 于 |x|>0.8 区间平滑过渡，确保输出 ∈ [−1,1]。增益变化逐样本一阶平滑（时间常数 50ms），杜绝咔哒声。

#### 2.4.3 提示音（一次性合成音，≤4s）
| Id | 名称 | 规格 |
|---|---|---|
| `chime` | 风铃（默认） | C6/E6/G6（1047/1319/1568Hz）琶音，间隔 120ms，每音正弦 + 0.2 倍二次谐波，τ=0.9s |
| `bowl` | 颂钵 | 528Hz + 531.5Hz（拍频）+ 1452Hz×0.3，起音 15ms，τ=2.2s |
| `wood` | 木鱼 | 两击（0/280ms）：820Hz + 1650Hz×0.4，τ=40ms + 3ms 噪声咔嗒 |
| `digital` | 电子 | 880Hz（正弦 + 0.3 三次谐波）120ms 开 / 80ms 关 × 2，两组间隔 400ms |
| `none` | 无 | 静音 |

设置页提供“试听”按钮与独立的提示音音量（0–100，默认 70）。提示音与环境音走同一混音器，提示音播放时环境音自动闪避（ducking）至 40%，结束后 400ms 恢复。

#### 2.4.4 播放实现（App `WaveOutPlayer`）
- winmm `waveOutOpen` + **CALLBACK_EVENT**（`AutoResetEvent.SafeWaitHandle`）+ 专用后台线程（`IsBackground=true`，`ThreadPriority.AboveNormal`）；PCM 16-bit 立体声 44100Hz；4 个缓冲 × 2205 帧（50ms）。
- 循环：等待事件 → 遍历 `WAVEHDR.dwFlags & WHDR_DONE` 的缓冲 → `mixer.Render()` 填充 → `waveOutWrite`。缓冲用 `Marshal.AllocHGlobal`，WAVEHDR 同样分配在非托管内存，关闭时 `waveOutReset → waveOutUnprepareHeader → waveOutClose → FreeHGlobal`。
- **禁止**在回调中调用 waveOut API（因此使用 EVENT 模式）。
- 无任何声源且无提示音 3 秒后关闭设备（空闲零 CPU）；有声源时惰性打开。
- `--no-sound` 参数：混音器照常运行（用于测试），但不打开设备。

### 2.5 桌面原生体验

#### 2.5.1 迷你悬浮窗（Mini Mode）
- 始终置顶、无边框、圆角、可拖动、贴边吸附（距工作区边缘 ≤12 DIP 自动贴齐）、位置持久化（启动时校正到可见区域）。
- 打开方式：标题栏迷你窗按钮、`Ctrl+Alt+M`、托盘菜单、`--mini` 参数。打开时若设置 `打开迷你窗时隐藏主窗口`（默认开）则隐藏主窗口。
- 双击迷你窗或点“展开”按钮 → 回到主窗口并关闭迷你窗。右键菜单：`返回主窗口 / 不透明度 ▸ (100%/90%/80%/70%) / 关闭迷你窗`。
- 布局规格见 3.8.4。

#### 2.5.2 系统托盘
- `System.Windows.Forms.NotifyIcon`；图标为**运行时绘制**的进度图标：圆环底 + 阶段色进度弧（Idle 时显示品牌 logo），每 10 秒或状态变化时刷新（尺寸取 `SystemInformation.SmallIconSize`，适配 175% DPI）。旧 HICON 用 `DestroyIcon` 释放。
- 悬停提示（≤63 字符）：`番茄专注 · 专注中 18:42` / `番茄专注 · 已暂停 12:05` / `番茄专注 · 准备专注`。
- 左键单击：显示/隐藏主窗口（迷你窗打开时则激活迷你窗）。
- 右键菜单（`ContextMenuStrip` + 自定义 `ToolStripProfessionalRenderer` 按当前主题着色，字体 Microsoft YaHei UI 9pt，项高 32px）：
  ```
  [禁用标题] 专注中 · 18:42
  开始专注 / 暂停 / 继续        (随状态变化)
  跳过当前阶段
  重置
  ────────
  迷你窗                        (勾选态)
  显示主窗口
  环境音 ▸  细雨 / 海浪 / … (勾选态) / ── / 全部停止
  ────────
  设置
  退出
  ```
- 设置 `显示托盘图标` 关闭时隐藏托盘；此时 `关闭主窗口时最小化到托盘` 自动失效（关闭即退出），设置页给出说明。

#### 2.5.3 桌面通知
- 托盘可用时：`NotifyIcon.ShowBalloonTip(5000, 标题, 正文, ToolTipIcon.None)`（Win11 渲染为系统通知）。
- 应用内完成卡片 ToastWindow（3.8.9）：右下角滑入，不抢焦点，8 秒自动关闭，悬停暂停倒计时，带操作按钮。
- 两种通知各自有设置开关。

#### 2.5.4 全局快捷键（`RegisterHotKey`，可在设置中录制修改）
| 动作 | 默认 |
|---|---|
| 开始 / 暂停 | `Ctrl+Alt+Space` |
| 跳过当前阶段 | `Ctrl+Alt+Right` |
| 显示 / 隐藏迷你窗 | `Ctrl+Alt+M` |
| 显示 / 隐藏主窗口 | `Ctrl+Alt+P` |

- 注册失败显示“已被其他程序占用”（警示色）；总开关 `启用全局快捷键`；`--no-hotkeys` 参数禁用注册。
- 组合键字符串的解析/格式化在 Core `HotkeyGesture`（需单测），要求至少一个修饰键（Ctrl/Alt/Shift/Win）。

#### 2.5.5 应用内快捷键
`Space` 开始/暂停（焦点不在文本框时）、`R` 重置、`S` 跳过、`+`/`=` 加 5 分钟、`M` 迷你窗、`Ctrl+1…5` 切换五个页面、`Ctrl+N` 新建任务（跳转任务页并聚焦快速添加栏）、`Ctrl+F` 搜索任务、`Esc` 关闭对话框/浮层。

#### 2.5.6 窗口行为
- 关闭按钮：`关闭主窗口时最小化到托盘`（默认开）→ 隐藏到托盘，**首次**隐藏时气泡提示“番茄专注仍在后台运行，可从托盘图标恢复”；否则退出。
- 单实例：命名 Mutex `Local\PomodoroFocus_<数据目录路径小写的 SHA1 前 12 位>`；第二个实例通过同名 `EventWaitHandle` 通知第一个实例显示主窗口后以退出码 0 结束（`--screenshot` 模式跳过单实例）。
- 开机自启：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 值名 `PomodoroFocus`，值 `"<exe 路径>" --minimized`；写入失败 Snackbar 报错并回滚开关。`--minimized` 启动只显示托盘（无托盘时正常显示窗口）。
- 主窗口位置、尺寸、最大化状态、上次页面持久化。

#### 2.5.7 纯本地持久化
- 目录：`%LocalAppData%\PomodoroFocus\`（`--data-dir` 可覆盖）。文件：`settings.json`、`tasks.json`、`sessions.json`、`timer_state.json`、`logs\app-yyyyMMdd.log`（保留 7 天）。
- 写入：防抖 500ms，后台线程序列化，**原子写**：写 `x.json.tmp` → `File.Replace(tmp, x.json, x.json.bak)`（目标不存在时 `File.Move`）；退出时同步刷盘。
- 读取：JSON 损坏 → 尝试 `.bak` → 仍失败则把坏文件重命名为 `x.corrupt-yyyyMMddHHmmss.json` 并使用默认值，主窗口顶部显示信息条“数据文件损坏，已从备份恢复 / 已重置为默认值”。
- 备份：设置页“导出备份”（单个 JSON：settings + tasks + sessions）与“导入备份”（校验 schemaVersion，确认后覆盖并即时刷新所有页面）。“清空所有数据”需二次确认（输入框键入“清空”二字才可点击）。

#### 2.5.8 数据目录不可写的降级
启动探测写权限（写入并删除 `.probe` 文件）。失败 → 以内存模式运行，顶部常驻警示条“数据目录不可写，本次更改不会被保存（<路径>）”，其余功能正常。

### 2.6 外观与设置中心
- **4 套主题**（Token 见 3.3）：`深邃夜空 midnight`（默认）、`晨雾浅色 mist`、`暖阳日落 sunset`、`静谧森林 forest`；实时切换、持久化，切换时整窗 300ms 交叉淡化（3.9）。
- 所有参数均可配置（分组见 3.8.8）：时长、长休息间隔、每日目标、新一天起点、自动化规则、提醒、提示音、主题、减少动态效果、迷你窗不透明度、快捷键、开机自启、托盘行为、数据管理。
- 设置修改即时生效并保存（无“保存”按钮）；计时中修改时长时显示行内提示“将在下一个阶段生效”。


---

## 3. 现代 UI/UX 设计系统

### 3.1 设计原则
1. **安静的专注感**：大面积深色/柔和底色 + 低饱和玻璃卡片，唯一高饱和元素是“阶段色”（专注=番茄珊瑚、短休=薄荷青、长休=星云紫），视线自然聚焦在计时环上。
2. **阶段色流动（Signature）**：阶段切换时，进度环渐变、主按钮、窗口背景左上角光晕、迷你窗光边、托盘图标**同步**以 600ms ColorAnimation 过渡到新阶段色。
3. **玻璃层次**：背景（Bg.Base + 两团大径向光晕）→ 卡片（半透明 Surface.1 + 1px Stroke.Subtle 边 + 顶部 1px 高光）→ 浮层（不透明 Surface.Solid + 阴影 Elevation2/3）。
4. **数字永不抖动**：所有变化中的数字使用 tabular 数字。
5. **一切可达**：任何视图在最小窗口尺寸下不截断、不重叠、不越界；长文本省略号 + ToolTip。
6. **克制的动效**：120–300ms、CubicEase/QuinticEase EaseOut；尊重“减少动态效果”。

### 3.2 简体中文文案规范
- 语气：简洁、温和、正向；不用感叹号堆叠（最多 1 个），不用网络用语，不用 emoji（WPF 不渲染彩色 emoji）。
- 中英文/数字之间加半角空格：“25 分钟”“第 3 个番茄”“Ctrl + Alt + M”（快捷键展示用 `键帽` 芯片，各键之间有间隔）。
- 时长格式（Core `Formatters`，需单测）：`< 1 分钟` → “不足 1 分钟”；`< 60 分钟` → “25 分钟”；`≥ 60 分钟` → “1 小时 05 分”；KPI 大数字场景 → 数值 “2.5” + 单位 “小时”。计时器：`< 1 小时` → `MM:SS`；`≥ 1 小时` → `H:MM:SS`。
- 日期：`11月19日 周四`；跨年显示 `2025年12月31日 周三`；时间 24 小时制 `09:05`；时间段 `09:12 – 09:37`（en dash 两侧空格）。
- 按钮用动词：“开始专注”“保存”“删除”；破坏性操作按钮用“删除 / 清空”，确认框标题用问句。

**核心文案表（必须逐字使用）**

| 场景 | 文案 |
|---|---|
| 导航 | 专注 / 任务 / 统计 / 环境音 / 设置 |
| 阶段标签 | 专注中 / 短休息 / 长休息 / 正计时；Idle 前缀“准备”：准备专注 / 准备休息 / 准备长休息 / 准备计时 |
| 暂停态 | 已暂停 |
| 计时副标题 | `第 {n} 个番茄 · 预计 {HH:mm} 结束`；Idle：`点击数字可调整本次时长`；正计时：`已专注 {时长}` |
| 主按钮 Tooltip | 开始（空格）/ 暂停（空格）/ 继续（空格） |
| 次按钮 | 重置（R）/ 跳过（S）/ 完成（正计时）/ ＋5 分钟 |
| 任务选择器 | 未选择时：`选择要专注的任务`；已选：`{任务标题}` + `{已完成}/{预估}`；菜单底部：`不关联任务`、`＋ 新建任务` |
| 切换阶段确认 | 标题“要中断当前专注吗？” 正文“本次已专注 {时长}，切换后将记为中断。” 按钮“继续专注 / 中断并切换” |
| 正计时放弃确认 | 标题“放弃本次计时？” 正文“已计时 {时长}，放弃后不会记录。” 按钮“取消 / 放弃” |
| 今日目标卡 | 标题“今日目标”；`{n} / {goal} 个番茄`；`已专注 {时长} · 还差 {k} 个`；达成：`今日目标已达成` |
| 今日记录空态 | “还没有专注记录” / “开始你的第一个番茄吧” |
| 任务页 | 标题“任务”；副标题 `共 {n} 项 · 已完成 {m} 项`；搜索占位“搜索任务…”；按钮“新建任务”；快速添加占位“添加任务，回车保存（#标签 !高 ~3 预估）” |
| 任务空态 | 进行中：“没有进行中的任务” / “在上方输入框添加一个吧”；已完成：“还没有完成的任务”；搜索无结果：“没有找到匹配的任务” |
| 任务编辑 | 对话框标题“新建任务 / 编辑任务”；字段：标题、备注、优先级（无/低/中/高）、预估番茄、标签（占位“输入后回车添加”）；按钮“取消 / 保存”；校验“请输入任务标题” |
| 删除 Snackbar | `已删除「{标题}」` + 操作“撤销” |
| 统计页 | KPI：今日专注 / 本周专注 / 连续专注 / 累计专注；卡片：专注趋势、专注时段分布、专注分布、专注热力图、历史记录；按钮“导出 CSV”；热力图图例“少 … 多” |
| 统计空态 | “暂无数据，完成一个番茄后这里会出现你的专注轨迹” |
| 环境音页 | 标题“环境音”；副标题“全部由本地实时合成，离线可用”；主音量；“定时关闭”；联动开关：仅在专注时播放 / 暂停计时时静音；卡片状态：点击播放 / 播放中 / 将在专注开始时播放 |
| 通知 · 专注完成 | 标题“专注完成”；正文“已完成「{任务}」的第 {n} 个番茄，休息 {m} 分钟吧。”（无任务：“又完成了一个番茄，休息 {m} 分钟吧。”）；按钮“开始休息 / 跳过休息”（休息已自动开始时只有“跳过休息”） |
| 通知 · 休息结束 | 标题“休息结束”；正文“准备好开始下一个番茄了吗？”；按钮“开始专注 / 稍后” |
| 通知 · 目标达成 | 标题“今日目标达成”；正文“今天已完成 {n} 个番茄，做得很好。” |
| 通知 · 预估达成 | 标题“已达到预估番茄数”；正文“「{任务}」已完成 {n} 个番茄。”；按钮“标记完成 / 继续专注” |
| 托盘首次隐藏 | “番茄专注仍在后台运行，可从托盘图标恢复” |
| 设置分组 | 计时 / 自动化 / 提醒与声音 / 外观 / 快捷键 / 系统 / 数据 / 关于 |

### 3.3 Design Tokens

所有颜色以 `SolidColorBrush`/`Color` 资源形式定义在 `Themes/Tokens.<Theme>.xaml`，键名完全一致，界面中**只能**用 `{DynamicResource Key}` 引用，**禁止**硬编码颜色（Tint 色与阶段色动画画刷除外）。ARGB 写法 `#AARRGGBB`。

#### 3.3.1 颜色（四主题）

| Token Key | 深邃夜空 midnight | 晨雾浅色 mist | 暖阳日落 sunset | 静谧森林 forest |
|---|---|---|---|---|
| `IsDark`（x:Boolean） | True | False | True | True |
| `Bg.Base` | `#0B0E14` | `#F3F5F9` | `#170F12` | `#09120F` |
| `Bg.Glow2`（右下光晕，60% 处透明） | `#244F46E5` | `#176366F1` | `#339D3C72` | `#1A0EA5E9` |
| `Bg.Glow1.Alpha`（左上阶段色光晕不透明度 x:Double） | 0.22 | 0.14 | 0.24 | 0.18 |
| `Sidebar.Bg` | `#66070A0F` | `#99FFFFFF` | `#660C0709` | `#66050B09` |
| `Surface.1`（卡片） | `#0BFFFFFF` | `#D9FFFFFF` | `#0DFFF3EC` | `#0BEFFFF6` |
| `Surface.2`（悬停/抬升） | `#14FFFFFF` | `#FFFFFFFF` | `#17FFF3EC` | `#14EFFFF6` |
| `Surface.3`（按下/输入框底） | `#1FFFFFFF` | `#0D0F172A` | `#21FFF3EC` | `#1FEFFFF6` |
| `Surface.Solid`（菜单/对话框/迷你窗/Toast） | `#161B26` | `#FFFFFF` | `#22171B` | `#121D19` |
| `Overlay`（对话框遮罩） | `#99000000` | `#660F172A` | `#99000000` | `#99000000` |
| `Stroke.Subtle` | `#14FFFFFF` | `#140F172A` | `#17FFE8DC` | `#14E6FFF2` |
| `Stroke.Strong` | `#26FFFFFF` | `#260F172A` | `#29FFE8DC` | `#26E6FFF2` |
| `Stroke.Highlight`（卡片顶部内高光） | `#1AFFFFFF` | `#FFFFFFFF` | `#1CFFF3EC` | `#1AEFFFF6` |
| `Text.Primary` | `#F2F4F8` | `#0F172A` | `#FFF4EC` | `#EAF5EF` |
| `Text.Secondary` | `#A0A8B8` | `#475569` | `#CBB5AA` | `#9BB3A8` |
| `Text.Tertiary` | `#687083` | `#8A97AB` | `#8C7671` | `#62786E` |
| `Text.Disabled` | `#454B59` | `#B8C2D0` | `#5A4A47` | `#3F4F48` |
| `Accent` | `#FF6B5E` | `#E5484D` | `#FF8A4C` | `#3DDC97` |
| `Accent.Hover` | `#FF8176` | `#EC5D61` | `#FF9E6B` | `#5BE3A9` |
| `Accent.Pressed` | `#E85A4E` | `#CE3B40` | `#EB773A` | `#2FC283` |
| `Accent.Soft`（标签底/选中底） | `#1FFF6B5E` | `#1AE5484D` | `#1FFF8A4C` | `#1F3DDC97` |
| `Text.OnAccent` | `#FFFFFF` | `#FFFFFF` | `#2A1206` | `#052E1C` |
| `Phase.Focus.Start / End` | `#FF6B5E` / `#FF9F68` | `#EF4E4E` / `#F7894A` | `#FF6A5C` / `#FFB45E` | `#FF7A66` / `#FFB37A` |
| `Phase.Short.Start / End` | `#2DD4BF` / `#7DF0C8` | `#0FA98F` / `#36C99B` | `#4FC9B8` / `#A3E8C4` | `#3DDC97` / `#A7F3D0` |
| `Phase.Long.Start / End` | `#7C83FF` / `#B892FF` | `#5B5FEF` / `#8B5CF6` | `#B57BFF` / `#F0A3E6` | `#5AA9FA` / `#A78BFA` |
| `Success` | `#34D399` | `#059669` | `#6EE7B7` | `#4ADE80` |
| `Warning` | `#FBBF24` | `#D97706` | `#FCD34D` | `#FACC15` |
| `Danger` | `#F87171` | `#DC2626` | `#FB7185` | `#F87171` |
| `Priority.High / Medium / Low` | `#F87171` / `#FBBF24` / `#60A5FA` | `#DC2626` / `#D97706` / `#2563EB` | `#FB7185` / `#FCD34D` / `#93C5FD` | `#F87171` / `#FACC15` / `#60A5FA` |
| `Ring.Track` | `#12FFFFFF` | `#120F172A` | `#14FFF3EC` | `#12EFFFF6` |
| `Ring.Tick` | `#26FFFFFF` | `#2E0F172A` | `#29FFF3EC` | `#26EFFFF6` |
| `Chart.Grid` | `#0FFFFFFF` | `#120F172A` | `#12FFF3EC` | `#0FEFFFF6` |
| `Heat.L0` | `#0FFFFFFF` | `#0F0F172A` | `#12FFF3EC` | `#0FEFFFF6` |
| `Heat.L1–L4` | Accent 不透明度 28% / 50% / 75% / 100%（四主题通用规则，由 Accent 计算写入资源） ||||
| `Shadow.Color` / `Shadow.Opacity` | `#000000` / 0.45 | `#1E293B` / 0.12 | `#000000` / 0.45 | `#000000` / 0.45 |
| `Caption.CloseHover` | `#C42B1C`（四主题相同，悬停文字白） ||||
| `DwmBorder`（DWMWA_BORDER_COLOR） | `#2A303C` | `#D5DBE5` | `#3A2A2E` | `#22332C` |

**阶段色动态画刷**（由 `ThemeService` 在代码中创建、**不冻结**，放入 `Application.Resources`，阶段/主题变化时对其 `Color` 做 600ms ColorAnimation）：
`Phase.StartBrush`、`Phase.EndBrush`、`Phase.Gradient`（LinearGradientBrush 0,0→1,1：Start→End）、`Phase.SoftBrush`（Start @ 14%）、`Phase.GlowBrush`（Start @ Bg.Glow1.Alpha，用于窗口左上光晕 RadialGradient 中心色）、`Phase.Color`（Color，用于 DropShadowEffect）。

#### 3.3.2 圆角 / 间距 / 尺寸
| Token | 值 | 用途 |
|---|---|---|
| `Radius.Tag` | 6 | 标签、键帽 |
| `Radius.Chip` | 8 | 芯片、小按钮 |
| `Radius.Control` | 10 | 按钮、输入框、下拉、导航项 |
| `Radius.Row` | 12 | 列表行、Snackbar |
| `Radius.Card` | 16 | 卡片、声音卡、Toast |
| `Radius.Mini` | 18 | 迷你窗 |
| `Radius.Dialog` | 20 | 对话框 |
| `Radius.Pill` | 999 | 分段控件、任务选择器、圆形按钮 |
| 间距刻度 | 4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 48 | 4pt 网格，禁止出现其他值（1px 描边除外） |
| 控件高度 | 小 28 / 标准 36 / 大 44 / 输入大 48 | |
| 页面内边距 | 左右 32、上 24、下 24（窗口宽 < 1000 时左右 24） | |
| 卡片内边距 | 20（紧凑卡 16） | |
| 卡片间距 | 16（栅格 gutter 24） | |

#### 3.3.3 阴影 / 层级
| 层级 | 规格（DropShadowEffect） | 用途 |
|---|---|---|
| E0 | 无阴影，靠 Stroke.Subtle + Stroke.Highlight 分层 | 暗色主题卡片 |
| E1 | Blur 16, Depth 2, Direction 270, Opacity = Shadow.Opacity×0.5 | 浅色主题卡片 |
| E2 | Blur 24, Depth 6, Opacity = Shadow.Opacity | 下拉/菜单/浮层/提示框 |
| E3 | Blur 40, Depth 12, Opacity = Shadow.Opacity×1.2（上限 0.6） | 对话框、迷你窗、Toast |
| Glow | Color = Phase.Color, Blur 28, Depth 0, Opacity 0.55（mist 0.35） | 主按钮、进度环头部光点 |

性能约束：列表项**禁止**逐项使用 DropShadowEffect；卡片阴影仅浅色主题启用。

#### 3.3.4 动效 Token
| Token | 时长 | 缓动 |
|---|---|---|
| `Motion.Fast` | 120ms | CubicEase EaseOut（悬停、按下） |
| `Motion.Normal` | 200ms | CubicEase EaseOut（页面切换、浮层） |
| `Motion.Slow` | 300ms | QuinticEase EaseOut（分段指示器、主题淡化） |
| `Motion.Phase` | 600ms | SineEase EaseInOut（阶段色流动） |
| `Motion.Chart` | 500ms | CubicEase EaseOut（图表生长） |
`减少动态效果` 开启或 `SystemParameters.ClientAreaAnimation == false` 时，所有时长取 0（直接到终态），粒子与呼吸动画关闭。由 `AnimationHelper.Duration(token)` 统一读取。

### 3.4 字体层级
- `Font.UI` = `Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI`
- `Font.Display` = `Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI`
- `Font.Icon` = `Segoe Fluent Icons, Segoe MDL2 Assets`
- 全局：`TextOptions.TextFormattingMode="Ideal"`；多行中文 `LineHeight = 字号×1.5`、`LineStackingStrategy="BlockLineHeight"`。
- 所有动态数字：`Typography.NumeralAlignment="Tabular"`（已实测 Segoe UI Variable Display 生效）。

| 样式 Key | 字体 | 字号 | 字重 | 颜色 | 用途 |
|---|---|---|---|---|---|
| `Text.Timer` | Display | 76 | Light | Text.Primary | 主计时数字（≥1 小时时 60） |
| `Text.TimerMini` | Display | 30 | SemiLight | Text.Primary | 迷你窗计时 |
| `Text.KPI` | Display | 32 | SemiBold | Text.Primary | 统计大数字 |
| `Text.PageTitle` | Display | 28 | SemiBold | Text.Primary | 页面标题 |
| `Text.SectionTitle` | UI | 18 | SemiBold | Text.Primary | 设置分组、对话框标题 |
| `Text.CardTitle` | UI | 15 | SemiBold | Text.Primary | 卡片标题 |
| `Text.BodyStrong` | UI | 14 | SemiBold | Text.Primary | 列表标题、按钮 |
| `Text.Body` | UI | 14 | Regular | Text.Primary | 正文 |
| `Text.Secondary` | UI | 13 | Regular | Text.Secondary | 副标题、说明 |
| `Text.Caption` | UI | 12 | Regular | Text.Tertiary | 辅助说明、时间 |
| `Text.Micro` | UI | 11 | Regular | Text.Tertiary | 坐标轴、标签芯片 |
| `Text.PhaseLabel` | UI | 14 | SemiBold | Phase.StartBrush | 环内阶段标签 |

### 3.5 图标

#### 3.5.1 Segoe Fluent Icons 码位（已在本机逐一渲染核对）
| 用途 | 码位 | | 用途 | 码位 |
|---|---|---|---|---|
| 开始（主按钮，实心） | `F5B0` | | 暂停（主按钮，实心） | `F8AE` |
| 重置 | `E777` | | 跳过 | `E893` |
| 正计时/秒表 | `E916` | | 时钟（导航·专注） | `E917` |
| 任务（导航） | `E9D5` | | 统计（导航） | `E9D2` |
| 环境音（导航，耳机） | `E7F6` | | 设置（导航） | `E713` |
| 新建 | `E710` | | 删除 | `E74D` |
| 编辑 | `E70F` | | 搜索 | `E721` |
| 未完成圆框 | `EA3A` | | 已完成实心勾 | `EC61` |
| 勾 | `E73E` | | 关闭/取消 | `E711` |
| 标题栏 最小化/最大化/还原/关闭 | `E921` / `E922` / `E923` / `E8BB` | | 置顶 / 取消置顶 | `E718` / `E77A` |
| 进入迷你窗 | `EE49` | | 迷你窗返回主窗口 | `EE47` |
| 标签 | `E8EC` | | 优先级旗标 | `E7C1` |
| 排序 | `E8CB` | | 日历 | `E787` |
| 音量 / 静音 | `E767` / `E74F` | | 提醒（铃） | `EA8F` |
| 主题/外观 | `E790` | | 键盘 | `E765` |
| 电源/开机自启 | `E7E8` | | 文件夹 | `E838` |
| 导出 / 导入 | `EDE1` / `E8B5` | | 信息 | `E946` |
| 警告 | `E7BA` | | 下拉箭头 / 右箭头 | `E70D` / `E76C` |
| 更多 | `E712` | | 火焰/连续天数 | 用 Lucide `flame` |

XAML 中以 `&#xF5B0;` 书写，集中定义在 `Themes/Icons.xaml`（`<sys:String x:Key="Icon.Play">&#xF5B0;</sys:String>`）。单测需校验所有使用的码位存在于 `Segoe Fluent Icons` 的 `GlyphTypeface.CharacterToGlyphMap`（该测试放在 UITests 自检阶段，因 Core 不引用 WPF）。

#### 3.5.2 环境音线性图标（Lucide 风格，24×24 网格，已在 WPF 中渲染核对）
以 `Path`（`Stretch=Uniform`, `Stroke`, `StrokeThickness=1.75`, 圆角端点与连接, 无填充）绘制，存为 `Geometry` 资源 `Geo.<name>`（README 注明 Lucide ISC 许可）：
```
rain   M4 14.9 A7 7 0 1 1 15.71 8 H17.5 A4.5 4.5 0 0 1 20 16.24 M16 14 V20 M8 14 V20 M12 16 V22
waves  M2 6 C2.6 6.5 3.2 7 4.5 7 C7 7 7 5 9.5 5 C12.1 5 11.9 7 14.5 7 C17 7 17 5 19.5 5 C20.8 5 21.4 5.5 22 6 M2 12 C2.6 12.5 3.2 13 4.5 13 C7 13 7 11 9.5 11 C12.1 11 11.9 13 14.5 13 C17 13 17 11 19.5 11 C20.8 11 21.4 11.5 22 12 M2 18 C2.6 18.5 3.2 19 4.5 19 C7 19 7 17 9.5 17 C12.1 17 11.9 19 14.5 19 C17 19 17 17 19.5 17 C20.8 17 21.4 17.5 22 18
tree   M17 14 L20 17.3 A1 1 0 0 1 19.3 19 H4.7 A1 1 0 0 1 4 17.3 L7 14 H6.7 A1 1 0 0 1 6 12.3 L9 9 H8.8 A1 1 0 0 1 8 7.3 L12 3 L16 7.3 A1 1 0 0 1 15.2 9 H15 L18 12.3 A1 1 0 0 1 17.3 14 Z M12 22 V19
coffee M10 2 V4 M14 2 V4 M6 2 V4 M16 8 A1 1 0 0 1 17 9 V17 A4 4 0 0 1 13 21 H7 A4 4 0 0 1 3 17 V9 A1 1 0 0 1 4 8 H18 A4 4 0 1 1 18 16 H17
flame  M8.5 14.5 A2.5 2.5 0 0 0 11 12 C11 10.62 10.5 10 10 9 C8.93 6.86 9.78 4.95 12 3 C12.5 5.5 14 7.9 16 9.5 C18 11.1 19 13 19 15 A7 7 0 1 1 5 15 C5 13.85 5.43 12.71 6 12 A2.5 2.5 0 0 0 8.5 14.5 Z
wave   M2 13 A2 2 0 0 0 4 11 V7 A2 2 0 0 1 8 7 V20 A2 2 0 0 0 12 20 V4 A2 2 0 0 1 16 4 V17 A2 2 0 0 0 20 17 V13 A2 2 0 0 1 22 11
wind   M12.8 19.6 A2 2 0 1 0 14 16 H2 M17.5 8 A2.5 2.5 0 1 1 19.5 12 H2 M9.8 4.4 A2 2 0 1 1 11 8 H2
clock  M12 2 A10 10 0 1 1 11.99 2 Z M12 6 V12 L16 14
moon   M12 3 A6 6 0 0 0 21 12 A9 9 0 1 1 12 3 Z
```
> 注意：WPF 路径迷你语言**不接受** `.6.5` 这类紧凑写法，所有数字须以空格分隔（上表已处理）。

#### 3.5.3 品牌 Logo（`DrawingImage` 资源 `Logo`，同时用于窗口图标、侧栏、托盘 Idle 图标与 `AppIcon.ico`）
256×256 画布：
1. 圆角方形 `Rect 0,0,256,256 RadiusX/Y=60`，`LinearGradient (0,0)→(1,1)`：`#FF9F68 → #FF5A5F`；
2. 白色进度环：圆心 (128,142)、半径 70、描边 22、圆端，弧从 −60°（顶部偏右）顺时针 300° 到 240°，顶部留缺口；
3. 叶片：`M128 82 C112 56 132 36 162 40 C158 64 144 78 128 82 Z`，填充 `#34D399`；叶柄 `M128 82 L128 60`，描边 `#34D399` 8 圆端。
`tools/New-AppIcon.ps1`（PowerShell + WPF 渲染）生成 16/24/32/48/64/256 六个 PNG 帧并打包为 `src/PomodoroFocus.App/Assets/AppIcon.ico`（PNG 压缩 ICO 条目），生成结果入库；脚本只写入该路径。

### 3.6 基础控件规格（`Themes/Controls/*.xaml`，全部为隐式或命名 Style + ControlTemplate）

| 控件 | 规格 |
|---|---|
| **Button.Primary** | 高 36、左右内边距 16、圆角 10、背景 `Accent`（专注页的主按钮用 `Phase.Gradient`）、文字 `Text.OnAccent` 14 SemiBold；悬停 `Accent.Hover`；按下 `Accent.Pressed` + ScaleTransform 0.97（80ms）；禁用不透明度 0.4 |
| **Button.Secondary** | 背景 `Surface.2`、1px `Stroke.Subtle`、文字 `Text.Primary`；悬停 `Surface.3` |
| **Button.Ghost** | 透明背景；悬停 `Surface.2`；用于工具按钮/图标按钮（图标按钮 32×32 或 36×36，图标 16） |
| **Button.Danger** | 背景 `Danger`、文字白；仅在确认对话框中使用 |
| **Button.CircleIcon** | 圆形，尺寸参数化（48/36/28），背景 `Surface.2` + 1px `Stroke.Subtle`；悬停 `Surface.3`、图标颜色 `Text.Secondary → Text.Primary` |
| **Button.TimerPrimary** | 76×76 圆形，背景 `Phase.Gradient`，图标 28 白色（`F5B0`/`F8AE`），Glow 阴影；悬停 Scale 1.04；按下 0.96；Idle 时外圈呼吸光环（直径 76→92、不透明度 0.35→0，2.4s 循环，减少动效时关闭） |
| **Caption 按钮** | 46×40，图标 10（Fluent 码位），悬停 `Surface.2`；关闭按钮悬停 `Caption.CloseHover` + 白图标；`WindowChrome.IsHitTestVisibleInChrome=True` |
| **ToggleSwitch**（ToggleButton 样式） | 轨道 40×20 圆角 10；关：1px `Stroke.Strong` 边 + 透明底，圆点 12 `Text.Secondary`；开：底 `Accent`，圆点 12 白色（sunset/forest 用 `Text.OnAccent`）右移；悬停圆点放大到 14；位移 150ms |
| **Slider** | 轨道高 4 圆角 2，`Ring.Track` 底 + `Accent` 已填充段；滑块 18 圆形白底 + 2px `Accent` 环 + E2 小阴影；悬停滑块 Scale 1.15；拖动时上方气泡显示数值 |
| **TextBox** | 高 36（快速添加 48）、圆角 10、底 `Surface.3`、1px `Stroke.Subtle`；焦点时边框 `Accent` 1.5px + 底边 2px `Accent`；占位符 `Text.Tertiary`（附加属性 `Placeholder`）；左侧可选图标 |
| **ComboBox** | 外观同 TextBox + 右侧 `E70D`；下拉 `Surface.Solid` 圆角 12 E2，项高 36，选中项左侧 3×16 Accent 条 + `Accent.Soft` 底 |
| **SegmentedControl**（ListBox 样式） | 外框 `Surface.1` + 1px `Stroke.Subtle` 圆角 999，内边距 4；项高 32（大号 40），文字 13/14 SemiBold `Text.Secondary`；选中项背景为**单个滑动指示器**（`Surface.3`，专注页阶段切换器用 `Phase.SoftBrush` + 文字 `Phase.StartBrush`），位置/宽度 300ms QuinticEase 动画 |
| **NumberStepper** | `[−] 数值 [+]`，总宽 128 高 36；数值 TextBox 居中、只接受数字、失焦/回车时夹紧到范围；按钮 28×28 Ghost；按住 400ms 后每 80ms 连续步进 |
| **TagChip** | 高 22、圆角 6、内边距 8×0、底 `Accent.Soft`、文字 `Accent` 11 SemiBold；可删除时右侧 `E711` 10 |
| **KeyCap** | 高 24、最小宽 24、圆角 6、底 `Surface.3`、1px `Stroke.Strong`、底部 2px 加深边、文字 12 SemiBold |
| **Card** | 圆角 16、底 `Surface.1`、1px `Stroke.Subtle`；内部顶部 1px `Stroke.Highlight` 内高光线（Border 叠加、左右缩进 16）；浅色主题加 E1 |
| **ScrollBar** | 细滚动条：宽 6（悬停 10），圆角，拇指 `Stroke.Strong`，悬停 `Text.Tertiary`；无箭头按钮 |
| **ToolTip** | `Surface.Solid` 圆角 8、1px `Stroke.Strong`、E2、内边距 10×6、12 号字、最大宽 280 自动换行、显示延迟 400ms |
| **ContextMenu**（WPF） | `Surface.Solid` 圆角 12、E2、内边距 4、项高 32 圆角 8、悬停 `Surface.2`、左侧图标列 28 |
| **焦点可视** | 仅键盘焦点显示：2px `Accent` 圆角描边，外扩 2px（FocusVisualStyle） |
| **Snackbar** | 主窗口内容区底部居中，距底 24；高 44、圆角 12、`Surface.Solid` + E2；文字 13 + 右侧文字按钮 `Accent` SemiBold；入场：上移 16 + 淡入 200ms；5–6 秒后淡出 |
| **InfoBar**（页顶信息条） | 高 40、圆角 10、左侧 3px 语义色条 + 图标 + 文案 + 可选关闭；语义 Info/Warning/Error |

### 3.7 主窗口尺寸、窗体框架与响应式布局策略

#### 3.7.1 窗口尺寸与 DWM 属性
- **主窗口默认尺寸**：`1080 × 720` DIP（居中启动 `WindowStartupLocation="CenterScreen"`；再次启动恢复上次保存的位置与尺寸，若落点不在任何显示器工作区内则回退居中）。
- **主窗口最小尺寸**：`860 × 600` DIP（在此尺寸下所有视图必须零截断、零重叠、零横向滚动条）。
- **迷你悬浮窗固定尺寸**：`288 × 92` DIP（不可调整大小，`ResizeMode="NoResize"`，`Topmost="True"`）。
- **窗体框架**：`WindowStyle="None"`、`AllowsTransparency="False"`（保持硬件加速与 Win11 原生 DWM 圆角阴影），使用 `WindowChrome`：
  - `CaptionHeight="40"`、`ResizeBorderThickness="6"`、`GlassFrameThickness="0"`、`CornerRadius="0"`、`UseAeroCaptionButtons="False"`。
  - `SourceInitialized` 时调用 `DwmSetWindowAttribute`：
    - `DWMWA_WINDOW_CORNER_PREFERENCE (33) = DWMWCP_ROUND (2)`（Win11 原生 8px 圆角 + 系统阴影）；
    - `DWMWA_USE_IMMERSIVE_DARK_MODE (20)`：按当前主题 `IsDark` 设 1 或 0；
    - `DWMWA_BORDER_COLOR (34)`：设为当前主题 `DwmBorder` 的 COLORREF（`0x00BBGGRR`）；最大化时窗体根容器 `Margin="8"`（抵消无边框最大化溢出）。
- **背景层**：主窗口根 `Grid` 底层放：
  1. `Bg.Base` 纯色底；
  2. 左上角椭圆 `800×800`（中心位于 `(-120, -120)`，`IsHitTestVisible="False"`）：`RadialGradientBrush` 从 `Phase.GlowBrush` 到透明；
  3. 右下角椭圆 `760×760`（中心位于右下外侧）：`RadialGradientBrush` 从 `Bg.Glow2` 到透明。

#### 3.7.2 响应式断点与防截断规则
监听主窗口 `SizeChanged`，按窗口宽度切换两档布局模式（通过 ViewModel `IsCompactLayout = ActualWidth < 980` 驱动）：

| 区域 | 标准模式（宽度 ≥ 980 DIP） | 紧凑模式（860 ≤ 宽度 < 980 DIP） |
|---|---|---|
| **左侧导航栏** | 展开宽 `200` DIP（图标 + 中文名称 + 底部今日目标小卡） | 收起为图标轨 `68` DIP（大图标居中 + 底部圆环进度微标，悬停出 ToolTip） |
| **专注主视图** | 左右双栏：左计时舞台 `*`（最小 460）+ 右侧栏 `300` DIP（今日目标卡 + 今日专注记录 + 快捷环境音条） | 单栏居中舞台 + 底部水平双卡（今日目标卡与快捷环境音并排，高 104 DIP） |
| **统计 KPI 行** | `UniformGrid Columns="4"`（4 卡一行） | 保持 4 卡一行，卡片内边距由 20 收至 16，KPI 数字 32 → 26 |
| **统计图表区** | 趋势图 `*` + 分布环形图 `320` 双列；下方时段图与热力图上下排布 | 全部卡片纵向单列堆叠（ScrollViewer 垂直滚动） |
| **环境音网格** | 3 列 `UniformGrid Columns="3"` | 3 列保持，卡片内边距 20 → 16 |

**防截断与防越界硬性规定**：
1. 严禁给包含文字的按钮或标签设置过死固定宽度；按钮一律 `MinWidth` + `Padding="16,0"`。
2. 动态长文本（任务标题、标签串、历史记录任务名、底部任务选择器）必须设置 `TextTrimming="CharacterEllipsis"` + `ToolTip="{Binding FullText}"`，并置于 `Grid` 的 `*` 列（绝不用 `StackPanel Orientation="Horizontal"` 包裹可变长文本，因为水平 StackPanel 给子项无限宽度导致省略号失效）。
3. 计时大数字容器固定 `MinWidth="260"`、居中对齐；当计时 `≥ 60:00`（变为 `H:MM:SS` 7 字符）时，字号自动从 `76` 降至 `60`，确保绝不触碰圆环内壁。

---

### 3.8 核心视图与自定义组件详细规格

#### 3.8.1 全局壳层（MainWindow = 自定义标题栏 + 左侧导航栏 + 右侧内容舞台）

```
+-----------------------------------------------------------------------------------+
| [Logo] 番茄专注              [专注中 · 18:42 胶囊]        [环境音指示] [置顶][迷你][-][口][X] |  <- 标题栏 40 DIP
+---------------+-------------------------------------------------------------------+
|               |  [可选 InfoBar：数据恢复提示 / 目录只读提示]                         |
|  [O] 专注     |                                                                   |
|  [=] 任务  3  |                                                                   |
|  [#] 统计     |                     当前页面视图 (ContentStage)                     |
|  [@] 环境音 ~ |                  （页面切换：淡入 + 上移 8->0，200ms）               |
|  [*] 设置     |                                                                   |
|               |                                                                   |
|  -----------  |                                                                   |
|  [今日目标卡] |                                          [底部居中 Snackbar 浮层]  |
+---------------+-------------------------------------------------------------------+
  200 / 68 DIP                              * (剩余宽度)
```

- **标题栏（40 DIP）**：
  - 左侧（`Margin="16,0,0,0"`）：18×18 品牌 Logo + `"番茄专注"`（13 SemiBold `Text.Secondary`）。
  - 中部：当当前页面**不是**“专注”页且计时处于 Running/Paused 时，显示一枚**迷你计时胶囊**（高 26、圆角 999、底 `Phase.SoftBrush`、8px 阶段色圆点 + `专注中 18:42` tabular 数字），点击立刻切回“专注”页（`AutomationId="TitleBarTimerPill"`）。
  - 右侧工具按钮组（`WindowChrome.IsHitTestVisibleInChrome="True"`）：
    - 环境音活动指示按钮（当有环境音正在发声时显示：耳机图标 `E7F6` + 3 根动态跳动小竖条 + 路数徽标，点击切到环境音页；无声源时隐藏）；
    - 窗口置顶切换按钮（36×32，`E718`/`E77A`，激活态底 `Accent.Soft`、图标 `Accent`，ToolTip `"始终置顶"`，`AutomationId="PinTopmostButton"`）；
    - 迷你窗按钮（36×32，`EE49`，ToolTip `"迷你悬浮窗 (Ctrl+Alt+M)"`，`AutomationId="EnterMiniModeButton"`）；
    - 标准窗口三键：最小化 `E921`、最大化/还原 `E922`/`E923`、关闭 `E8BB`（各 46×40）。
- **左侧导航栏（宽 200 / 紧凑 68，背景 `Sidebar.Bg`，右边界 1px `Stroke.Subtle`）**：
  - 顶部 `12` DIP 留白，5 个导航项纵向排列（间距 4，左右 Margin 10）：
    - 项高 `42`、圆角 `10`；左侧 `3×18` 圆角指示条（选中时显示 `Phase.StartBrush`，高度从 0→18 动画 200ms）；图标 `18` + 间距 `12` + 名称 `14`（选中 SemiBold `Text.Primary` + 底 `Surface.2`；未选中 Regular `Text.Secondary`；悬停底 `Surface.1`）。
    - **任务**项右侧显示未完成任务数徽标（高 20、最小宽 20、圆角 10、底 `Surface.3`、文字 11 SemiBold `Text.Secondary`；为 0 时隐藏）。
    - **环境音**项右侧在发声时显示 6px 绿色/阶段色呼吸圆点。
    - `AutomationId`：`NavFocusButton`、`NavTasksButton`、`NavStatsButton`、`NavAudioButton`、`NavSettingsButton`。
  - 底部（`Margin="12"`）：**侧栏今日进度小卡**（紧凑模式下只显示 36×36 迷你环形进度 + 悬停详情）：
    - 第一行：`今日目标`（12 `Text.Secondary`） + 右侧 `{今日番茄}/{DailyGoal}`（12 SemiBold `Text.Primary` tabular）；
    - 第二行：高 6 圆角 3 进度条（底 `Ring.Track`，填充 `Phase.Gradient`，宽度百分比平滑过渡）；
    - 第三行：`已专注 {今日时长}`（11 `Text.Tertiary`）。

---

#### 3.8.2 自定义矢量进度圆环控件（`CircularTimerRing`，继承 `FrameworkElement`，`OnRender` 绘制）
这是整个应用的**视觉中心**，必须精细实现：

- **尺寸**：在专注主视图中 `312 × 312` DIP（窗口高度 `< 640` 时自适应缩至 `276 × 276`）。
- **几何层次（从底到顶，圆心 `(W/2, H/2)`，主半径 `R = W/2 - 22`）**：
  1. **环境柔光环**：半径 `R`、线宽 `24`、不透明度 `0.08` 的 `Phase.StartBrush` 圆环（Running 时不透明度在 `0.06..0.14` 间 3 秒正弦呼吸；Paused/Idle 固定 `0.05`）。
  2. **外圈 60 刻度线**：半径 `R + 14` 到 `R + 17`；每 5 格（12 个主刻度）长 `5`、线宽 `1.5`、色 `Ring.Tick`；其余 48 个次刻度长 `2.5`、线宽 `1`、不透明度 `0.5`。已走过的角度区间内，刻度颜色染为 `Phase.StartBrush`（50% 透明度）。
  3. **轨道底环**：半径 `R`、线宽 `10`、色 `Ring.Track`。
  4. **主进度弧**：从顶部 `−90°` 顺时针绘制角度 `θ = clamp(Progress, 0, 1) × 360°`（正计时模式下每 25 分钟转满一圈，`θ = ((elapsedSec % 1500) / 1500.0) × 360°`）；线宽 `10`、`PenLineCap.Round`、`Brush = Phase.Gradient`。
     - 注意：当 `θ >= 359.9°` 时直接 `DrawEllipse` 避免 WPF 360° `ArcSegment` 退化消失；当 `θ < 0.5°` 时不画弧。
  5. **弧端发光点（Head Cap）**：当 `Progress > 0.005` 时，在弧终点 `(cx + R·cos(α), cy + R·sin(α))` 画：
     - 外层光晕圆：半径 `11`，`RadialGradientBrush`（`Phase.End` @ 55% → 透明）；
     - 内核白点：半径 `4.5`，填充 `#FFFFFF`，描边 `2px Phase.EndBrush`。
  6. **完成粒子层**：阶段自然完成瞬间，从半径 `R` 均匀发射 18 颗圆点（半径 2.5–4，颜色取 `Phase.Start`/`Phase.End`/`#FFFFFF`），650ms 内沿径向外飞 `28–48` DIP 并淡出（由 `CompositionTarget.Rendering` 或单个 0→1 `DoubleAnimation` 驱动，结束后停表，平时零开销）。

- **环内居中内容层（叠在 `CircularTimerRing` 之上）**：
  - 第 1 行（距中心偏上 58 DIP）：**阶段胶囊标签**（高 26、圆角 999、底 `Phase.SoftBrush`、内边距 `12,0`）：6px 阶段色圆点 + `专注中 / 短休息 / 长休息 / 正计时 / 已暂停`（13 SemiBold `Phase.StartBrush`；Paused 时圆点闪烁 / 颜色 `Warning`）。
  - 第 2 行（正中心）：**计时大数字** `25:00`（`Text.Timer` 76pt Light，`Typography.NumeralAlignment="Tabular"`，`AutomationId="TimerDisplayText"`）。
    - Idle 且为专注阶段时：鼠标悬停数字底部出现虚线下划线与小铅笔图标，点击弹出**调整本次时长浮层**（Popup：5 个快捷芯片 `15 / 25 / 45 / 60 / 90` + `NumberStepper` 1–180 分钟 + “确定”按钮）。
  - 第 3 行（距中心偏下 52 DIP）：副标题（12 `Text.Secondary`）：`第 2 个番茄 · 预计 16:45 结束`（Idle 显示 `点击数字可调整本次时长`）。
  - 第 4 行（距中心偏下 78 DIP）：**周期番茄点阵**（`ItemsControl` 水平排列 `LongBreakInterval` 个圆点，默认 4 个，间距 8）：
    - 已完成：`8×8` 实心圆 `Phase.StartBrush`；
    - 当前正在进行：`18×8` 圆角胶囊 `Phase.Gradient`；
    - 未开始：`8×8` 空心圆 `Ring.Tick`。

---

#### 3.8.3 专注主视图（`FocusView.xaml`）

```
+-------------------------------------------------------------------+
|         [ 专注 | 短休息 | 长休息 | 正计时 ]   (SegmentedControl)    |
|                                                                   |
|                    . - ~ ~ ~ - .                                  |  +--- 右侧栏 300 DIP (宽>=980) ---+
|                . '   [专注中]    ' .                              |  | 今日目标            3 / 8 个番茄 |
|               /                     \                             |  | [=========>          ] 1小时15分 |
|              |        25:00          |                            |  +--------------------------------+
|               \  第2个番茄·16:45结束  /                            |  | 今日专注记录                 3项 |
|                .     ● ━━ ○ ○      .                              |  | ● 15:10-15:35 架构设计     25分 |
|                  ' - . _ _ _ . - '                                |  | ● 14:30-14:55 代码评审     25分 |
|                                                                   |  +--------------------------------+
|             [重置]      ((  ▶  ))      [跳过]     [+5分钟]         |  | 环境音 · 雨夜书房         [静音] |
|              44           76            44        胶囊36           |  | [细雨] [篝火] [海浪] [咖啡馆]    |
|                                                                   |  +--------------------------------+
|          [ 🎯 正在专注：撰写系统架构规格书  2/4 番茄  ▾ ]            |
+-------------------------------------------------------------------+
```

1. **顶部阶段切换器**：`SegmentedControl` 水平居中（宽 360，高 40），4 项：`专注`、`短休息`、`长休息`、`正计时`（`AutomationId="ModeSegment_Focus"` / `_ShortBreak` / `_LongBreak` / `_Stopwatch`）。
2. **中央计时环**：`CircularTimerRing` + 环内四行信息。
3. **控制按钮栏**（环下方 20 DIP，水平居中，间距 20）：
   - 左：`重置` 圆形按钮 44×44（`Button.CircleIcon`，图标 `E777`，ToolTip `"重置 (R)"`，`AutomationId="TimerResetButton"`；Idle 且时长未改时半透明禁用）；
   - 中：**主开始/暂停按钮** 76×76（`Button.TimerPrimary`，图标 `F5B0` 开始 / `F8AE` 暂停，ToolTip `"开始 (空格)"` / `"暂停 (空格)"` / `"继续 (空格)"`，`AutomationId="TimerToggleButton"`）；
   - 右 1：`跳过` 圆形按钮 44×44（图标 `E893`，正计时模式图标改为 `E73E` 勾、ToolTip `"完成本次计时"`，`AutomationId="TimerSkipButton"`）；
   - 右 2：`＋5 分钟` 胶囊按钮（高 36、圆角 999、`Button.Secondary`、文字 `"＋5 分钟"`，`AutomationId="TimerAddFiveButton"`；正计时模式 `Visibility="Collapsed"`）。
4. **当前任务绑定胶囊**（控制栏下方 18 DIP，居中，最大宽 440，高 40，圆角 999，底 `Surface.1`，1px `Stroke.Subtle`，悬停 `Surface.2`，`AutomationId="CurrentTaskSelector"`）：
   - 左侧优先级色点（无任务时为靶心/清单图标 `E9D5`） + 任务标题（省略号截断） + 右侧番茄进度芯片 `2/4` + 下拉箭头 `E70D` +（已绑定时）右侧独立 `×` 解绑小按钮。
   - 点击弹出**任务选择 Popup**（宽 380、最大高 320、`Surface.Solid` 圆角 14 E2）：顶部搜索框 + 未完成任务列表（显示标题、优先级色条、标签、`已完成/预估`） + 底部分隔线 + `"不关联任务"` 与 `"＋ 新建任务"`。
5. **右侧辅助栏（宽 300 DIP，间距 12；紧凑模式下转为底部双卡）**：
   - **卡片 1 · 今日目标卡**：标题 `"今日目标"` + 右上 `{n} / {goal} 个番茄`；高 8 圆角 4 渐变进度条；副文案 `"已专注 1 小时 15 分 · 还差 5 个"`（达成时显示绿色勾 + `"今日目标已达成"`）。
   - **卡片 2 · 今日专注记录**（占剩余高度 `*`）：标题 `"今日专注记录"` + 数量；`ScrollViewer` 内列今日 Completed/Interrupted 专注条目（时间段 `14:30 – 14:55`、任务名、时长徽标 `25 分钟`、中断显示灰色 `"中断"`）；空态显示精致线性插画 + `"还没有专注记录"`。
   - **卡片 3 · 快捷环境音条**：标题 `"环境音"` + 当前播放状态/一键静音按钮；下方 4 个常用声音快捷圆钮（`细雨 / 海浪 / 林间鸟鸣 / 咖啡馆`）+ `"更多 ▸"`（点击跳转环境音页）。点亮某个圆钮立即开/关该路声音（默认音量 60%）。

---

#### 3.8.4 迷你悬浮窗（`MiniTimerWindow.xaml`，288 × 92 DIP）

```
+---------------------------------------------------------------+
| ● 专注中 · 撰写系统架构规格书                    [置顶] [展开] |
| 24:18                        [重置]  (( ▶/❚❚ ))  [跳过]       |
| [=====================>                                     ] | <- 底部 3px 阶段色进度条
+---------------------------------------------------------------+
```

- 外框：`Border` 圆角 `18`、底 `Surface.Solid`（不透明度受设置 `MiniWindowOpacity` 0.70–1.0 控制）、1.5px 边框（用 `Phase.StartBrush` @ 35% 透明度，形成呼吸光边）、阴影 E3。
- 顶部行（高 28）：8px 阶段色圆点 + `阶段名 · 当前任务标题`（12 `Text.Secondary`，`TextTrimming="CharacterEllipsis"`） + 右侧 `返回主窗口` 按钮（26×26，`EE47`，ToolTip `"返回主窗口 (双击窗口)"`，`AutomationId="MiniRestoreButton"`）。
- 中部行（高 46）：左侧大号计时数字 `24:18`（`Text.TimerMini` 30pt SemiLight tabular，`AutomationId="MiniTimerText"`）；右侧三个紧凑控制钮：重置 28×28、开始/暂停 38×38（`Phase.Gradient` 圆钮）、跳过 28×28。
- 底部贴边：高 3 圆角进度条（`Phase.Gradient`）。
- 交互：鼠标按住任意非按钮空白处 `DragMove()`；松开时若距当前屏幕 `WorkingArea` 左/上/右/下边缘 `≤ 12` DIP 则吸附贴齐；双击空白处返回主窗口。

---

#### 3.8.5 任务视图（`TasksView.xaml`）

```
+-------------------------------------------------------------------------------+
| 任务                                     [搜索任务…        🔍] [排序▾] [+ 新建] |
| 共 6 项 · 已完成 2 项                                                          |
+-------------------------------------------------------------------------------+
| [ + 添加任务，回车保存（支持 #标签 !高 ~3 预估番茄）                    ] [添加] |
+-------------------------------------------------------------------------------+
| [ 进行中 (4) | 已完成 (2) | 全部 (6) ]     标签：[全部] [#工作] [#学习] [#架构] |
+-------------------------------------------------------------------------------+
| ┃ [○] 撰写系统架构规格书               [专注中] [#架构]  🍅🍅○○ 2/4   [▶] [✎][🗑] |
| ┃ [○] 音频合成器滤波器单元测试                  [#工作]  🍅○○   1/3   [▶] [✎][🗑] |
| ┃ [○] 整理周会演示文稿                          [#工作]  ○○     0/2   [▶] [✎][🗑] |
|   [✓] 搭建 MSBuild 离线编译工程 (已完成，删除线) [#架构]  🍅     1/1       [✎][🗑] |
+-------------------------------------------------------------------------------+
```

1. **页头行**：左 `Text.PageTitle` `"任务"` + 副标题 `"共 {n} 项 · 已完成 {m} 项"`；右：搜索框（宽 220，左侧 `E721`，有内容时右侧出现 `×` 清空按钮，`AutomationId="TaskSearchBox"`） + 排序 `ComboBox`（宽 148，`AutomationId="TaskSortCombo"`） + `"＋ 新建任务"` 主按钮（`AutomationId="NewTaskDialogButton"`）。
2. **快速添加栏**（高 48，圆角 12，底 `Surface.2`，1px `Stroke.Subtle`，焦点时 `Accent` 描边）：
   - 左侧 `+` 图标 `E710` + 输入框（`AutomationId="QuickAddTaskInput"`，回车触发创建） + 右侧内嵌 `"添加"` 按钮（`AutomationId="QuickAddTaskSubmit"`）。
   - 输入框下方（当输入框获得焦点时展开高 22 的语法提示条）：`提示：输入 #标签  !高/!中/!低 优先级  ~数字 预估番茄数`。
3. **筛选与标签行**：
   - 左：`SegmentedControl` `进行中 ({a}) | 已完成 ({b}) | 全部 ({c})`（`AutomationId="TaskFilter_Active"` / `_Completed` / `_All`）；
   - 右：水平滚动的标签过滤芯片条（`"全部标签"` + 当前所有任务出现过的去重标签，点击切换多选高亮）。
4. **任务列表区**（`ScrollViewer` + `ItemsControl`）：
   - 每行是一张紧凑卡片（高 58，有备注时展开为 76；圆角 12，底 `Surface.1`，悬停 `Surface.2`，间距 8）：
     - 左边缘 `4×28` 圆角**优先级色条**（高=`Priority.High`、中=`Priority.Medium`、低=`Priority.Low`、无=透明）；
     - 圆形完成勾选框 22×22（未完成空心圆 `Stroke.Strong`，悬停变 `Accent`；已完成实心 `Success` + 白勾 `E73E` + Scale 1→1.2→1 弹跳动画）；
     - 标题区（`*` 列）：第一行任务标题（14 SemiBold；已完成时 `TextDecorations="Strikethrough"` + 色 `Text.Tertiary`） + `"专注中"` 徽标（若为当前绑定任务：底 `Phase.SoftBrush`、文字 `Phase.StartBrush`） + 标签芯片组；第二行（若有备注）：备注单行省略 `12 Text.Tertiary`；
     - 番茄进度区：当 `EstimatedPomodoros <= 6` 时绘制实心/空心小圆点 + `{Done}/{Est}` tabular 数字；`> 6` 时绘制浓缩胶囊 `● {Done}/{Est}`；未设预估但 `Done > 0` 时显示 `● ×{Done}`；
     - 右侧操作钮组（常驻显示“开始专注”按钮 `F5B0`，32×32 圆钮，ToolTip `"绑定并开始专注"`；编辑 `E70F` 与删除 `E74D` 在行悬停或键盘焦点时完全显现，平时不透明度 0.35，保证触摸/截图均可见且不杂乱）。

---

#### 3.8.6 统计与可视化视图（`StatsView.xaml`）

```
+-------------------------------------------------------------------------------+
| 统计                                                              [导出 CSV]   |
+-----------------+-----------------+-----------------+-------------------------+
| 今日专注         | 本周专注         | 连续专注         | 累计专注                 |
| 4 个番茄         | 12.5 小时        | 7 天            | 86.0 小时                |
| 1 小时 40 分     | 较上周 +25%      | 最长 14 天       | 完成率 92% · 206 个番茄  |
+-----------------+-----------------+-----------------+-------------------------+
| 专注趋势                    [近7天|近30天] | 专注分布               [标签|任务] |
|  分钟                                     |        . - ~ - .                  |
|  150 | - - - - - - - - - - - - 目标线     |      /   12.5h   \   ● #架构  45% |
|  100 |     ██          ██                 |     |    总时长   |  ● #工作  30% |
|   50 | ██  ██  ██  ██  ██  ██  ▓▓(今日)   |      \           /   ● #学习  15% |
|    0 +----------------------------        |        ' - _ - '     ● 其他   10% |
|        一  二  三  四   五  六  日         |                                   |
+-------------------------------------------+-----------------------------------+
| 专注时段分布（24 小时） · 高效时段 09:00 – 11:00                                |
| [平滑面积折线图 00:00 ............................................... 23:00]   |
+-------------------------------------------------------------------------------+
| 专注热力图（近 26 周）                                       少 [][][][][] 多  |
| [周一..周日 × 周列 圆角方格矩阵，悬停显示 "11月19日：4 个番茄 · 1 小时 40 分"]   |
+-------------------------------------------------------------------------------+
| 历史记录明细                         [全部 | 专注 | 休息]                       |
| 11月19日 周四 · 4 个番茄 · 1 小时 40 分                                        |
|   15:10 – 15:35  [专注]  撰写系统架构规格书              25 分钟   [完成]  [🗑] |
+-------------------------------------------------------------------------------+
```

**四个自绘图表控件（放在 `Controls/Charts/`，继承 `FrameworkElement`，全部纯 WPF `OnRender` 矢量绘制，零第三方图表库）**：
1. **`TrendBarChart`（专注趋势柱状图）**：
   - 左侧 3 条水平虚线网格线（0 / 中值 / 最大值）与 Y 轴分钟刻度（`Text.Micro`）；底部日期标签（7 天显示 `周一..周日` 或 `11/19`；30 天每 3–4 天显示一个刻度防止拥挤）。
   - 柱体：顶部圆角 `6`，普通柱用 `Phase.StartBrush` @ 55%，**今日柱**或**悬停柱**用完整 `Phase.Gradient` 100%。
   - 目标虚线：`DailyGoal × FocusMinutes` 高度处画 1px 虚线 `Accent` + 右端小标签 `"目标"`。
   - 悬停任一柱：浮出暗色气泡显示 `11月19日 周四 · 4 个番茄 · 100 分钟`。
   - 入场/切换 7↔30 天：驱动 `AnimationProgress` 依赖属性 `0 → 1`（500ms CubicEase），柱高乘以该系数生长。
2. **`HourlyAreaChart`（24 小时专注时段面积图）**：
   - X 轴 0..23 时（标注 `00:00 / 06:00 / 12:00 / 18:00 / 23:00`），将 24 个点用 Catmull-Rom 样条转三次贝塞尔 `PathGeometry` 绘制平滑曲线，上方描边 `2px Phase.StartBrush`，下方填充 `LinearGradient (Phase.Start @ 35% → 0%)`。最高峰小时画 4px 实心圆点。
3. **`DistributionDonutChart`（标签/任务分布环形图）**：
   - 左侧 `156×156` 圆环（外径 72、环宽 18，各扇区之间留 2° 间隙，颜色依次取调色板：`#FF6B5E, #2DD4BF, #7C83FF, #FBBF24, #60A5FA, #94A3B8`），环心显示总时长 + `"总专注"`；右侧图例列 Top 5 + 其他（色点 + 名称省略 + 时长 + 百分比 tabular）。无数据时画一条浅灰整环 + `"暂无数据"`。
4. **`FocusHeatmapControl`（GitHub 风格专注热力图）**：
   - 格子 `13×13` DIP、圆角 `3`、间距 `3`；左侧标注 `周一 / 周三 / 周五 / 周日`；顶部标注月份首周 `6月 / 7月 / … / 11月`；根据可用宽度自动计算显示周数 `cols = clamp(floor((ActualWidth - 36) / 16), 12, 52)`；颜色按当日完成番茄数映射 `Heat.L0..L4`；鼠标移动命中格子时 ToolTip 显示日期、番茄数与总分钟。

---

#### 3.8.7 环境音面板（`AmbientAudioView.xaml`）

```
+-------------------------------------------------------------------------------+
| 环境音                                        [主音量 🔊 ━━━━━━●━━ 75%] [静音] |
| 全部由本地实时合成，离线可用                                                   |
+-------------------------------------------------------------------------------+
| 预设场景：[雨夜书房] [林间晨光] [海边咖啡] [深度专注] [全部停止]   定时：[不限▾] |
| 联动规则：[✓ 仅在专注时播放]   [✓ 暂停计时时静音]                              |
+-------------------------+-------------------------+---------------------------+
| [🌧] 细雨          ●    | [🌊] 海浪               | [🌲] 林间鸟鸣              |
|      播放中 · 70%       |      点击播放           |      点击播放              |
|      ━━━━━━●━━━         |      ━━━━●━━━━━         |      ━━━━●━━━━━           |
+-------------------------+-------------------------+---------------------------+
| [☕] 咖啡馆             | [🔥] 篝火          ●    | [🍃] 微风                  |
| ...                     |      播放中 · 35%       | ...                       |
+-------------------------+-------------------------+---------------------------+
| [〰] 白噪音             | [🌙] 棕噪音             | [🕒] 时钟滴答         ●    |
+-------------------------+-------------------------+---------------------------+
```

- **顶部主控栏**：右侧主音量图标（点击切换静音 `E767`/`E74F`）+ 主音量 `Slider`（宽 160，0–100）+ 百分比 `75%`（tabular）。
- **场景与联动栏**：5 个预设芯片（当前混音状态与某预设完全一致时该芯片高亮 `Accent.Soft` + `Accent` 边框） + 右侧“定时关闭”下拉框（`不限 / 15 分钟 / 30 分钟 / 60 分钟 / 90 分钟`，激活后显示剩余倒计时 `24:12 后停止`） + 两个联动 `ToggleSwitch`。
- **3 × 3 声音卡片网格**（`UniformGrid Columns="3"`，间距 16）：
  - 每张卡片（高 124，圆角 16）：
    - **未激活态**：底 `Surface.1`、边框 `1px Stroke.Subtle`、40×40 圆形图标底 `Surface.2` + 22×22 Lucide 线性图标（色 `Text.Secondary`）、名称 15 SemiBold、状态 `"点击播放"`（12 `Text.Tertiary`）、底部滑块半透明（0.45）。
    - **激活态**：底叠加该声音 `Tint` 色 14% 半透明渐变、边框 `1.5px Tint @ 70%`、右上角 4 根微动画声波竖条（发声中跳动，因联动待机时静止为圆点）、圆形图标底 `Tint @ 22%` + 图标色 `Tint`、状态 `"播放中 · 70%"`（或 `"将在专注开始时播放 · 70%"`）、底部音量滑块已填充段使用 `Tint` 色。
    - 点击卡片上半部切换该路开关；拖动底部滑块调整音量（若当前为关闭态，拖动滑块 `> 0` 自动开启该路）。
    - `AutomationId`：`SoundCard_rain`、`SoundCard_ocean`、`SoundCard_forest`、`SoundCard_cafe`、`SoundCard_fire`、`SoundCard_wind`、`SoundCard_white`、`SoundCard_brown`、`SoundCard_clock`。

---

#### 3.8.8 设置视图（`SettingsView.xaml`）
单列卡片分组布局（最大内容宽 `760` DIP，水平居中，置于 `ScrollViewer` 中），每组一张 `Card`，组内每项高 `52`（左：标题 14 SemiBold + 下方说明 12 `Text.Secondary`；右：对应控件；项间 1px `Stroke.Subtle` 分隔线）：

1. **外观**：
   - **主题选择**：4 张可视化主题预览卡并排（高 88、圆角 12）：每张卡用对应主题的真实 `Bg.Base`、`Surface.1`、`Accent` 绘制微缩窗口示意 + 底部主题名（`深邃夜空` / `晨雾浅色` / `暖阳日落` / `静谧森林`），选中卡带 `2px Accent` 外框与右上角实心勾徽标（`AutomationId="ThemeCard_midnight"` / `_mist` / `_sunset` / `_forest`）。
   - `减少动态效果`（ToggleSwitch）；
   - `迷你窗不透明度`（Slider 70%–100%，步长 5%）。
2. **计时**：
   - `专注时长`（NumberStepper 1–180 分钟，默认 25）；
   - `短休息时长`（NumberStepper 1–60 分钟，默认 5）；
   - `长休息时长`（NumberStepper 1–120 分钟，默认 15）；
   - `长休息间隔`（NumberStepper 2–12 个番茄，默认 4）；
   - `每日目标`（NumberStepper 1–24 个番茄，默认 8）；
   - `新一天起点`（ComboBox `00:00` .. `06:00`，默认 `00:00`）。
3. **自动化**：
   - `专注结束后自动开始休息`（ToggleSwitch，默认关）；
   - `休息结束后自动开始专注`（ToggleSwitch，默认关）；
   - `启动时恢复上次未完成的计时`（ToggleSwitch，默认开）；
   - `打开迷你窗时隐藏主窗口`（ToggleSwitch，默认开）。
4. **提醒与声音**：
   - `阶段结束提示音`（ComboBox：`风铃 / 颂钵 / 木鱼 / 电子 / 无` + 右侧 `"试听"` 按钮 `AutomationId="PreviewSoundButton"`）；
   - `提示音音量`（Slider 0–100，默认 70）；
   - `结束前 1 分钟轻声提醒`（ToggleSwitch，默认关）；
   - `桌面系统通知`（ToggleSwitch，默认开）；
   - `应用内完成卡片提醒`（ToggleSwitch，默认开）。
5. **全局快捷键**：
   - `启用全局快捷键`（ToggleSwitch，默认开）；
   - 4 行快捷键录制器（`HotkeyRecorderBox`：点击显示 `"请按下组合键… (Esc 取消)"`，按下 `修饰键+主键` 即保存并立即重新注册；右侧显示状态芯片 `"已生效"` 或琥珀色 `"已被占用"` + `"恢复默认"` 按钮）。
6. **系统与托盘**：
   - `开机自动启动`（ToggleSwitch，默认关）；
   - `显示系统托盘图标`（ToggleSwitch，默认开）；
   - `关闭主窗口时最小化到托盘`（ToggleSwitch，默认开；当上一项关闭时自动禁用并提示 `"需开启系统托盘图标"`）。
7. **数据管理**：
   - 数据存储位置：显示当前路径文本 + `"打开文件夹"` 按钮（`Process.Start("explorer.exe", DataDir)`）；
   - 备份与恢复：`"导出备份"` 按钮 + `"导入备份"` 按钮 + `"导出专注记录 CSV"` 按钮；
   - 危险区：`"恢复默认设置"` 按钮 + `"清空所有数据"` 危险按钮（弹出输入 `"清空"` 确认对话框）。
8. **关于**：应用图标 + `"番茄专注 PomodoroFocus v1.0.0"` + `"纯本地离线 · 零外部依赖 · Windows 桌面专注工具"` + 应用内快捷键速查表（2 列 `KeyCap` 对照表）。

---

#### 3.8.9 应用内完成通知卡片（`ToastWindow.xaml`）
- 尺寸 `360 × 132` DIP，`WindowStyle="None"`、`Topmost="True"`、`ShowActivated="False"`、`ShowInTaskbar="False"`，定位于主屏幕 `WorkingArea` 右下角（右边距 20、底边距 20）。
- 外观：`Surface.Solid` 圆角 16、顶部 3px `Phase.Gradient` 彩条、1px `Stroke.Strong`、阴影 E3。
- 内容：第一行阶段色图标 + 标题（15 SemiBold） + 右上关闭 `×`；第二行正文（13 `Text.Secondary`）；第三行右侧两个操作按钮（如 `"跳过休息"` Secondary + `"开始休息"` Primary）。
- 动效：从右侧偏移 `+40` DIP 滑入并淡入（220ms CubicEase），8 秒后淡出关闭（鼠标悬停在卡片上时暂停 8 秒倒计时）。

#### 3.8.10 模态对话框（任务新建/编辑、确认框、清空确认）
- 不新建 OS 子窗口，而是在 `MainWindow` 顶层 `OverlayHost` Grid 中渲染（避免多窗口焦点丢失，且截图能一次截全）：
  - 全窗遮罩 `Overlay`（点击遮罩等同取消）+ 居中对话框卡片（宽 `460` DIP，圆角 `20`，底 `Surface.Solid`，1px `Stroke.Strong`，阴影 E3，入场 Scale `0.95→1.0` + Opacity `0→1` 180ms）。
  - 任务编辑对话框包含：标题输入框、备注多行框（高 72）、优先级 4 选 1 `SegmentedControl`（`无 | 低 | 中 | 高`）、预估番茄 `NumberStepper`（0–20）、标签编辑器（已添加的 `TagChip` + 输入框回车添加，最多 5 个）、底部 `"取消"` + `"保存"`。

---

### 3.9 微交互与状态过渡清单
1. **阶段切换 600ms 色彩流动**：`ThemeService.AnimatePhaseBrush(phase)` 同时对 `Phase.StartBrush`、`Phase.EndBrush`、`Phase.Gradient` 的两个 `GradientStop`、`Phase.SoftBrush`、`Phase.GlowBrush` 启动 600ms `ColorAnimation`。
2. **主题切换 300ms 交叉淡化**：切换主题前先将主窗口根视觉用 `RenderTargetBitmap` 截图铺在顶层 `Image` 上，立即替换 `MergedDictionaries` 中的主题字典，然后将顶层截图 `Opacity` 从 `1 → 0`（300ms）后移除——实现丝滑零闪烁换肤。
3. **导航切换**：目标视图 `Opacity 0→1` + `TranslateTransform.Y 8→0`（200ms CubicEase EaseOut）。
4. **按钮触感**：所有按钮 `PreviewMouseLeftButtonDown` 缩放至 `0.97`（80ms），松开回弹 `1.0`（120ms）。
5. **阶段自然完成庆祝**：进度环 18 颗粒子迸发（650ms）+ 环内数字 Scale `1.0 → 1.06 → 1.0`（300ms）。

---

## 4. 工程目录结构、核心数据模型与状态机契约

### 4.1 仓库完整目录结构（`e:\wu\workSpace\LearningProject\windows_pomodoro`）

```text
windows_pomodoro/
├── DESIGN_SPEC.md                          # 本设计规格书（保留，勿删）
├── README.md                               # 构建、测试、运行、功能与架构说明
├── build.ps1                               # 一键构建 / 单元测试 / UI 测试 / 启动脚本
├── PomodoroFocus.sln                       # Visual Studio / MSBuild 解决方案（4 个项目）
├── tools/
│   └── New-AppIcon.ps1                     # 生成 src/PomodoroFocus.App/Assets/AppIcon.ico
├── src/
│   ├── PomodoroFocus.Core/                 # 纯业务逻辑库（零 WPF/WinForms 引用，100% 可单测）
│   │   ├── PomodoroFocus.Core.csproj
│   │   ├── Models/
│   │   │   ├── Enums.cs                    # TimerMode, TimerPhase, TimerRunState, SessionOutcome, TaskPriority, ThemeId, SoundId, NotificationSoundId
│   │   │   ├── AppSettings.cs              # 全部可配置项 + 默认值 + 范围夹紧 Clamp()
│   │   │   ├── TaskItem.cs                 # 任务模型
│   │   │   ├── FocusSession.cs             # 专注/休息历史会话记录
│   │   │   ├── TimerSnapshot.cs            # 计时器不可变快照 + 崩溃恢复持久化模型 TimerPersistedState
│   │   │   └── BackupBundle.cs             # 全量导出/导入备份包模型
│   │   ├── Engine/
│   │   │   ├── IClock.cs                   # UtcNow / LocalNow 抽象（测试注入 FakeClock）
│   │   │   ├── PomodoroEngine.cs           # 核心无漂移计时状态机
│   │   │   └── QuickAddParser.cs           # "#标签 !高 ~3" 任务快速添加解析器
│   │   ├── Services/
│   │   │   ├── TaskService.cs              # 任务增删改查、筛选、排序、撤销缓冲
│   │   │   ├── StatsService.cs             # KPI、7/30天趋势、24h时段、分布、热力图、CSV导出
│   │   │   ├── StorageService.cs           # DataContractJson 原子读写、.bak 恢复、只读降级、演示数据种子 DemoDataSeeder
│   │   │   ├── Formatters.cs               # 中文时长、日期、百分比格式化纯函数
│   │   │   └── HotkeyGesture.cs            # 全局快捷键解析与格式化
│   │   └── Audio/
│   │       ├── XorShift32.cs               # 确定性伪随机数
│   │       ├── Filters.cs                  # 一阶 LP/HP + 状态变量滤波器 SVF + 粉红/棕噪声发生器
│   │       ├── AmbientGenerators.cs        # 9 种环境音程序化生成器
│   │       ├── NotificationSynthesizer.cs  # 4 种阶段结束提示音 + 1 分钟轻提醒合成器
│   │       └── AmbientMixer.cs             # 多轨混音、50ms 增益平滑、提示音闪避 ducking、软削波
│   │
│   └── PomodoroFocus.App/                  # WPF 桌面端应用（WinExe，x64）
│       ├── PomodoroFocus.App.csproj
│       ├── App.config
│       ├── app.manifest                    # PerMonitorV2 DPI 声明
│       ├── App.xaml
│       ├── App.xaml.cs                     # CLI 参数解析、单实例互斥、--screenshot 批量截图模式
│       ├── Assets/
│       │   └── AppIcon.ico                 # 多尺寸 PNG 图标（16/24/32/48/64/256）
│       ├── Interop/
│       │   ├── DwmInterop.cs               # DWM 圆角、暗色模式、边框色
│       │   ├── HotkeyManager.cs            # Win32 RegisterHotKey / UnregisterHotKey + HwndSource Hook
│       │   ├── WaveOutPlayer.cs            # winmm waveOutOpen CALLBACK_EVENT 双缓冲播放器
│       │   ├── TrayIconService.cs          # NotifyIcon 动态进度图标绘制 + 主题化右键菜单
│       │   └── StartupRegistryService.cs   # HKCU Run 开机自启读写
│       ├── Themes/
│       │   ├── Icons.xaml                  # Segoe Fluent Icons 码位字符串 + Lucide Geometry + 品牌 Logo
│       │   ├── Typography.xaml             # 字体与文本样式
│       │   ├── Controls.xaml               # 按钮、开关、滑块、输入框、下拉、分段控件、卡片等模板
│       │   ├── Tokens.midnight.xaml        # 深邃夜空主题 Token
│       │   ├── Tokens.mist.xaml            # 晨雾浅色主题 Token
│       │   ├── Tokens.sunset.xaml          # 暖阳日落主题 Token
│       │   ├── Tokens.forest.xaml          # 静谧森林主题 Token
│       │   └── ThemeService.cs             # 主题热切换 300ms 交叉淡化 + 阶段色 600ms 动画
│       ├── Controls/
│       │   ├── CircularTimerRing.cs        # 自绘矢量渐变进度圆环 + 刻度 + 发光头 + 完成粒子
│       │   ├── SegmentedControl.cs         # 带滑动指示器动画的分段切换控件
│       │   ├── NumberStepper.cs            # [−] 数值 [+] 步进器
│       │   ├── HotkeyRecorderBox.cs        # 快捷键录制控件
│       │   └── Charts/
│       │       ├── TrendBarChart.cs        # 7/30 天柱状趋势图 + 目标线
│       │       ├── HourlyAreaChart.cs      # 24 小时平滑面积折线图
│       │       ├── DistributionDonutChart.cs # 标签/任务分布环形图
│       │       └── FocusHeatmapControl.cs  # GitHub 风格专注热力图
│       ├── ViewModels/
│       │   ├── ObservableObject.cs         # INotifyPropertyChanged + RelayCommand (C# 5)
│       │   ├── MainViewModel.cs            # 导航、全局状态、Snackbar、InfoBar、对话框宿主
│       │   ├── FocusViewModel.cs           # 专注页 VM
│       │   ├── TasksViewModel.cs           # 任务页 VM
│       │   ├── StatsViewModel.cs           # 统计页 VM
│       │   ├── AmbientAudioViewModel.cs    # 环境音页 VM
│       │   └── SettingsViewModel.cs        # 设置页 VM
│       └── Views/
│           ├── MainWindow.xaml / .cs       # 主窗口壳层
│           ├── MiniTimerWindow.xaml / .cs  # 始终置顶迷你悬浮窗
│           ├── ToastWindow.xaml / .cs      # 右下角应用内完成卡片
│           ├── FocusView.xaml / .cs        # 专注主视图
│           ├── TasksView.xaml / .cs        # 任务管理视图
│           ├── StatsView.xaml / .cs        # 专注统计视图
│           ├── AmbientAudioView.xaml / .cs # 环境音面板视图
│           └── SettingsView.xaml / .cs     # 设置中心视图
│
└── tests/
    ├── PomodoroFocus.Tests/                # 零依赖控制台单元测试运行器（退出码 0=通过）
    │   ├── PomodoroFocus.Tests.csproj
    │   ├── Program.cs                      # 反射发现 *Tests 类并汇总断言结果
    │   ├── AssertEx.cs
    │   ├── PomodoroEngineTests.cs          # 状态机全分支 + 跨日 + 崩溃恢复测试
    │   ├── QuickAddParserTests.cs          # 快速添加语法解析测试
    │   ├── TaskServiceTests.cs             # CRUD + 筛选 + 排序 + 撤销测试
    │   ├── StatsServiceTests.cs            # KPI + 跨小时拆分 + 连续天数 + 热力图 + CSV 测试
    │   ├── StorageServiceTests.cs          # 原子写入 + 损坏 .bak 恢复 + 备份导入导出测试
    │   └── AudioSynthesisTests.cs          # 9 种环境音 + 4 种提示音非零、无 NaN、峰值 <=1、RMS 范围测试
    │
    └── PomodoroFocus.UITests/              # 真实进程启动 + UIAutomation + 视觉截图自动化套件
        ├── PomodoroFocus.UITests.csproj
        └── Program.cs                      # 启动真实 exe、验证 UIA 交互、生成全状态截图并校验图像非空与尺寸
```

---

### 4.2 核心枚举与数据模型契约（`PomodoroFocus.Core.Models`）

序列化统一使用 `System.Runtime.Serialization.Json.DataContractJsonSerializer`（配置 `UseSimpleDictionaryFormat = true`，日期时间一律存 ISO-8601 字符串 `"yyyy-MM-ddTHH:mm:ss.fffZ"` 或本地日期 `"yyyy-MM-dd"`，避免 `/Date(...)/` 格式）。

```csharp
namespace PomodoroFocus.Core.Models
{
    public enum TimerMode { Pomodoro = 0, Stopwatch = 1 }
    public enum TimerPhase { Focus = 0, ShortBreak = 1, LongBreak = 2 }
    public enum TimerRunState { Idle = 0, Running = 1, Paused = 2 }
    public enum SessionOutcome { Completed = 0, Interrupted = 1 }
    public enum TaskPriority { None = 0, Low = 1, Medium = 2, High = 3 }
    public enum ThemeId { Midnight = 0, Mist = 1, Sunset = 2, Forest = 3 }
    public enum NotificationSoundId { Chime = 0, Bowl = 1, Wood = 2, Digital = 3, None = 4 }
}
```

#### 4.2.1 `AppSettings`（`settings.json`）
| 属性 | 类型 | 默认值 | 有效范围 (`Clamp()`) |
|---|---|---|---|
| `SchemaVersion` | `int` | `1` | `1` |
| `Theme` | `string` | `"midnight"` | `"midnight" / "mist" / "sunset" / "forest"` |
| `ReduceMotion` | `bool` | `false` | — |
| `FocusMinutes` | `int` | `25` | `1..180` |
| `ShortBreakMinutes` | `int` | `5` | `1..60` |
| `LongBreakMinutes` | `int` | `15` | `1..120` |
| `LongBreakInterval` | `int` | `4` | `2..12` |
| `DailyGoal` | `int` | `8` | `1..24` |
| `DayStartHour` | `int` | `0` | `0..6` |
| `AutoStartBreaks` | `bool` | `false` | — |
| `AutoStartFocus` | `bool` | `false` | — |
| `RestoreTimerOnLaunch` | `bool` | `true` | — |
| `HideMainOnMini` | `bool` | `true` | — |
| `MiniWindowOpacity` | `double` | `0.95` | `0.70..1.00` |
| `NotificationSound` | `string` | `"chime"` | `"chime" / "bowl" / "wood" / "digital" / "none"` |
| `NotificationVolume` | `int` | `70` | `0..100` |
| `OneMinuteWarning` | `bool` | `false` | — |
| `EnableDesktopNotification` | `bool` | `true` | — |
| `EnableInAppToast` | `bool` | `true` | — |
| `AmbientMasterVolume` | `int` | `75` | `0..100` |
| `AmbientOnlyDuringFocus` | `bool` | `true` | — |
| `AmbientMuteOnPause` | `bool` | `true` | — |
| `AmbientTrackVolumes` | `Dictionary<string,int>` | 9 路默认 60 | `0..100` |
| `AmbientActiveTracks` | `List<string>` | 空列表 | 9 路合法 ID |
| `EnableGlobalHotkeys` | `bool` | `true` | — |
| `HotkeyToggle` | `string` | `"Ctrl+Alt+Space"` | 合法组合键 |
| `HotkeySkip` | `string` | `"Ctrl+Alt+Right"` | 合法组合键 |
| `HotkeyMini` | `string` | `"Ctrl+Alt+M"` | 合法组合键 |
| `HotkeyMain` | `string` | `"Ctrl+Alt+P"` | 合法组合键 |
| `LaunchAtStartup` | `bool` | `false` | — |
| `ShowTrayIcon` | `bool` | `true` | — |
| `MinimizeToTrayOnClose` | `bool` | `true` | — |
| `AlwaysOnTop` | `bool` | `false` | — |
| `MainWindowLeft / Top / Width / Height` | `double` | `NaN / NaN / 1080 / 720` | `Width >= 860, Height >= 600` |
| `MainWindowMaximized` | `bool` | `false` | — |
| `MiniWindowLeft / Top` | `double` | `NaN / NaN` | 屏幕工作区内 |
| `ActiveTaskId` | `string` | `null` | — |

#### 4.2.2 `TaskItem`（`tasks.json`）
- `Id` (`string`, GUID `"N"`)、`Title` (`string`, 1..80)、`Notes` (`string`, 0..500)、`Priority` (`int`, 0..3)、`Tags` (`List<string>`, ≤5 项，每项 ≤12 字)、`EstimatedPomodoros` (`int`, 0..20)、`CompletedPomodoros` (`int`, ≥0)、`IsCompleted` (`bool`)、`CreatedAtUtc` (`string`)、`CompletedAtUtc` (`string`)。

#### 4.2.3 `FocusSession`（`sessions.json`）
- `Id` (`string`)、`Mode` (`string`: `"Pomodoro"` / `"Stopwatch"`)、`Phase` (`string`: `"Focus"` / `"ShortBreak"` / `"LongBreak"`)、`Outcome` (`string`: `"Completed"` / `"Interrupted"`)、`StartedAtUtc` (`string`)、`EndedAtUtc` (`string`)、`LocalDate` (`string`, `"yyyy-MM-dd"`，按 `StartedAtUtc` 转本地时间并减去 `DayStartHour` 计算)、`PlannedSeconds` (`int`)、`EffectiveSeconds` (`int`)、`TaskId` (`string`)、`TaskTitleSnapshot` (`string`)、`TagsSnapshot` (`List<string>`)。

---

### 4.3 核心状态机 `PomodoroEngine` 接口契约

```csharp
public sealed class PomodoroEngine
{
    public PomodoroEngine(IClock clock, AppSettings settings);

    public TimerSnapshot Snapshot { get; }
    public event EventHandler<TimerSnapshot> Tick;
    public event EventHandler<PhaseCompletedEventArgs> PhaseCompleted;
    public event EventHandler<TimerSnapshot> OneMinuteLeft;

    public void SelectModeAndPhase(TimerMode mode, TimerPhase phase, bool confirmInterruptIfNeeded);
    public void SetCustomFocusMinutes(int minutes); // 仅 Idle + Focus 生效，范围 1..180
    public void Start();
    public void Pause();
    public void Resume();
    public void Toggle();
    public FocusSession Reset();                    // 若产生 Interrupted 会话则返回，否则 null
    public FocusSession Skip();                     // Pomodoro 跳到下一阶段；Stopwatch >=60s 记录 Completed
    public bool AddFiveMinutes();                   // 上限 240 分钟
    public void AdvanceTo(DateTime utcNow);         // 供 UI 定时器每 200ms 调用，也供单元测试推进时间
    public TimerPersistedState ExportState();
    public FocusSession RestoreState(TimerPersistedState state); // 若恢复时发现已到期，返回补记的 Completed 会话
}
```

- **有效专注秒数计算**：`EffectiveSeconds` 仅累计处于 `Running` 状态的时间（暂停期间不计入）。
- **最小记录阈值**：`MinRecordableFocusSeconds = 60`。无论 Reset、Skip、切换模式还是正计时完成，专注有效时长 `< 60` 秒一律**不写入** `FocusSession`。

---

### 4.4 命令行参数与自动化可测性契约（**必须全部实现**）

为确保 `reviewer` 与自动化测试可在沙箱中 100% 确定性地验证所有功能并截取高清多状态视觉图，`PomodoroFocus.exe` 必须支持以下命令行参数：

| 参数 | 作用 |
|---|---|
| `--data-dir <path>` | 覆盖数据存储目录（单实例 Mutex 按该路径哈希隔离，允许与用户实例并行） |
| `--seed-demo` | 启动时在 `--data-dir` 中灌入一套丰富、真实的中文演示数据（6 项带标签/优先级/进度的任务、近 28 天共 65 条专注会话记录、绑定当前任务 `"撰写系统架构规格书"`、启用 `"细雨"` + `"篝火"` 环境音） |
| `--theme <midnight\|mist\|sunset\|forest>` | 启动时强制应用指定主题 |
| `--view <focus\|tasks\|stats\|audio\|settings>` | 启动后直接切换到指定主视图 |
| `--timer-state <idle\|running\|paused\|shortbreak\|longbreak\|stopwatch>` | 启动后把计时器置于指定演示状态（例如 `running` 置于专注 18:42、`shortbreak` 置于短休 03:20 Running、`longbreak` 置于长休 12:10 Running、`paused` 置于已暂停 15:00） |
| `--mini` | 启动后直接打开迷你悬浮窗（在 `--screenshot` 模式下主窗口与迷你窗同时可见以便截图） |
| `--dialog <none\|new-task>` | 启动后自动打开指定模态对话框（如新建任务对话框） |
| `--no-tray` | 不创建系统托盘图标 |
| `--no-hotkeys` | 不注册全局 Win32 快捷键 |
| `--no-sound` | 混音器运行但不打开物理 waveOut 设备 |
| `--screenshot <output.png>` | **自动截图模式**：窗口完成布局与首帧渲染后（延迟 450ms 确保字体、图表生长动画到位），使用 `RenderTargetBitmap`（按当前窗口 DPI 或固定 144 DPI 高清渲染）将当前窗口（或 `--mini` 时的 `MiniTimerWindow`）完整保存为 `<output.png>`，随后自动 `Application.Shutdown(0)` 退出！ |
| `--screenshot-suite <output-dir>` | **一键全状态截图套件模式**：在单次启动内依次切换并渲染导出以下 **10 张标准状态截图** 到 `<output-dir>`，全部完成后以退出码 0 退出（耗时 < 5 秒）：<br>1. `01_focus_midnight_running.png`（深邃夜空 · 专注进行中 18:42 + 绑定任务 + 今日记录）<br>2. `02_focus_shortbreak.png`（深邃夜空 · 短休息薄荷青阶段色 03:20）<br>3. `03_focus_longbreak.png`（深邃夜空 · 长休息星云紫阶段色 12:10）<br>4. `04_tasks_view.png`（任务管理视图 · 含优先级/标签/番茄点阵/快速添加栏）<br>5. `05_task_edit_dialog.png`（新建/编辑任务模态对话框展开态）<br>6. `06_stats_view.png`（专注统计视图 · 4 KPI + 趋势柱图 + 环形分布 + 24h 时段 + 热力图 + 历史明细）<br>7. `07_ambient_audio_view.png`（环境音面板 · 雨夜书房激活态，细雨+篝火+时钟滴答点亮）<br>8. `08_settings_mist_light.png`（设置中心 · 切换为**晨雾浅色 mist** 主题）<br>9. `09_focus_sunset_compact.png`（**暖阳日落 sunset** 主题 + 窗口缩至最小尺寸 `860×600` 紧凑响应式布局验证）<br>10. `10_mini_floating_window.png`（**静谧森林 forest** 主题下的 `288×92` 迷你悬浮窗） |

> 提示：`--screenshot` 与 `--screenshot-suite` 使用 WPF 原生 `RenderTargetBitmap` 离屏光栅化窗口根视觉树（强制 `UpdateLayout()` 后渲染），不受桌面锁屏、窗口遮挡或高分屏裁剪影响，且 100% 反映真实 WPF 布局、字体、颜色与自绘 `OnRender` 控件效果。同时 `PomodoroFocus.UITests.exe` 还会使用 Win32 + `UIAutomationClient` 对真实运行窗口执行端到端点击交互测试。

---

## 5. 独立审核与 UI 视觉验收量表（供 `reviewer` 逐项核验）

`reviewer` 必须按以下 5 大类共 28 项逐条执行验证；**任何一项未通过均须打回 `coder` 修复并闭环复测**。

### 5.1 构建与工程卫生（Gate 1）
- [ ] **G1.1 零报错零警告构建**：执行 1.4 节 MSBuild 命令，`0 个警告，0 个错误`，生成 `PomodoroFocus.exe`、`PomodoroFocus.Tests.exe`、`PomodoroFocus.UITests.exe`。
- [ ] **G1.2 编码与语法合规**：全仓源码均为 UTF-8 with BOM，csproj 含 `<CodePage>65001</CodePage>` 与 `<LangVersion>5</LangVersion>`，界面无任何中文乱码（如“鐣寗”）。
- [ ] **G1.3 仓库干净无脏文件**：无旧 `WindowsPomodoro.*` 残留，无提交到仓库内的临时截图、测试日志或 `.tmp` 文件。

### 5.2 自动化单元测试与 UI 端到端测试（Gate 2）
- [ ] **G2.1 单元测试 100% 通过**：运行 `PomodoroFocus.Tests.exe`，退出码为 0，覆盖：
  - 状态机：Start / Pause / Resume / Reset（<60s 不记 vs ≥60s 记 Interrupted）/ Skip / +5 分钟 / 4 个番茄进长休 / 正计时完成与放弃 / 跨 `DayStartHour` 归日 / 崩溃恢复补记；
  - 快速添加解析器：`#标签 !高 ~3` 各类组合与边界；
  - 任务服务：CRUD、完成解绑、筛选、4 种排序、删除撤销；
  - 统计服务：今日/本周/连续天数、7/30 天趋势、跨小时会话按比例拆分、标签/任务 Top5+其他分布、热力图 L0–L4 分级、UTF-8 BOM CSV 导出；
  - 存储服务：原子写入、JSON 损坏从 `.bak` 恢复、备份包导入导出；
  - 音频合成：9 种环境音 + 4 种提示音各渲染 1 秒样本，断言无 NaN/Inf、峰值 `≤ 1.0`、激活时 RMS 落在 `[0.03, 0.35]`、静音时全 0。
- [ ] **G2.2 UIA 端到端交互测试通过**：运行 `PomodoroFocus.UITests.exe`，退出码为 0，真实启动 `PomodoroFocus.exe` 并通过 UIAutomation 查找 `AutomationId` 完成：点击开始/暂停/重置、切换 5 大导航页、通过快速添加栏创建任务并在列表中验证出现、点亮环境音卡片、切换主题、打开并恢复迷你悬浮窗。

### 5.3 功能完整性（Gate 3 · 零占位按钮）
- [ ] **G3.1 计时全模式**：专注/短休/长休/正计时切换正常；Idle 点击数字可改本次时长；+5 分钟立即增加剩余时间；阶段完成触发提示音 + 完成卡片 + 粒子。
- [ ] **G3.2 任务闭环**：快速添加、对话框新建/编辑、删除 + Snackbar 撤销、勾选完成、标签芯片过滤、搜索、排序、绑定到专注页并累计番茄数全部真实生效。
- [ ] **G3.3 统计可视化**：4 张 KPI 卡、7/30 天趋势柱图、24h 时段面积图、标签/任务环形图、热力图、历史记录筛选与删除撤销、导出 CSV 全部真实计算渲染。
- [ ] **G3.4 离线环境音**：9 种环境音卡片可多选混音，5 个预设场景芯片一键切换，主音量/分轨音量/定时关闭/专注联动开关全部有效。
- [ ] **G3.5 桌面特性与设置**：迷你悬浮窗置顶/拖动/吸附/返回正常；托盘图标与右键菜单可用；4 套主题实时切换；所有设置项修改后重启依然保持；备份导入导出可用。

### 5.4 UI 视觉与审美验收（Gate 4 · `reviewer` 必须使用 `read_file` 逐张检视 10 张截图）
`reviewer` 运行 `PomodoroFocus.exe --data-dir "$env:TEMP\pf_review" --seed-demo --no-tray --no-hotkeys --no-sound --screenshot-suite "$env:TEMP\pf_shots"` 生成 10 张截图（并用单独一条 `BypassSandbox` 命令复制到 `E:\wu\localTemp\gemini_downloads\windows_pomodoro_review\`），逐张看图检查：
- [ ] **G4.1 视觉层次与玻璃质感**（图 01/04/06/07）：深色背景带柔和阶段色光晕，卡片与背景对比清晰，圆角平滑，无生硬刺眼的纯白粗边框或 Win95 原生灰底控件。
- [ ] **G4.2 进度圆环品质**（图 01/02/03）：60 刻度清晰、渐变进度弧圆润平滑、弧端发光白点准确落在弧尾、环内 4 行信息（阶段胶囊、76pt 大数字、预计结束副标题、周期番茄点阵）垂直居中协调，无任何重叠。
- [ ] **G4.3 阶段色一致性**（对比图 01 珊瑚红、图 02 薄荷青、图 03 星云紫）：进度环、主按钮、阶段胶囊、左上背景光晕、侧栏选中指示条随阶段同步变色。
- [ ] **G4.4 四套主题美感与对比度**（图 01 `midnight`、图 08 `mist` 浅色、图 09 `sunset` 日落、图 10 `forest` 森林）：特别是 `mist` 浅色主题下文字深色清晰、卡片白底柔和阴影，绝无“白底白字”或“深底黑字”看不清的问题。
- [ ] **G4.5 图表与数据展示品质**（图 06）：柱状图圆角柱体与目标虚线清晰、环形图扇区与图例对齐、24h 面积图曲线平滑、热力图方格矩阵整齐美观。
- [ ] **G4.6 最小尺寸响应式零截断**（图 09 `860×600`）：侧边栏自动收起为 68px 图标轨，专注页右侧栏转为底部水平双卡，所有按钮、文字、进度环完整显示，无任何文字被裁切一半或控件挤出窗外。
- [ ] **G4.7 迷你悬浮窗精致度**（图 10 `288×92`）：圆角边框、阶段标题、30pt 计时数字、右侧三枚圆钮、底部 3px 进度条排版紧凑精致。
- [ ] **G4.8 图标与字体**：所有图标均为清晰的 Segoe Fluent Icons 或 Lucide 矢量路径，无任何 `□` 缺字豆腐块；中文文案完全符合 3.2 节规范。
