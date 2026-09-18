using System;

namespace Game.Battle.Core.Math
{
    /// <summary>
    /// 万分位定点数。所有运算都使用确定性的整数算法。
    /// 运算结果超出 long 范围时抛出 OverflowException，禁止静默回绕。
    /// </summary>
    public readonly partial struct FP :
        IEquatable<FP>,
        IComparable<FP>
    {
        /// <summary>
        /// 公开的Precision数据。
        /// </summary>
        public const long Precision = 10000L;

        private const ulong NegativeLimit = 1UL << 63;

        /// <summary>
        /// 公开的最小值数据。
        /// </summary>
        public static readonly FP MinValue = new FP(long.MinValue);
        /// <summary>
        /// 公开的最大值数据。
        /// </summary>
        public static readonly FP MaxValue = new FP(long.MaxValue);
        /// <summary>
        /// 公开的Zero数据。
        /// </summary>
        public static readonly FP Zero = new FP(0L);
        /// <summary>
        /// 公开的One数据。
        /// </summary>
        public static readonly FP One = new FP(Precision);

        /// <summary>
        /// 创建定点数实例。
        /// </summary>
        public FP(long rawValue)
        {
            RawValue = rawValue;
        }

        /// <summary>
        /// 向调用方提供原始值值。
        /// </summary>
        public long RawValue { get; }

        /// <summary>
        /// 执行从原始值相关逻辑。
        /// </summary>
        public static FP FromRaw(long value) => new FP(value);

        /// <summary>
        /// 执行从整数相关逻辑。
        /// </summary>
        public static FP FromInt(int value) =>
            new FP(checked((long)value * Precision));

        /// <summary>
        /// 执行从长整数相关逻辑。
        /// </summary>
        public static FP FromLong(long value)
        {
            if (!TryMultiplyDivide(
                    value,
                    Precision,
                    1L,
                    out long rawValue))
            {
                throw CreateOverflow(
                    "整数转换",
                    value,
                    Precision);
            }

            return new FP(rawValue);
        }

        /// <summary>
        /// 执行从Ratio相关逻辑。
        /// </summary>
        public static FP FromRatio(long numerator, long denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException(
                    "FP 比例转换的分母不能为 0。");
            }

            if (!TryMultiplyDivide(
                    numerator,
                    Precision,
                    denominator,
                    out long rawValue))
            {
                throw CreateOverflow(
                    "比例转换",
                    numerator,
                    denominator);
            }

            return new FP(rawValue);
        }

        /// <summary>
        /// 将定点数向下取整为整数。
        /// </summary>
        public int FloorToInt()
        {
            long result = RawValue / Precision;
            if (RawValue < 0 && RawValue % Precision != 0)
            {
                result--;
            }

            return checked((int)result);
        }

        /// <summary>
        /// 计算绝对值。
        /// </summary>
        public static FP Abs(FP value)
        {
            if (value.RawValue == long.MinValue)
            {
                throw CreateOverflow(
                    "绝对值",
                    value.RawValue,
                    0L);
            }

            return value.RawValue >= 0
                ? value
                : new FP(-value.RawValue);
        }

        /// <summary>
        /// 执行Min相关逻辑。
        /// </summary>
        public static FP Min(FP left, FP right) =>
            left <= right ? left : right;

        /// <summary>
        /// 执行Max相关逻辑。
        /// </summary>
        public static FP Max(FP left, FP right) =>
            left >= right ? left : right;

