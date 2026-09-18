# 001 · Balatro Card Hover

最小实现根据鼠标与四个顶点的屏幕距离，分别修改顶点的裁剪空间 `w`，形成悬停透视形变。卡牌 Transform 保持不旋转。

投影机制见 [[Concepts/ClipSpaceW|裁剪空间 w]]。以下记录 Unity 实现与检查方法。

节点实现与对应关系见 [[001_BalatroCardHover_ShaderGraph]]，其中说明了 Shader Graph 顶点接口的适配和 UV 插值修正。

> [!info] 实现范围
> 当前版本使用 Unity 6000.6.0f1、URP 17.6.0、正交相机和轴对齐的四顶点卡片。它实现卡面形变与悬停过渡，不包含溶解、阴影和原作美术资源。

## 整体数据流

1. `CardHoverDemo` 读取鼠标的像素坐标，计算悬停值、距离尺度和视口尺寸。
2. 脚本通过 `MaterialPropertyBlock` 把这些值传给顶点 Shader。
3. 顶点 Shader 计算各角到鼠标的距离，据此求出各自的 $\Delta w$ 并修改 `clip.w`。
4. GPU 对修改后的裁剪坐标做透视除法，并插值卡面纹理。

实现由四部分组成：

| 资源 | 职责 |
| --- | --- |
| `Models/FourVertexQuad.asset` | 提供四个角和两个三角形 |
| `Shaders/CardHover.shader` | 计算每个角的 $\Delta w$ 并采样卡面纹理 |
| `Scripts/CardHoverDemo.cs` | 读取鼠标、计算悬停状态并传入 Shader |
| `Scenes/001_CardHover_Minimal.unity` | 固定相机、卡片和材质的最小场景 |

资源都位于 Unity 工程的 `Assets/_Lab/Experiments/001_BalatroCardHover/`。

## 卡片几何：四顶点 Quad

使用中心在原点、宽高各为 1 的 Quad：

```csharp
vertices = new[]
{
    new Vector3(-.5f, -.5f, 0), // 左下
    new Vector3( .5f, -.5f, 0), // 右下
    new Vector3( .5f,  .5f, 0), // 右上
    new Vector3(-.5f,  .5f, 0), // 左上
};

uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
triangles = new[] { 0, 2, 1, 0, 3, 2 };
```

当前公式只为四个角计算不同的 `w`，无需内部细分。

GPU 可能把角推出原始矩形，因此需要适当扩大 `mesh.bounds`，避免卡片仍在屏幕内却被 CPU 视锥剔除。

## 坐标约定与参数传递

CPU 与 Shader 使用相同的视口坐标约定：

- 原点在视口左下角；
- 单位为像素；
- 鼠标位置先减去 `camera.pixelRect` 的偏移；
- Shader 中的顶点位置先按**未形变状态**投影到相同的视口像素坐标。

`CardHoverDemo` 先计算未形变卡片的屏幕矩形，再准备以下参数：

| 参数 | 含义 |
| --- | --- |
| `_MousePixels` | 鼠标在当前视口内的像素坐标 |
| `_ViewportSize` | 相机视口的像素宽高 |
| `_ScreenScale` | 距离归一化尺度，当前取卡片屏幕高度的 0.8 倍 |
| `_Hover` | 0 表示恢复，1 表示完整形变 |
| `_Strength` | 额外暴露的整体强度 |

使用 `MaterialPropertyBlock` 传值，避免每张卡都实例化一份材质。

### 悬停过渡

鼠标位于未形变矩形内时，目标值 $h^*=1$，否则为 0。当前值使用指数形式接近目标：

$$
h_{new}=\operatorname{lerp}\left(h,h^*,1-e^{-r\Delta t}\right)
$$

$r$ 是响应速度。与直接使用固定插值系数相比，这种写法在不同帧率下更一致。

## 顶点 Shader：计算 Δw

### 1. 得到未形变裁剪坐标

```hlsl
float4 clip = TransformObjectToHClip(input.positionOS);
```

### 2. 把顶点换算到视口像素

```hlsl
float2 ndc = clip.xy / clip.w;
ndc.y *= _ProjectionParams.x;
float2 pixels = (ndc * 0.5 + 0.5) * _ViewportSize.xy;
```

