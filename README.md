# Technical Art Lab

一个持续积累的 Unity 技术美术与实时渲染实验室。从游戏中有趣的视觉效果出发，通过观察、分析和最小复现，理解画面背后的几何、像素、动画驱动与渲染流程。

## 研究方向

- Shader 与材质：UV 扰动、溶解、描边、风格化光照。
- 几何与交互：顶点形变、卡牌交互、程序化几何。
- 渲染与特效：屏幕空间效果、RenderTexture、多 Pass 与粒子效果。

每个实验从一个具体问题开始：先用最少的组成部分验证核心原理，再按需要匹配参考效果、探索自己的变化。实验中的发现会整理为笔记，并逐步连接成可复用的图形学知识与技术模块。

## 当前进度

项目基础工程与资源目录已建立，当前实验：

| 实验 | 研究问题 | 状态 |
| --- | --- | --- |
| 001 · Balatro Card Hover | 修改四顶点 Quad 的 clip-space w，如何产生随鼠标变化的透视感？ | Minimal 已验证；原作匹配待补证据 |
| 002 · Balatro Card Dissolve | 同一程序化遮罩如何表现进入与退出？ | 独立 Minimal；双向溶解、边缘着色与同步阴影 |
| 003 · Balatro Card Drag | 鼠标目标如何驱动位置跟随、速度倾斜与释放回位？ | 独立 Minimal；预留抓取/释放事件 |
| 004 · Dissolve Particles | 卡面和粒子如何共用场数据，从消融边缘发射？ | 独立改进版；退出粒子、进入保留 |
| 005 · Card Integrated | 悬停、拖拽、离位计时和溶解如何协调？ | 独立整合版；离位锁定计时、溶解前松手取消 |
| 006 · Low-res 3D Pixelated | 内部分辨率与放大采样如何改变实时 3D 轮廓与运动细节？ | Minimal 已验证；四档分辨率、Point/Bilinear、正交/透视与平移回放 |
| 007 · Pixel-stable 3D | 固定正交视角下如何稳定采样并减小平移跳格？ | Minimal 已验证；连续、对齐、补偿三视图，四档分辨率与保护边 |
| 008 · Lo-fi Low-poly | 几何、法线与配色分别如何影响低模风格？ | Minimal 已验证；三级网格、Flat/Smooth、4/6/8 色、光照与分辨率对照 |

实验入口：`Assets/_Lab/Experiments/001_BalatroCardHover/Scenes/001_CardHover_Minimal.unity`。进入 Play 后移动鼠标，或用 0–4 切换固定状态。实现与证据见 [实验笔记](Notes/TANotes/Experiments/001_BalatroCardHover.md)。

Shader Graph 对照入口：同目录 `001_CardHover_ShaderGraph.unity`。运行时可切换 HLSL 和 Shader Graph 材质；节点说明见 [Shader Graph 实现](Notes/TANotes/Experiments/001_BalatroCardHover_ShaderGraph.md)。当前 DX11 固定相机下，六组状态的两版输出逐像素一致。

002 入口：`Assets/_Lab/Experiments/002_BalatroCardDissolve/Scenes/002_CardDissolve_Minimal.unity`。进入 Play，按 `1` 播放橙色退出，`2` 播放绿色进入，Space 暂停，`F` 冻结场时间。面板支持进度调节及 Color / Mask / Field 视图。机制与验证见 [002 实验笔记](Notes/TANotes/Experiments/002_BalatroCardDissolve.md)。

003 入口：`Assets/_Lab/Experiments/003_BalatroCardDrag/Scenes/003_CardDrag_Minimal.unity`。进入 Play 后按住卡片拖动；`R` 复位、`T` 固定轨迹回放、Space 暂停回放。参数与组合接口见 [003 实验笔记](Notes/TANotes/Experiments/003_BalatroCardDrag.md)。

## 打开项目

004 菜单：`TechArtLab > 004 > Create or Open Minimal`。Play 后 `1` 退出并发射粒子、`2` 进入、`R` 复位、Space 暂停。见 [004 笔记](Notes/TANotes/Experiments/004_BalatroDissolveParticles.md)。

005 菜单：`TechArtLab > 005 > Create or Open Minimal`。按住卡牌拖离原位，1 秒后开始溶解；溶解前松手取消，溶解后松手留在释放位置消失。`R` 复位、`T` 回放、Space 暂停。见 [005 笔记](Notes/TANotes/Experiments/005_BalatroCardIntegrated.md)。

使用 Unity Hub 打开 `UnityProjects/URPTALab`。

- Unity：`6000.6.0f1`
- Universal Render Pipeline：`17.6.0`
- 学习笔记：使用 Obsidian 打开 `Notes/TANotes`

## 仓库导览

- `UnityProjects/URPTALab`：Unity 工程与实验实现。
- `Notes/TANotes`：实验记录与概念笔记。

仓库保留学习笔记和可运行的实验实现。用于分析的原始视频、抓帧、美术源文件和本地辅助工具不随仓库分发。

006 菜单：`TechArtLab > 006 > Create or Open Minimal`。Play 后 `1–4` 切换分辨率、`B` 切换滤波、`N` 原生分辨率对照、`O` 切换投影；右键旋转、滚轮缩放、`F/R` 恢复等距视角、`T` 慢速平移。见 [006 笔记](Notes/TANotes/Experiments/006_LowRes3DPixelated.md)；后续六项规划见 [3D 风格与程序动画路线](Notes/TANotes/Experiments/3D像素风格与程序动画实验路线.md)。

007 菜单：`TechArtLab > 007 > Create or Open Minimal`。Play 后 `0` 三视图比较，`1/2/3` 单独放大，Space 暂停，`R` 复位，`T` 开关平移，`L` 切换阴影；右键拖动画面可平移。见 [007 实验笔记](Notes/TANotes/Experiments/007_PixelStable3D.md)。

008 菜单：`TechArtLab > 008 > Create or Open Minimal`。Play 后 `1/2/3` 切换细分，`N` 切换法线，`P` 切换底色色板，`V` 切换诊断视图，`L` 切换阴影，`R` 复位；右键旋转，滚轮缩放。见 [008 实验笔记](Notes/TANotes/Experiments/008_LoFiLowPoly.md)。
