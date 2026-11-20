# 番茄专注（PomodoroFocus）· 套餐 A 旗舰级 UI 精修规格书（APPLE_POLISH_V2）

> 版本：v2.1-ApplePolishV2（2026-11-20）　作者：`architect`　执行：`coder`　审核：`reviewer`
>
> **铁律约束（违反任意一条即视为失败）**：
> 1. **严禁执行任何 `git add / commit / push / tag / checkout / reset`** —— 所有改动只留在本地工作区供用户直接预览。
> 2. **严禁触碰或覆盖 `docs/screenshots/*.png`** —— 本轮新生成的 **11 张截图**（含新增的第 11 张禅模式截图 `11_focus_zen_mode.png`）输出到 `%TEMP%\PomodoroFocus_apple_v2_shots`。
> 3. **工具链与编码硬约束不变**：`WPF + .NET Framework 4.8 (x64) + C# 5 (<LangVersion>5</LangVersion>)`、纯 BCL 零外部依赖、所有文件保持 **UTF-8 with BOM**、`Debug|x64` 与 `Release|x64` 均 **0 错误 0 警告**、18/18 单元测试与 `PomodoroFocus.UITests.exe` 100% 通过。

---

## 1. 全高一体侧边栏 + Win11 原生 Mica 材质与内嵌画布（Inset Content Sheet）

### 1.1 全高一体侧边栏布局树（`Views/MainWindow.xaml`）
消除原先“40px 标题栏横切整个窗口顶部、把左侧栏劈成两半”的割裂感，升级为 **macOS Sequoia / Linear / Arc 全高一体侧栏 + 右侧内嵌圆角画布（Inset Content Sheet）**：

```
+-------------------+---------------------------------------------------------------+
| [🍅] 番茄专注     |            [● 专注中 · 18:42]      [🎵3] [禅] [📌][迷你][-][口][X] | <- 右侧无边框顶栏 42 DIP
|                   |  +---------------------------------------------------------+  |
| 专注工作台        |  |                                                         |  |
| [🕒] 专注         |  |                                                         |  |
| [≡]  任务      4  |  |               右侧内嵌圆角画布 (Inset Sheet)            |  |
| [📊] 统计         |  |           CornerRadius="14,0,0,0" (或 Margin="0,0,8,8"  |  |
| [🎧] 环境音    ●  |  |            CornerRadius="14") + Surface/Bg.Base         |  |
| [⚙]  设置         |  |                                                         |  |
|                   |  |                                                         |  |
| +---------------+ |  |                                                         |  |
| | 今日目标  4/8 | |  |                                                         |  |
| | [======>    ] | |  +---------------------------------------------------------+  |
+-------------------+---------------------------------------------------------------+
  Col 0: 204 / 68 DIP                   Col 1: * (右侧主舞台)
```

**`MainWindow.xaml` 改造要点**：
1. 将 `Main Layout Shell` 由原先的“上下两行（Row 0 标题栏 + Row 1 左右分栏）”改为 **顶层左右两列（Column 0 全高侧栏 `Width="{Binding SidebarWidth}"` + Column 1 右侧主区 `*`）**：
   - **Column 0（全高侧栏，从 `Y=0` 贯通到窗口底边）**：
     - 底部 `Border`：`Background="{DynamicResource Sidebar.Bg}"`、`BorderBrush="{DynamicResource Stroke.Subtle}"`、`BorderThickness="0,0,1,0"`。
     - 内部 `Grid` 分 3 行：
       - `Row 0`（`Height="48"`，应用品牌头）：左侧 `Margin="16,4,0,0"` 放置 `20×20` `Logo` 图标 + `"番茄专注"`（`FontSize="14" FontWeight="Bold" Foreground="{DynamicResource Text.Primary}"`，紧凑模式 `IsCompactLayout` 下隐藏文字并将 Logo 居中）。
       - `Row 1`（`Height="*"`）：`"专注工作台"` 分组小标题 + 5 个 macOS Source List 导航按钮。
       - `Row 2`（`Height="Auto"`）：左下角常驻「今日目标」进度小卡（保留，作为全局唯一的今日目标进度卡）。
   - **Column 1（右侧区域）**：内部 `Grid` 分 2 行：
     - `Row 0`（`Height="42"`，右侧专属无边框工具顶栏）：
       - 左侧/居中：非专注页时显示的 `TitleBarTimerPill`（`● 专注中 · 18:42`）；
       - 右侧按钮组（`WindowChrome.IsHitTestVisibleInChrome="True"`）：
         1. 环境音活动指示按钮；
         2. **新增：禅模式 / 右侧栏折叠切换按钮**（`AutomationId="ToggleZenModeButton"`，仅在 `IsFocusView && !IsCompactLayout` 时显示，图标使用 SF Symbol `sidebar.right` 矢量几何 `Geo.sidebar`，ToolTip `"收起/展开右侧栏 · 禅模式 (Ctrl+\)"`，激活态即禅模式开启时底色为 `{DynamicResource Accent.Soft}`、图标色 `{DynamicResource Accent}`）；
         3. 始终置顶按钮 `PinTopmostButton`；
         4. 迷你悬浮窗按钮 `EnterMiniModeButton`；
         5. 最小化 / 最大化 / 关闭三键。
     - `Row 1`（`Height="*"`）：**右侧内嵌画布（Inset Content Sheet）**：
       - 用一层 `<Border x:Name="ContentSheetBorder" CornerRadius="14,0,0,0" Background="{DynamicResource Bg.Base}" BorderBrush="{DynamicResource Stroke.Subtle}" BorderThickness="1,1,0,0" ClipToBounds="True">` 包裹背景氛围光晕、`InfoBar`、`ViewStageHost` 与 `Snackbar`！
       - 这样左侧全高侧栏（`Sidebar.Bg`）与右侧主内容区（`Bg.Base` 圆角内嵌画布）形成纯正的 **macOS Sequoia / Linear / Arc 双层材质对比**！

