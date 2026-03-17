using System;

namespace LightCtrl
{
    /// <summary>
    /// 面向初学者的简化 FOC（Field Oriented Control，磁场定向控制）示例。
    ///
    /// 这个类每调用一次 <see cref="Step"/>，就完成一次“电流环控制周期”：
    /// 1. 读取三相电流 ia/ib/ic；
    /// 2. 通过 Clarke 变换进入静止坐标系(alpha/beta)；
    /// 3. 通过 Park 变换进入旋转坐标系(d/q)；
    /// 4. 在 d/q 轴上分别做 PI 控制，得到 vd/vq；
    /// 5. 反变换回三相电压 va/vb/vc；
    /// 6. 做一个简单的电压限幅，避免超过母线电压能力。
    ///
    /// 注意：本实现强调“易读和理解”，不等同于量产级电机控制固件。
    /// </summary>
    public sealed class SimpleFocController
    {
        // -------------- 基本参数（构造时给定）--------------

        /// <summary>
        /// 直流母线电压（例如 24V、48V）。
        /// </summary>
        private readonly float _busVoltage;

        /// <summary>
        /// d 轴 PI 参数（比例项）。
        /// </summary>
        private readonly float _idKp;

        /// <summary>
        /// d 轴 PI 参数（积分项）。
        /// </summary>
        private readonly float _idKi;

        /// <summary>
        /// q 轴 PI 参数（比例项）。
        /// </summary>
        private readonly float _iqKp;

        /// <summary>
        /// q 轴 PI 参数（积分项）。
        /// </summary>
        private readonly float _iqKi;

        // -------------- 控制器状态（每次 Step 会更新）--------------

        /// <summary>
        /// d 轴 PI 积分状态。
        /// </summary>
        private float _idIntegral;

        /// <summary>
        /// q 轴 PI 积分状态。
        /// </summary>
        private float _iqIntegral;

        /// <summary>
        /// 创建一个简易 FOC 控制器。
        /// </summary>
        public SimpleFocController(float busVoltage, float idKp, float idKi, float iqKp, float iqKi)
        {
            _busVoltage = busVoltage;
            _idKp = idKp;
            _idKi = idKi;
            _iqKp = iqKp;
            _iqKi = iqKi;
        }

        /// <summary>
        /// 执行一次 FOC 电流环控制。
        /// </summary>
        /// <param name="input">输入采样和控制目标。</param>
        /// <returns>本周期计算得到的 d/q 电流及三相电压。</returns>
        public FocOutput Step(FocInput input)
        {
            // 保护：采样周期必须为正。若无效，则返回全 0，避免异常控制输出。
            if (input.Dt <= 0.0f)
            {
                return new FocOutput(0.0f, 0.0f, 0.0f, 0.0f, 0.0f);
            }

            // Step-1: 三相电流 -> 静止坐标系(alpha/beta)
            AlphaBeta iAlphaBeta = Clarke(input.Ia, input.Ib, input.Ic);

            // Step-2: alpha/beta -> 旋转坐标系(d/q)
            Dq iDq = Park(iAlphaBeta.Alpha, iAlphaBeta.Beta, input.ElectricalAngle);

            // Step-3: d/q 电流 PI 控制，得到 d/q 轴电压指令 vd/vq
            float vd = Pi(
                reference: input.IdRef,
                feedback: iDq.D,
                kp: _idKp,
                ki: _idKi,
                integral: ref _idIntegral,
                dt: input.Dt);

            float vq = Pi(
                reference: input.IqRef,
                feedback: iDq.Q,
                kp: _iqKp,
                ki: _iqKi,
                integral: ref _iqIntegral,
                dt: input.Dt);

            // Step-4: 反 Park，把电压从 d/q 旋转坐标系转回 alpha/beta
            AlphaBeta vAlphaBeta = InversePark(vd, vq, input.ElectricalAngle);

            // Step-5: 反 Clarke，把 alpha/beta 转回三相电压 va/vb/vc
            PhaseAbc vAbc = InverseClarke(vAlphaBeta.Alpha, vAlphaBeta.Beta);

            // Step-6: 简易限幅（按相电压最大绝对值等比例缩放）
            LimitPhaseVoltage(ref vAbc);

            // 返回：观测到的 d/q 电流 + 计算得到的三相电压命令
            return new FocOutput(iDq.D, iDq.Q, vAbc.Va, vAbc.Vb, vAbc.Vc);
        }

        /// <summary>
        /// 离散 PI 控制器：u = kp*e + integral。
        /// </summary>
        private float Pi(float reference, float feedback, float kp, float ki, ref float integral, float dt)
        {
            float error = reference - feedback;

            // 积分项离散更新：integral[k] = integral[k-1] + ki*e*dt
            integral += error * ki * dt;

            return kp * error + integral;
        }

        /// <summary>
        /// 三相电压限幅：如果任一相超过 |Vbus/2|，按比例缩放三相。
        /// 这是“最简单”的实现，真实工程常用 SVPWM + 更严格的限幅策略。
        /// </summary>
        private void LimitPhaseVoltage(ref PhaseAbc phase)
        {
            float maxAbs = Max3(Abs(phase.Va), Abs(phase.Vb), Abs(phase.Vc));
            float limit = _busVoltage * 0.5f;

            if (maxAbs > limit && maxAbs > 0.0f)
            {
                float scale = limit / maxAbs;
                phase = new PhaseAbc(phase.Va * scale, phase.Vb * scale, phase.Vc * scale);
            }
        }