`pixels` 与 `_MousePixels` 都采用左下原点的像素坐标。像素位置由未形变的 `clip` 计算，避免形变结果再次参与距离计算。

### 3. 构造距离量

令 $p$ 为当前顶点像素坐标，$S$ 为视口尺寸，$m$ 为鼠标像素坐标，$s$ 为距离归一化尺度：

$$
d=\frac{\lVert p-S/2\rVert}{\lVert S\rVert},\qquad
o=\frac{p-m}{s}
$$

- $d$ 表示顶点离屏幕中心有多远。
- $o$ 表示顶点离鼠标有多远，并用 $s$ 消除像素尺寸带来的量级差异。
- $o\cdot o$ 是归一化距离的平方。

对应 HLSL：

```hlsl
float midDistance = length(pixels - 0.5 * _ViewportSize.xy)
                  / length(_ViewportSize.xy);
float2 mouseOffset = (pixels - _MousePixels.xy)
                   / max(_ScreenScale, 1.0);
```

### 4. 计算并写入 w

当前复现保留了参考公式的系数：

$$
\Delta w=
0.2\left[-0.03-0.3\max(0,0.3-d)\right]
h\frac{o\cdot o}{\max(2-d,0.1)}
$$

```hlsl
float deltaW = 0.2 * (-0.03 - 0.3 * max(0.0, 0.3 - midDistance))
             * _Hover * dot(mouseOffset, mouseOffset)
             / max(2.0 - midDistance, 0.1);

clip.w = max(clip.w + deltaW * _Strength, 0.2);
output.positionCS = clip;
```

在当前取值范围内，$\Delta w$ 通常为负。离鼠标越远的角，$o\cdot o$ 越大，`w` 减少得越多，因此该角在透视除法后会更远离视口中心。四个角的变化量不同，就形成了倾斜感。

`0.2` 的下限只是安全措施，用来避免 `w` 接近 0；它不是效果公式的一部分。

## 像素 Shader：采样卡面

像素阶段采样卡面纹理：

```hlsl
return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
```

默认的透视正确插值会使用各顶点的 `w` 修正 UV，因此纹理会随四边形一起产生透视变化。材质使用普通 Alpha 混合，当前 Pass 不写深度。

## 验证顺序

先固定输入验证公式，再检查实时鼠标和缓动：

1. **`_Strength=0`**：应与原始矩形完全一致。否则问题不在形变公式。
2. **`_Hover=0`**：也应恢复原状，用来确认悬停开关没有残留偏移。
3. **鼠标固定在卡片中心**：居中的卡片主要表现为整体放大，因为四角到鼠标的距离接近。
4. **鼠标固定在左上附近**：右下角离鼠标最远，应得到更负的 $\Delta w$，并更明显地远离视口中心。
5. **改用右下输入**：形变方向应与上一步相反。若没有反转，优先检查 Y 轴方向和鼠标坐标原点。
6. **实时鼠标与缓动**：固定状态正确后再检查输入响应与恢复时间。

测试纹理在左上和右下使用不同颜色，便于识别坐标是否翻转。

## 运行现有场景

打开：

`Assets/_Lab/Experiments/001_BalatroCardHover/Scenes/001_CardHover_Minimal.unity`

进入 Play 后可使用：

| 操作 | 作用 |
| --- | --- |
| `1` / Mouse | 实时鼠标输入 |
| `0` / Idle | 固定无形变 |
| `2` / Center | 固定在卡片中心 |
| `3` / Top-left | 固定在卡片左上附近 |
| `4` / Bottom-right | 固定在卡片右下附近 |
| Strength 滑条 | 调整整体形变强度 |

也可使用菜单 `TechArtLab > 001 > Create or Open Minimal` 打开场景。场景已存在时，该菜单不会覆盖它。

## 当前实现的边界

- CPU 端的矩形命中检测只适用于轴对齐卡片与当前正交相机配置。
- 只修改 `w` 的缩放中心是视口中心，因此卡片离开屏幕中心后会出现额外位置漂移。
- 参考顶点代码的输入坐标语义和 CPU 更新逻辑并不完整；这里先把顶点与鼠标统一到视口像素，是 Unity 复现选择，不声称与原实现逐字等价。
- 当前只实现最小卡面 Pass。阴影、溶解和原作动画时序应作为独立功能继续添加，而不是混进 `w` 形变公式。
