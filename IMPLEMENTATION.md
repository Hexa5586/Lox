# Lox 实现文档

> 本文档面向后续实现者（包括 AI Agent），用于把 `SUMMARY.md` 中的设定落成可运行的 Tuanjie 工程。
>
> 阅读顺序建议：先看“设计决策与待确认”，再看“坐标系统”“颜色模型”“光学仿真”，最后按“里程碑”实现。

## 目录

- [0. 文档约定](#0-文档约定)
- [1. 设计决策与待确认](#1-设计决策与待确认)
- [2. 系统架构](#2-系统架构)
- [3. 工程结构](#3-工程结构)
- [4. 坐标系统与盒子模型](#4-坐标系统与盒子模型)
- [5. 数据模型与 JSON Schema](#5-数据模型与-json-schema)
- [6. 颜色模型](#6-颜色模型)
- [7. 光学仿真](#7-光学仿真)
- [8. Unity 实现](#8-unity-实现)
- [9. 游戏流程](#9-游戏流程)
- [10. 关卡制作与校验](#10-关卡制作与校验)
- [11. 测试计划](#11-测试计划)
- [12. 里程碑](#12-里程碑)
- [13. 风险与备选方案](#13-风险与备选方案)
- [14. 给后续实现者的检查清单](#14-给后续实现者的检查清单)

---

## 0. 文档约定

### 0.1 单位与角度

- 长度单位：Unity 单位。盒子边长默认 `1.0`。
- 角度：JSON 和 UI 使用角度（度）；数学计算内部使用弧度。
- 颜色：游戏逻辑使用线性 RGB，三个分量范围 `[0, 1]`，每束光的总亮度为 `R + G + B`。
- 时间：光学仿真是瞬时的，不使用物理引擎或时间积分。

### 0.2 面内坐标

每个子谜题是一个二维平面。面内坐标记作 `(u, v)`：

- `u`：从盒子外部看该面时的水平轴，向右为正。
- `v`：从盒子外部看该面时的竖直轴，向上为正。
- 面法线：`n = u × v`，指向盒子外部。
- 元素旋转角 `rotation`：绕法线旋转，正方向定义为“从盒子外部看逆时针”。

> Unity 的四元数旋转方向在实现时需要用一个小测试确认；如果视觉方向相反，只翻转 `ElementView` 应用角度时的符号，不要改 JSON 语义。

### 0.3 “面内二维、渲染三维”

- 每个子谜题的所有器件中心、光路、碰撞体都位于同一个面平面内。
- 器件本身有厚度，沿法线方向挤出，但**光学计算只使用二维截面**。
- 不允许沿法线方向平移；玩家只能在 `(u, v)` 平面内移动和绕法线旋转。

### 0.4 当前实现状态

**已实现：**

- Tuanjie `2022.3.62t18` 工程、`Main.unity`、Windows x86_64 构建。
- 无色透明亚克力外壳（`Lox/AcrylicGlass`，运行时双层壳表现壁厚、`Lox/FrostedAluminum` 磨砂铝合金包边）、`#bbbbbb` 缎面磨砂玻璃内胆、六个 `FaceRoot`。
- 24 段边界墙（`#777777`）；固定墙、平面镜和凹面镜由关卡 JSON 生成。
- 14 个发射器、10 个接收器、1 个三棱镜、6 个滤光片、4 面可操作平面镜、顶面总锁底座、6 盏环灯和 1 盏中心白灯。
- 选中、`#ffff77` 二维轮廓描边、沿轴移动/旋转、摄像机参考系、盒子拖动/吸附/数字键盘旋转。
- 简化面内激光：接收器、平面镜反射、墙体阻挡、柱面凸透镜折射、三棱镜 R/G/B 分光。
- 颜色核心：16 色 `Palette`、`LightColor`/`MixResult`、混色与调色板拟合（`ColorMath`）。
- 三棱镜：`LaserPrism` + `PrismMesh`，等边三角直棱柱；白光/复合光进入时按 R/G/B 分量拆成独立分支，每个分量使用独立折射率并支持全反射。
- 滤光片仿真：`LaserFilter` 按分量吸收，命中极窄端时阻断；光束按段使用滤光后的颜色渲染；接收器按拟合后的调色板 ID 判定，亮度不敏感，支持多束光进入同一接收器。
- 锁：`LaserLock` 覆盖器件、圆半径阻断光线、按全局颜色解锁、开盖与筒壁下沉、JSON `unlockRequires`；小型锁顶部用所需颜色球冠指示灯，总锁使用环形灯；锁体、锁盖和总锁底座统一使用 `LockBase.mat`（`Lox/SatinFrostedGlass`，深灰锁体色）。
- back 面“可移动平面镜 + 固定平面镜 + 凹面镜 + 3 段墙”关卡、left 面“三发射器 + 自由移动凸透镜”关卡。
- 黄面（right / Y）关卡：左侧白色光源、顶部白色光源（G+B 解锁）、45° 橄榄透镜、两段斜墙、绿色/黄色滤光片、右下角青色光源、`G+C` 接收器、`G+C` 锁覆盖的黄色接收器。
- 完整关卡 JSON 几何加载器：`Assets/StreamingAssets/Levels/level_01.json` 提供 39 个器件、5 段固定墙、back 的固定/可移动平面镜与凹面镜、顶面正方形反射环、14 条光束和顶面总锁灯环；`DeviceConfigLoader` 在 `Start` 中生成全部关卡几何。
- IMGUI 左下角键位提示，使用 `Assets/Resources/Icons/` PNG。
- 计时/重开/通关 UI：右上角计时器、左上角仅图标无外框的重新开始按钮、通关后的顶锁 `TOUCH` 和“你赢了！”对话框。

**尚未实现：**

- 钥匙孔动画、锁的点击提示 HUD、选中器件详细信息。
- 自动化测试、终版材质/动画。

**当前初始状态：**

- 初始视角正对顶面（总锁的 6 盏环灯与中心白灯）。
- front（M/C）有红、蓝、白三个固定发射器：红、蓝两束光由自由移动的柱面透镜聚焦，在洋红接收器处混成 `M`；白光经青色滤光片后，由同一块透镜折射到左下角的青色接收器。透镜被绿色（`G`）锁覆盖，因此依赖 back 面先点亮 `G`。该面初始不亮。
- bottom 面是“调色台”：3 个白色发射器、中心三棱镜（被 Y+M 锁覆盖）、顶部 C/M/Y 滤光片、底部 `B+Y` / `R+C` 局部接收器，以及两者正中间的红色全局接收器（绑定顶面 R 灯）；局部接收器不绑定顶灯，初始都不亮；M 滤光片保留，`G+M` 接收器已删除。
- top 面是正方形反射环：白色发射器（B+Y 锁）、R+C 锁的对角镜、M/C 锁的两面角镜、白色接收器和中心白灯；所有锁打开后光线绕环回到接收器。
- back（G）被墙面 1 挡住；left（B）的三个发射器都朝向蓝色接收器，凸透镜居中时无法只保留蓝光，且凸透镜带有绿色锁。
- right（Y）的黄色接收器被 `G+C` 锁覆盖，顶部白色光源又被 G+B 锁住，因此 Y 初始不亮；左侧白色光源经过黄色滤光片后变黄。G+B 解锁后，顶部白光经绿色滤光片与右下角青色光源在 `G+C` 接收器会合，才能解锁 Y。
- 顶面 6 盏环灯中 G/B/Y/R/M/C 六盏初始全部亮起（六个全局接收器都未满足），中心白灯初始亮（白色接收器未命中）；通关后顶锁出现 `TOUCH`，点击打开通关对话框。

### 0.5 完整关卡几何加载器（已实现）

`DeviceConfigLoader` 现在按 `level_01.json` 在运行时生成关卡内容，场景只保留固定结构：

**由 JSON 生成**

- 39 个器件：类型（`emitter` / `receiver` / `cylindrical_lens` / `filter` / `mirror` / `prism` / `concave_mirror`）、颜色、面、位姿、初始朝向、尺寸、是否投影、可操作性、锁条件。
- 每个发射器对应的 `Beam_<器件 ID 后缀>` LineRenderer；同一面可以有两个同色发射器而不会重名。
- 5 段固定墙（back 3 段、right 两段斜墙）、back 固定平面镜/可移动平面镜和 back 凹面镜。
- 顶面总锁：灰色底座、6 盏环灯和 1 盏中心白灯及其位置/顺序；接收器通过 `lamp` 字段绑定同色顶灯。
- 器件的 `LaserLock`、顶灯、`LaserReceiver` 连接和 `DeviceSelectable` 都在生成时配置。

**仍由场景提供**

- `BoxRoot`、内胆、外壳、6 个 `FaceRoot` 与各面 `Puzzle` 容器。
- 24 段边界墙（自动生成，带稳定 `boundary.<face>.<side>` ID；不进入 JSON）。
- 摄像机、方向光、`DeviceSelector`、`DeviceConfigLoader`。
- 材质和 `Lens` 网格通过 `Assets/Resources/LevelAssets.asset` 引用；加载器不依赖场景里序列化的器件对象。

**加载顺序**

1. 清空各面 `Puzzle` 下的旧内容与 `TopLock`。
2. 生成顶锁与灯，再生成光束、墙、镜、透镜/滤光片、接收器、发射器。
3. 发射器最后激活，`LaserEmitter` 缓存本面的镜/墙/透镜/滤光片/接收器/锁后开始追踪。

> 直接在编辑器打开 `Main.unity` 时各面是空的；按 Play 后由加载器生成关卡。尺寸/材质/墙体不再需要手工放进场景。

---

## 1. 设计决策与待确认

这一节记录实现时采用的口径，以及 `SUMMARY.md` 中仍有歧义的地方。后续如果用户修改设定，优先改这里，再改代码。

### 1.1 玻璃砖已删除

- 玻璃砖“起到的作用有限且不便于判定”，已删除，后续不再考虑。
- `SUMMARY.md` 的初版清单已同步：主要光学器件为滤光片、平面镜、凸面镜、凸透镜、三棱镜；不再列出玻璃砖。
- 引擎层不实现玻璃砖；关卡数据允许任意数量、任意类型的器件组合，不强制“六面六种”。
- 当前场景中，back 面使用可移动平面镜 + 固定平面镜 + 凹面镜 + 3 段固定墙，left 面使用三个固定发射器 + 自由移动凸透镜（带绿色锁），right 面使用两个白色光源 + 一个青色角光源、透镜、滤光片和锁；front 面用同一块柱面透镜同时完成红蓝混色聚焦和青色光折射，bottom 面是调色台，top 是正方形反射环与总锁。

### 1.2 调色板与颜色拟合

`SUMMARY.md` 给出 16 个颜色名。调色板只保留能独立作为混合结果出现的颜色：

- 基础色 `R/G/B` 各占一个主分量。
- `C/M/Y` 分别是 `G+B`、`R+B`、`R+G`。
- 带 `+` 的组合只保留“主色 + 次色”的 9 种；它们都等于对应两束光按接收器规则等权混合的结果。
- `W` 是 `R+G+B` 等权混合。
- 这样 `R+C = (0.5, 0.25, 0.25)`，正好对应文档中“浅粉红”的例子；并且 16 个颜色互不相同。

已移除 `C+M`、`C+Y`、`M+Y`：按两束光等权混合，它们分别会得到 `B+Y`、`G+M`、`R+C`，无法作为独立结果出现。

### 1.3 接收器混色规则

采用“平均亮度 + 满亮度混色 + 调色板拟合”：

1. 取所有进入接收器的光，计算总亮度 `B_i = R_i + G_i + B_i`。
2. 平均亮度 `B_avg = mean(B_i)`。
3. 每束光归一化到满亮度：`C_i' = C_i / B_i`。
4. 等权混色：`M = mean(C_i')`。
5. 把 `M` 拟合到最近的调色板颜色 `P`；拟合阈值见 6.6。
6. 最终光色：`C_final = P * B_avg`。

这样：

- `R + G + B → (1/3, 1/3, 1/3) = W`，符合文档例子。
- 两束同亮度光混合后总亮度不变。
- 一束暗光和一束亮光混合时，结果亮度是两者的平均值；暗光不会因为亮度低而在色相上被忽略。
- 接收器判定使用拟合后的 `P`，**不看亮度**；亮度只影响渲染。

### 1.4 只有三棱镜色散

- 三棱镜按 R/G/B 三个分量拆开，每个分量独立折射/全反射。
- 默认折射率为 R `1.42`、G `1.52`、B `1.62`；比真实玻璃色散更强，便于在盒子尺度内看清分光，可通过 JSON 的 `iorR` / `iorG` / `iorB` 覆盖。该数值属于可调表现参数，尚未由用户最终确认。
- 其他折射器件（柱面透镜）对所有颜色使用同一个折射率，不产生色散。
- 滤光片不考虑折射和反射。
- 玻璃砖不实现。

### 1.5 柱面透镜，不使用球面透镜

- 初版和终版都只使用柱面透镜：二维截面是凸透镜轮廓，沿面法线挤出。
- 透镜曲率由固定的 `LensType` 决定，玩家不能调节。
- 初版提供 `biconvex_small`、`biconvex_large`、`plano_convex_small`、`plano_convex_large` 四种固定轮廓。

### 1.6 子谜题之间没有光路

- 每个面的光路只在该面平面内传播。
- 光线到达面边界即被不透明内胆吸收，不进入相邻面。
- 六个面通过“灯”和“接收器状态”产生全局联系，而不是通过三维光路。

### 1.7 锁定器件被锁覆盖并阻断光线

- 带有 `unlockRequires` 且尚未解锁的器件，被一个“锁”覆盖。
- 锁是实体障碍：光线射到锁上会被吸收/阻断，不会与下面的器件发生反射/折射。
- 锁覆盖期间，器件不能被选中、移动或旋转，也不参与其原有的光学行为。
- 小型锁和总锁使用同一套需求灯语义：对应接收器**未激活**时灯亮，激活后熄灭。小型锁顶部是一排颜色球冠指示灯，总锁是环形灯；形状不同，但亮灭逻辑与尺寸规则一致（球冠底面半径 = 所属锁半径 / 5，高度为完整球体高度的 1/4）。
- 所有所需颜色亮起后，锁打开；本版采用“打开后保持打开”，即使之后对应灯熄灭，锁也不会重新关闭。
- 这样关卡可以设计成“先点亮某色灯，才能使用被锁住的镜子”；同时锁会挡住原本可能穿过该位置的光路。

### 1.8 通关状态锁存

- 当顶面六种全局颜色（M/Y/G/B/C/R）同时满足时，进入 `Won` 状态。
- 通关后钥匙孔出现，冻结输入或允许继续观察，但不会因为后续移动而取消通关。
- 初版建议通关后冻结器件操作，显示通关 UI。

### 1.9 镜子的双面性

- 平面镜默认双面反射，降低操作挫败感。
- 凸面镜默认只反射凸面一侧，背面吸收。
- JSON 可以用 `twoSided` 覆盖默认值。

### 1.10 顶面灯环是特殊锁

- 顶面中心圆形区域是一个特殊的锁，覆盖钥匙孔；它和普通锁一样是实体障碍，光线射到锁上会被阻断。
- 锁顶部的灯环固定为 `M / Y / G / B / C / R` 六种全局颜色，每个颜色一个灯；对应接收器未激活时灯亮，激活后熄灭。白光等局部颜色不进入总锁，也没有顶面白灯。
- 总锁六盏颜色灯和中心白灯全部熄灭后，特殊锁打开：钥匙孔出现，游戏通关。
- 特殊锁的光学阻挡半径取 `ringLampRadius + lampRadius`；顶面的普通子谜题器件必须放在这个外接圆之外，关卡设计需要避免依赖穿过中心区域的光路。
- 特殊锁打开后保持打开，通关状态锁存。
- 注意：front 面已由红色改为洋红色，`R` 现在由 bottom 面中央的红色接收器提供，并进入顶层总锁；因此当前全局六色为 `M/Y/G/B/C/R`。

### 1.11 盒子结构

- 透明亚克力外壳是边长 `side` 的立方体。
- 不透明缎面磨砂玻璃内胆是边长 `side - 2 * linerInset` 的实心立方体。
- 六个面的器件安装在内胆外表面，位于内胆与外壳之间的薄层里。
- 这样每个面都有一个不透明背景，玩家透过透明外壳看到该面的器件和光路。
- 这是对“不透明内胆 + 透明外壳 + 内部器件”的实现解释；如果用户希望内胆是空心结构，需要改 `BoxView`。

### 1.12 引擎与工程基线

- 引擎：Tuanjie（团结引擎）`2022.3.62t18`，编辑器路径 `C:\Program Files\Tuanjie\Hub\Editor\2022.3.62t18\Editor\Tuanjie.exe`。
- 渲染管线：内置渲染管线（Built-in Render Pipeline）。不安装、不切换到 URP。
- 输入：旧版 Input Manager（`Input.GetKey`、`Input.GetMouseButton`）。
- 程序集：初版使用默认程序集（`Assembly-CSharp` / `Assembly-CSharp-Editor`），不引入 asmdef。
- JSON：使用引擎自带的 `JsonUtility`；当前读取器件类型/颜色/位置/尺寸/可操作性、固定墙、平面镜和顶面总锁，并由加载器生成关卡几何。
- UI：内置 IMGUI（`OnGUI`）+ 动态系统中文字体（Windows 下可用“Microsoft YaHei”）；不使用 uGUI/TextMeshPro。
- 目标平台：Windows x86_64（Windows Standalone）。
- 当前工程状态：`Main.unity` 已包含盒子、六个面、边界墙和加载器；器件、固定墙、镜面、光束和总锁灯在运行时由 JSON 生成。脚本、材质、shader、网格和图标资源均已加入工程。详见 0.4。

### 1.13 黄面（right / Y）关卡：已实现

设计坐标以面内开口为 `[0,1]²`，`(0,0)` 是从外看的左下角；实现时按 `local = (c - 0.5) * 0.86` 映射，贴边器件再按自身半尺寸内缩。

| 设计 | 实现 |
| --- | --- |
| 左侧白色光源 (0,1/2) | `Emitter_Y` 保持在 `(-0.397, 0)`，颜色改为 `W`；可平移、不可旋转。 |
| 顶部白色光源 (1/2,1) | `Emitter_W` 在 `(0, 0.397)`；被 `LaserLock` 覆盖，`unlockRequires = ["G","B"]`；可平移、不可旋转。 |
| 右下角青色光源 (1,0) | `Emitter_GC` 在 `(0.397, -0.397)`，固定且无锁；`aim = (-0.612, -0.011)`，直指 `G+C` 接收器。 |
| 透镜 (1/3,1/3)-(2/3,2/3) | `Lens_Y` 中心 `(0,0)`，旋转 `-45°`，长 `0.405` 局部单位（`0.471×0.86`），孔径 `0.405`，焦距 `0.1`；只旋转不移动。 |
| 墙 (0,0)-(1/3,1/3)、(2/3,2/3)-(1,1) | `Wall_A`、`Wall_B` 中心 `(±0.28667, ±0.28667)`，旋转 `-45°`，长 `0.4059`。 |
| 绿色滤光片 (1/2,1/6) | `Filter_G` 在 `(0, -0.28667)`，初始 `90°`（横跨白色光源的竖直光束），长 `0.215`、厚 `0.02`；只旋转不移动。 |
| 黄色滤光片 (5/6,1/2) | `Filter_Y` 在 `(0.28667, 0)`，初始 `0°`（横跨左侧白色发射器的水平光束），长 `0.215`、厚 `0.02`；只旋转不移动。 |
| G+C 接收器 (1/4,0) | `Receiver_GC` 在 `(-0.215, -0.408)`，颜色 `G+C`，材质 `Device_G+C`；不进入总锁（无顶面灯）。 |
| 黄色接收器 (1,3/4) | `Receiver_Y` 移到 `(0.408, 0.215)`，由 `LaserLock` 覆盖，`unlockRequires = ["G+C"]`；绿色滤光后的白光与青色角光源在 `G+C` 接收器会合后解锁。 |

> 滤光片的初始角度按“宽面横跨对应光束”设置；1.13 早期草稿中的 0°/90° 已对调，否则光束会打在极窄端被阻断。
>
> 光束已按段着色：白光穿过绿色滤光片后，后续线段显示为绿色；滤光逻辑与视觉颜色使用同一份 `LightColor`。

以上参数（焦距、尺寸、初始角度）后续可能按关卡可行性调整。

> 锁的半径为 `0.036`；贴边器件（白光源、黄接收器）的锁会有一部分嵌进边界墙，当前接受这一视觉表现。
>
> `G+C` 接收器不会只靠青色角光源点亮。G+B 解锁后，还需要把顶部白光发射器移到约 `x = 0.05`、透镜旋转约 `-124°`，让经绿色滤光片后的光束与青色光束同时进入该接收器（验证用配置，不是初始状态）。

### 1.14 关卡几何由 JSON 定义

- `level_01.json` 的每个器件条目包含 `type`、`face`、`color`、`position {x, y, z}`、`rotation`、`size`、可操作性、锁条件和透镜参数；`DeviceConfigLoader` 在 `Start` 中生成对象，而不是在场景里查找模板。
- 场景中只保留盒子结构、六个 `FaceRoot`、空的 `Puzzle` 容器和边界墙；修改 JSON 即可改变开局布局、尺寸和关卡内容。
- 运行时生成的器件同样使用 `DeviceSelectable`，`ApplyPlacement` 负责刷新移动轴与旋转基准；发射器使用 `RebaseAim` 重新计算朝向。
- `position.z` 显式给出器件所在的面平面偏移（当前 `0.05`）。
- `walls` / `mirrors` / `goal` 分别提供固定墙体、平面镜和顶面总锁；边界墙仍由场景自动生成。

---

## 2. 系统架构

### 2.1 分层

```text
┌──────────────────────────────────────────────────────────┐
│                        Unity View                        │
│  BoxView / FaceView / ElementView / LaserRenderer / UI   │
└───────────────▲───────────────────────────┬──────────────┘
                │ 更新视图                   │ 鼠标/键盘
                │                           ▼
┌───────────────┴──────────────────────────────────────────┐
│                      Input / Control                     │
│  BoxRotator / ElementSelector / ElementManipulator       │
└───────────────┬──────────────────────────────────────────┘
                │ 修改模型
                ▼
┌──────────────────────────────────────────────────────────┐
│                       PuzzleModel                        │
│  Faces / Elements / Receivers / Lamps / Goal / Won       │
└───────────────┬──────────────────────────────────────────┘
                │ 只读
                ▼
┌──────────────────────────────────────────────────────────┐
│                     OpticsSimulator                      │
│  Ray2 / Hit2 / Intersections / Refraction / RayPath      │
└───────────────┬──────────────────────────────────────────┘
                │ SimulationResult
                ▼
┌──────────────────────────────────────────────────────────┐
│                GameManager / GameFlow                    │
│  应用结果、更新灯、解锁、通关、刷新 UI                   │
└──────────────────────────────────────────────────────────┘
```

### 2.2 核心原则

1. **模型与视图分离**：`PuzzleModel` 是唯一事实来源；Unity 场景对象只是它的表现。
2. **仿真函数尽量纯**：`OpticsSimulator.Simulate(model)` 不修改模型，返回 `SimulationResult`，便于单元测试。
3. **面内二维**：光学全部在 `(u, v)` 平面完成；三维只负责渲染和交互。
4. **数据驱动**：器件类型、位置、锁定条件、目标颜色都来自 JSON。
5. **先正确、再好看**：先完成无材质、彩色线条的初版，再做 PBR、玻璃、Bloom。

### 2.3 数据流

```text
JSON
  │
  ▼
LevelLoader ──► LevelData (DTO) ──► LevelValidator ──► PuzzleModel
                                                          │
                                                          ▼
                                                OpticsSimulator
                                                          │
                                                          ▼
                                                  SimulationResult
                                                          │
                      ┌───────────────────────────────────┼──────────────────────────────┐
                      ▼                                   ▼                              ▼
                LaserRenderer                       Lamp/Receiver 状态              GameManager
                (光路线段)                          (灯亮/灭、解锁)                 (通关/UI)
```

玩家操作时：

```text
键盘/鼠标
  │
  ▼
ElementManipulator / BoxRotator
  │
  ▼
修改 PuzzleModel 中某个元素的 Position / Rotation
  │
  ▼
GameManager 标记 dirty
  │
  ▼
下一帧重新 Simulate，刷新光路、灯、解锁、通关
```

---

## 3. 工程结构

### 3.1 目录

> 本节是目标架构，包含部分尚未创建的文件。当前实际文件列表见 `README.md` 的“项目结构”。

```text
Assets/
├─ Scenes/
│  └─ Main.unity
├─ Scripts/
│  ├─ Core/
│  │  ├─ GameManager.cs
│  │  ├─ GameFlow.cs
│  │  └─ Constants.cs
│  ├─ Data/
│  │  ├─ LevelData.cs          // JSON DTO
│  │  ├─ LevelLoader.cs
│  │  ├─ LevelValidator.cs
│  │  └─ Palette.cs
│  ├─ Model/
│  │  ├─ PuzzleModel.cs
│  │  ├─ FaceModel.cs
│  │  ├─ FaceDefinition.cs
│  │  ├─ ElementModel.cs
│  │  ├─ EmitterModel.cs
│  │  ├─ ReceiverModel.cs
│  │  ├─ MirrorModel.cs
│  │  ├─ LensModel.cs
│  │  ├─ PrismModel.cs
│  │  ├─ FilterModel.cs
│  │  └─ LampModel.cs
│  ├─ Optics/
│  │  ├─ LightColor.cs
│  │  ├─ Ray2.cs
│  │  ├─ Hit2.cs
│  │  ├─ Intersections.cs
│  │  ├─ OpticsSimulator.cs
│  │  └─ SimulationResult.cs
│  ├─ View/
│  │  ├─ BoxView.cs
│  │  ├─ FaceView.cs
│  │  ├─ ElementView.cs
│  │  ├─ ElementViewFactory.cs
│  │  ├─ LaserRenderer.cs
│  │  ├─ LampView.cs
│  │  └─ SelectionHighlighter.cs
│  ├─ Input/
│  │  ├─ BoxRotator.cs
│  │  ├─ ElementSelector.cs
│  │  └─ ElementManipulator.cs
│  └─ UI/
│     ├─ HudController.cs
│     ├─ ColorStatusView.cs
│     └─ WinView.cs
├─ StreamingAssets/
│  └─ Levels/
│     └─ level_01.json
├─ Materials/
├─ Prefabs/
└─ Tests/
   ├─ EditMode/
   │  ├─ PaletteTests.cs
   │  ├─ ColorMixingTests.cs
   │  ├─ OpticsTests.cs
   │  └─ LevelLoaderTests.cs
   └─ PlayMode/
      └─ GameFlowTests.cs
```

### 3.2 程序集

- 初版不引入 asmdef，全部运行时脚本放在 `Assets/Scripts/`，使用默认程序集 `Assembly-CSharp`。
- 编辑器工具（关卡校验菜单等）放在 `Assets/Editor/`，使用 `Assembly-CSharp-Editor`。
- 即使在同一程序集内，也保持“光学核心不依赖场景对象”的写法，便于后续测试。
- 如果代码量变大、需要加快编译，再单独评估引入 asmdef；这不属于初版任务。

### 3.3 依赖

当前工程的 `Packages/manifest.json` 只包含内置模块（`com.unity.modules.*`），没有额外三方包。

- 不新增 URP。
- 关卡 JSON 使用 `JsonUtility`（`DeviceConfigLoader`）。
- UI 使用内置 IMGUI（`com.unity.modules.imgui`），不安装 `com.unity.ugui`。
- 测试框架（`com.unity.test-framework`）在需要写测试时再作为单独任务添加。
- 图标使用 `Assets/Resources/Icons/` 下的 PNG，通过 `Resources.Load<Texture2D>` 读取。

---

## 4. 坐标系统与盒子模型

### 4.1 盒子尺寸

当前场景的局部尺寸（`BoxRoot` 缩放为 `5`）：

| 参数 | 值 | 说明 |
| --- | --- | --- |
| `side` | 1.1 | 外壳局部边长；世界边长 5.5 |
| `linerInset` | 0.1 | 外壳到内胆的间距 |
| `linerSide` | 0.9 | 内胆局部边长；世界边长 4.5 |
| `faceSize` | 0.86 | 边界墙内开口边长 |
| `devicePlaneZ` | 0.05 | 器件中心到内胆表面的偏移（墙高中点） |
| `laserZ` | 0.05 | 激光线段所在高度 |

- 外壳中心在原点。
- 内胆中心在原点。
- 边界墙从内胆表面延伸到外壳内表面，高度 `0.1`。
- 器件安装在边界墙内侧，厚度不超过 `0.1`。

### 4.2 六个面的基向量

`FaceDefinition` 静态表。`origin` 是内胆表面对应面的中心，`u`、`v`、`normal` 是盒子局部空间中的方向。

| Face | normal | u（从外看向右） | v（从外看向上） | origin（内胆表面） |
| --- | --- | --- | --- | --- |
| `front` | `(0,0,1)` | `(1,0,0)` | `(0,1,0)` | `(0,0,linerSide/2)` |
| `back` | `(0,0,-1)` | `(-1,0,0)` | `(0,1,0)` | `(0,0,-linerSide/2)` |
| `right` | `(1,0,0)` | `(0,0,-1)` | `(0,1,0)` | `(linerSide/2,0,0)` |
| `left` | `(-1,0,0)` | `(0,0,1)` | `(0,1,0)` | `(-linerSide/2,0,0)` |
| `top` | `(0,1,0)` | `(1,0,0)` | `(0,0,-1)` | `(0,linerSide/2,0)` |
| `bottom` | `(0,-1,0)` | `(1,0,0)` | `(0,0,1)` | `(0,-linerSide/2,0)` |

验证：每一行都满足 `u × v = normal`。

### 4.3 面根节点

每个面创建一个空物体 `FaceRoot`：

```csharp
faceRoot.position = faceOrigin;                  // 内胆表面
faceRoot.rotation = Quaternion.LookRotation(normal, v);
```

`LookRotation(normal, v)` 会让：

- 本地 `+Z` = `normal`
- 本地 `+Y` = `v`
- 本地 `+X` = `u`

因此：

- 面内坐标 `(u, v)` 直接对应 `FaceRoot` 的本地 `(x, y)`。
- 器件放在本地 `z = puzzlePlaneZ`。
- 激光放在本地 `z = laserZ`。
- 器件绕本地 `+Z` 旋转，就是绕面法线旋转。

### 4.4 面内边界

子谜题可用范围默认：

```text
u ∈ [-faceSize/2, +faceSize/2]
v ∈ [-faceSize/2, +faceSize/2]
```

- 器件中心、平移范围、初始位置都必须在这个范围内（再减去器件外接半径）。
- 光线到达边界即终止，表示被不透明内胆吸收。
- 不做跨面传送。

### 4.5 顶面特殊锁（总机关）

顶面中心有一个圆形特殊锁：

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `ringInnerRadius` | 0.12 | 特殊锁盖体半径 / 中心保留区半径，器件不能进入 |
| `ringLampRadius` | 0.16 | 灯环半径；灯集合固定为六种全局颜色（M/Y/G/B/C/R），顺序取自 `goal.lamps` |
| `keyholeRadius` | 0.08 | 钥匙孔半径 |

> 当前实际：总锁有灰色圆柱底座（半径 `0.225`，即内胆边长 `0.9` 的 1/4）和 6 盏球冠指示灯（灯环半径 `0.165`，球冠底面半径 `0.045`，即锁半径的 1/5；高度约 `0.026`，为完整球体高度的 1/4，底面落在锁座顶面）；对应接收器未激活时灯亮、激活后熄灭；锁盖阻挡、钥匙孔和通关判定尚未实现。

- 灯环使用 M/Y/G/B/C/R 六种全局颜色，均匀分布；顺序由 `goal.lamps` 决定（当前 JSON 为 `M/G/B/Y/C/R`），第一个灯在 `v` 正方向（角度 90°），顺时针排列；白色等局部颜色不设顶面灯。
- 特殊锁盖体是实体障碍，参与光学；射线命中锁盖即被阻断。
- 特殊锁的光学阻挡半径取 `ringLampRadius + lampRadius`，即覆盖灰色盖体和灯环的整体；初版 `lampRadius` 取 `0.02`。
- 灯环上的灯是锁的视觉/状态元素，不单独参与光学求交。
- 顶面的普通器件必须放在特殊锁外接圆之外：`ringLampRadius + lampRadius + 器件半径 + margin`。
- 六种全局颜色全部亮起后特殊锁打开，钥匙孔从无到有缩放出现，并触发通关。
- 特殊锁打开后保持打开，通关状态锁存。

### 4.6 盒子旋转

- `BoxRoot` 在原点，包含外壳、内胆、六个 `FaceRoot`。
- 实际摄像机在 `(0, 0, -10)`，看向 `+Z`，FOV 60，背景黑色；`BoxRoot` 初始欧拉角为 `(-90, 0, 0)`（即绕 X 轴 `270°`），使顶面朝向摄像机。`BoxRotator.Start` 会把它吸附到最近的合法朝向。
- 鼠标拖动时旋转 `BoxRoot`，不移动摄像机。
- 旋转使用摄像机轴：

```csharp
boxRoot.Rotate(camera.transform.up, -deltaX * 0.2f, Space.World);
boxRoot.Rotate(camera.transform.right, deltaY * 0.2f, Space.World);
```

- 松开后自动吸附到最近的 24 个正位之一；小键盘 8/2/4/6/1/3 可整体旋转。
- 规划：`R` 键重置盒子旋转；当前尚未实现。

> 不强制限制俯仰角；立方体可以自由翻转。

---

## 5. 数据模型与 JSON Schema

### 5.1 顶层结构

```json
{
  "version": 1,
  "id": "level_id",
  "name": "Level Name",
  "description": "Optional text.",
  "box": { "side": 1.1, "linerInset": 0.1, "faceSize": 0.86 },
  "colors": ["R", "G", "B"],
  "goal": {
    "face": "top",
    "ringInnerRadius": 0.12,
    "ringLampRadius": 0.16,
    "keyholeRadius": 0.08
  },
  "elements": []
}
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `version` | int | 是 | 当前为 `1` |
| `id` | string | 是 | 关卡唯一标识 |
| `name` | string | 是 | 显示名称 |
| `description` | string | 否 | 说明 |
| `box` | object | 否 | 缺省用默认尺寸 |
| `colors` | string[] | 是 | 本关使用的颜色，必须是调色板子集，且不能重复 |
| `goal` | object | 否 | 缺省顶面总机关 |
| `elements` | object[] | 是 | 器件列表，可为空（不建议） |

> 顶面总锁固定使用 M/Y/G/B/C/R 六种全局颜色，与 `colors` 是否包含白色等局部颜色无关；`colors` 里可以出现 `W` 等仅用于本面机关的局部颜色。

### 5.2 通用元素字段

```json
{
  "id": "unique_id",
  "type": "plane_mirror",
  "face": "front",
  "position": { "u": 0.0, "v": 0.0 },
  "rotation": 45.0,
  "adjustable": { "move": true, "rotate": true },
  "limits": {
    "u": [-0.35, 0.35],
    "v": [-0.35, 0.35],
    "rotation": [-60.0, 60.0]
  },
  "unlockRequires": ["R"],
  "size": { "length": 0.20 },
  "color": null,
  "targetColor": null,
  "lensType": null,
  "twoSided": null
}
```

| 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- |
| `id` | string | 无 | 全局唯一 |
| `type` | string | 无 | `emitter` / `receiver` / `plane_mirror` / `convex_mirror` / `concave_mirror` / `cylindrical_lens` / `prism` / `filter` / `lamp` |
| `face` | string | 无 | `front` / `back` / `left` / `right` / `top` / `bottom` |
| `position` | `{u,v}` | 无 | 面内坐标，原点在面中心 |
| `rotation` | float | `0` | 面内角度；正方向为从外看逆时针 |
| `adjustable.move` | bool | `false` | 玩家是否可平移 |
| `adjustable.rotate` | bool | `false` | 玩家是否可旋转 |
| `limits.u` | `[min,max]` | 面边界减器件半径 | 平移范围，绝对坐标 |
| `limits.v` | `[min,max]` | 面边界减器件半径 | 平移范围，绝对坐标 |
| `limits.rotation` | `[min,max]` | `[0,360]` | 旋转范围，绝对角度 |
| `unlockRequires` | string[] | `[]` | 需要点亮的颜色列表；空表示初始解锁 |
| `size` | object | 按类型默认 | 几何尺寸 |
| `color` | string | 无 | 调色板颜色名 |
| `targetColor` | string | 无 | 接收器目标颜色 |
| `lensType` | string | `biconvex_small` | 柱面透镜固定轮廓 |
| `twoSided` | bool | 平面镜 `true`，凸面镜 `false` | 是否双面反射 |

- `unlockRequires` 非空时，加载器会在该器件上生成一个锁。
- 锁的尺寸自动取器件包围盒 + 少量余量，不需要在 JSON 中单独配置。
- 锁覆盖器件并阻断光线；锁打开后，器件才恢复原有光学行为。

> 当前实现（5.7）已经落地，但与本节草案仍有差异：
>
> - 5.7 使用 `position {x, y, z}` 和 Unity 本地 Z 欧拉角 `rotation`；本节使用 `{u, v}` 和“从外看逆时针”。`x/y` 与 `u/v` 是同一面内坐标系（原点在面中心，范围约 ±0.43）。
> - 5.7 中 `unlockRequires` 为空表示没有锁（初始可用），非空才生成 `LaserLock`。
> - 5.7 中锁的尺寸以 `LaserLock` 的 `outerRadius` 为基准，并在运行时按器件包围盒自动放大；`innerRadius` 由 `outerRadius - 0.009` 推导。小型锁顶部球冠指示灯的底面半径取锁半径的 1/5。
> - 5.7 的 `size` 是本地缩放（网格本身为基准），不是本节的 `length/radius` 语义；透镜另有 `focalLength` / `aperture`。
> - 固定墙、平面镜和顶面总锁分别由 `walls` / `mirrors` / `goal` 描述，尚未统一进本节设想的 `elements` 数组。

### 5.3 各类型字段

#### `emitter`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `color` | 是 | 发射颜色，调色板名；总亮度固定为 1 |
| `rotation` | 是 | 出射方向；`0°` = 本地 `+u`，`90°` = 本地 `+v` |
| `size.radius` | 否 | 视觉圆柱半径，默认 `0.025` |
| `size.length` | 否 | 视觉圆柱长度，默认 `2 * radius` |
| `adjustable` / `limits` | 否 | 控制玩家能否移动/旋转发射器 |

- 发射器的出射点：`position + direction * (length/2 + EPS)`。
- 发射器本身不阻挡其他光线（光线可以穿过发射器模型）。
- 亮度不可调。

#### `receiver`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `targetColor` | 是 | 目标颜色，必须是 `colors` 中的颜色；每种颜色全局唯一 |
| `size.x` | 否 | 接收器直径，默认 `0.066`；圆弧半径取其一半 |
| `size.z` | 否 | 凸起深度，默认 `0.066` |

- 接收器是贴合墙面的凸起：挂在墙上的生成半圆柱体，挂在两墙交角上的生成 1/4 圆柱体；不贴墙的自由摆放接收器生成完整圆柱体。
- 网格由 `ReceiverMesh` 在面内 XY 平面生成，沿本地 Z 挤出；平面边贴在边界墙上，弧面朝面内，`+Z` 是可见的接收面。
- 因此接收器不再绕本地 X 轴倾斜 90°；JSON 的 `rotation` 对接收器忽略。
- 挂载类型由 `position` 到面边界（`±0.43`）的距离自动判断：两个轴都靠近边界视为角落，否则视为单面墙。
- 判定圆与模型贴合：圆心取模型平面边所在的墙面/墙角点，半径取 `size.x / 2 + 0.004`（默认 `0.037`）。光束会在可见弧面前约 `0.004` 处终止，不再停在模型外侧。
- 判定半径和圆心由加载器显式传给 `LaserReceiver`，不再从网格包围盒推导。
- 任意方向进入接收区的激光都会被收集。
- 接收器吸收光线，光线在该点终止。
- 接收器只匹配拟合后的调色板颜色，亮度不参与判定。
- 接收器满足后，同色需求灯熄灭；未满足时灯亮。
- 接收器颜色全局唯一：`level.colors` 中每个颜色恰好一个接收器（可含白色等局部颜色）；顶面总锁使用 M/Y/G/B/C/R 六种全局颜色。

#### `plane_mirror`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `size.length` | 否 | 反射线长度，默认 `0.20` |
| `size.thickness` | 否 | 视觉三棱柱厚度，默认 `0.03` |
| `twoSided` | 否 | 默认 `true` |

- 局部标准朝向：反射线沿本地 `v`，法线沿本地 `u`。
- `rotation = 0` 时，镜子竖直，反射水平方向的光。
- 二维光学只用反射线；三维视觉是银色直角三棱柱，斜面为反射面。

#### `convex_mirror`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `size.radius` | 否 | 凸面圆弧半径，默认 `0.20` |
| `size.aperture` | 否 | 弦长/开口高度，默认 `0.18` |
| `size.thickness` | 否 | 视觉厚度，默认 `0.03` |
| `twoSided` | 否 | 默认 `false` |

- 局部标准朝向：凸面顶点在元素原点，凸面朝本地 `+u`，弦沿 `v`。
- 反射面是圆弧；背面吸收（默认）。
- 曲率由关卡给定，玩家不能调节。

#### `concave_mirror`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `radius` | 否 | 凹面圆弧半径，默认 `0.18` |
| `aperture` | 否 | 开口高度，默认 `0.2` |
| `thickness` | 否 | 镜片厚度，默认 `0.02` |
| `size.z` | 否 | 视觉深度，默认 `0.1` |

- 当前 `level_01.json` 使用上述字段；凹面镜的网格按实际半径/开口生成，不使用 X/Y 缩放。
- 局部圆弧圆心为 `(-R, 0)`，顶点在原点，凹面朝本地 `-X`；弦沿本地 `Y`。
- 射线命中凹面时按圆法线反射；命中背面时按墙体吸收。
- 曲率由关卡给定，玩家不能调节；凹面镜作为器件可以只沿允许轴平移。

#### `cylindrical_lens`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `lensType` | 否 | 固定轮廓，默认 `biconvex_small` |
| `size.aperture` | 否 | 覆盖 `lensType` 的开口高度 |
| `size.thickness` | 否 | 覆盖 `lensType` 的中心厚度 |
| `size.radius` | 否 | 覆盖计算出的曲率半径 |
| `ior` | 否 | 折射率，默认 `1.52` |

固定轮廓表：

| LensType | 类型 | 开口 H | 中心厚度 t | 曲率半径 R（计算） | 近似焦距（n=1.52） |
| --- | --- | --- | --- | --- | --- |
| `biconvex_small` | 双凸 | 0.16 | 0.03 | 0.2208 | 0.212 |
| `biconvex_large` | 双凸 | 0.24 | 0.04 | 0.3700 | 0.356 |
| `plano_convex_small` | 平凸 | 0.16 | 0.03 | 0.1217 | 0.234 |
| `plano_convex_large` | 平凸 | 0.24 | 0.04 | 0.2000 | 0.385 |

- 局部标准朝向：光轴沿本地 `u`，轮廓关于 `u` 对称。
- 平凸型的平面在 `-u` 侧，凸面在 `+u` 侧。
- 透镜对所有颜色使用同一个 `ior`，不色散。
- 只在全反射时考虑反射；普通界面忽略反射光。
- 三维视觉是轮廓沿本地 `z` 挤出的半透明白色柱体。

#### `prism`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `size.x` | 否 | 等边三角形底边长度，默认 `0.16` |
| `size.y` | 否 | 三角形高，默认 `0.16 × √3 / 2 ≈ 0.138564` |
| `size.z` | 否 | 三维挤出厚度，默认 `0.1` |
| `iorR` / `iorG` / `iorB` | 否 | 默认 `1.42` / `1.52` / `1.62` |

- 局部标准朝向：三角形以外心/重心为原点；底边沿 X、顶点朝 `+Y`。
- 顶点坐标：
  ```text
  ( -side/2, -height/3 )
  ( +side/2, -height/3 )
  (  0,      +2·height/3 )
  ```
- 外接圆半径 `R = 2·height/3`；`rotation = 0` 时顶点朝 `+Y`。
- 复色光进入棱镜立即拆成 R/G/B 三条分支，每条独立折射/全反射。
- 三维视觉是等边三角柱。

#### `filter`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `color` | 是 | 通带颜色，调色板名 |
| `size.thickness` | 否 | 薄片厚度（沿本地 `u`），默认 `0.01` |
| `size.height` | 否 | 通光高度（沿本地 `v`），默认 `0.18` |

- 局部标准朝向：厚度沿本地 `u`，高度沿本地 `v`。
- 光线从 `+u` / `-u` 方向穿过两个大面时，按通带滤光。
- 光线打到 `v = ±height/2` 的极窄面时，直接阻断。
- 不考虑折射和反射。
- 通带规则：取 `color` 中亮度最大的分量；多个并列最大则全部通过。
  - `R = (1,0,0)` → 只通过 R。
  - `C = (0,0.5,0.5)` → 通过 G+B。
  - `R+C = (0.5,0.25,0.25)` → 只通过 R。
  - `W = (1/3,1/3,1/3)` → 通过 R+G+B（相当于透明）。
- 通过的分量保持原亮度，不做衰减。

#### `lamp`

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `color` | 是 | 灯的颜色，必须在 `colors` 中 |
| `size.radius` | 否 | 视觉半径，默认 `0.02` |

- 灯不参与光学，不阻挡光线。
- 灯由同色接收器控制：接收器未激活时灯亮，激活后熄灭；灯表示“还缺少这个颜色”。
- 顶面灯环的灯由加载器自动生成，不需要在 `elements` 中重复写。
- `elements` 中的灯是额外指示灯，可用于视觉提示或解锁条件展示。
- 普通锁顶部的颜色灯是锁视图的一部分，不是全局灯，不进入 `LampModel` 列表。

### 5.4 接收器唯一性与颜色集合

- `colors` 是本关的“目标颜色集合”。
- 每个颜色必须有且只有一个 `receiver`，其 `targetColor` 等于该颜色。
- 接收器可以放在任意面。
- 灯的颜色必须在 `colors` 中；可以有多个同色灯。
- 器件 `unlockRequires` 中的颜色必须在 `colors` 中。
- 发射器颜色可以是任意调色板颜色，不要求出现在 `colors` 中；但如果它最终要命中某个接收器，接收器目标必须在 `colors` 中。

### 5.5 完整关卡格式示例

> 这是早期设想的完整目标关卡格式示例，不是当前实际的 `level_01.json`。当前实际格式见 5.7（`devices` / `walls` / `mirrors` / `goal`）。

示例路径：`Assets/StreamingAssets/Levels/full_level_example.json`（规划）：

```json
{
  "version": 1,
  "id": "full_level_example",
  "name": "Full Level Example",
  "description": "Early full-level format sketch; see 5.7 for the implemented format.",
  "box": {
    "side": 1.1,
    "linerInset": 0.1,
    "faceSize": 0.86
  },
  "colors": ["R"],
  "goal": {
    "face": "top",
    "ringInnerRadius": 0.12,
    "ringLampRadius": 0.16,
    "keyholeRadius": 0.08
  },
  "elements": [
    {
      "id": "emitter_front",
      "type": "emitter",
      "face": "front",
      "position": { "u": -0.30, "v": 0.00 },
      "rotation": 0.0,
      "color": "R",
      "size": { "radius": 0.025, "length": 0.05 },
      "adjustable": { "move": true, "rotate": true },
      "limits": {
        "u": [-0.35, 0.35],
        "v": [-0.35, 0.35],
        "rotation": [-30.0, 30.0]
      }
    },
    {
      "id": "mirror_front",
      "type": "plane_mirror",
      "face": "front",
      "position": { "u": 0.00, "v": 0.00 },
      "rotation": 0.0,
      "size": { "length": 0.20 },
      "adjustable": { "move": true, "rotate": true },
      "limits": {
        "u": [-0.35, 0.35],
        "v": [-0.35, 0.35],
        "rotation": [-60.0, 60.0]
      }
    },
    {
      "id": "receiver_front_red",
      "type": "receiver",
      "face": "front",
      "position": { "u": 0.00, "v": -0.30 },
      "rotation": 0.0,
      "targetColor": "R",
      "size": { "radius": 0.022 },
      "adjustable": { "move": false, "rotate": false }
    }
  ]
}
```

这个例子：

- 发射器在 `(-0.3, 0)` 向右发射。
- 平面镜在 `(0, 0)`，初始角度 `0°`，光线会被原路反射，打不到接收器。
- 玩家把镜子转到约 `45°`，光线反射到 `(0, -0.3)` 的接收器。
- 接收器满足后，顶面灯环上的洋红灯亮起，通关。

### 5.6 校验规则

`LevelValidator` 至少检查：

1. `version == 1`。
2. `id`、`name` 非空。
3. `colors` 非空、无重复、全部在 `Palette` 中。
4. `goal.face` 是合法面；当前只支持 `top`。
5. 每个 `color` 恰好有一个 `receiver` 的 `targetColor` 等于它。
6. 不存在 `targetColor` 不在 `colors` 中的接收器。
7. 所有 `id` 唯一。
8. `face` 合法。
9. `position` 在面边界内。
10. `limits` 的 `min <= max`，初始位置/角度在 limits 内，limits 在面边界内。
11. 顶面元素不进入特殊锁外接圆（`ringLampRadius + lampRadius + 器件半径`）。
12. `unlockRequires` 的颜色都在 `colors` 中。
13. 类型必填字段存在且合法。
14. `lensType` 合法。
15. 调色板颜色名大小写正确。
16. 至少有一个发射器（警告）；至少有一个接收器（若 `colors` 非空则必须）。
17. 检测明显不可解的依赖环（警告，不阻塞）。

校验失败时：

- `LevelLoader` 抛出 `LevelValidationException`，携带错误列表。
- `GameManager` 捕获并在屏幕上显示错误，不进入 `Playing`。

---

### 5.7 关卡几何配置（level_01.json，已实现）

`Assets/StreamingAssets/Levels/level_01.json` 现在描述整关的可变内容。`DeviceConfigLoader` 在 `Start` 中清空各面旧内容并生成器件、光束、固定墙、平面镜和顶面总锁；场景只保留盒子结构、六个面、边界墙和 `Puzzle` 容器。

```json
{
  "version": 1,
  "devices": [
    {
      "id": "emitter.W",
      "type": "emitter",
      "face": "right",
      "color": "W",
      "position": { "x": 0, "y": 0.397, "z": 0.05 },
      "rotation": 0,
      "size": { "x": 0.033, "y": 0.033, "z": 0.033 },
      "castShadows": false,
      "movable": true,
      "moveMin": -0.43,
      "moveMax": 0.43,
      "rotatable": false,
      "rotationMin": 0,
      "rotationMax": 0,
      "unlockRequires": ["G", "B"]
    }
  ],
  "walls": [
    {
      "id": "wall.back.central",
      "name": "CentralWall",
      "face": "back",
      "position": { "x": 0, "y": 0, "z": 0.05 },
      "rotation": 0,
      "size": { "x": 0.02, "y": 0.645, "z": 0.1 },
      "castShadows": false
    }
  ],
  "mirrors": [
    {
      "id": "mirror.back",
      "name": "Mirror",
      "face": "back",
      "position": { "x": 0, "y": -0.4285, "z": 0.05 },
      "rotation": 90,
      "size": { "x": 0.02, "y": 0.1075, "z": 0.1 },
      "castShadows": false
    }
  ],
  "goal": {
    "face": "top",
    "position": { "x": 0, "y": 0, "z": 0 },
    "baseRadius": 0.225,
    "baseHeight": 0.02,
    "baseZ": 0.01,
    "ringRadius": 0.165,
    "lamps": ["R", "G", "B", "Y", "C"]
  }
}
```

**`devices`**

- `id`：唯一 ID，同时作为 `DeviceSelectable.deviceId`；当前命名为 `emitter.<颜色>` / `receiver.<颜色>` / `lens.<面>` / `filter.<颜色>` / `mirror.<面>` / `mirror.concave.<面>`。
- `type`：`emitter` / `receiver` / `cylindrical_lens` / `filter` / `mirror` / `prism` / `concave_mirror`。决定网格、碰撞体和行为组件。
- `face`：`front` / `back` / `left` / `right` / `top` / `bottom`；对象生成到对应面的 `Puzzle` 下。
- `color`：发射器/接收器/滤光片使用的调色板颜色 ID。
- `lamp`：可选；接收器绑定的顶面灯 ID。`receiver.GC` 是局部颜色，没有顶灯，因此省略。
- `position`：面内坐标 `(x, y, z)`，相对 `Puzzle` 本地坐标；当前器件平面为 `z = 0.05`。
- `mountPivot`：可选；接收器的贴墙点/墙角点相对 `position` 的偏移 `{x,y}`。省略或为零时按是否靠近面边界自动推导半圆柱/1/4 圆柱；`position` 不靠近任何边界且偏移为零时生成完整圆柱体；用于 L 形自建墙角时显式指定非零偏移。
- `aim`：可选；发射器的初始出射方向 `{x,y}`，指向接收器或关卡指定方向。缺省时按“从面中心向外”的反方向推导。
- `rotation`：初始绕面法线的 Unity Z 欧拉角（度）。
- `size`：本地缩放；省略的轴使用类型默认值。发射器 `0.033` 立方缩放，接收器由 `size.x` 决定直径、`size.z` 决定凸起深度（默认 `0.066/0.066`），滤光片 `0.02/0.215/0.08`，平面镜 `0.02/0.1075/0.1`，三棱镜为实际尺寸 `0.16/0.138564/0.1`（等边三角形），透镜通常为 `1/1.883721/1`。当前 blue 面透镜通光高度改为 `0.32`，Y 缩放 `1.488372`。凹面镜不缩放，网格按实际半径生成；`size.z` 表示视觉深度。
- `castShadows`：是否让渲染器投影并接收阴影。
- `movable` / `moveMin` / `moveMax`：是否允许移动及绝对局部范围。默认沿墙单轴移动；`freeMove = true` 时同时允许面内 X/Y 两轴自由移动（blue 面透镜）。
- `rotatable` / `rotationMin` / `rotationMax`：是否允许旋转及相对初始朝向的范围。
- `unlockRequires`：可选；非空时生成 `LaserLock`，空/省略表示没有锁。
- `focalLength` / `aperture`：柱面透镜使用；`aperture` 同时用于视觉长度（`aperture / 0.215` 作为 Y 缩放）。凹面镜也使用 `aperture`，并额外使用 `radius`（曲率半径）和 `thickness`（镜片厚度）。
- `iorR` / `iorG` / `iorB`：可选；三棱镜 R/G/B 分量各自的折射率，缺省为 `1.42 / 1.52 / 1.62`。为便于观察色散，默认色散强于真实玻璃。
- 发射器会自动生成 `Beam_<器件 ID 后缀>`；材质按 `Device_<颜色>` / `Beam_<颜色>` / `Filter_<颜色>` / `Lens` / `Mirror` 映射。三棱镜使用运行时材质 `Prism_Acrylic`：与外壳/透镜/滤光片共用 `Lox/AcrylicGlass`，但 `_BaseAlpha = 0.06`、`_EdgeAlpha = 0.16`、`_FresnelAlpha = 0.28`，让厚实心三棱柱读起来更接近透明亚克力。黄面现有 `Emitter_Y`、`Emitter_W` 两个白色光源和 `Emitter_GC` 青色光源；光束对象分别是 `Beam_Y`、`Beam_W`、`Beam_GC`。

**`walls` / `mirrors`**

- `id`、可选 `name`、`face`、`position`、`rotation`、`size`、`castShadows`。
- 生成 `Cube` 网格 + `BoxCollider`，分别附加 `LaserWall` / `LaserMirror`；材质为 `SatinGlassWall` / `Mirror`。凹面镜是 `devices` 中的 `concave_mirror`，生成圆弧网格和 `LaserConcaveMirror`。
- 边界墙不在此数组中，仍由场景的 24 段 `BoundaryWall` 提供。

**`goal`**

- 固定顶面总锁：圆柱底座（`baseRadius` / `baseHeight` / `baseZ`）和环形指示灯（`ringRadius`）。当前 `baseRadius = 0.225`（内胆边长 `0.9` 的 1/4）。
- 指示灯是球冠：底面半径 = 锁半径 / 5，高度 = 完整球体高度的 1/4（约为底面半径的 `0.577`），底面落在 `baseZ + baseHeight / 2` 的锁座顶面上，避免穿模。`lamps` 决定灯的颜色与环上顺序；第一个灯位于本地 `+y`（90°），顺时针排列。当前为 `M/G/B/Y/C/R`。
- 接收器控制 `lamp` 指定的同色需求灯：未激活亮、激活灭；白色等局部颜色不设顶灯。

> 材质与透镜网格通过 `Assets/Resources/LevelAssets.asset` 引用（`Device_*`、`Beam_*`、`Filter_*`、`Lamp`、`Lens`、`LockBase`、`Mirror`、`SatinGlassWall`、`SelectionGlow` 和 `Lens`）。
>
> 当前 `level_01.json` 含 39 个器件、5 段固定墙、5 面平面镜（1 面固定 + 4 面可操作）、6 盏环灯和 1 盏中心白灯。修改 JSON 后重新运行即可得到新关卡；尚未实现运行时热重载。

## 6. 颜色模型

> 本节的颜色核心已实现：`LightColor`/`MixResult`（`LightColor.cs`）、`Palette`（16 色）、`ColorMath`（混色、距离、滤光、匹配）。`LaserEmitter` 用发射器颜色 ID 取得初始 `LightColor`，`LaserFilter` 在射线路径上按分量滤光，`LaserPrism` 在进入棱镜时按 R/G/B 拆成独立分支，`LaserReceiver` 在 `LateUpdate` 聚合所有光束后按调色板 ID 判定；`LaserEmitter` 会把每段射线的颜色写到对应的 LineRenderer 材质。



### 6.1 调色板

`Palette` 是一个静态表：`string -> LightColor`。

| ID | 配方 | R | G | B |
| --- | --- | --- | --- | --- |
| `R` | R | 1 | 0 | 0 |
| `G` | G | 0 | 1 | 0 |
| `B` | B | 0 | 0 | 1 |
| `C` | G+B | 0 | 1/2 | 1/2 |
| `M` | R+B | 1/2 | 0 | 1/2 |
| `Y` | R+G | 1/2 | 1/2 | 0 |
| `R+C` | 2R+C | 1/2 | 1/4 | 1/4 |
| `R+M` | 2R+M | 3/4 | 0 | 1/4 |
| `R+Y` | 2R+Y | 3/4 | 1/4 | 0 |
| `G+C` | 2G+C | 0 | 3/4 | 1/4 |
| `G+M` | 2G+M | 1/4 | 1/2 | 1/4 |
| `G+Y` | 2G+Y | 1/4 | 3/4 | 0 |
| `B+C` | 2B+C | 0 | 1/4 | 3/4 |
| `B+M` | 2B+M | 1/4 | 0 | 3/4 |
| `B+Y` | 2B+Y | 1/4 | 1/4 | 1/2 |
| `W` | R+G+B | 1/3 | 1/3 | 1/3 |

- 所有颜色总亮度为 1。
- 16 个颜色互不相同。
- `R+C` 正好是 `(0.5, 0.25, 0.25)`，对应文档中的“浅粉红”。

### 6.2 `LightColor`

```csharp
public readonly struct LightColor
{
    public readonly float R, G, B;

    public float Total => R + G + B;
    public static LightColor Black => new LightColor(0, 0, 0);

    public LightColor(float r, float g, float b) { R = r; G = g; B = b; }

    public static LightColor operator +(LightColor a, LightColor b)
        => new LightColor(a.R + b.R, a.G + b.G, a.B + b.B);

    public static LightColor operator *(LightColor c, float k)
        => new LightColor(c.R * k, c.G * k, c.B * k);

    public bool ApproximatelyEquals(LightColor other, float eps = 1e-3f)
        => Mathf.Abs(R - other.R) <= eps
        && Mathf.Abs(G - other.G) <= eps
        && Mathf.Abs(B - other.B) <= eps;
}
```

> 颜色分量参与仿真时不要做 sRGB 转换；仿真用线性强度。渲染时再交给 Unity 的颜色空间处理。

`MixResult` 保存一次混色的结果：

```csharp
public readonly struct MixResult
{
    public readonly LightColor Color;   // 拟合后的调色板颜色，总亮度 1
    public readonly string ColorId;     // 调色板 ID；白色退化时为 "W"
    public readonly float Brightness;   // 平均亮度
    public readonly LightColor Final;   // Color * Brightness，用于渲染

    public MixResult(LightColor color, string colorId, float brightness)
    {
        Color = color;
        ColorId = colorId;
        Brightness = brightness;
        Final = color * brightness;
    }
}
```

- `Color` 与 `ColorId` 用于接收器判定。
- `Final` 用于渲染激光；亮度低时线会变暗。
- 空光束或全黑返回 `ColorId = "W"`、`Brightness = 0`、`Final = Black`；接收器判定会额外要求亮度大于阈值。

### 6.3 混合

以下方法放在静态类 `ColorMath` 中；`MixResult` 可以放在同一文件。

```csharp
public static MixResult Mix(IReadOnlyList<LightColor> beams)
{
    if (beams == null || beams.Count == 0)
        return new MixResult(Palette.W, "W", 0f);

    float brightnessSum = 0f;
    LightColor normalizedSum = LightColor.Black;
    int count = 0;

    foreach (var beam in beams)
    {
        float total = beam.Total;
        if (total <= 1e-6f) continue;

        brightnessSum += total;
        normalizedSum += beam * (1f / total);
        count++;
    }

    if (count == 0)
        return new MixResult(Palette.W, "W", 0f);

    float averageBrightness = brightnessSum / count;
    LightColor mixedAtFull = normalizedSum * (1f / count);

    var (paletteId, paletteColor) = Palette.NearestOrWhite(mixedAtFull);
    return new MixResult(paletteColor, paletteId, averageBrightness);
}
```

`Palette.NearestOrWhite`：

```csharp
public static (string id, LightColor color) NearestOrWhite(LightColor color)
{
    string bestId = null;
    LightColor bestColor = default;
    float bestDistance = float.MaxValue;

    foreach (var pair in All)
    {
        float d = ColorMath.Distance(color, pair.Value);
        if (d < bestDistance)
        {
            bestDistance = d;
            bestId = pair.Key;
            bestColor = pair.Value;
        }
    }

    if (bestId != null && bestDistance <= ColorMath.SnapEpsilon)
        return (bestId, bestColor);

    return ("W", W);
}
```

`ColorMath.Distance`：

```csharp
public static float Distance(LightColor a, LightColor b)
{
    float dr = a.R - b.R;
    float dg = a.G - b.G;
    float db = a.B - b.B;
    return Mathf.Sqrt(dr * dr + dg * dg + db * db);
}
```

示例：

| 输入 | 平均亮度 | 满亮度混合 | 拟合 | 最终颜色 |
| --- | --- | --- | --- | --- |
| R + G + B（都满亮度） | 1 | (1/3,1/3,1/3) | W | W |
| R + C（都满亮度） | 1 | (0.5,0.25,0.25) | R+C | R+C |
| R + G（都满亮度） | 1 | (0.5,0.5,0) | Y | Y |
| 2 × R（都满亮度） | 1 | (1,0,0) | R | R |
| 亮 R + 暗 C（C 亮度 0.5） | 0.75 | (0.5,0.25,0.25) | R+C | 0.75 × R+C |
| R + G + C | 1 | (1/3,1/2,1/6) | 无（距离 > 0.05） | W |
| 白光过红滤光片 | 1/3 | (1,0,0) | R | (1/3,0,0) |

> 混合结果先拟合到调色板，再乘平均亮度。接收器只看拟合出的调色板 ID；因此最后一行的暗红光仍然算 R。

- 亮度为 0 的分支不参与平均；若全部为 0，返回 `W` + 亮度 0（渲染为黑，接收器不亮）。

### 6.4 色散

```csharp
public static IEnumerable<LightColor> Disperse(LightColor c)
{
    if (c.R > 1e-6f) yield return new LightColor(c.R, 0, 0);
    if (c.G > 1e-6f) yield return new LightColor(0, c.G, 0);
    if (c.B > 1e-6f) yield return new LightColor(0, 0, c.B);
}
```

- 只有三棱镜调用；当前实现由 `LaserPrism.ComponentColor` 逐分量生成，效果与上面的 `Disperse` 一致。
- 分解后每个分量的亮度等于原颜色的对应分量。
- 例如 `(0.5, 0.25, 0.25)` → R 0.5、G 0.25、B 0.25。
- 每个分量独立传播，可以走不同路径。

### 6.5 滤光

```csharp
public static LightColor ApplyFilter(LightColor beam, LightColor filter)
{
    float max = Mathf.Max(filter.R, Mathf.Max(filter.G, filter.B));
    if (max <= 1e-6f) return LightColor.Black;

    return new LightColor(
        filter.R >= max - 1e-6f ? beam.R : 0f,
        filter.G >= max - 1e-6f ? beam.G : 0f,
        filter.B >= max - 1e-6f ? beam.B : 0f);
}
```

- 取最大分量作为通带；并列最大时全部通过。
- 通过的分量保持原亮度。
- 例：白光 `(1/3,1/3,1/3)` 通过红色滤光片 `(1,0,0)` → `(1/3,0,0)`。
- 例：绿光 `(0,1,0)` 通过红色滤光片 → 黑，被吸收。
- 例：白光通过青色滤光片 `(0,0.5,0.5)` → `(0,1/3,1/3)`。
- 滤光后的光亮度可能小于 1；接收器不看亮度，所以暗色仍然可以匹配对应颜色。

### 6.6 调色板拟合与白色退化

```csharp
public const float SnapEpsilon = 0.05f;
public const float MatchEpsilon = 1e-3f;
public const float MinBrightness = 1e-4f;
```

- `SnapEpsilon`：满亮度混合颜色与最近调色板颜色的最大 RGB 欧氏距离。
- 当前 16 色调色板的最小两两距离是 `0.204124`（`R+C` 与 `W`），因此 `SnapEpsilon` 必须小于 `0.102062`。取 `0.05` 既安全，又能吸收浮点误差和轻微数值偏差。
- 如果最近距离 `<= SnapEpsilon`，拟合到该调色板颜色。
- 否则退化为 `W`。这样混合结果永远落在调色板内，接收器可以做精确的 ID 匹配。
- `MatchEpsilon` 用于调试/测试中的 RGB 比较；实际判定推荐直接比较调色板 ID。
- `MinBrightness`：接收器判定时要求最终亮度大于该值，避免“空光束 + 白色退化”误点亮白色接收器。

### 6.7 接收器匹配（颜色精确，亮度不敏感）

```csharp
public static bool Matches(MixResult mix, string targetColorId)
    => mix.Brightness > MinBrightness
    && mix.ColorId == targetColorId;
```

- 接收器只比较拟合后的调色板颜色 ID。
- **亮度不参与判定**：暗红 `(1/3,0,0)` 与满亮度红 `(1,0,0)` 都算 `R`。
- 这样滤光片、棱镜分光造成的亮度衰减不会让接收器永远无法满足。
- 亮度只用于渲染：`MixResult.Final` 是 `Color * Brightness`。
- `ReceiverModel` 同时保存 `TargetColorId` 和 `TargetColor`；JSON 的 `targetColor` 反序列化为 ID，再从 `Palette` 取 RGB。
- 如果需要调试，可以用 `mix.Color.ApproximatelyEquals(targetColor, MatchEpsilon)` 作为二次校验；调色板颜色都归一化到总亮度 1，二者应一致。
- 如果后续确实需要“亮度敏感”的接收器，给接收器增加 `targetBrightness` 字段，并在 `Matches` 中比较 `mix.Brightness`；当前版本不做。

---

## 7. 光学仿真

> 当前实际使用的是 `LaserEmitter` 内部的简化二维射线：支持接收器、平面镜、凹面镜、墙体、薄透镜、`LaserFilter` 和 `LaserPrism`；光束携带 `LightColor` 分量，接收器在 `LateUpdate` 中按调色板 ID 判定，光束按段使用滤光后的颜色渲染。棱镜在进入时把复合光拆成 R/G/B 分支，每个分量独立折射/全反射。



### 7.1 总体流程

```csharp
public static SimulationResult Simulate(PuzzleModel model)
{
    var result = new SimulationResult();

    foreach (var face in model.Faces)
    {
        foreach (var emitter in face.Emitters)
        {
            if (!emitter.IsUnlocked) continue; // 被锁覆盖的发射器不发光

            var ray = new Ray2(
                origin: emitter.BeamOrigin,
                direction: emitter.Direction,
                color: emitter.Color,
                insideElementId: null);

            Trace(face, ray, result, depth: 0);
        }
    }

    foreach (var receiver in model.Receivers)
    {
        var mix = ColorMath.Mix(result.GetHits(receiver.Id));
        bool satisfied = ColorMath.Matches(mix, receiver.TargetColorId);
        result.ReceiverSatisfied[receiver.Id] = satisfied;
        result.ReceiverMix[receiver.Id] = mix;
    }

    foreach (var color in model.LevelColors)
    {
        var receiver = model.ReceiversByColor[color];
        result.ColorSatisfied[color] = result.ReceiverSatisfied[receiver.Id];
    }

    return result;
}
```

### 7.2 射线与命中

```csharp
public struct Ray2
{
    public Vector2 Origin;
    public Vector2 Direction;      // 单位向量
    public LightColor Color;
    public string InsideElementId; // null 表示在空气中
}

public struct Hit2
{
    public float Distance;
    public Vector2 Point;
    public Vector2 Normal;         // 指向射线来的方向，或由具体表面解释
    public ElementModel Element;
    public SurfaceKind Surface;    // Front / Back / Narrow / Entry / Exit
}
```

- `Ray2.Direction` 必须归一化。
- `Hit2.Distance` 是沿射线的正距离。
- 所有求交在元素本地坐标中完成，再把点/法线变换回面坐标。

### 7.3 最近命中

```text
FindNearest(face, ray):
  如果 ray.InsideElementId != null:
      只与该元素求交
  否则:
      遍历该面所有元素
      如果元素未解锁:
          用锁的矩形阻挡体求交
          命中后直接视为“被锁阻断”
      否则:
          忽略 emitter、lamp（它们不挡光）
          包括 receiver、plane_mirror、convex_mirror、cylindrical_lens、prism、filter
      取 Distance > EPS 的最小命中
  计算射线离开面边界的距离 boundaryT
  如果最近命中不存在，或 hit.Distance > boundaryT:
      返回 null（光线在边界处终止）
  否则返回 hit
```

- 锁的二维截面是覆盖器件的圆形，圆心跟随器件当前 `localPosition`。
- 射线命中锁即终止，不发生反射/折射，也不与锁下面的器件交互。
- 锁定的发射器不发射激光；但它的锁仍会阻挡其他光线。
- 顶面特殊锁同样作为阻挡体加入顶面求交；它覆盖中心圆形区域。
- `EPS = 1e-4`。
- 每次反射/折射后，新射线起点沿新方向偏移 `EPS`，避免自交。
- 最大反弹次数 `MaxBounces = 64`；超过则终止该分支并可选地打一条警告日志。
- 面边界是正方形 `[-faceSize/2, faceSize/2]^2`。

### 7.4 反射

```csharp
public static Vector2 Reflect(Vector2 d, Vector2 n)
{
    if (Vector2.Dot(d, n) > 0) n = -n; // n 朝向入射方向
    return (d - 2f * Vector2.Dot(d, n) * n).normalized;
}
```

- 平面镜：`n` 是镜面法线。
- 凸面镜：`n` 是圆弧在命中点的外法线。
- 镜子是“无损耗”的，颜色不变。

### 7.5 折射与全反射

```csharp
public static bool TryRefract(
    Vector2 d, Vector2 n, float n1, float n2, out Vector2 t)
{
    if (Vector2.Dot(d, n) > 0) n = -n; // n 朝向入射方向
    float eta = n1 / n2;
    float cosI = -Vector2.Dot(d, n);
    float sin2T = eta * eta * (1f - cosI * cosI);

    if (sin2T > 1f)
    {
        t = default;
        return false; // 全反射
    }

    t = (eta * d + (eta * cosI - Mathf.Sqrt(1f - sin2T)) * n).normalized;
    return true;
}
```

- 进入玻璃：`n1 = 1.0`，`n2 = ior`。
- 离开玻璃：`n1 = ior`，`n2 = 1.0`。
- 全反射时调用 `Reflect`，并保持在元素内部。
- 普通折射时忽略反射光（文档要求“不发生全反射则忽略透镜的反射”）。
- 三棱镜的每个分量使用自己的 `ior`；柱面透镜所有颜色共用 `ior`。

### 7.6 各器件行为

#### 发射器

- 不参与求交。
- 锁定的发射器不发射激光。
- 光线起点：`position + direction * (visualLength / 2 + EPS)`。
- 颜色：调色板颜色，总亮度 1。

#### 接收器

- 圆与射线求交。
- 命中即吸收：记录 `(receiverId, ray.Color)`，该分支结束。
- 不要求入射方向；从任何方向进入圆都算。
- 多个分支命中同一接收器时全部收集。
- 收集完成后按 6.3 混色、按 6.6 拟合、按 6.7 判定。
- 判定只看拟合后的调色板 ID；亮度不参与。
- 空光束（亮度 0）不会点亮任何接收器。

#### 平面镜

- 线段与射线求交。
- 线段：本地 `u = 0`，`v ∈ [-length/2, +length/2]`。
- 法线：本地 `±u`。
- `twoSided = true`：两面都反射。
- `twoSided = false`：只有 `dot(d, +u) < 0` 时反射，否则吸收。

#### 凸面镜

- 圆弧与射线求交。
- 圆弧圆心在本地 `(-R, 0)`，半径 `R`，顶点在 `(0,0)`。
- 弦长 `H`，半弦高 `h = H/2`。
- 矢高 `s = R - sqrt(R² - h²)`。
- 圆弧角度范围：从 `-β` 到 `+β`，其中 `sin β = h / R`。
- 外法线：`(P - C) / R`，其中 `C = (-R, 0)`。
- 默认只反射凸面一侧；背面吸收。
- `twoSided = true` 时背面也反射。

#### 凹面镜

- 圆弧圆心 `(-R, 0)`，半径 `R`，顶点在 `(0,0)`，凹面朝本地 `-X`。
- 开口 `H`，半弦高 `h = H/2`，圆弧角度范围 `θ ∈ [-β, +β]`，`sin β = h/R`。
- 射线与圆求交后取靠近顶点的交点；命中 `|y| ≤ h` 才有效。
- 凹面法线取 `(C - P)/R`（指向圆心/左侧）；`dot(direction, normal) < 0` 时反射，否则按背面吸收。
- 当前实现：`LaserConcaveMirror` + `ConcaveMirrorMesh`，参数 `radius=0.18`、`aperture=0.2`、`thickness=0.02`、`depth=0.1`。

#### 柱面透镜

- 轮廓由左右两条圆弧（双凸）或一条平边 + 一条圆弧（平凸）组成。
- 双凸：
  - 左弧圆心 `(R - t/2, 0)`，半径 `R`。
  - 右弧圆心 `-(R - t/2, 0)`，半径 `R`。
  - 内部判定：`uL(v) <= u <= uR(v)`，其中
    ```text
    uL(v) = (R - t/2) - sqrt(R² - v²)
    uR(v) = -(R - t/2) + sqrt(R² - v²)
    ```
- 平凸：
  - 平面在 `u = -t/2`。
  - 凸弧圆心 `(t/2 - R, 0)`，半径 `R`。
  - 内部判定：`-t/2 <= u <= uR(v)`，其中
    ```text
    uR(v) = (t/2 - R) + sqrt(R² - v²)
    ```
- 求交：分别与各表面求交，取最近的正距离。
- 根据 `InsideElementId` 判断进入/离开，选择 `n1/n2`。
- 全反射时反射并保持 inside；否则折射并离开 inside。
- 透镜轮廓两端是尖的，射线正好打到端点时用 EPS 容差处理。

#### 三棱镜

- 等边三角形的直棱柱，沿面法线挤出；光学计算只看二维截面。
- 本地截面：底边沿 X、顶点朝 `+Y`，以外心/重心为原点：底边 `y = -h/3`、顶点 `y = +2h/3`；默认 `baseWidth = 0.16`、`height = 0.16 × √3 / 2`、`depth = 0.1`，外接圆半径 `R = 2h/3`。
- 与三条边求交，取最近的正距离；外法线指向实体外，由边的方向 `(edge.y, -edge.x)` 得到。
- 进入时：
  1. 把入射 `LightColor` 按 R/G/B 拆成最多三个分量，保留各自强度。
  2. 每个分量用各自的 `ior`（缺省 `1.42 / 1.52 / 1.62`）折射。
  3. 每个分量成为独立分支，并记录 `insidePrism`。
- 内部时：
  - 只与当前棱镜的三条边求交。
  - 每个分量用各自 `ior` 尝试离开；超过临界角时全反射并保持 `insidePrism`，否则折射出去并清除。
- 分量分支相互独立，允许不同颜色走不同路径、分别进入不同接收器。
- 单色光进入时只有一个分量分支，不发生额外拆分。
- 三棱镜使用运行时创建、克隆自 `Lens` 的 `Prism_Acrylic` 材质，与外壳/透镜/滤光片共用 `Lox/AcrylicGlass` Shader；当前位置/角度/尺寸由 JSON 决定，可像其他器件一样移动或旋转。

#### 滤光片

- 本地矩形：`u ∈ [-t/2, +t/2]`，`v ∈ [-h/2, +h/2]`。
- 用 slab 法求进入/离开参数 `tMin/tMax`。
- 判断 `tMin` 对应的是 `u` 面还是 `v` 面：
  - 如果是 `v` 面（上下极窄面），直接吸收，分支结束。
  - 如果是 `u` 面（大面），继续判断 `tMax`：
    - 如果 `tMax` 也是 `u` 面，穿过，颜色应用 `ApplyFilter`，从离开点继续。
    - 如果 `tMax` 是 `v` 面（擦边），吸收。
- 滤光不改变方向。
- 如果滤光后为黑，分支结束。

#### 锁

- 任何 `unlockRequires` 非空且尚未解锁的器件，其光学形状被锁替代。
- 锁的二维截面是覆盖器件的圆形，圆心取器件 `localPosition`；半径取“网格所有顶点经本地缩放后到器件原点的最大平面距离 + `0.004`”，再加 `0.005` 作为阻挡容差。按顶点计算可以覆盖非居中的异形网格（例如三棱镜顶点、接收器贴墙 pivot）。
- 光线命中锁圆即被吸收，分支结束；不发生反射/折射。
- 锁不参与其下方器件的任何光学行为。
- 小型锁顶部有一排颜色球冠指示灯，颜色来自 `unlockRequires`；球冠底面半径 = 锁半径的 1/5，高度为完整球体高度的 1/4，底面贴在锁盖顶面。与总锁一致，对应接收器未激活时亮、激活后熄灭。
- 所有所需颜色亮起后锁打开；本版锁打开后保持打开，器件恢复光学行为。

#### 灯

- 不参与求交。
- 状态由接收器决定。

#### 顶面特殊锁

- 顶面中心圆形区域是一个特殊的锁，覆盖钥匙孔。
- 它按圆形阻挡体参与顶面求交，半径取 `ringLampRadius + lampRadius`；射线命中即阻断。
- 灯环固定按 M/Y/G/B/C/R 排列，每个颜色一个需求灯；未满足时亮、满足后熄灭，六种颜色都满足后特殊锁打开。
- 特殊锁打开后，钥匙孔出现并触发通关；打开状态和通关状态都锁存。
- 顶面的普通器件必须放在特殊锁的外半径之外。

### 7.7 仿真结果

```csharp
public sealed class SimulationResult
{
    public readonly List<RaySegment> Segments = new();
    public readonly Dictionary<string, List<LightColor>> ReceiverHits = new();
    public readonly Dictionary<string, MixResult> ReceiverMix = new();
    public readonly Dictionary<string, bool> ReceiverSatisfied = new();
    public readonly Dictionary<string, bool> ColorSatisfied = new();

    public IReadOnlyList<LightColor> GetHits(string receiverId)
        => ReceiverHits.TryGetValue(receiverId, out var list)
            ? list
            : Array.Empty<LightColor>();
}
```

- `ReceiverMix` 保存每个接收器的拟合颜色 ID、平均亮度和最终渲染颜色。

`RaySegment`：

```csharp
public struct RaySegment
{
    public FaceId Face;
    public Vector2 Start;
    public Vector2 End;
    public LightColor Color;
}
```

- `Segments` 用于渲染激光。
- 如果同一段被多个分支重复记录，渲染时可以用 `(face, start, end, color)` 去重；初版不去重也不影响观感。

### 7.8 数值与性能

- 盒子尺度约 1，`EPS = 1e-4` 足够。
- `MaxBounces = 64`。
- 每个面最多几个发射器，每个发射器最多分裂成 3 条，计算量很小。
- 不需要空间加速结构；直接遍历元素即可。
- 仿真在玩家操作后按帧合并执行，不要每次按键事件都重复计算。

---

## 8. Unity 实现

### 8.1 场景层级

```text
Main.unity
├─ GameManager
├─ Main Camera
├─ Directional Light（终版）
├─ EventSystem
├─ Canvas
│  ├─ HudPanel
│  ├─ SelectedPanel
│  ├─ ColorStatusPanel
│  └─ WinPanel
└─ BoxRoot
   ├─ Shell
   ├─ Liner
   └─ Faces
      ├─ Front
      ├─ Back
      ├─ Left
      ├─ Right
      ├─ Top
      └─ Bottom
```

- `Shell`：边长 `side` 的 Cube，透明亚克力材质，无碰撞体或放在 `Ignore Raycast`；运行时由 `AcrylicShellThickness` 生成 `InnerShell` 子物体（缩放到 `0.95`）来表现壁厚，由 `AcrylicShellAluminumEdging` 生成 `AluminumEdging` 子物体包住所有棱。
- `Liner`：边长 `linerSide` 的 Cube，缎面磨砂玻璃材质，放在 `Occluder` 层，有 Collider。
- `Faces/<Face>`：空物体，位置/旋转按 `FaceDefinition` 设置。
- 器件是 `FaceRoot` 下的 prefab 实例。

### 8.2 材质

当前初版材质：

| 对象 | 材质 | 说明 |
| --- | --- | --- |
| 外壳 | `Lox/AcrylicGlass` + 内层壳 | 无色透明亚克力：双层壳体现厚度；底色几乎全透明（`_BaseAlpha 0.01`），加菲涅耳、边缘高光、厚度带和环境反射；不投影 |
| 外壳棱边 | `Lox/FrostedAluminum` | 磨砂铝合金包边：每面宽 `0.0909`（外壳局部单位，约等于边界墙高 `0.1`），厚 `0.012`；一圈连续包住 12 条棱和 8 个角 |
| 内胆 | `Lox/SatinFrostedGlass` | `#bbbbbb` 缎面磨砂玻璃：宽而软的高光、漫反射包裹、较大颗粒磨砂噪声；亮度校准到原 Standard 观感 |
| 边界墙/中央墙 | `Lox/SatinFrostedGlass` | `#777777`（`SatinGlassWall.mat`），与内胆同一套缎面磨砂玻璃参数 |
| 器件 | Standard | `Device_M/R/G/B/Y/C.mat`；新增 `Device_G+C.mat`、`Device_B+Y.mat`、`Device_R+C.mat` |
| 激光核心 | `Unlit/Color` | `Beam_M/M/G/B/Y/C.mat`，线宽 `0.03` |
| 激光光晕 | `Lox/BeamGlow` | 每条线段附加 3× 宽的软边加法线，`_Color.a 0.28` |
| 灯（灭） | `Unlit/Color` | 深灰 |
| 灯（亮） | `Unlit/Color` + MaterialPropertyBlock | 灯色 |
| 灯光晕（待调优） | `Lox/LampGlow` | 贴壳外扩 1.5× 球冠，正常深度测试；当前效果过弱，暂缓继续调优 |
| 平面镜 | Standard | 银色金属（`Mirror.mat`） |
| 凸透镜 | `Lox/AcrylicGlass` | 与外壳同一亚克力着色器；`Lens.mat` 浅青透明，`_BaseAlpha 0.12`、菲涅耳和环境反射 |
| 滤光片 | `Lox/AcrylicGlass` | 与外壳同一亚克力着色器；`Filter_C/M/G/Y.mat` 保留各自颜色，`_BaseAlpha 0.2`，带棱边/厚度高光 |
| 选中描边 | `Lox/SelectionGlow` | `#ffff77` 二维轮廓：stencil 遮罩 + 屏幕空间扩张，宽 `3px`（按 1080p 缩放） |

亚克力外壳（`Assets/Shaders/AcrylicGlass.shader`）：

- 主体 Pass 使用透明混合、`ZWrite Off`、`Cull Off`，底色 alpha 仅 `0.01`，叠加菲涅耳和环境反射；从正面看几乎无色透明。
- 高光 Pass 使用加色混合、`Cull Back`，绘制棱边高光与方向光高光；棱边按立方体网格的局部坐标计算，只出现在正面可见的棱边上。
- 外壳的 `MeshRenderer` 关闭投影（`CastShadows = 0`），避免透明外壳给内部投下不透明阴影。
- 厚度：`AcrylicShellThickness` 在运行时在外壳内生成 `InnerShell`，缩放 `0.95`，形成约 `0.1375` 世界单位的壁厚；两层壳的棱边产生可见的双层边缘和视差。
- 外壳主体额外绘制 `_ThicknessWidth 0.03` 的厚度带，用低 alpha 的白色填充两层棱边之间，避免厚度区域呈现为黑色缝隙。
- 参数：`_EdgeCoreWidth 0.004`、`_EdgeHaloWidth 0.014`、`_EdgeAlpha 0.1`、`_EdgeGlow 0.38`、`_ThicknessWidth 0.03`、`_ThicknessAlpha 0.04`、`_ThicknessGlow 0.09`、`_FresnelPower 4`、`_ReflectionStrength 0.3`。棱边高光比上一版略微减淡。
- 磨砂铝合金包边由 `AcrylicShellAluminumEdging` 在运行时生成 `AluminumEdging` 网格：6 个面的圆角窗框、12 条圆棱条、8 个角球和 8 个内侧角块；总宽度取 `0.0909`（`0.1 / 1.1`），约等于边界墙高度；外表面比外壳高 `0.012`。
- 圆角参数：外棱圆角 `edgeRadius = 0.018`，窗口内角圆角 `innerCornerRadius = 0.03`。铝合金包边不投影。

磨砂铝合金（`Assets/Shaders/FrostedAluminum.shader`）：

- `Cull Off` 双面绘制，避免包边拼接处因绕序出现缺面。
- `_Color (0.42, 0.44, 0.46)`、`_Metallic 0.35`、`_Smoothness 0.35`、`_SpecularStrength 0.6`、`_ReflectionStrength 0.5`；整体是偏中灰、低反射的磨砂铝合金效果，而不是镜面钢。
- 包边使用 `LevelAssets` 中的 `FrostedAluminumEdge` 材质。

缎面磨砂玻璃（`Assets/Shaders/SatinFrostedGlass.shader`）：

- 使用包裹式漫反射（`_Wrap 0.35`）让明暗过渡更柔和，避免硬边。
- 高光使用指数约 `lerp(6, 80, smoothness²)` 的宽而低强度 Blinn-Phong 高光（`_SpecularStrength 0.35`），模拟磨砂表面的柔和高光。
- `_Grain 0.035`、`_GrainScale 20`：使用平滑三线性值噪声对漫反射做扰动；颗粒比上一版更大、更柔和。`_Sheen 0.1` 增加菲涅耳边缘柔光。
- `_Brightness 0.78` 将整体亮度校准到与原 Standard `#bbbbbb` / `#777777` 观感接近，避免材质变亮后改变关卡可读性。
- 内胆和所有墙（场景边界墙、JSON 固定墙/中央墙）共用该 shader，仅 `_Color` 不同。

终版（仍使用内置渲染管线）：

- 镜子：高金属度、低光滑度的 Standard 材质。
- 透镜/棱镜：透明材质近似玻璃；不做屏幕空间折射。
- 激光：HDR 自发光 + 自建 Bloom 后处理；如果内置后处理成本过高，可只用加色混合的发光面片。
- 灯：自发光 + 简单光晕面片。

> 视觉材质不能改变仿真结果；仿真始终使用二维几何和 RGB 颜色。

### 8.3 器件 prefab 标准朝向

所有 prefab 的本地坐标约定：

- `+X` = 面内 `u`
- `+Y` = 面内 `v`
- `+Z` = 面法线
- 元素自身的 `rotation` 绕本地 `+Z` 旋转。

| 类型 | 视觉形状 | 轴/朝向 |
| --- | --- | --- |
| 发射器 | 圆柱，半径:高度 = 1:2 | 圆柱轴沿本地 `+X`；光束从 `+X` 端射出 |
| 接收器 | 完整圆柱 / 墙面半圆柱 / 角落 1/4 圆柱 | 自由摆放为完整圆柱；墙面件平面边贴墙，弧面朝面内，`+Z` 是可见接收面 |
| 平面镜 | 银色直角三棱柱 | 斜面（反射面）沿本地 `Y`，本体在 `-X` 侧 |
| 凸面镜 | 银色凸面棱柱 | 凸面朝 `+X`，本体在 `-X` 侧 |
| 凹面镜 | 银色凹面弧片 | 凹面朝 `-X`，弦沿 `Y`；背面吸收 |
| 柱面透镜 | 半透明白色挤出体 | 光轴沿 `+X`，轮廓在 XY 平面，沿 Z 挤出 |
| 三棱镜 | 半透明白色等边三角柱 | 平边在 `+X` 侧，顶点在 `-X` 侧 |
| 滤光片 | 很薄的半透明长方体 | 厚度沿 `X`，高度沿 `Y` |
| 灯 | 小球/小圆柱 | 无方向 |
| 小型锁 | 灰色盖体 + 顶部颜色球冠指示灯 | 覆盖器件，不透明，阻断光线 |
| 总锁 | 灰色底座 + 环形灯 | 顶面中心，覆盖钥匙孔 |

- 发射器圆柱的 `+X` 端面中心是出射点。
- 接收器的 `+Z` 面是接收面；二维仿真仍使用以器件中心为圆心的判定圆。
- 平面镜的三维斜面与二维反射线重合。
- 透镜/棱镜的三维挤出厚度默认 `0.04`，必须小于 `linerInset`。
- 锁的尺寸取器件包围盒 + 少量余量；锁体盖住器件。
- 小型锁顶部球冠指示灯的颜色来自 `unlockRequires`；未激活亮、激活灭。球冠底面半径 = 锁半径的 1/5，高度为完整球体高度的 1/4，底面贴在锁盖顶面；锁会自动放大到覆盖器件圆面。
- 锁打开后隐藏锁体，恢复器件的显示和光学行为。

#### 当前发射器/接收器尺寸基线

当前场景（外壳 `1.1`、内胆 `0.9`、墙高 `0.1`）使用以下尺寸：

| 器件 | localScale | 实际半径 | 实际长度/深度 |
| --- | --- | --- | --- |
| 发射器 | `(0.033, 0.033, 0.033)` | `0.033` | `0.066` |
| 接收器 | `(0.066, 0.066, 0.066)` | `0.033` | 深度 `0.066`（墙面半圆 / 角落 1/4 圆） |

注意：Tuanjie 的 `Resources.GetBuiltinResource<Mesh>("Cylinder.fbx")` 返回的圆柱半径为 `1`、高度为 `2`，因此发射器实际半径等于 `localScale.x/z`，实际长度等于 `localScale.y * 2`。接收器使用 `ReceiverMesh` 生成单位半径 `0.5` 的网格，实际半径是 `localScale.x * 0.5`，凸起深度是 `localScale.z`；判定半径按这个直径计算并额外加 `0.004` 容差。

器件贴墙规则：器件背面与墙内表面齐平，中心沿光束方向向内偏移自身半长。墙内表面在 `±0.43` 时：

- 发射器中心：`±0.397`（半长 `0.033`）。
- 接收器中心：`±0.408`（半长 `0.022`）。

不要再用 `±0.43` 作为器件中心，否则模型会穿进墙体。

### 8.4 渲染激光

- 每个 `RaySegment` 对应一条 `LineRenderer`，或合并为自定义 Mesh。
- 初版建议用 `LineRenderer` 池（例如 64 条），避免频繁创建销毁。
- 参数：
  - `positionCount = 2`
  - `startWidth = endWidth = 0.006`
  - `startColor = endColor = beamColor`
  - `useWorldSpace = false`（放在 `FaceRoot` 下，使用面内坐标）
  - `alignment = View`
- 颜色可以按顶点写入支持顶点色的材质，或像当前实现一样每条线段使用独立的材质实例。
- 颜色亮度很低时，视觉上可能看不见。可以只在渲染时把颜色提升到一个最低可见亮度（例如 `max(color, 0.15)`），但仿真仍用真实颜色。终版用 Bloom 解决。
- 当前实现：每个发射器有一个 `Beam_<器件后缀>` 父对象；光路按段拆成多条 `LineRenderer`（第一段用父对象，后续为 `Segment_1`、`Segment_2`……子对象）。每条线只画两个端点，写入该段的显示颜色（`LightColor` 按最大分量归一化）；宽度 `0.03`，使用 `Beam_*` 的 `Unlit/Color` 材质实例。
- 光晕：每条核心线段下再生成一条 `Glow` `LineRenderer`，宽度为核心线的 `3×`，使用 `Lox/BeamGlow`（`Queue Transparent`、`ZWrite Off`、`Blend One One` 的加法线）。片段按线宽方向的 UV 做平方衰减，`_Color.a = 0.28`，颜色随该段滤光后的颜色更新。光晕只加一层，保持轻微。
- 激光线放在 `z = laserZ`，避免与内胆 z-fighting。
- 如果一条光路被接收器吸收，线段终点就是命中点。
- 如果光线离开面边界，线段终点是边界点。

### 8.5 选择与高亮

> 以下是分层方案规划；当前实际使用 `DevicePicker` 的 `Physics.Raycast` 取最近 Collider，不依赖 layer mask。当前实现见本节末尾。

- 每个器件 prefab 加 Collider（BoxCollider/CapsuleCollider 均可），放在 `Interactable` 层。
- 内胆放在 `Occluder` 层，有 Collider。
- 外壳无 Collider，或放在 `Ignore Raycast`。
- 射线检测：

```csharp
int mask = LayerMask.GetMask("Interactable", "Occluder");
if (Physics.Raycast(camera.ScreenPointToRay(mousePos), out var hit, 100f, mask))
{
    var view = hit.collider.GetComponentInParent<ElementView>();
    if (view != null) Select(view.Model);
    else Deselect();
}
```

- 因为内胆是不透明的，射线如果先打到内胆，就不会选中背面的器件。
- 选中高亮：
  - 初版：将器件颜色提亮 20%，或在器件下方加一个圆环。
  - 终版：描边 shader + 轻微脉冲。
- 当前发射器/接收器选择实现：
  - 每个器件带 `CapsuleCollider`；内胆带 `BoxCollider` 作为遮挡体。
  - 鼠标点击用 `DevicePicker` 取最近 Collider；命中器件则选中。
  - 在空白处按下即进入旋转盒子状态，并立即取消当前器件选中；按下后几乎不移动就松开（单击空白）同样保持未选中状态。
  - 选中后 `DeviceSelectable` 启用一个与器件同网格、同变换的子物体，材质为 `Lox/SelectionGlow`：
    - 第一个 Pass 用 `ColorMask 0` 把器件的屏幕足迹写入 stencil（透明透镜也能遮罩）。
    - 第二个 Pass 在裁剪空间沿屏幕方向向外扩张固定像素数，并忽略 stencil 标记的足迹区域。
    - 结果是当前视图下的二维轮廓描边，不填充器件内部，也不随盒子旋转失效；被墙体/内胆挡住时由深度测试自然隐藏。
  - 移动：沿所在墙双向移动；`A/D`、`W/S` 按屏幕方向解释，再投影到器件唯一可移动的轴上；范围 `±0.43`。
  - 旋转：`←/→`，绕面法线旋转，速度 `45°/s`；不限制角度。平移/旋转按键刚按下时执行一个与连续操作相同的单帧步长，持续按住 `0.5s` 后才进入连续操作。
  - 激光：`LaserEmitter` 每帧按当前朝向做面内射线；命中接收器时同色需求灯熄灭，未命中时保持点亮。
- 锁定器件（当前实现）：
  - `LaserLock` 挂在被锁器件上；`Awake` 隐藏器件渲染器并关闭它的 Collider，锁的视觉对象建在同级的面节点下。
  - 运行时构建灰色空心筒壁、顶盖和顶盖上的颜色灯；灯色由 `Palette` 取得并按最大分量归一化。
  - 关闭时以 `outerRadius + lampRadius` 的圆阻断光线；被锁的发射器不发射（`LaserEmitter` 检查自身的 `LaserLock`）。
  - 解锁条件来自 `level_01.json` 的 `unlockRequires`（也可序列化在组件上）；颜色状态由 `LaserReceiver.IsColorLit` 全局查询，所需颜色全部亮起时打开。
  - 打开后顶盖和灯隐藏、筒壁下沉，器件渲染器/Collider 恢复，锁体 Collider 关闭，器件恢复可选中；锁一旦打开就保持打开。
  - 规划：点击锁显示“需要点亮：…”的 HUD；当前尚未实现。

### 8.6 输入

使用旧版 Input Manager。

#### 盒子旋转

- 鼠标左键按下时记录位置；如果按下位置在器件上，本次点击交给 `DeviceSelector`，不拖动盒子。
- 在空白处按下后进入拖动状态，按鼠标位移旋转盒子。
- 旋转使用摄像机参考系：

```csharp
Vector3 delta = Input.mousePosition - lastMousePosition;
lastMousePosition = Input.mousePosition;

transform.Rotate(camera.transform.up, -delta.x * 0.2f, Space.World);
transform.Rotate(camera.transform.right, delta.y * 0.2f, Space.World);
```

- 松开后自动吸附到最近的 24 个正位之一。
- 规划：`R` 键重置盒子旋转；当前尚未实现。

#### 数字键盘整体旋转

以摄像机参考系为准，使用魔方术语：

- `8`：`x`，绕世界 X 轴 `-90°`（从 +X 方向看顺时针）。
- `2`：`x'`，绕世界 X 轴 `+90°`。
- `4`：`y'`，绕世界 Y 轴 `-90°`。
- `6`：`y`，绕世界 Y 轴 `+90°`（从 +Y 方向看顺时针）。
- `1`：`z'`，绕世界 Z 轴 `+90°`。
- `3`：`z`，绕世界 Z 轴 `-90°`（从 +Z 方向看顺时针）。

每次旋转 `90°`，用 `0.2` 秒平滑过渡。数字键盘旋转会取消鼠标拖动/吸附；如果当前已有旋转在进行，会先完成当前旋转，再开始下一段。

#### 选择

- 鼠标左键按下时，用 `DevicePicker` 取最近 Collider。
- 最近 Collider 属于 `DeviceSelectable` 时选中该器件。
- 在空白处按下时即视为开始拖动盒子，并立即取消当前选中；单击空白也保持未选中状态。
- 内胆的 `BoxCollider` 作为遮挡体，避免选中背面的器件。
- 选中后 `DeviceSelectable` 启用同网格同变换的 `SelectionOutline` 子物体，`Lox/SelectionGlow` 用 stencil 遮罩 + 屏幕空间扩张绘制 `#ffff77` 二维轮廓。轮廓每帧按当前视图重新计算，盒子旋转后仍然贴合器件；器件被内胆或墙挡住时描边也会被深度测试隐藏。

#### 平移

- 选中发射器/接收器后，移动沿其所在墙进行（水平墙沿面内 `x`，垂直墙沿面内 `y`）。
- `A` / `D` 表示屏幕左/右，`W` / `S` 表示屏幕下/上；键位不再固定绑定到水平墙或垂直墙。
- 大多数器件把自己唯一可移动的面内轴投影到摄像机的右/上平面，取按键方向在该轴上的分量。因此盒子整体旋转后，例如原来“向上”的位移要改按“向左”才能得到同样的结果。
- blue 面透镜使用 `freeMove = true`：同时投影面内 X/Y 两轴，按键可在面内自由平移。
- 如果投影长度接近 0（面几乎侧对摄像机），该帧不移动。
- 移动速度 `0.25` 局部单位/秒；刚按下时执行一个与连续帧相同的步长（`moveSpeed * deltaTime`），持续按住 `0.5s` 后才连续移动。
- 位置范围由关卡 JSON 的 `moveMin` / `moveMax` 提供（当前 `±0.43`）。
- 暂不考虑器件与墙、器件的穿模。

#### 旋转

- `←` / `→` 绕子谜题面法线旋转。
- 左/右方向按屏幕逆/顺时针解释；内部根据面法线朝向摄像机的方向修正局部符号。
- 旋转速度 `45°/s`；刚按下时执行一个与连续帧相同的步长（`rotateSpeed * deltaTime`），持续按住 `0.5s` 后才连续旋转。
- 角度范围由关卡 JSON 的 `rotationMin` / `rotationMax` 提供（当前 `-360°..360°`）。
- 对发射器，旋转会改变出射方向；接收器旋转只改变外观（命中区域仍是圆）。

> `DeviceSelectable` / `DeviceSelector` 只负责“怎么移动、怎么旋转、如何夹紧”；是否允许移动/旋转以及范围来自 JSON。缺少 JSON 配置的器件会保持 `canMove = false`、`canRotate = false`。

#### 锁定与解锁

- 锁是锁存的：一旦满足条件打开，本关内保持打开。
- 每次仿真后检查尚未解锁的器件：
  ```csharp
  foreach (var element in model.Elements)
  {
      if (!element.IsUnlocked &&
          element.UnlockRequires.All(c => result.ColorSatisfied[c]))
      {
          element.IsUnlocked = true;
          locksChanged = true;
      }
  }
  ```
- 如果有新解锁，必须重新仿真，因为锁不再阻断光线。
- 锁定期间器件不可选中；点击锁只查看解锁条件。
- 锁打开后更新视觉：隐藏锁体、显示器件、移除锁定提示。

### 8.7 UI

当前 HUD 由两个运行时创建的 IMGUI 组件组成：`KeyHintUI`（左下角键位提示）和 `GameFlowUI`（计时、重新开始、`TOUCH` 与通关对话框）。

- 左上：仅显示 `Assets/Resources/Icons/Restart.png` 的无外框图标按钮；计时尚未开始时，按钮右侧显示鼠标左键图标与 `W` 键图标（高度与计时器字体高度一致），表示按鼠标或键盘开始计时。
- 右上：计时器，粗体微软雅黑，颜色 `#999999`，格式 `mm:ss.cc`；等待输入时显示 `00:00.00`。
- 计时在进入游戏或重开后第一次鼠标按下/键盘按下时开始；总锁所有指示灯熄灭时停止并锁存最终用时。
- 调试作弊：任意时刻连续输入 `↑↑↓↓←→←→BABA`，熄灭总锁的六盏环灯和中心白灯，并直接进入通关状态，显示 `TOUCH`；点击 `TOUCH` 可打开通关对话框。作弊不改变接收器或锁状态，重新开始后恢复。
- 左下：`KeyHintUI` 的键位提示。
- 通关：总锁所有指示灯熄灭后，顶面总锁中心显示 `TOUCH`（粗体微软雅黑，`#BBBBBB`）；点击后弹出“你赢了！”对话框，显示最终用时和“确定”按钮。
- `重新开始` 与对话框“确定”执行同一操作：重新加载 `Main` 场景，计时清零并重新等待鼠标/键盘输入。
- `GameFlowUI` 使用 `DontDestroyOnLoad` 并在 `sceneLoaded` 时重置状态，使 HUD 在场景重载后继续存在；通关状态锁存，重开或确定后清除。

中文字体：

- `KeyHintUI` / `GameFlowUI` 使用 `Font.CreateDynamicFontFromOSFont(...)` 创建动态字体，并赋给 `GUIStyle.font`。
- 如果目标平台不是 Windows，改为项目内包含一个支持中文的字体文件。
- 不使用 TextMeshPro，也不依赖 uGUI。

当前已实现：

- `KeyHintUI` 通过 `RuntimeInitializeOnLoadMethod` 在运行时创建左下角键位提示。
- 使用内置 IMGUI（`OnGUI`），不依赖 `com.unity.ugui`。
- 半透明黑色面板；鼠标左键项始终显示：
  - 未选中器件：只显示鼠标左键“选中元件；旋转盒子”。
  - 选中且可平移：额外显示 WASD“平移元件”。
  - 选中且可旋转：额外显示左右方向键“旋转元件”。
- 图标来自 `Assets/Resources/Icons/` 的 PNG（`MouseLeft`、`W/A/S/D`、`ArrowLeft/Right`），通过 `Resources.Load<Texture2D>` 读取。
- 文本颜色为 `#777777`；PNG 图标保留原始灰色圆形和白色符号，不额外着色。
- 图标与文字的间距在 `KeyHintUI` 中加大（文字起始 `panel.x + 120`）。
- 文字使用 `FontStyle.Bold`；行距为 `56 * scale`，面板高度按可见行数收缩为 `(24 + 56 × 行数) * scale`。鼠标左键项始终占一行。
- 使用动态系统中文字体；键位提示在播放时自动出现，不阻挡 3D 点选。

尚未实现：

- 右上颜色状态列表。
- 左下选中器件的类型、位置、角度、锁定条件等详细信息。
- 钥匙孔动画。

### 8.8 顶面特殊锁、灯环与钥匙孔

> 当前已实现总锁底座和 7 盏需求灯：对应接收器未激活时亮、激活后熄灭；全部熄灭后显示 `TOUCH` 与通关对话框。锁盖的光学阻挡和钥匙孔动画仍为规划内容。

- 顶面中心是一个特殊锁：灰色圆盘/圆柱盖覆盖钥匙孔，外圈是固定 M/Y/G/B/C/R 六种全局颜色的灯环（白色不设顶灯）。当前底座半径 `0.225`，即内胆边长 `0.9` 的 1/4。
- 灯是球冠指示灯，与普通锁使用同一规则：球冠底面半径 = 锁半径 / 5 = `0.045`，高度为完整球体高度的 1/4（约 `0.026`），底面落在锁座顶面；灯环半径保持 `0.165`。
- 特殊锁和普通锁一样是实体障碍：阻挡半径为 `ringLampRadius + lampRadius`；顶面子谜题器件必须放在这个外接圆之外。
- 加载关卡后，按固定六种颜色在顶面生成灯环：
  ```text
  angle_i = 90° + i * 360° / N
  position = (cos(angle_i), sin(angle_i)) * ringLampRadius
  ```
- 每个灯是一个 `LampView`，颜色为对应颜色。
- 每盏点亮的灯由 `TopLockLamp` 生成一个 `LampGlow` 球冠壳：复用灯的球冠网格、放大 `1.5×`，使用正常深度测试，因此被内胆/墙体/盒子挡住时不会透出。当前边缘光晕过弱，效果待调优。
- 这里曾尝试过 `ZTest Always` 的方案，但会让光晕穿透遮挡；已移除。指示灯光晕的整体效果暂列为低优先级。
- 灯亮表示同色接收器尚未满足；满足后熄灭。
- 总锁六盏颜色灯和中心白灯全部熄灭后，特殊锁打开：锁盖消失/打开，钥匙孔出现。
- 钥匙孔：
  - 一个位于顶面中心的深色模型/贴图。
  - 初始 `SetActive(false)` 或缩放为 0。
  - 特殊锁打开时用 0.5 秒缩放/淡入。
- 特殊锁打开后保持打开，通关状态锁存。
- 初版只需要灰色盖 + 一圈灯 + 钥匙孔缩放入场，不需要复杂机械动画。

### 8.9 摄像机

- 透视相机，FOV 约 32°。
- 位置 `(2.2, 1.8, 2.6)`，看向原点。
- Near 0.01，Far 100。
- 摄像机不随盒子旋转。
- 可选：终版增加“聚焦当前面”的平滑动画，但初版不需要。

### 8.10 窗口与全屏

- `PlayerSettings`：默认 `FullScreenMode.FullScreenWindow`（原生分辨率），`resizableWindow = 1`，`allowFullscreenSwitch = 1`。
- `WindowModeController` 在启动时创建常驻对象；`F11` 或 `Alt+Enter` 在“无边框全屏”和 `1600x900` 窗口模式之间切换。
- 窗口模式下可拖动边缘缩放或最大化；画面按窗口尺寸重新计算比例，键位提示按 `Screen.height` 缩放。
- Unity 会把上次的屏幕模式/窗口尺寸写入 `HKCU\Software\Lox\Lox` 的 `Screenmanager *` 项；删除这些项即可恢复首次启动的默认全屏状态。

---

## 9. 游戏流程

> 当前普通锁已实现；总锁的锁盖阻挡、钥匙孔动画尚未实现；总锁所有灯熄灭后的通关判定已由 `GameFlowUI` 的 `TOUCH` 流程实现。

### 9.1 状态机

```text
Loading ──成功──► Playing ──M/Y/G/B/C/R 满足──► Won
   │                  ▲
   │                  │ 玩家操作/重新仿真
   └──失败──► Error   └───────────────────
```

### 9.2 启动

1. `GameManager.Start()` 读取 Inspector 中的关卡文件名（默认 `level_01.json`）。
2. `LevelLoader.Load(path)`：
   - 读取 JSON。
   - 反序列化为 DTO。
   - `LevelValidator` 校验。
   - 构建 `PuzzleModel`。
3. `BoxView.Build(model)` 创建外壳、内胆、面根节点、器件视图、锁、灯环。
4. `GameManager` 运行仿真，并处理“锁打开 → 重新仿真”的循环，直到没有新锁打开。
5. 应用最终结果：更新激光、灯、锁、UI。
6. 状态进入 `Playing`。

### 9.3 玩家操作循环

```text
Update:
  BoxRotator.Tick()
  ElementSelector.Tick()
  ElementManipulator.Tick()

  if model dirty:
      result = Solve(model)   // 仿真 + 处理新打开的锁，见 9.4
      ApplyResult(result)
      UpdateHud(result)
      CheckWin(result)
```

- 一帧内多次移动只触发一次求解。
- 视图的 Transform 在模型变化后立即更新，不必等仿真。
- 锁打开会改变光路，所以不能只仿真一次；`Solve` 需要迭代到没有新锁打开。
- 锁是锁存的，迭代是单调的，最多执行“锁定器件数 + 1”次。

### 9.4 求解与锁

```csharp
SimulationResult Solve(PuzzleModel model)
{
    SimulationResult result = null;
    int guard = 0;
    int maxIterations = model.Elements.Count + 1;

    while (guard++ < maxIterations)
    {
        result = OpticsSimulator.Simulate(model);

        bool unlockedAny = false;
        foreach (var element in model.Elements)
        {
            if (element.IsUnlocked) continue;
            if (element.UnlockRequires.All(c => result.ColorSatisfied[c]))
            {
                element.IsUnlocked = true; // 锁存
                unlockedAny = true;
            }
        }

        if (!unlockedAny) break;
    }

    return result;
}
```

- 先按当前锁状态仿真；如果本轮有新锁打开，下一轮重新仿真。
- 锁打开后不会再关闭，所以循环一定终止。
- 如果达到 `maxIterations` 仍有新解锁，记录警告并使用最后一次结果（正常关卡不应发生）。

```csharp
void ApplyResult(SimulationResult result)
{
    laserRenderer.Render(result.Segments);

    foreach (var lamp in model.Lamps)
    {
        bool lit = result.ColorSatisfied[lamp.ColorId];
        lampView.SetLit(lit);
    }

    foreach (var element in model.Elements)
    {
        elementView.SetLocked(!element.IsUnlocked);
    }
}
```

- `ApplyResult` 只负责把最终结果同步到视图；锁状态已经在 `Solve` 中更新。
- 锁视图顶部的灯环读取 `result.ColorSatisfied`，不单独保存状态。

### 9.5 通关

- 条件：`DeviceConfigLoader.GoalLamps` 中全部 7 盏灯（六盏颜色环灯 + 中心白灯）都已熄灭。
- 触发一次：
  - 冻结并锁存计时。
  - 在顶面总锁位置显示 `TOUCH`（`#BBBBBB`，粗体微软雅黑）。
  - 点击 `TOUCH` 后弹出“你赢了！”对话框，显示最终用时，并提供“确定”按钮。
- 通关状态锁存，不因后续操作取消；当前用 `TOUCH` 代替钥匙孔动画。
- “确定”与“重新开始”执行同一重置流程。

### 9.6 重新开始（已实现，初版）

当前通过重新加载场景实现：

- 左上角 `重新开始` 按钮，或通关对话框的“确定”按钮。
- `GameFlowUI` 持久化在 `DontDestroyOnLoad`，收到 `SceneManager.sceneLoaded` 后重置计时、`TOUCH` 和对话框状态。
- 场景重新加载会重建全部器件、锁、激光和灯，使谜题回到初始状态。
- `AcrylicShellThickness` 与 `AcrylicShellAluminumEdging` 原本只在启动时挂到 `Shell`；现在同时监听 `SceneManager.sceneLoaded`，重载后重新挂载，恢复内层壳、包边以及包边的圆角。
- 计时清零，并等待下一次鼠标按下或键盘按下来开始。
- 初版直接重载场景，避免维护一整套手动复位逻辑。

---

## 10. 关卡制作与校验

### 10.1 制作流程

1. 复制一个现有 JSON。
2. 修改 `id`、`name`。
3. 选择本关颜色集合 `colors`。
4. 为每个颜色放一个接收器，设置 `targetColor`。
5. 放置发射器、镜子、透镜、棱镜、滤光片。
6. 设置初始位置/角度、可调范围和旋转范围。
7. 如果需要解锁机制，给器件加 `unlockRequires`。
   - 这会在器件上生成一个锁；锁会阻断光线，注意不要把唯一光路锁死。
8. 用编辑器里的“Validate Levels”菜单或直接运行检查错误。
9. 在 Play 模式中实际通关一次。

### 10.2 几何朝向速查

| 器件 | `rotation = 0` 的含义 |
| --- | --- |
| 发射器 | 光束沿 `+u` |
| 平面镜 | 镜面线沿 `v`，法线沿 `u`；水平光会被原路反射 |
| 凸面镜 | 凸面朝 `+u`，弦沿 `v` |
| 柱面透镜 | 光轴沿 `u`，轮廓高度沿 `v` |
| 三棱镜 | 平边在 `+u` 侧，顶点在 `-u` 侧；光从 `+u` 射入 |
| 滤光片 | 厚度沿 `u`，高度沿 `v`；光从 `u` 方向穿过 |

### 10.3 网格与精度

- 初版不强制网格吸附；按键步长已经是离散的。
- 如果关卡需要精确对齐，建议器件位置使用 `0.05` 的倍数，角度使用 `5°` 的倍数。
- 接收器实际半径 `0.033`；判定圆以墙面、墙角或自由位置为圆心、半径 `0.037`，光束停在可见弧面前约 `0.004`。与挂载样式（完整圆柱 / 半圆柱 / 1/4 圆柱）无关。
- 如果玩家反馈太难对准，可以：
  - 增大接收器半径；
  - 减小旋转步长；
  - 增加“按住 Shift 微调”；
  - 在终版加入吸附。

### 10.4 编辑器校验工具（可选）

建议加一个菜单：

```text
Tools/Lox/Validate All Levels
```

遍历 `Assets/StreamingAssets/Levels/*.json`，逐个运行 `LevelValidator`，把错误打印到 Console。这个工具不是初版必须，但能显著减少手写 JSON 的错误。

### 10.5 当前六面内容

| 面 | 当前内容 |
| --- | --- |
| front（M/C） | 红、蓝两束光经可移动柱面透镜聚焦混成洋红；白光经青色滤光片后，由同一块透镜折射到左下角青色接收器；透镜带绿色（G）锁 |
| back（G） | 3 段墙 + 底部固定平面镜 + 顶部可移动平面镜 + 可平移凹面镜；接收器位于墙面的 L 形交角 |
| left（B） | 三发射器 + 自由移动凸透镜关卡（凸透镜带绿色锁） |
| right（Y） | 白色光源 + 青色角光源 + 透镜 + 滤光片 + `G+C` 接收器与锁 |
| bottom（C） | 三白光发射器 + 三棱镜 + C/M/Y 滤光片 + `B+Y` / `R+C` / `R` 接收器 |
| top | 总锁底座 + 6 盏环灯 + 中心白灯；全部熄灭后显示 `TOUCH` 与通关对话框 |

玻璃砖已删除。当前已放入平面镜、凹面镜、凸透镜和滤光片；三棱镜、凸面镜等尚未放入当前六面内容，后续按关卡设计逐步添加。

### 10.6 当前 back 面（绿色）关卡

设计坐标 `[0,1] × [0,1]` 映射到边界墙内侧 `[-0.43,0.43] × [-0.43,0.43]`，局部坐标约为 `local = (c - 0.5) * 0.86`。

- 绿色发射器：设计 `(0, 1/2)`，局部 `(-0.397, 0, 0.05)`，初始旋转 `90°`（圆柱轴沿 X，光线从 `+X` 圆面射出），初始朝 `+X`；不可平移、可旋转。
- 绿色接收器：移到墙面 2 与墙面 3 的 L 形交角，局部位置 `(-0.10333, 0.255, 0.05)`，`mountPivot = (-0.03, -0.03)`；生成 `ReceiverQuarterCylinder`，贴合交角内侧，`HitCenter = (-0.13333, 0.225)`。不可平移、不可旋转，仍绑定顶面 `G` 灯。
- 固定平面镜：设计 `(1/2, 0)`，局部 `(0, -0.4285, 0.05)`，旋转 `90°`（水平反射线），尺寸 `(0.02, 0.1075, 0.1)`；在 `mirrors` 数组中定义，固定。
- 可移动平面镜：设计在绿色接收器原所在墙、靠近凹面镜一侧，局部初始 `(0.25, 0.408, 0.05)`，旋转 `90°`，尺寸 `(0.02, 0.1075, 0.1)`；作为 `devices` 中的 `mirror` 类型，沿 X 平移 `-0.05..0.37`，不可旋转。
- 凹面镜：设计 `(1, 1/2)`，局部 `(0.41, 0, 0.05)`，凹面朝局部 `-X`；曲率半径 `0.18`、开口 `0.2`、厚度 `0.02`、深度 `0.1`；可上下平移 `-0.25..0.25`，不可旋转。模型最靠墙的背面点 `x = 0.43`，与边界墙内表面相切。
- 墙面 1：设计 `(1/2, 1/8)` 到 `(1/2, 3/4)`，局部中心 `(0, -0.05375)`，长 `0.5375`，竖直。
- 墙面 2：设计 `(1/3, 3/4)` 到 `(2/3, 3/4)`，局部中心 `(-0.005, 0.215)`，长 `0.29667`，水平；左侧向交点延伸半个墙厚。
- 墙面 3：设计 `(1/3, 3/4)` 到 `(1/3, 1)`，局部中心 `(-0.14333, 0.3175)`，长 `0.225`，竖直；下端向交点延伸半个墙厚，使两面墙的外缘对齐为一个干净的 L 形交角。
- 初始时墙面 1 挡住发射器的水平直线光路，绿色接收器不亮。
- 预期解法：旋转绿色发射器使光束从墙面 1 下方通过，打到下方固定平面镜；平面镜把光束反射到右侧凹面镜；上下移动凹面镜，使光束打到顶部可移动平面镜；再左右移动该平面镜，把光束送入 L 形交角处的绿色接收器。当前参数下存在解，例如发射器相对初始朝向旋转约 `-50°`、凹面镜高度约 `0.090`、顶部平面镜 X 约 `0.010`。
- 蓝色面的 `Lens_B` 带有绿色锁：`unlockRequires = ["G"]`，绿色接收器点亮后解锁。

### 10.7 当前 left 面（蓝色）关卡

设计坐标 `[0,1] × [0,1]` 映射到边界墙内侧 `[-0.43,0.43] × [-0.43,0.43]`，局部坐标约为 `local = (c - 0.5) * 0.86`。

- 蓝色接收器：设计 `(0, 1)`，局部 `(-0.408, 0.408, 0.05)`；不可平移、不可旋转，目标颜色 `B`。
- 青色发射器：设计 `(1, 1)`，局部 `(0.397, 0.397, 0.05)`；初始旋转 `90°`，`aim = (-0.805, 0.011)`；不可平移、不可旋转。
- 蓝色发射器：设计 `(1, 0)`，局部 `(0.397, -0.397, 0.05)`；初始旋转 `45°`，`aim = (-0.805, 0.805)`；不可平移、不可旋转。
- 红色发射器：设计 `(0, 0)`，局部 `(-0.397, -0.397, 0.05)`；初始旋转 `0°`，`aim = (-0.011, 0.805)`；不可平移、不可旋转。
- 三个发射器的 `aim` 均指向蓝色接收器，模型圆柱轴与 `aim` 对齐，使光线从圆形端面射出。
- 凸透镜：设计 `(1/2, 1/2)`，局部 `(0, 0, 0.05)`；焦距 `0.1`、通光高度 `0.32`、中心厚度 `0.05`、深度 `0.08`。
  - `movable = true`、`freeMove = true`、`moveMin = -0.34`、`moveMax = 0.34`，即解锁后可在面内自由平移。
  - `rotatable = true`、`rotationMin = -360`、`rotationMax = 360`。
  - 仍保留上一轮的绿色锁：`unlockRequires = ["G"]`。
- 初始时三个发射器的光束都射向接收器，混合颜色不等于蓝，接收器不亮。解锁透镜后，可把透镜移到约 `(-0.34, 0.29)` 并旋转约 `-40°`，让蓝光继续进入接收器、青/红光被偏折离开。
- 透镜采用薄透镜近似，可见的橄榄形网格只负责视觉。

### 10.8 当前 bottom 面（青色）调色台

设计坐标 `[0,1] × [0,1]` 映射到边界墙内侧 `[-0.43,0.43] × [-0.43,0.43]`，局部坐标约为 `local = (c - 0.5) * 0.86`。

- 白色发射器 3 个，初始朝向指向面中心（三棱镜）：
  - `emitter.W1`：设计 `(0, 1/2)`，局部 `(-0.397, 0, 0.05)`；初始旋转 `90°`，底面沿 `+X` 出射方向；可沿 Y 平移 `-0.43..0.43`，可旋转 `-360..360`。
  - `emitter.W2`：设计 `(1/2, 1)`，局部 `(0, 0.397, 0.05)`；初始旋转 `0°`，底面沿 `-Y` 出射方向；可沿 X 平移 `-0.43..0.43`，可旋转 `-360..360`。
  - `emitter.W3`：设计 `(1, 1/2)`，局部 `(0.397, 0, 0.05)`；初始旋转 `270°`，底面沿 `-X` 出射方向；可沿 Y 平移 `-0.43..0.43`，可旋转 `-360..360`。
- 三棱镜：设计 `(1/2, 1/2)`，局部 `(0, 0, 0.05)`，初始旋转 `0°`；等边三角形外接圆半径 `√2/8 = 0.1767767`，因此边长 `0.3061862`、高 `0.265165`、深度 `0.1`；不可平移、可旋转；`unlockRequires = ["Y", "M"]`，黄、洋红锁打开前棱镜被锁覆盖并阻断光线。
- 接收器（都位于底墙 `y = -0.43`，不可平移/旋转）：
  - `receiver.R`：设计 `(1/2, 0)`，局部 `(0, -0.408, 0.05)`，颜色 `R`，`lamp = "R"`，绑定顶面总锁的红色需求灯。
  - `receiver.RC`：设计 `(1/6, 0)`，局部 `(-0.2866667, -0.408, 0.05)`，颜色 `R+C`，与 C 滤光片同侧；不绑定顶灯。
  - `receiver.BY`：设计 `(5/6, 0)`，局部 `(0.2866667, -0.408, 0.05)`，颜色 `B+Y`，与 Y 滤光片同侧；不绑定顶灯。
  - 原 `G+M` 接收器已删除；M 滤光片保留。
- 滤光片（长度 `1/6 → 0.1433333`，旋转 `90°` 使板面水平，不可平移/旋转）：
  - `filter.cyan`：设计 `(1/6, 5/6)`，局部 `(-0.2866667, 0.2866667, 0.05)`，颜色 `C`。
  - `filter.magenta`：设计 `(1/2, 5/6)`，局部 `(0, 0.2866667, 0.05)`，颜色 `M`。
  - `filter.yellow`：设计 `(5/6, 5/6)`，局部 `(0.2866667, 0.2866667, 0.05)`，颜色 `Y`。
- 初始状态下三个接收器都不亮，因此顶面 R 灯亮起。

> **已处理：** `G+M` 接收器已按用户要求删除（M 滤光片保留）。之前记录的“M 滤光片到 G+M 的直线穿过棱镜”不再作为待确认项；底面目前要求点亮 `B+Y`、`R+C` 和红色全局接收器。

### 10.9 当前 top 面正方形反射环

设计坐标 `[0,1] × [0,1]` 映射到边界墙内侧 `[-0.43,0.43] × [-0.43,0.43]`。

- 正方形四个角：
  - `P1 = (-0.36, -0.30)`：白色发射器 `emitter.topBY`，`unlockRequires = ["B+Y"]`。
  - `P2 = (0.36, 0.30)`：平面镜 `mirror.topRC`，`unlockRequires = ["R+C"]`（替换原来的第二个白色光源）。
  - `Q1 = (-0.30, 0.36)`：平面镜 `mirror.topM`，`unlockRequires = ["M"]`。
  - `Q2 = (0.30, -0.36)`：平面镜 `mirror.topC`，`unlockRequires = ["C"]`。
- 发射器朝向 `Q1`：`aim = (0.06, 0.66)`，初始旋转 `174.806°`，使圆柱的 **底面**（本地 `-Y`）沿出射方向，光线从平底面射出而不是侧面。
- 镜面朝向：
  - `Q1` / `Q2` 的镜面平行于 `P1-P2` 对角线，旋转 `-50.194°`。
  - `P2` 的镜面平行于 `Q1-Q2` 对角线，旋转 `39.806°`。
- 光路：`P1 → Q1 → P2 → Q2 → P1`。在最后一小段、回到发射器之前放置白色接收器 `receiver.W`，局部约 `(-0.2405, -0.3109)`，颜色 `W`，`lamp = "W"`；它不贴墙，因此使用完整圆柱形态；光束在接收器圆面处终止。
- 总锁中心新增白色指示灯 `CenterLamp_W`（`goal.centerLamp = "W"`），由 `receiver.W` 控制：白色接收器未命中时亮，命中后熄灭。
- 四把锁（`B+Y`、`R+C`、`M`、`C`）全部打开后，光路才闭合；任意一把锁未开，光路会在对应位置被锁挡住。

### 10.10 当前 front 面（洋红/青色）关卡

设计坐标 `[0,1] × [0,1]` 映射到边界墙内侧 `[-0.43,0.43] × [-0.43,0.43]`。

- 目标接收器：`receiver.M` 在底墙中央 `(0, -0.408)`，`lamp = "M"`，判定圆心 `(0, -0.43)`；`receiver.C` 在左下角 `(0.408, -0.408)`，`lamp = "C"`，判定圆心为墙角 `(0.43, -0.43)`，生成 1/4 圆柱。
- 发射器（替换原来的洋红/青色直连发射器）：
  - `emitter.Rf`：局部 `(-0.06, 0.397, 0.05)`，颜色 `R`，`aim = (0, -1)`，固定不可操作。
  - `emitter.Bf`：局部 `(0.06, 0.397, 0.05)`，颜色 `B`，`aim = (0, -1)`，固定不可操作。
  - `emitter.Wf`：局部 `(-0.397, -0.2, 0.05)`，颜色 `W`，`aim = (1, 0)`，初始旋转 `90°`，固定不可操作。
- 关键器件：
  - `lens.front`：柱面透镜，初始局部 `(0, 0.12, 0.05)`、旋转 `0°`；`focalLength = 0.1`、`aperture = 0.32`；可 2D 自由平移 `-0.34..0.34`、可旋转 `-360..360`；`unlockRequires = ["G"]`，被绿色锁覆盖，必须先点亮 back 面的绿色接收器。
  - `filter.cyanFront`：颜色 `C`，局部 `(-0.22, -0.2, 0.05)`，旋转 `0°`（板面竖直，白光水平穿过）。
  - 本面不再放置平面镜。
- 光路与解法：
  - 洋红：`R` 与 `B` 两束平行竖直光从 `x = ±0.06` 向下，经同一块透镜聚焦到 `(0, -0.43)`，两束同时进入 `receiver.M`，按等权混色得到 `M`。
  - 青色：白光沿 `y = -0.2` 水平向右，穿过青色滤光片后变成 `C`；同一块透镜在约 `(0.034, -0.2)` 处接住这束光，并把它折射到左下角 `(0.43, -0.43)` 的 `receiver.C`。因此 M、C 共用同一块透镜，透镜位姿同时决定两路光。
  - 透镜被锁住时，其锁体同时挡住 `R`/`B` 两束光；解锁后仍需把透镜移到正确位置并旋转。
- 初始状态：`receiver.M` 与 `receiver.C` 都不亮，因此顶面 `M`、`C` 两盏环灯亮起。
- 验证参数：透镜 `(0, -0.2503)`、旋转 `146.19°` 时，R/B 聚焦到 M，青色光经同一透镜折射到左下角 C，两个接收器同时点亮；把透镜移回 `(0, 0.12)`、旋转 `0°` 后重新变暗。该位姿附近有一片连续可行区域（约 `40°..148°`），不是单点解。

---

## 11. 测试计划

### 11.1 EditMode 单元测试

#### 调色板

- `Palette.Count == 16`。
- 每个颜色总亮度约等于 1。
- 16 个 RGB 两两不相等。
- `R+C ≈ (0.5, 0.25, 0.25)`。
- `W ≈ (1/3, 1/3, 1/3)`。

#### 混色

- `Mix([R,G,B]).ColorId == "W"`，`Brightness ≈ 1`。
- `Mix([R,G]).ColorId == "Y"`。
- `Mix([R,C]).ColorId == "R+C"`。
- `Mix([R,R]).ColorId == "R"`，`Brightness ≈ 1`（不翻倍）。
- `Mix([]).Brightness == 0`，`Matches` 为 false。
- 亮 R + 暗 C（C 亮度 0.5）：`ColorId == "R+C"`，`Brightness ≈ 0.75`，`Final ≈ 0.75 × R+C`。
- 无调色板可拟合的混合：`ColorId == "W"`，`Brightness` 等于平均亮度。
- 白光过红滤光片：`ColorId == "R"`，`Brightness ≈ 1/3`，`Matches("R") == true`。
- 暗红光 `(1/3,0,0)` 与满亮度红光 `(1,0,0)` 都被判定为 `R`。

#### 调色板拟合

- 精确调色板颜色距离为 0，拟合到自身。
- 距离 `<= 0.05` 的近似颜色拟合到最近调色板。
- 距离 `> 0.05` 的颜色退化为 `W`。
- 最小调色板间距为 `0.117851`，确保 `0.05` 不会同时命中两个颜色。
- `SnapEpsilon` 必须小于 `0.058925`。

#### 色散与滤光

- `Disperse((0.5,0.25,0.25))` 得到三个分量，亮度分别为 0.5/0.25/0.25。
- 白光通过红色滤光片 → `(1/3,0,0)`。
- 绿光通过红色滤光片 → 黑。
- 白光通过青色滤光片 → `(0,1/3,1/3)`。
- 黄光通过红色滤光片 → 只剩 R 分量。

#### 反射/折射

- 45° 平面镜把 `+u` 反射为 `-v` 或 `+v`（按符号约定）。
- 垂直入射平面镜：反射方向反向。
- 平面界面折射：入射角 0° 时方向不变。
- 从玻璃到空气超过临界角：返回 `false`（全反射）。
- 三棱镜：R/G/B 三个分量出射角不同；单色光不分裂。

#### 关卡加载

- 合法 JSON 能构建模型。
- 重复 `id` 报错。
- `colors` 中有非法颜色报错。
- 某颜色没有接收器报错。
- 两个接收器同色报错。
- 位置超出面边界报错。
- 初始角度超出 `limits.rotation` 报错。
- 顶面器件进入特殊锁外接圆报错。

#### 锁

- 未解锁器件的锁矩形能阻断射线。
- 解锁后同一射线不再被锁阻断，而是与器件正常交互。
- 小型锁顶部球冠指示灯颜色与 `unlockRequires` 一致；总锁灯环固定使用 M/Y/G/B/C/R。
- 锁打开后保持打开，再次仿真不会重新锁上。
- 顶面特殊锁在 M/Y/G/B/C/R 六种颜色满足前阻断中心区域，满足后打开并触发通关。

### 11.2 PlayMode 测试

- 加载测试关卡，运行仿真，断言初始接收器状态。
- 通过代码移动镜子，重新仿真，断言接收器从灭到亮。
- 断言灯的状态与接收器一致。
- 断言锁未打开时射线被阻断，打开后器件恢复光学行为。
- 断言锁打开后保持打开，并且锁打开会触发重新仿真。
- 断言通关状态锁存。
- 使用旧版 Input，输入逻辑难以自动化测试；以手动测试为主。
- 测试框架尚未加入工程；添加 `com.unity.test-framework` 与测试程序集属于单独任务。

### 11.3 手动检查清单

- [ ] 盒子可以拖动旋转，摄像机不动。
- [ ] 点击器件能选中，点击空白能取消。
- [ ] 背面器件不会被误选。
- [ ] W/S/A/D 只在面内移动，不能沿法线移动。
- [ ] 左右方向键旋转方向与视觉一致。
- [ ] 位置/角度到达 limits 后停止。
- [ ] 发射器旋转时激光实时更新。
- [ ] 平面镜、凸面镜反射方向正确。
- [ ] 透镜聚焦/发散方向正确。
- [ ] 三棱镜能把白光分成三条不同颜色的光路。
- [ ] 滤光片只吸收对应分量，不改变方向。
- [ ] 混合结果先平均亮度，再拟合到调色板；无法拟合时退化为对应亮度的白色。
- [ ] 接收器按拟合后的调色板 ID 亮灯；亮度不敏感，暗红也算红。
- [ ] 锁定器件被灰色锁覆盖，不能操作，且锁会阻断光线。
- [ ] 小型锁顶部球冠指示灯在对应接收器未激活时亮、激活后熄灭；全部熄灭后锁打开并保持打开。
- [ ] 接收器圆面与墙面平行，判定半径覆盖视觉圆面。
- [ ] 锁打开后器件恢复显示和光学行为，且会触发重新仿真。
- [ ] 顶面灯环作为特殊锁，六盏颜色灯和中心白灯全部熄灭后顶锁出现 `TOUCH`；点击后弹出通关对话框并停止计时。
- [ ] JSON 错误有清晰提示。

### 11.4 性能

- 目标 60 FPS。
- 单次仿真应在 1 ms 以内（初版规模）。
- 如果激光线数量过多，使用对象池和合并 Mesh。
- 不要在 `Update` 中每帧无条件重新仿真；只在模型 dirty 时执行。

---

## 12. 里程碑

### M0：工程初始化（已完成）

- 使用 Tuanjie `2022.3.62t18` 创建工程，使用内置渲染管线。
- 已创建 `Assets/Scenes/Main.unity`，摄像机背景为黑色，并加入 Build Settings。
- 已验证可构建 Windows x86_64 播放器（`Build/Lox.exe`）。
- 未引入 asmdef 和额外包。
- 后续目录、脚本、材质按 M1 之后的里程碑逐步添加。

### M1：数据与关卡加载

- 实现 `LightColor`、`Palette`。
- 实现 JSON DTO、`LevelLoader`、`LevelValidator`。
- 编写调色板/混色/加载测试。
- 能加载示例 JSON 并打印模型。

### M2：光学核心

- 实现 `Ray2`、`Hit2`、求交工具。
- 实现平面镜、凸面镜、柱面透镜、三棱镜、滤光片、接收器。
- 实现 `OpticsSimulator`。
- 用单元测试覆盖主要光学行为。

### M3：三维表现

- 实现 `BoxView`、`FaceView`、`ElementView`。
- 实现外壳/内胆/器件 prefab。
- 实现 `LaserRenderer`。
- 实现盒子旋转和摄像机。

### M4：交互与游戏流程

- 实现选择、平移、旋转、limits。
- 实现锁定/解锁、灯、通关。
- 实现 HUD。
- 完成 `level_01.json` 的可玩闭环。

### M5：初版六面关卡

- 按确认后的器件分配制作六面内容。
- 调整难度、容差、初始角度。
- 完成初版验收。

### M6：终版表现

- 终版 PBR 材质、Bloom。
- 钥匙孔动画、灯效、音效。
- 更多关卡和关卡选择。
- 性能与兼容性检查。

---

## 13. 风险与备选方案

| 风险 | 影响 | 备选方案 |
| --- | --- | --- |
| 玻璃砖已删除 | 初版清单曾列出六种器件 | SUMMARY 已同步；当前六面内容按实际关卡逐步补充，不强制六面六种 |
| 调色板已裁剪为 16 色 | `C+M`、`C+Y`、`M+Y` 在两束光混合下会与 `B+Y`、`G+M`、`R+C` 重合，故移除 | 如需恢复，改 `Palette.cs`、`SUMMARY.md` 和本文档表格 |
| 混合结果不接近任何调色板颜色 | 会退化为白色，可能导致意外点亮白色接收器 | 使用 `SnapEpsilon = 0.05`；关卡设计时避免无意义的混色；UI 显示拟合结果 |
| 拟合阈值过大 | 两个不同调色板颜色被混淆 | 最小调色板间距 `0.204124`，阈值必须 `< 0.102062`；当前取 `0.05` |
| 亮度不参与接收器判定 | 暗光也能点亮接收器 | 这是有意设计，保证滤光片可用；如要亮度敏感，增加 `targetBrightness` |
| 透镜二维轮廓在端点处数值不稳 | 光线擦边时行为异常 | 用 EPS 忽略端点附近命中；或把端点做成很小的圆角 |
| 透明外壳渲染顺序问题 | 外壳遮挡内部或闪烁 | 已处理：外壳使用 `Lox/AcrylicGlass`，`ZWrite Off`、透明队列、关闭投影；仍保留简单线框外壳作为后备方案 |
| `JsonUtility` 对嵌套/多态支持有限 | 复杂关卡字段难以反序列化 | 先用扁平 DTO + 手动组装模型；确实不够时再单独评估引入 Newtonsoft.Json |
| 内置管线缺少 URP 的后处理 | 终版 Bloom/玻璃效果受限 | 自建简单后处理，或用加色发光面片近似；不为视觉效果切换渲染管线 |
| 中文字体缺失 | UI 显示方块 | 使用系统字体或项目内 CJK 字体 |
| 关卡 JSON 手写易错 | 加载失败 | 编辑器校验工具 + 清晰错误信息 |
| 锁定器件导致死锁 | 玩家无法继续 | 锁会阻断光线；关卡设计时保证至少一条初始可操作路径；校验器给警告 |
| 锁打开改变光路 | 接收器/灯状态可能随之变化 | 锁打开后重新仿真，直到没有新锁打开；锁存避免反复开关 |

---

## 14. 给后续实现者的检查清单

在开始写代码前，确认以下约束：

- [ ] 不实现玻璃砖；`ElementType` 中没有它。
- [ ] 不使用球面透镜；只实现柱面透镜。
- [ ] 滤光片不折射、不反射；只做分量吸收。
- [ ] 只有三棱镜产生色散；其他折射器件单一折射率。
- [ ] 接收器颜色全局唯一；每个 `colors` 颜色恰好一个接收器。
- [ ] 子谜题是二维的；没有跨面光路。
- [ ] 顶面六盏颜色灯和中心白灯全部熄灭 → 特殊锁打开 → 当前版本显示 `TOUCH`，点击后弹出通关对话框；钥匙孔动画尚未实现。
- [ ] 混色先平均亮度，再按满亮度等权混合，最后乘平均亮度。
- [ ] 混合颜色拟合到最近调色板；阈值 `0.05`，无法拟合时退化为 `W`。
- [ ] 接收器按拟合后的调色板 ID 匹配，亮度不敏感。
- [ ] 暗红光仍然能点亮红色接收器。
- [ ] 关卡 JSON 在运行时从 `StreamingAssets/Levels` 加载。
- [ ] 初版先做无材质彩色版本；终版基于内置管线再加材质、发光和简单后处理。
- [ ] 顶面灯环作为特殊锁，参与光学阻挡；六盏颜色灯和中心白灯全部熄灭后打开。
- [ ] 锁定器件被灰色锁覆盖，锁阻断光线；小型锁球冠指示灯与总锁灯环统一为未激活亮、激活灭；锁打开后器件恢复光学。
- [ ] 锁打开后重新求解，且锁打开后保持打开。
- [ ] 先完成 M1→M4 的可玩闭环，再做 M5/M6。
- [ ] 每完成一个光学器件，补对应单元测试（测试框架加入后）。
- [ ] 使用 Tuanjie `2022.3.62t18` 和内置渲染管线，不引入 URP。
- [ ] 输出平台为 Windows x86_64；构建命令见 `README.md`。

如果实现过程中发现本文档与 `SUMMARY.md` 冲突，以 `SUMMARY.md` 为准，并把差异记录到第 1 节。
