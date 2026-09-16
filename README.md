# Technical Art Lab

一个持续积累的 Unity 技术美术与实时渲染实验室。从游戏中有趣的视觉效果出发，通过观察、分析和最小复现，理解画面背后的几何、像素、动画驱动与渲染流程。

## 研究方向

- Shader 与材质：UV 扰动、溶解、描边、风格化光照。
- 几何与交互：顶点形变、卡牌交互、程序化几何。
- 渲染与特效：屏幕空间效果、RenderTexture、多 Pass 与粒子效果。

每个实验从一个具体问题开始：先用最少的组成部分验证核心原理，再按需要匹配参考效果、探索自己的变化。实验中的发现会整理为笔记，并逐步连接成可复用的图形学知识与技术模块。

## 当前进度

项目基础工程与资源目录已建立，首个实验正在准备中：

| 实验 | 研究问题 | 状态 |
| --- | --- | --- |
| 001 · Balatro Card Hover | 修改四顶点 Quad 的 clip-space w，如何产生随鼠标变化的透视感？ | 待实现 |

后续将随实验完成补充演示、实现说明和学习笔记。

## 打开项目

使用 Unity Hub 打开 `UnityProjects/URPTALab`。

- Unity：`6000.6.0f1`
- Universal Render Pipeline：`17.6.0`
- 学习笔记：使用 Obsidian 打开 `Notes/TANotes`

## 仓库导览

- `UnityProjects/URPTALab`：Unity 工程与实验实现。
- `Notes/TANotes`：实验记录与概念笔记。
- `References`：参考分析与证据目录说明。
- `SourceAssets`：美术制作源文件的目录说明。
- `Tools`：辅助工具。

原始参考资料与美术源文件不随仓库分发。