### 1.2 Win11 原生 Mica 材质接入与离线截图 100% 兼容方案（`Interop/DwmInterop.cs` & `App.xaml.cs`）
1. 在 `DwmInterop.cs` 中新增常量与设置：
   ```csharp
   private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
   private const int DWMSBT_MAINWINDOW = 2; // Mica
   ```
   在 `ApplyWindowChromeAttributes(Window window, bool isDark, Color borderColor)` 中，调用完 `DWMWA_USE_IMMERSIVE_DARK_MODE` 与 `DWMWA_BORDER_COLOR` 后，继续调用：
   ```csharp
   int backdrop = DWMSBT_MAINWINDOW;
   DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
   ```
2. **Mica 透射与 `RenderTargetBitmap` 截图双全设计**：
   - 在 `MainWindow.xaml` 中，`WindowChrome` 设置 `GlassFrameThickness="-1"`，同时 **`RootHost` 的 `Background` 保持绑定 `{DynamicResource Bg.Base}`**，左侧全高侧栏 `Border` 的 `Background` 绑定 `{DynamicResource Sidebar.Bg}`（在 `Tokens.*.xaml` 中将 `Sidebar.Bg` 设为与 `Bg.Base` 有明显明度/冷暖区分的高质感材质色：
     - `midnight`：侧栏 `Sidebar.Bg = #FF131316`，右侧内嵌画布 `Bg.Base = #FF0B0B0D`，卡片 `Surface.1 = #FF18181B`；
     - `mist`：侧栏 `Sidebar.Bg = #FFEAEAEF`，右侧内嵌画布 `Bg.Base = #FFF6F6F9`，卡片 `Surface.1 = #FFFFFFFF`；
     - `sunset`：侧栏 `Sidebar.Bg = #FF1A1316`，右侧内嵌画布 `Bg.Base = #FF120D0F`，卡片 `Surface.1 = #FF1D1619`；
     - `forest`：侧栏 `Sidebar.Bg = #FF101915`，右侧内嵌画布 `Bg.Base = #FF0A100D`，卡片 `Surface.1 = #FF141F1A`）。
   - **为什么这样设计最稳妥**：
     - 既通过 `DWMWA_SYSTEMBACKDROP_TYPE = 2` + `DWMWA_USE_IMMERSIVE_DARK_MODE` 激活了 Win11 原生 DWM 云母边框与窗口阴影合成；
     - 又保证 `RootHost` 在 WPF 视觉树内拥有完整的不透明底色层，因此无论在实机桌面还是在 `--screenshot` / `--screenshot-suite` 的 `RenderTargetBitmap` 离屏渲染下，**100% 不会出现任何透明穿透发黑或合成丢帧**，且侧栏与右侧内嵌圆角画布的层次感在截图与实机下完全一致！

---

## 2. 专注页去重 + 禅模式（Inspector 折叠）+ 自适应紧凑右侧栏

