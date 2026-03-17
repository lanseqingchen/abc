# abc

## 简易 FOC 算法示例（C#，初学者向）

我把示例代码放在：`LightCtrl/LightCtrl/SimpleFocController.cs`。

它不是“完整工业控制器”，而是一个可以帮助你理解 FOC 主流程的**教学版本**：

1. 三相电流 `ia/ib/ic` 做 Clarke 变换得到 `alpha/beta`
2. `alpha/beta` 做 Park 变换得到 `d/q`
3. 在 `d/q` 轴上分别做 PI 控制，得到 `vd/vq`
4. 再通过反 Park + 反 Clarke 变换回 `va/vb/vc`
5. 对三相电压做简单限幅

---

## 代码里都做了什么（逐步解释）

### 1) 输入数据（`FocInput`）

每次控制周期要提供：

- `Ia/Ib/Ic`：三相电流采样（单位 A）
- `ElectricalAngle`：电角度（单位 rad）
- `IdRef`：d 轴电流目标（表贴式 PMSM 常设为 0）
- `IqRef`：q 轴电流目标（主要决定转矩）
- `Dt`：控制周期（单位 s）

### 2) 主入口（`Step`）

`Step` 表示“执行一次电流环控制”。

- 先做参数保护：`Dt <= 0` 时返回 0，避免错误输出。
- 然后按 FOC 顺序计算。
- 最后返回 `FocOutput`（包含 `Id/Iq` 和 `Va/Vb/Vc`）。

### 3) PI 控制器（`Pi`）

采用离散 PI 形式：

- `error = reference - feedback`
- `integral += ki * error * dt`
- `output = kp * error + integral`

这是最基础、最容易读懂的写法。

### 4) 限幅（`LimitPhaseVoltage`）

将 `Va/Vb/Vc` 的最大绝对值限制在 `Vbus/2` 内。

- 如果超限：三相按相同比例缩放。
- 这样可保持三相相对关系不变。

---

## 最小调用示例

```csharp
var foc = new SimpleFocController(
    busVoltage: 24.0f,
    idKp: 1.2f,
    idKi: 150.0f,
    iqKp: 1.2f,
    iqKi: 150.0f);

var input = new FocInput(
    ia: 1.1f,
    ib: -0.4f,
    ic: -0.7f,
    electricalAngle: 0.35f,
    idRef: 0.0f,
    iqRef: 2.5f,
    dt: 0.0001f);

FocOutput output = foc.Step(input);

// output.Va / output.Vb / output.Vc
// 可作为后续 PWM（如 SVPWM）的电压参考
```

---

## 给初学者的调参建议

1. 先固定 `IdRef = 0`，只调 q 轴。
2. `Ki` 不要一开始太大，先小值稳定后再增加。
3. 控制周期 `Dt` 要稳定（例如 10kHz 对应 0.0001s）。
4. 先在低电压/空载下验证方向和极性。

---

## 与 ODrive / 工程化实现的差异

本示例为了易懂，省略了很多工程细节，例如：

- 积分抗饱和（anti-windup）
- 电流采样滤波与校准
- 前馈解耦项（例如 L*di/dt、反电动势相关项）
- 完整 SVPWM 扇区计算与死区补偿
- 速度环/位置环级联与保护状态机

如果你希望，我可以下一步给你补：

- 一个可直接输出占空比的简化 `SVPWM`
- 外环速度 PI + 内环电流 PI 的串级示例
- 更接近 ODrive 命名风格的参数结构体
