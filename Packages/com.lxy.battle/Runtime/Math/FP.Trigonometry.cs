namespace Game.Battle.Core.Math
{
    public readonly partial struct FP
    {
        /// <summary>
        /// 公开的Degrees90数据。
        /// </summary>
        public static readonly FP Degrees90 = FromInt(90);
        /// <summary>
        /// 公开的Degrees180数据。
        /// </summary>
        public static readonly FP Degrees180 = FromInt(180);
        /// <summary>
        /// 公开的Degrees360数据。
        /// </summary>
        public static readonly FP Degrees360 = FromInt(360);

        private const long CordicScale = 1L << 30;
        private const long CordicGainInverse = 652032874L;

        private static readonly long[] CordicAnglesRaw =
        {
            450000L,
            265651L,
            140362L,
            71250L,
            35763L,
            17899L,
            8952L,
            4476L,
            2238L,
            1119L,
            560L,
            280L,
            140L,
            70L,
            35L,
            17L,
            9L,
            4L,
            2L,
            1L,
        };

        /// <summary>
        /// 角度制正弦。输入会被规整到 [-180, 180) 后使用整数 CORDIC。
        /// </summary>
        public static FP Sin(FP degrees)
        {
            SinCos(degrees, out FP sin, out _);
            return sin;
        }

        /// <summary>
        /// 角度制余弦。输入会被规整到 [-180, 180) 后使用整数 CORDIC。
        /// </summary>
        public static FP Cos(FP degrees)
        {
            SinCos(degrees, out _, out FP cos);
            return cos;
        }

        /// <summary>
        /// 执行计算正弦值计算余弦值相关逻辑。
        /// </summary>
        public static void SinCos(
            FP degrees,
            out FP sin,
            out FP cos)
        {
            long angle = NormalizeAngle(degrees).RawValue;
            long sign = 1L;
            if (angle > Degrees90.RawValue)
            {
                angle -= Degrees180.RawValue;
                sign = -1L;
            }
            else if (angle < -Degrees90.RawValue)
            {
                angle += Degrees180.RawValue;
                sign = -1L;
            }

            long x = CordicGainInverse;
            long y = 0L;
            long remaining = angle;
            for (int index = 0;
                 index < CordicAnglesRaw.Length;
                 index++)
            {
                long previousX = x;
                if (remaining >= 0L)
                {
                    x -= y >> index;
                    y += previousX >> index;
                    remaining -= CordicAnglesRaw[index];
                }
                else
                {
                    x += y >> index;
                    y -= previousX >> index;
                    remaining += CordicAnglesRaw[index];
                }
            }

            long sinRaw = ScaleCordic(y * sign);
            long cosRaw = ScaleCordic(x * sign);
            sin = FromRaw(ClampRawToUnit(sinRaw));
            cos = FromRaw(ClampRawToUnit(cosRaw));
        }

        /// <summary>
        /// 角度制 Atan2，返回范围为 [-180, 180]。
        /// </summary>
        public static FP Atan2(FP yValue, FP xValue)
        {
            long originalX = xValue.RawValue;
            long originalY = yValue.RawValue;
            if (originalX == 0L)
            {
                if (originalY > 0L)
                {
                    return Degrees90;
                }

                if (originalY < 0L)
                {
                    return -Degrees90;
                }

                return Zero;
            }

            if (originalY == 0L)
            {
                return originalX > 0L ? Zero : Degrees180;
            }

            NormalizeCordicInput(
                originalX,
                originalY,
                out long x,
                out long y);
            long angle = 0L;
            if (x < 0L)
            {
                bool upperHalf = y >= 0L;
                x = -x;
                y = -y;
                angle = upperHalf
                    ? Degrees180.RawValue
                    : -Degrees180.RawValue;
            }

            for (int index = 0;
                 index < CordicAnglesRaw.Length;
                 index++)
            {
                long previousX = x;
                if (y > 0L)
                {
                    x += y >> index;
                    y -= previousX >> index;
                    angle += CordicAnglesRaw[index];
                }
                else
                {
                    x -= y >> index;
                    y += previousX >> index;
                    angle -= CordicAnglesRaw[index];
                }
            }

            return FromRaw(angle);
        }

        /// <summary>
        /// 执行规范化Angle相关逻辑。
        /// </summary>
        public static FP NormalizeAngle(FP degrees)
        {
            long normalized =
                degrees.RawValue % Degrees360.RawValue;
            if (normalized >= Degrees180.RawValue)
            {
                normalized -= Degrees360.RawValue;
            }
            else if (normalized < -Degrees180.RawValue)
            {
                normalized += Degrees360.RawValue;
            }

            return FromRaw(normalized);
        }

        /// <summary>
        /// 执行缩放Cordic相关逻辑。
        /// </summary>
        private static long ScaleCordic(long value)
        {
            long scaled = checked(value * Precision);
            long half = CordicScale >> 1;
            return scaled >= 0L
                ? checked(scaled + half) / CordicScale
                : checked(scaled - half) / CordicScale;
        }

        /// <summary>
        /// 执行限制原始值转换为Unit相关逻辑。
        /// </summary>
        private static long ClampRawToUnit(long value)
        {
            if (value > Precision)
            {
                return Precision;
            }

            return value < -Precision ? -Precision : value;
        }

        /// <summary>
        /// 执行规范化Cordic输入相关逻辑。
        /// </summary>
        private static void NormalizeCordicInput(
            long sourceX,
            long sourceY,
            out long x,
            out long y)
        {
            x = sourceX;
            y = sourceY;
            ulong maximum = MaxMagnitude(sourceX, sourceY);
            while (maximum > (1UL << 30))
            {
                x >>= 1;
                y >>= 1;
                maximum >>= 1;
            }

            while (maximum < (1UL << 29))
            {
                x <<= 1;
                y <<= 1;
                maximum <<= 1;
            }
        }

        /// <summary>
        /// 执行MaxMagnitude相关逻辑。
        /// </summary>
        private static ulong MaxMagnitude(long left, long right)
        {
            ulong leftMagnitude = GetMagnitude(left);
            ulong rightMagnitude = GetMagnitude(right);
            return leftMagnitude >= rightMagnitude
                ? leftMagnitude
                : rightMagnitude;
        }
    }
}