### 2.1 删除专注页重复的「今日目标」卡片 & 消除「今日专注记录」底部空黑框（`Views/FocusView.xaml`）
1. **删除专注页右侧栏的 `Card 1: Today Goal`**（原 `FocusView.xaml` 第 434–461 行）：
   - 因为左下角侧边栏已经有常驻的「今日目标 4/8 · 已专注 1 小时 40 分」进度卡片，删除右侧重复卡片后，画面立刻清爽呼吸！
2. **右侧栏改为自适应紧凑高度（消除大块空黑框）**：
   - 将右侧栏 `RightRailPanel`（宽 `288` DIP，垂直居中或顶部对齐 `VerticalAlignment="Center"`）的 `RowDefinitions` 改为两行 `Auto`（不再让「今日专注记录」用 `*` 撑满整个窗口高度）：
     - `Row 0` (`Height="Auto"`)：**今日专注记录卡片**（内部的 `ScrollViewer` 设置 `MaxHeight="250"`，当只有 4 条记录时卡片高度刚好贴合 4 条记录，**彻底消除底部那一大块空洞的黑框**！）；
     - `Row 1` (`Height="Auto"`)：**环境音快捷卡片**（`Margin="0,14,0,0"`）。
3. **860×600 紧凑模式底部栏同步精简**：
   - 在 `860×600` 紧凑布局下（左侧栏已收起为 `68px` 图标轨+底部圆形进度标），底部卡片区可以展示紧凑的水平双卡（左：今日专注概览/目标；右：快捷环境音）。

