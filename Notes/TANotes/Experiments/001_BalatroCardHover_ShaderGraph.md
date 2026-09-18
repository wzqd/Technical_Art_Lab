# 001 · 卡牌悬停的 Shader Graph 实现

这版节点图复现 [[001_BalatroCardHover]] 的卡片透视形变。研究问题是：Shader Graph 的顶点输出只有三维 Position 时，如何表达修改裁剪空间 `w` 的效果，并保持卡面纹理的插值一致？

图中使用普通数学节点、矩阵节点和 Custom Interpolator，没有 Custom Function，也没有把核心公式藏在外部 HLSL 文件里。

## 场景与资源

资源位于 `Assets/_Lab/Experiments/001_BalatroCardHover/`：

| 资源 | 用途 |
| --- | --- |
| `Shaders/CardHoverGraph.shadergraph` | 可编辑的节点图 |
| `Materials/CardHoverGraph.mat` | Shader Graph 材质，与 HLSL 版使用相同卡面纹理 |
| `Scenes/001_CardHover_ShaderGraph.unity` | 独立对照场景 |
| `Scripts/CardHoverComparison.cs` | 在同一卡片上切换两种材质 |
| `Editor/CardHoverGraphExperiment.cs` | 打开场景、保存固定状态对照图 |

通过菜单 `TechArtLab > 001 > Open Shader Graph Comparison` 打开场景。进入 Play 后，右上角按钮切换 Shader Graph 与 HLSL；左上角控制输入和强度。`0–4` 的操作与 Minimal 相同。原来的 `001_CardHover_Minimal.unity` 保留为 HLSL 教学入口。

双击 `.shadergraph` 打开节点图，按 `A` 显示全部节点。图中分为六组，沿编号阅读；节点名称和便签保留英文，便于与 Unity 节点菜单对应。Main Preview 没有场景中的鼠标驱动，且贴图来自材质，实际效果以对照场景为准。

## 四个实现部分

| 部分 | 当前实现 |
| --- | --- |
| Geometry | 同一张四顶点、双三角形 Quad；节点反算顶点位置，以得到目标屏幕投影 |
| Pixel | 接收经过修正的 UV 插值数据，重建 UV 后采样卡面 |
| Driver | 复用 `CardHoverDemo`，由同一组 MaterialPropertyBlock 参数控制两种材质 |
| Pass | URP Unlit、Transparent、Alpha 混合、Both Faces、关闭深度写入与阴影；本实验对比卡面颜色输出 |

图和节点使用 Single 精度。卡牌驱动约定为正交相机、完整视口和轴对齐卡片；当前纹理使用默认 UV，不包含 HLSL 材质的额外 Tiling/Offset 功能。

## 节点组 01–03：计算 Δw

前三组与 HLSL 版的公式直接对应：

| 组 | 输入与运算 | 输出 |
| --- | --- | --- |
| 01 · Viewport pixels and midDistance | `Screen Position (Default).xy × Viewport Size`；减去半个视口尺寸，再计算 Length | 顶点像素坐标 $p$、顶点距屏幕中心的归一化距离 $d$ |
| 02 · Mouse distance squared | `p − Mouse Pixels`；除以 `max(Screen Scale, 1)`；Dot Product 自身 | 鼠标距离平方 $o\cdot o$ |
| 03 · Original deltaW formula | Subtract、Maximum、Multiply、Divide | 乘过 Strength 的 $\Delta w$ |

这些计算用于顶点阶段，因此 Screen Position 来自**未形变的顶点**。默认模式返回归一化屏幕坐标，并处理投影的 Y 方向；乘视口尺寸后，与驱动传入的左下原点像素坐标对应。

令 $S$ 为视口像素尺寸、$m$ 为鼠标像素坐标、$s$ 为距离尺度、$h$ 为 Hover、$k$ 为 Strength：

$$
d=\frac{\lVert p-S/2\rVert}{\lVert S\rVert},\qquad
o=\frac{p-m}{\max(s,1)}
$$

$$
\Delta w=k\cdot0.2\left[-0.03-0.3\max(0,0.3-d)\right]
h\frac{o\cdot o}{\max(2-d,0.1)}
$$