        /// <summary>
        /// Clarke 变换（abc -> alpha/beta）。
        /// 这里采用“平衡三相”常见简化写法，便于初学者理解。
        /// </summary>
        private static AlphaBeta Clarke(float ia, float ib, float ic)
        {
            // 当前简化公式没有直接使用 ic；在平衡三相中 ia + ib + ic ≈ 0。
            // 如果要更严谨，可改为矩阵形式并显式包含 ic。
            float alpha = ia;
            float beta = (ia + 2.0f * ib) / 1.7320508f; // 1.732... = sqrt(3)

            return new AlphaBeta(alpha, beta);
        }

        /// <summary>
        /// Park 变换（alpha/beta -> d/q）。
        /// theta 为电角度（单位：弧度）。
        /// </summary>
        private static Dq Park(float alpha, float beta, float theta)
        {
            float cos = (float)Math.Cos(theta);
            float sin = (float)Math.Sin(theta);

            float d = alpha * cos + beta * sin;
            float q = -alpha * sin + beta * cos;

            return new Dq(d, q);
        }

        /// <summary>
        /// 反 Park 变换（d/q -> alpha/beta）。
        /// </summary>
        private static AlphaBeta InversePark(float d, float q, float theta)
        {
            float cos = (float)Math.Cos(theta);
            float sin = (float)Math.Sin(theta);

            float alpha = d * cos - q * sin;
            float beta = d * sin + q * cos;

            return new AlphaBeta(alpha, beta);
        }

        /// <summary>
        /// 反 Clarke 变换（alpha/beta -> abc）。
        /// 得到三相电压参考值，用于后续 PWM 调制。
        /// </summary>
        private static PhaseAbc InverseClarke(float alpha, float beta)
        {
            float va = alpha;
            float vb = -0.5f * alpha + 0.8660254f * beta;  // 0.866... = sqrt(3)/2
            float vc = -0.5f * alpha - 0.8660254f * beta;

            return new PhaseAbc(va, vb, vc);
        }

        private static float Abs(float x)
        {
            return x >= 0.0f ? x : -x;
        }

        private static float Max3(float a, float b, float c)
        {
            float m = a > b ? a : b;
            return m > c ? m : c;
        }
    }

    /// <summary>
    /// FOC 输入：一个控制周期中需要的测量值和目标值。
    /// </summary>
    public struct FocInput
    {
        /// <summary>三相电流 A 相（单位 A）。</summary>
        public readonly float Ia;

        /// <summary>三相电流 B 相（单位 A）。</summary>
        public readonly float Ib;

        /// <summary>三相电流 C 相（单位 A）。</summary>
        public readonly float Ic;

        /// <summary>电角度 theta（单位 rad）。</summary>
        public readonly float ElectricalAngle;

        /// <summary>d 轴电流目标（常设为 0，单位 A）。</summary>
        public readonly float IdRef;

        /// <summary>q 轴电流目标（决定电磁转矩，单位 A）。</summary>
        public readonly float IqRef;

        /// <summary>控制周期时长（单位 s）。</summary>
        public readonly float Dt;

        public FocInput(float ia, float ib, float ic, float electricalAngle, float idRef, float iqRef, float dt)
        {
            Ia = ia;
            Ib = ib;
            Ic = ic;
            ElectricalAngle = electricalAngle;
            IdRef = idRef;
            IqRef = iqRef;
            Dt = dt;
        }
    }

    /// <summary>
    /// FOC 输出：控制器估算/变换后的电流与电压命令。
    /// </summary>
    public struct FocOutput
    {
        /// <summary>d 轴电流反馈（单位 A）。</summary>
        public readonly float Id;

        /// <summary>q 轴电流反馈（单位 A）。</summary>
        public readonly float Iq;

        /// <summary>A 相电压命令（单位 V）。</summary>
        public readonly float Va;

        /// <summary>B 相电压命令（单位 V）。</summary>
        public readonly float Vb;

        /// <summary>C 相电压命令（单位 V）。</summary>
        public readonly float Vc;

        public FocOutput(float id, float iq, float va, float vb, float vc)
        {
            Id = id;
            Iq = iq;
            Va = va;
            Vb = vb;
            Vc = vc;
        }
    }

    /// <summary>
    /// 静止坐标系分量（alpha/beta）。
    /// </summary>
    public struct AlphaBeta
    {
        public readonly float Alpha;
        public readonly float Beta;

        public AlphaBeta(float alpha, float beta)
        {
            Alpha = alpha;
            Beta = beta;
        }
    }

    /// <summary>
    /// 旋转坐标系分量（d/q）。
    /// </summary>
    public struct Dq
    {
        public readonly float D;
        public readonly float Q;

        public Dq(float d, float q)
        {
            D = d;
            Q = q;
        }
    }

    /// <summary>
    /// 三相分量（a/b/c）。
    /// </summary>
    public struct PhaseAbc
    {
        public readonly float Va;
        public readonly float Vb;
        public readonly float Vc;

        public PhaseAbc(float va, float vb, float vc)
        {
            Va = va;
            Vb = vb;
            Vc = vc;
        }
    }
}