### 2.2 禅模式（Zen Mode / Inspector 折叠，`MainViewModel.cs` & `FocusView.xaml`）
1. 在 `MainViewModel`（或 `FocusViewModel`）中新增属性：
   - `bool IsZenMode { get; set; }`（默认 `false`）及 `ICommand ToggleZenModeCommand`。
   - 在 `MainWindow.xaml.cs` 的 `OnWindowPreviewKeyDown` 中增加快捷键支持：按下 `Ctrl + \`（`Key.Oem5` 且 `Keyboard.Modifiers == ModifierKeys.Control`）时触发 `ToggleZenModeCommand`。
2. 在 `FocusView.xaml.cs`（或通过 DataTrigger / 布局同步方法 `ApplyResponsiveLayout`）中：
   - 当 `!IsCompactLayout && IsZenMode == true` 时：将右侧栏 `RightRailPanel.Visibility = Visibility.Collapsed`，并将 `RightRailCol.Width = new GridLength(0)`、`CenterStageGrid.Margin = new Thickness(0)` —— 此时中央计时圆环、控制按钮与底部关联任务胶囊在右侧内嵌画布正中央居安展示，形成极具沉浸感的 **Zen Mode 禅模式**！
3. 在 `Themes/Icons.xaml` 中新增 SF Symbol `sidebar.right` 矢量图标：
   ```xml
   <Geometry x:Key="Geo.sidebar">M4 4 H20 A2 2 0 0 1 22 6 V18 A2 2 0 0 1 20 20 H4 A2 2 0 0 1 2 18 V6 A2 2 0 0 1 4 4 Z M15 4 V20</Geometry>
   ```

---

## 3. 字体字重升级与 `MM:SS` 冒号 `:` 光学垂直居中微雕

### 3.1 主计时数字与迷你窗数字：字重升级 + 冒号光学居中（100% 兼容 UIA 测试）
- **问题根源**：字体中的冒号 `:` 基线对齐在字母底部，放在大号数字 `18:42` 中间时视觉重心明显偏低；且 `Light` 字重在深色/浅色背景下略显纤弱，迷你窗更是出现了“标题粗、数字细”的字重倒挂。
- **光学微雕与 UIA 兼容方案**：
  1. 在 `FocusViewModel.cs` 中，当 `TimerText` 更新时（如 `"18:42"` 或 `"1:05:20"`），同时拆分出两个辅助只读属性（或三个部分）：
     - `string TimerLeadText`：最后一个冒号之前的部分（或第一个冒号之前，实际上對於 `"18:42"`，`Split(':')` 得到 `TimerPart1 = "18"`, `TimerPart2 = "42"`；对于 `"1:05:20"`，可按首个/末个冒号或直接判断：若含 1 个冒号则 `TimerLeftText = "18"`, `TimerRightText = "42"`, `HasSingleColon = true`；若含 2 个冒号如 `1:05:20` 则 `TimerLeftText = "1:05"`, `TimerRightText = "20"`）。
     - 更简洁通用的写法：在 `FocusViewModel` 中提供：
       - `TimerLeadText`（如 `"18"`）
       - `TimerTrailText`（如 `"42"`）
  2. **如何在 XAML 中让冒号光学垂直居中上移，同时让 UIA 测试 `WaitForAutomationId(mainWin, "TimerDisplayText")` 100% 读到完整的 `"25:00"` / `"30:00"`？**
     - 在 WPF 中，`TextBlockAutomationPeer` 读取 `AutomationProperties.Name` 属性具有最高优先级！
     - 因此，在 `FocusView.xaml` 中，将 `TimerDisplayText` 保持为 `TextBlock`（保留 `x:Name="TimerDisplayText"` 与 `AutomationProperties.AutomationId="TimerDisplayText"`），**显式绑定 `AutomationProperties.Name="{Binding TimerText}"`**，并在 `TextBlock` 内部使用 `Run` + 基线上移的冒号 `Run`（或者 `BaselineAlignment="Center"`）：
       ```xml
       <TextBlock x:Name="TimerDisplayText"
                  AutomationProperties.AutomationId="TimerDisplayText"
                  AutomationProperties.Name="{Binding TimerText}"
                  FontFamily="{StaticResource Font.Display}"
                  FontSize="{Binding TimerFontSize}"
                  FontWeight="Medium"
                  Foreground="{DynamicResource Text.Primary}"
                  Typography.NumeralAlignment="Tabular"
                  HorizontalAlignment="Center"
                  TextAlignment="Center">
           <Run Text="{Binding TimerLeadText, Mode=OneWay}" /><Run Text=":" BaselineAlignment="Center" /><Run Text="{Binding TimerTrailText, Mode=OneWay}" />
       </TextBlock>
       ```
     - **实测验证原理**：在 WPF `TextBlock` 中，给 `<Run Text=":" BaselineAlignment="Center" />` 设置 `BaselineAlignment="Center"`（或配合 `FontSize` 微调），WPF 排版引擎会自动将该 `Run` 的字形垂直中心对齐到整行数字的光学中轴线上！而且它本身仍然是同一个 `TextBlock`，不仅 `AutomationProperties.Name` 是完整的 `"25:00"`，连 `TextBlock.Text` 属性拼接也是 `"25:00"`，对 `UiaEndToEndInteractionTest` 100% 零风险！
  3. **迷你悬浮窗（`Views/MiniTimerWindow.xaml`）字重倒挂修复**：
     - 顶部任务标题 `MiniHeaderText`：从 `FontWeight="SemiBold"` 改为 **`FontSize="12" FontWeight="Normal" Foreground="{DynamicResource Text.Secondary}"`**（克制退后）；
     - 主计时数字 `MiniTimerText`：字号 **`32`**，字重从 `Light` 升级为 **`FontWeight="SemiBold"`**，颜色 `{DynamicResource Text.Primary}`，同样采用：
       ```xml
       <TextBlock Grid.Column="0"
                  x:Name="MiniTimerText"
                  AutomationProperties.AutomationId="MiniTimerText"
                  AutomationProperties.Name="{Binding FocusVM.TimerText}"
                  Style="{StaticResource Text.TimerMini}"
                  FontWeight="SemiBold"
                  VerticalAlignment="Center">
           <Run Text="{Binding FocusVM.TimerLeadText, Mode=OneWay}" /><Run Text=":" BaselineAlignment="Center" /><Run Text="{Binding FocusVM.TimerTrailText, Mode=OneWay}" />
       </TextBlock>
       ```

### 3.2 设置页升级为 iOS / macOS System Settings 经典外置分组标题布局（`Views/SettingsView.xaml`）
将全部 8 个分组（外观、计时、自动化、提醒与声音、快捷键、系统、数据、关于）的分组标题移到 `CardBorder` **外部上方**：
- **外置分组标题行**（位于每张 `<Border Style="{StaticResource CardBorder}">` 上方，`Margin="6,4,0,8"`）：
  - 左侧 `22×22` 圆角方形彩色图标徽章（`CornerRadius="5"`，图标 `12px #FFFFFF`） + 右侧分组名（`FontSize="13.5" FontWeight="SemiBold" Foreground="{DynamicResource Text.Secondary}" Margin="8,0,0,0"`）。
- **分组圆角卡片**（`<Border Style="{StaticResource CardBorder}" Padding="18,6" Margin="0,0,0,18">`）：
  - 卡片内部只放干净紧凑的设置项行与 `1px Stroke.Subtle` 细分隔线！