这部分适合对照源码学习：一个算术节点通常对应一个运算符或函数。参数含义及形变方向见 [[001_BalatroCardHover#顶点 Shader：计算 Δw]]。

## 节点组 04：把裁剪坐标反算成 Position

HLSL 可以直接输出四维裁剪坐标。Shader Graph 的 Vertex Position Block 接收对象空间三维位置，Unity 随后再为它执行模型、视图和投影变换。不能把 `clip.xyz` 直接接到 Position，也不能把修改后的 `w` 填进 Z。

设 $V\!P$ 为视图投影矩阵，$P_W$ 为原始世界空间位置。先求原始裁剪坐标，再替换它的第四个分量：

$$
C=V\!P\begin{pmatrix}P_W\\1\end{pmatrix},\qquad
C'=(C_x,C_y,C_z,\max(C_w+\Delta w,0.2))
$$

然后使用逆矩阵：

$$
Q=(V\!P)^{-1}C',\qquad
P'_W=Q_{xyz}/Q_w
$$

节点连接顺序：

1. `Position (World)` → Split → Combine，组成 $(x,y,z,1)$。
2. `Transformation Matrix (View Projection)` 接 Multiply 的 A，世界坐标接 B，得到 $C$。
3. Split $C$；W 加上 $\Delta w$，取与 0.2 的 Maximum；Combine 保留 XYZ，替换 W，得到 $C'$。
4. `Transformation Matrix (Inverse View Projection)` 乘 $C'$，得到 $Q$。
5. 将 $Q_{xyz}$ 除以 $Q_w$；通过 `Transform (World → Object, Position)` 接到 Vertex Position。

Unity 再次投影这个三维位置，得到的硬件裁剪坐标为：

$$
C_{SG}=V\!P\begin{pmatrix}Q_{xyz}/Q_w\\1\end{pmatrix}
=C'/Q_w
$$

在当前参数下 $Q_w>0$，$C_{SG}$ 和 $C'$ 的 NDC 坐标相同，因为齐次坐标整体缩放不改变透视除法结果。屏幕上的顶点位置因此一致。

这是 **Unity Reproduction Choice**：节点版通过改变三维位置获得目标投影，HLSL 版直接修改裁剪坐标。两者不具有相同的世界空间几何含义，不能据此认为光照、阴影、运动矢量或所有额外 Pass 都等价。当前方案还要求 $Q_w$ 非零；改变相机与几何条件后需要重新检查。

## 节点组 05–06：修正 UV 插值

只有轮廓一致还不够。GPU 插值 UV 会使用硬件裁剪坐标的 `w`。HLSL 的该值为 $C'_w$，节点版则为 $C'_w/Q_w$。若直接把 UV0 接入纹理采样，纹理插值通常不同；即使卡片边缘重合，格线仍可能偏移。

顶点组 05 将以下三维数据写入名为 `HoverUVQ` 的 Custom Interpolator：

$$
T=\frac{(u,v,1)}{Q_w}
$$

片元组 06 读取插值后的 $T$，重建纹理坐标：

$$
UV=T_{xy}/T_z
$$

连接过程：

1. `UV0` → Split → Combine，组成 $(u,v,1)$。
2. Divide by $Q_w$ → Vertex 的 `HoverUVQ` 自定义插值块，类型 Vector3。
3. 片元侧 `HoverUVQ (Custom Interpolator)` → Split。
4. Combine R/G，再除以 B；结果接 `Sample Texture 2D` 的 UV。
5. 采样 RGBA 接 Base Color，采样 Alpha 接 Alpha。

设 $\lambda_i$ 是三角形的屏幕空间重心权重，$w_i=C'_{w,i}$，$q_i=Q_{w,i}$。硬件使用 $w_i/q_i$ 插值后，UV 分子与分母相除得到：

$$
\frac{\sum_i\lambda_i(u_i,v_i)/w_i}{\sum_i\lambda_i/w_i}
$$

中间引入的 $q_i$ 被抵消，恢复了 HLSL 版的透视正确 UV 插值。Custom Interpolator 在这里负责顶点到片元的数据传递，不是自定义代码节点。

## 对照结果与适用范围

**Verified**：在 Unity 6000.6.0f1、URP/Shader Graph 17.6.0、Windows DX11 下，固定正交相机 size=3.5、1280×720、卡片 scale=(2.84,3.8,1)、距离尺度为卡片高度的 0.8 倍，比较两种材质的六组输出：Idle、Center、Top-left、Bottom-right、Zero Strength、Top-left / Strength=3。六组 PNG 均为 0 个差异像素。Shader 编译检查通过。

复查入口：在对照场景进入 Play，执行 `TechArtLab > 001 > Capture HLSL and Graph Comparison`。本机证据位于 `References/001_BalatroCardHover/analysis/shadergraph/`，包括 `hlsl_*.png`、`graph_*.png` 与 `pixel_comparison.json`；该目录仅本地保存。

这里验证的是**两种 Unity 实现的卡面输出**。原游戏坐标输入、动画时序和材质外观尚未完成匹配；其他图形 API、透视相机、旋转卡片和阴影也不在本次验证范围内。

## 学习顺序

本案例适合先读 HLSL，再用节点图对照前三组公式。修改 `clip.w` 在源码里只需要少量计算；节点组 04–06 增加的反算和插值修正，是为了适配 Shader Graph 接口，不应当被误认为原效果必需的全部步骤。

建议按以下顺序练习：

1. 理解 [[ClipSpaceW]] 中的裁剪坐标、透视除法与 UV 插值。
2. 阅读 HLSL 的坐标换算与 $\Delta w$，预测参数变化，再用固定状态验证。
3. 对照 Shader Graph 的前三组，熟悉向量、Dot、Length、Multiply、Maximum 等节点。
4. 最后阅读节点组 04–06，理解同样的屏幕形状为什么仍需要修正纹理插值。

学习普通材质的颜色混合、UV 滚动、遮罩和溶解时，可先用 Shader Graph 观察数据流；涉及裁剪坐标、插值、Pass 或渲染状态时，以 HLSL 为主更便于直接表达和检查机制。

## 接口参考

- [Unity Built-in Blocks：Vertex Position 的对象空间 Vector3 接口](https://docs.unity3d.com/cn/Packages/com.unity.shadergraph%4010.5/manual/Built-In-Blocks.html)
- [Unity Custom Interpolators：顶点到片元的数据传递](https://docs.unity.cn/Packages/com.unity.shadergraph%4017.3/manual/Custom-Interpolators.html)

以上是接口说明；本次实际节点序列化与实现以工程内 Shader Graph 17.6.0 源码和运行结果为准。