        /// <summary>
        /// 将数值限制在指定范围内。
        /// </summary>
        public static FP Clamp(FP value, FP min, FP max)
        {
            if (min > max)
            {
                throw new ArgumentException(
                    "FP.Clamp 要求 min 小于或等于 max。");
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <summary>
        /// 执行Sqrt相关逻辑。
        /// </summary>
        public static FP Sqrt(FP value)
        {
            if (value.RawValue < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "不能对负定点数开平方。");
            }

            UInt128 scaled = MultiplyUnsigned(
                (ulong)value.RawValue,
                (ulong)Precision);
            return new FP((long)IntegerSqrt(scaled));
        }

        /// <summary>
        /// 尝试加法，并返回是否成功。
        /// </summary>
        public static bool TryAdd(FP left, FP right, out FP result)
        {
            long leftRaw = left.RawValue;
            long rightRaw = right.RawValue;
            if ((rightRaw > 0 && leftRaw > long.MaxValue - rightRaw) ||
                (rightRaw < 0 && leftRaw < long.MinValue - rightRaw))
            {
                result = Zero;
                return false;
            }

            result = new FP(leftRaw + rightRaw);
            return true;
        }

        /// <summary>
        /// 尝试减法，并返回是否成功。
        /// </summary>
        public static bool TrySubtract(
            FP left,
            FP right,
            out FP result)
        {
            long leftRaw = left.RawValue;
            long rightRaw = right.RawValue;
            if ((rightRaw > 0 && leftRaw < long.MinValue + rightRaw) ||
                (rightRaw < 0 && leftRaw > long.MaxValue + rightRaw))
            {
                result = Zero;
                return false;
            }

            result = new FP(leftRaw - rightRaw);
            return true;
        }

        /// <summary>
        /// 尝试乘法，并返回是否成功。
        /// </summary>
        public static bool TryMultiply(
            FP left,
            FP right,
            out FP result)
        {
            if (!TryMultiplyDivide(
                    left.RawValue,
                    right.RawValue,
                    Precision,
                    out long rawValue))
            {
                result = Zero;
                return false;
            }

            result = new FP(rawValue);
            return true;
        }

        /// <summary>
        /// 尝试除法，并返回是否成功。
        /// </summary>
        public static bool TryDivide(
            FP left,
            FP right,
            out FP result)
        {
            if (right.RawValue == 0 ||
                !TryMultiplyDivide(
                    left.RawValue,
                    Precision,
                    right.RawValue,
                    out long rawValue))
            {
                result = Zero;
                return false;
            }

            result = new FP(rawValue);
            return true;
        }

        /// <summary>
        /// 执行 + 运算符重载。
        /// </summary>
        public static FP operator +(FP left, FP right)
        {
            if (!TryAdd(left, right, out FP result))
            {
                throw CreateOverflow(
                    "加法",
                    left.RawValue,
                    right.RawValue);
            }

            return result;
        }

        /// <summary>
        /// 执行 - 运算符重载。
        /// </summary>
        public static FP operator -(FP left, FP right)
        {
            if (!TrySubtract(left, right, out FP result))
            {
                throw CreateOverflow(
                    "减法",
                    left.RawValue,
                    right.RawValue);
            }

            return result;
        }

        /// <summary>
        /// 执行 - 运算符重载。
        /// </summary>
        public static FP operator -(FP value)
        {
            if (value.RawValue == long.MinValue)
            {
                throw CreateOverflow(
                    "取负",
                    value.RawValue,
                    0L);
            }

            return new FP(-value.RawValue);
        }

        /// <summary>
        /// 执行 * 运算符重载。
        /// </summary>
        public static FP operator *(FP left, FP right)
        {
            if (!TryMultiply(left, right, out FP result))
            {
                throw CreateOverflow(
                    "乘法",
                    left.RawValue,
                    right.RawValue);
            }

            return result;
        }

        /// <summary>
        /// 执行 / 运算符重载。
        /// </summary>
        public static FP operator /(FP left, FP right)
        {
            if (right.RawValue == 0)
            {
                throw new DivideByZeroException(
                    "FP 除数不能为 0。");
            }

            if (!TryDivide(left, right, out FP result))
            {
                throw CreateOverflow(
                    "除法",
                    left.RawValue,
                    right.RawValue);
            }

            return result;
        }

        /// <summary>
        /// 执行 == 运算符重载。
        /// </summary>
        public static bool operator ==(FP left, FP right) =>
            left.RawValue == right.RawValue;

        /// <summary>
        /// 执行 != 运算符重载。
        /// </summary>
        public static bool operator !=(FP left, FP right) =>
            left.RawValue != right.RawValue;

        /// <summary>
        /// 执行 < 运算符重载。
        /// </summary>
        public static bool operator <(FP left, FP right) =>
            left.RawValue < right.RawValue;

        /// <summary>
        /// 执行 > 运算符重载。
        /// </summary>
        public static bool operator >(FP left, FP right) =>
            left.RawValue > right.RawValue;

        /// <summary>
        /// 执行 <= 运算符重载。
        /// </summary>
        public static bool operator <=(FP left, FP right) =>
            left.RawValue <= right.RawValue;

        /// <summary>
        /// 执行 >= 运算符重载。
        /// </summary>
        public static bool operator >=(FP left, FP right) =>
            left.RawValue >= right.RawValue;

        /// <summary>
        /// 比较当前实例与指定对象是否相等。
        /// </summary>
        public bool Equals(FP other) =>
            RawValue == other.RawValue;

        /// <summary>
        /// 比较当前实例与指定对象是否相等。
        /// </summary>
        public override bool Equals(object obj) =>
            obj is FP other && Equals(other);

        /// <summary>
        /// 获取当前实例的哈希代码。
        /// </summary>
        public override int GetHashCode() =>
            RawValue.GetHashCode();

        /// <summary>
        /// 比较当前实例与指定对象的排序关系。
        /// </summary>
        public int CompareTo(FP other) =>
            RawValue.CompareTo(other.RawValue);

        /// <summary>
        /// 生成当前实例的字符串表示。
        /// </summary>
        public override string ToString()
        {
            ulong magnitude = GetMagnitude(RawValue);
            ulong whole = magnitude / (ulong)Precision;
            ulong fraction = magnitude % (ulong)Precision;
            string sign = RawValue < 0 ? "-" : string.Empty;
            return $"{sign}{whole}.{fraction:D4}";
        }

        /// <summary>
        /// 尝试执行乘除运算，并检测计算溢出。
        /// </summary>
        private static bool TryMultiplyDivide(
            long left,
            long right,
            long divisor,
            out long result)
        {
            if (divisor == 0)
            {
                result = 0L;
                return false;
            }

            bool isNegative =
                (left < 0) ^ (right < 0) ^ (divisor < 0);
            UInt128 product = MultiplyUnsigned(
                GetMagnitude(left),
                GetMagnitude(right));
            if (!TryDivideUnsigned(
                    product,
                    GetMagnitude(divisor),
                    out ulong quotient))
            {
                result = 0L;
                return false;
            }

            ulong limit = isNegative
                ? NegativeLimit
                : (ulong)long.MaxValue;
            if (quotient > limit)
            {
                result = 0L;
                return false;
            }

            if (!isNegative)
            {
                result = (long)quotient;
                return true;
            }

            result = quotient == NegativeLimit
                ? long.MinValue
                : -(long)quotient;
            return true;
        }

        /// <summary>
        /// 执行MultiplyUnsigned相关逻辑。
        /// </summary>
        private static UInt128 MultiplyUnsigned(
            ulong left,
            ulong right)
        {
            ulong leftLow = (uint)left;
            ulong leftHigh = left >> 32;
            ulong rightLow = (uint)right;
            ulong rightHigh = right >> 32;

            unchecked
            {
                ulong lowLow = leftLow * rightLow;
                ulong lowHigh = leftLow * rightHigh;
                ulong highLow = leftHigh * rightLow;
                ulong highHigh = leftHigh * rightHigh;
                ulong middle =
                    (lowLow >> 32) +
                    (uint)lowHigh +
                    (uint)highLow;

                return new UInt128(
                    highHigh +
                    (lowHigh >> 32) +
                    (highLow >> 32) +
                    (middle >> 32),
                    (middle << 32) | (uint)lowLow);
            }
        }

        /// <summary>
        /// 尝试除法Unsigned，并返回是否成功。
        /// </summary>
        private static bool TryDivideUnsigned(
            UInt128 value,
            ulong divisor,
            out ulong quotient)
        {
            if (divisor == 0 || value.High >= divisor)
            {
                quotient = 0UL;
                return false;
            }

            if (value.High == 0)
            {
                quotient = value.Low / divisor;
                return true;
            }

            ulong result = 0UL;
            ulong remainder = 0UL;
            for (int bitIndex = 127; bitIndex >= 0; bitIndex--)
            {
                ulong nextBit = bitIndex >= 64
                    ? (value.High >> (bitIndex - 64)) & 1UL
                    : (value.Low >> bitIndex) & 1UL;
                bool carried =
                    (remainder & NegativeLimit) != 0UL;
                remainder = unchecked(
                    (remainder << 1) | nextBit);
                if (!carried && remainder < divisor)
                {
                    continue;
                }

                remainder = unchecked(remainder - divisor);
                if (bitIndex >= 64)
                {
                    quotient = 0UL;
                    return false;
                }

                result |= 1UL << bitIndex;
            }

            quotient = result;
            return true;
        }

        /// <summary>
        /// 计算无符号整数的平方根。
        /// </summary>
        private static ulong IntegerSqrt(UInt128 value)
        {
            if (value.High == 0 && value.Low == 0)
            {
                return 0UL;
            }

            ulong low = 0UL;
            ulong high = 1UL << 39;
            while (low + 1UL < high)
            {
                ulong middle = low + ((high - low) >> 1);
                UInt128 square = MultiplyUnsigned(middle, middle);
                if (square.CompareTo(value) <= 0)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// 获取Magnitude。
        /// </summary>
        private static ulong GetMagnitude(long value)
        {
            return value >= 0
                ? (ulong)value
                : (ulong)(-(value + 1L)) + 1UL;
        }

        /// <summary>
        /// 创建溢出。
        /// </summary>
        private static OverflowException CreateOverflow(
            string operation,
            long leftRaw,
            long rightRaw)
        {
            return new OverflowException(
                $"FP {operation}溢出：LeftRaw={leftRaw}，" +
                $"RightRaw={rightRaw}。");
        }

        private readonly struct UInt128 : IComparable<UInt128>
        {
            /// <summary>
            /// 创建UInt128实例。
            /// </summary>
            public UInt128(ulong high, ulong low)
            {
                High = high;
                Low = low;
            }

            /// <summary>
            /// 向调用方提供高位。
            /// </summary>
            public ulong High { get; }
            /// <summary>
            /// 向调用方提供低位。
            /// </summary>
            public ulong Low { get; }

            /// <summary>
            /// 比较当前实例与指定对象的排序关系。
            /// </summary>
            public int CompareTo(UInt128 other)
            {
                int highResult = High.CompareTo(other.High);
                return highResult != 0
                    ? highResult
                    : Low.CompareTo(other.Low);
            }
        }
    }
}