---

## 4. 任务页去冗余竖线 + 10% 极淡同色底圆框 + Hover 渐进显露（`Views/TasksView.xaml`）

1. **移除最左侧多余的优先级彩色竖线**：
   - 删除 `TasksView.xaml` 第 226–243 行的 `<Border Grid.Column="0" Width="3" Height="20" ... />` 竖条，并将该列去掉。
2. **Apple Reminders 圆形勾选框（`20×20` 彩色圆环 + 10% 极淡同色内底）**：
   - 未完成态（`IsCompleted == False`）：
     - 高优先级（`Priority == 3`）：`BorderBrush="{DynamicResource Priority.High}"`、`Background="#1AFF453A"`；
     - 中优先级（`Priority == 2`）：`BorderBrush="{DynamicResource Priority.Medium}"`、`Background="#1AFF9F0A"`；
     - 低优先级（`Priority == 1`）：`BorderBrush="{DynamicResource Priority.Low}"`、`Background="#1A0A84FF"`；
     - 无优先级（`Priority == 0`）：`BorderBrush="{DynamicResource Stroke.Strong}"`、`Background="Transparent"`。
   - 已完成态（`IsCompleted == True`）：实心 `{DynamicResource Success}` + 白色勾 `Icon.Check`。
3. **右侧操作按钮 Hover 渐进显露（Progressive Disclosure，100% 兼容 UIA）**：
   - 将任务行右侧的操作按钮容器 `<StackPanel x:Name="RowActionsPanel" Grid.Column="..." Orientation="Horizontal" VerticalAlignment="Center">` 的样式设置为：
     - 常态 `Opacity="0.16"`（保持 `Visibility="Visible"`，视觉上极度克制隐退，不抢任务标题与番茄点阵的视线，且 UIA 树始终可见可点）；
     - 当鼠标悬停在整行卡片 `RowCard` 上时（通过 `<DataTrigger Binding="{Binding IsMouseOver, ElementName=RowCard}" Value="True">`），`RowActionsPanel.Opacity` 变为 `1.0`，同时 `RowCard.Background` 抬升为 `{DynamicResource Surface.2}`、`RowCard.BorderBrush` 抬升为 `{DynamicResource Stroke.Strong}`！

---

## 5. 环境音未激活磁贴滑块降噪（`Views/AmbientAudioView.xaml`）

解决图 07 中“未开启的音轨卡片底部仍然挂着一条彩色进度条”的视觉噪音：
- 修改 `AmbientAudioView.xaml` 第 310–326 行卡片底部的 `Slider` 样式：
  ```xml
  <Slider Grid.Row="1"
          Value="{Binding Volume, Mode=TwoWay}"
          Minimum="0"
          Maximum="100"
          Margin="0,10,0,0">
      <Slider.Style>
          <Style TargetType="Slider" BasedOn="{StaticResource AppSlider}">
              <Setter Property="Foreground" Value="{DynamicResource Switch.Off}" />
              <Setter Property="Opacity" Value="0.28" />
              <Style.Triggers>
                  <DataTrigger Binding="{Binding IsActive}" Value="True">
                      <Setter Property="Foreground" Value="{Binding TintBrush}" />
                      <Setter Property="Opacity" Value="1.0" />
                  </DataTrigger>
              </Style.Triggers>
          </Style>
      </Slider.Style>
  </Slider>
  ```
- **效果**：未播放的 6 张音轨磁贴底部滑块变为极低调的 `28%` 透明度纯灰细轨（无任何彩色填充）；一旦点击激活播放（如“细雨 / 篝火 / 时钟滴答”），立刻亮起 `100%` 鲜明的专属 Apple 语义色填充轨与纯白瓷感滑块！

---

## 6. 设置页 4 张主题预览卡重绘为真实微缩窗口骨架（`SettingsView.xaml` & `SettingsViewModel.cs`）

解决图 08 中“三张深色主题预览卡看起来几乎一样、只有一个小圆圈”的问题：
1. 在 `SettingsViewModel.cs` 的 `ThemeCardItem` 中补充微缩骨架色值属性：
   - `SidebarHex`（微缩侧栏底色）、`CanvasHex`（微缩内嵌画布底色）、`CardHex`（微缩卡片色）、`RingHex`（微缩专注环主色）、`AccentHex`（微缩交互强调色）、`GlowHex`（微缩氛围柔光色）：
     - `midnight`（深邃夜空）：`SidebarHex="#141418"`, `CanvasHex="#0B0B0E"`, `CardHex="#1C1C20"`, `RingHex="#FF453A"`, `AccentHex="#0A84FF"`, `GlowHex="#280A84FF"`
     - `mist`（晨雾浅色）：`SidebarHex="#E6E6EC"`, `CanvasHex="#F5F5F7"`, `CardHex="#FFFFFF"`, `RingHex="#FF3B30"`, `AccentHex="#0071E3"`, `GlowHex="#180071E3"`
     - `sunset`（暖阳日落）：`SidebarHex="#211519"`, `CanvasHex="#140D10"`, `CardHex="#2B1C21"`, `RingHex="#FF693A"`, `AccentHex="#FF9F0A"`, `GlowHex="#38FF9F0A"`
     - `forest`（静谧森林）：`SidebarHex="#111E18"`, `CanvasHex="#0A120E"`, `CardHex="#182A22"`, `RingHex="#30D158"`, `AccentHex="#30D158"`, `GlowHex="#3530D158"`
2. 在 `SettingsView.xaml` 的 `ThemeCards` DataTemplate（`Height="98"`）中绘制**真实微缩窗口骨架**：
   - 上部微缩窗口预览框（高 62，圆角 7，外框 `1px Stroke.Subtle`）：
     - 左列微缩全高侧栏（宽 `26`，底 `{Binding SidebarHex}`）：顶部一个小品牌圆点（`RingHex`），下方 3 条微缩导航胶囊条（第一条高亮 `AccentHex`）；
     - 右列微缩内嵌画布（底 `{Binding CanvasHex}`，右上角叠一团 `{Binding GlowHex}` 椭圆柔光）：
       - 中央偏左：一枚 `24×24` 微缩计时圆环（底环 `#33888888` + 彩色圆环 `{Binding RingHex}` `StrokeThickness="2.5"` + 中心微缩播放圆点）+ 下方一枚 `{Binding AccentHex}` 微缩胶囊按钮（`16×4`）；
       - 右侧：上下两张 `{Binding CardHex}` 微缩辅助卡片（`28×18` 与 `28×16`，内含微缩进度条）！
   - 右上角选中态勾徽标（`18×18` `{DynamicResource Accent}` 实心圆 + 白勾）。
   - 底部主题名称（`12.5px SemiBold`）。

---

## 7. 统计页 Apple Health / Stripe 级图表与 KPI 升级

### 7.1 KPI 卡片同比微徽章（`Views/StatsView.xaml` & `ViewModels/StatsViewModel.cs`）
- 在 `StatsViewModel.cs` 中新增属性：
  - `string WeekDeltaBadgeText`（例如 `kpi.WeekFocusSeconds >= kpi.PrevWeekSamePeriodSeconds` 时为 `"↗ +40%"` 或 `"↗ 持平"`，下降时为 `"↘ -15%"`）；
  - `bool IsWeekDeltaPositive`（`>= 0` 为 `true`）；
  - `string CompletionRateBadgeText`（例如 `"完成率 94%"`）与 `string TotalPomodorosSubText`（例如 `"61 个番茄"`）。
- 在 `StatsView.xaml` 的第 2 张 KPI 卡（本周专注）底部：
  - 左侧放置一枚圆角胶囊微徽章（`<Border CornerRadius="6" Padding="6,2" Background="#2230D158">` + `<TextBlock Text="{Binding WeekDeltaBadgeText}" FontSize="11" FontWeight="SemiBold" Foreground="{DynamicResource Success}" />`）+ 右侧 `<TextBlock Text="较上周" Style="{StaticResource Text.Caption}" Margin="6,0,0,0" VerticalAlignment="Center" />`。
- 在第 4 张 KPI 卡（累计专注）底部：
  - 左侧放置 `<Border CornerRadius="6" Padding="6,2" Background="{DynamicResource Accent.Soft}">` + `<TextBlock Text="{Binding CompletionRateBadgeText}" FontSize="11" FontWeight="SemiBold" Foreground="{DynamicResource Accent}" />` + 右侧 `<TextBlock Text="{Binding TotalPomodorosSubText}" Style="{StaticResource Text.Caption}" Margin="6,0,0,0" VerticalAlignment="Center" />`。

### 7.2 `TrendBarChart.cs` 柔和现代渐变胶囊柱 + 悬停浮动玻璃 Tooltip
1. **非今日柱体告别沉闷暗红**：
   - 将非今日柱体画刷 `normalBarBrush` 升级为柔和现代的垂直线性渐变 `LinearGradientBrush(0,0 -> 0,1)`：顶部 `WithAlpha(startColor, 0xCC)`（80% 透明度）平滑过渡到底部 `WithAlpha(startColor, 0x55)`（33% 透明度）；今日柱体或悬停柱体使用 100% 高亮 `Phase.Gradient` 并在柱顶加 `1px` 高光边。
2. **画布内浮动玻璃 Tooltip（On-Canvas Floating Glass Pill）**：
   - 当 `_hoverIndex >= 0 && _hoverIndex < pts.Count` 时，在悬停柱顶部上方直接用 `DrawingContext` 绘制一枚圆角玻璃浮层胶囊（`CornerRadius=7`、底 `Surface.Solid`、描边 `1px Stroke.Strong`、文字 `11px SemiBold Text.Primary` 显示 `pts[_hoverIndex].TooltipTitle`），左右坐标自动夹紧在 `[4, w - pillW - 4]` 内防止越界。

### 7.3 `HourlyAreaChart.cs` 悬停竖向十字准线 + 浮动数值胶囊
- 当 `_hoverHour >= 0 && _hoverHour < 24` 时：
  - 绘制竖向虚线十字准线（`DashStyle(3,3)`）与 `5px` 发光圆点；
  - 同时在准线上方绘制一枚圆角玻璃数值胶囊（`CornerRadius=7`、底 `Surface.Solid`、描边 `1px Stroke.Strong`、文字 `"09:00 – 10:00 · 50 分钟"`），水平位置自动夹紧在图表宽度内。

### 7.4 `FocusHeatmapControl.cs` 默认近 20 周饱满大格视图 + `近 20 周 / 全年` 切换器（`StatsView.xaml` & `StatsViewModel.cs`）
1. **消除左半屏大片空置灰格**：
   - 在 `StatsViewModel.cs` 中新增 `HeatmapRangeOptions`（`SegmentOption { Key="20", Label="近 20 周" }`, `SegmentOption { Key="52", Label="全年" }`）与属性 `string HeatmapRangeKey`（**默认 `"20"`**！），标题随选项显示 `"专注热力图（近 20 周）"` 或 `"专注热力图（全年 52 周）"`。
2. 在 `StatsView.xaml` 的专注热力图卡片右上角（图例左侧或卡片右上）：
   - 放置 `<controls:SegmentedControl Width="148" SegmentHeight="26" Items="{Binding HeatmapRangeOptions}" SelectedKey="{Binding HeatmapRangeKey, Mode=TwoWay}" Margin="0,0,14,0" />` + `少 [][][][][] 多` 图例。
3. 在 `FocusHeatmapControl.cs` 中：
   - 新增依赖属性 `TargetWeeks`（`int`，默认 `20`，绑定 `{Binding HeatmapWeeksCount}`）：
   - **动态饱满大格计算**：在 `OnRender` 与 `OnHeatmapMouseMove` 中，根据当前 `weeks = Math.Max(1, cells.Count / 7)` 和控件可用宽度 `availW = ActualWidth - LeftAxisWidth - 4.0` 动态计算格子尺寸：
     - 当 `weeks <= 24`（即 **近 20 周** 默认模式）时：`cellGap = 4.0`，`cellSize = Math.Floor((availW - (weeks - 1) * cellGap) / weeks)`，夹紧在 `[16.0, 26.0]`，圆角 `radius = 4.5`！同时控件总高度自适应为 `TopAxisHeight + 7 * (cellSize + cellGap) + 6.0`（在 `MeasureOverride` 中根据 `availableSize.Width` 计算返回精确高度）。
     - **视觉效果**：在 `1080` 宽度的统计页中，`availW ≈ 920px`，20 列大格子每格自动达到 **`22×22` DIP（圆角 4.5px）** 或居中排满整个卡片宽度，方格饱满润泽如 Apple Fitness 奖章矩阵，且近 20 周的数据紧密铺展，彻底告别“左半边全是空灰点”！

---

## 8. 截图套件扩展（11 张标准截图，新增 `11_focus_zen_mode.png`）

在 `src/PomodoroFocus.App/App.xaml.cs` 的 `RunScreenshotSuite(string outDir)` 数组末尾追加第 11 步（保持前 10 张文件名与顺序 100% 不变，确保 `PomodoroFocus.UITests.exe` 校验 100% 通过）：
- **第 10 步**（`10_mini_floating_window.png`）之后，追加 **第 11 步**：
  ```csharp
  // 11: Midnight Focus Zen Mode (Inspector Right Rail Collapsed, 1080x720)
  delegate
  {
      _mainWindow.Width = 1080;
      _mainWindow.Height = 720;
      _mainVm.IsCompactLayout = false;
      _mainVm.IsZenMode = true;
      _mainVm.SwitchTheme("midnight", true);
      _mainVm.NavigateTo("focus");
      _mainVm.Engine.SetDemoState(TimerMode.Pomodoro, TimerPhase.Focus, TimerRunState.Running, 18 * 60 + 42, 1);
      _mainVm.ThemeService.AnimatePhaseBrush(TimerPhase.Focus, true);
      RenderFlushAndSave(_mainWindow, Path.Combine(outDir, "11_focus_zen_mode.png"));
      _mainVm.IsZenMode = false;
  }
  ```

---

## 9. 需修改的文件清单（共 17 个文件）

1. `src/PomodoroFocus.App/Interop/DwmInterop.cs`（接入 `DWMWA_SYSTEMBACKDROP_TYPE = 38` Mica）
2. `src/PomodoroFocus.App/Themes/Icons.xaml`（新增 `Geo.sidebar` SF Symbol 矢量图标）
3. `src/PomodoroFocus.App/Themes/Tokens.midnight.xaml` / `Tokens.mist.xaml` / `Tokens.sunset.xaml` / `Tokens.forest.xaml`（微调全高侧栏 `Sidebar.Bg` 与右侧内嵌画布 `Bg.Base` 的明度阶差）
4. `src/PomodoroFocus.App/Themes/Typography.xaml`（`Text.Timer` 与 `Text.TimerMini` 字重升级为 `Medium` / `SemiBold`）
5. `src/PomodoroFocus.App/Views/MainWindow.xaml` & `MainWindow.xaml.cs`（全高一体侧栏 + 右侧圆角内嵌画布 `Inset Content Sheet` + 顶栏禅模式按钮 `ToggleZenModeButton` + `Ctrl+\` 快捷键）
6. `src/PomodoroFocus.App/ViewModels/MainViewModel.cs`（`IsZenMode` 与 `ToggleZenModeCommand`）
7. `src/PomodoroFocus.App/Views/FocusView.xaml` & `FocusView.xaml.cs` & `ViewModels/FocusViewModel.cs`（删除右侧重复今日目标卡、自适应高度消除黑框、禅模式折叠右侧栏、`TimerLeadText : TimerTrailText` 冒号 `BaselineAlignment="Center"` 光学居中 + `Medium` 字重）
8. `src/PomodoroFocus.App/Views/MiniTimerWindow.xaml`（修复字重倒挂：数字 `32pt SemiBold` + 冒号光学居中，标题 `12pt Normal` 次级灰）
9. `src/PomodoroFocus.App/Views/TasksView.xaml`（移除最左侧冗余彩色竖线、保留 10% 极淡同色底优先级圆框、右侧操作按钮 `0.16 -> 1.0` Hover 渐进显露）
10. `src/PomodoroFocus.App/Views/AmbientAudioView.xaml`（未激活磁贴滑块降噪为 `0.28` 纯灰轨，激活才亮彩色）
11. `src/PomodoroFocus.App/Views/SettingsView.xaml` & `ViewModels/SettingsViewModel.cs`（iOS/macOS 外置分组标题布局 + 4 张主题预览卡重绘为真实微缩窗口骨架）
12. `src/PomodoroFocus.App/Views/StatsView.xaml` & `ViewModels/StatsViewModel.cs`（KPI 同比箭头胶囊微徽章 + 热力图 `近 20 周 / 全年` 切换器，默认近 20 周）
13. `src/PomodoroFocus.App/Controls/Charts/TrendBarChart.cs`（柔和现代垂直渐变胶囊柱 + 画布内浮动玻璃 Tooltip）
14. `src/PomodoroFocus.App/Controls/Charts/HourlyAreaChart.cs`（悬停竖向十字准线 + 画布内浮动数值胶囊）
15. `src/PomodoroFocus.App/Controls/Charts/FocusHeatmapControl.cs`（近 20 周动态饱满大格计算与 `MeasureOverride` 自适应高度）
16. `src/PomodoroFocus.App/App.xaml.cs`（`RunScreenshotSuite` 增加第 11 张 `11_focus_zen_mode.png`）
