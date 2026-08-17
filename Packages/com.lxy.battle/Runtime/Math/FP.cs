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
        public const long Precision = 10000L;

        private const ulong NegativeLimit = 1UL << 63;

        public static readonly FP MinValue = new FP(long.MinValue);
        public static readonly FP MaxValue = new FP(long.MaxValue);
        public static readonly FP Zero = new FP(0L);
        public static readonly FP One = new FP(Precision);

        public FP(long rawValue)
        {
            RawValue = rawValue;
        }

        public long RawValue { get; }

        public static FP FromRaw(long value) => new FP(value);

        public static FP FromInt(int value) =>
            new FP(checked((long)value * Precision));

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

        public int FloorToInt()
        {
            long result = RawValue / Precision;
            if (RawValue < 0 && RawValue % Precision != 0)
            {
                result--;
            }

            return checked((int)result);
        }

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

        public static FP Min(FP left, FP right) =>
            left <= right ? left : right;

        public static FP Max(FP left, FP right) =>
            left >= right ? left : right;

        public static FP Clamp(FP value, FP min, FP max)
        {
            if (min > max)
            {
                throw new ArgumentException(
                    "FP.Clamp 要求 min 小于或等于 max。");
            }

            return value < min ? min : value > max ? max : value;
        }

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

        public static bool operator ==(FP left, FP right) =>
            left.RawValue == right.RawValue;

        public static bool operator !=(FP left, FP right) =>
            left.RawValue != right.RawValue;

        public static bool operator <(FP left, FP right) =>
            left.RawValue < right.RawValue;

        public static bool operator >(FP left, FP right) =>
            left.RawValue > right.RawValue;

        public static bool operator <=(FP left, FP right) =>
            left.RawValue <= right.RawValue;

        public static bool operator >=(FP left, FP right) =>
            left.RawValue >= right.RawValue;

        public bool Equals(FP other) =>
            RawValue == other.RawValue;

        public override bool Equals(object obj) =>
            obj is FP other && Equals(other);

        public override int GetHashCode() =>
            RawValue.GetHashCode();

        public int CompareTo(FP other) =>
            RawValue.CompareTo(other.RawValue);

        public override string ToString()
        {
            ulong magnitude = GetMagnitude(RawValue);
            ulong whole = magnitude / (ulong)Precision;
            ulong fraction = magnitude % (ulong)Precision;
            string sign = RawValue < 0 ? "-" : string.Empty;
            return $"{sign}{whole}.{fraction:D4}";
        }

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

        private static ulong GetMagnitude(long value)
        {
            return value >= 0
                ? (ulong)value
                : (ulong)(-(value + 1L)) + 1UL;
        }

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
            public UInt128(ulong high, ulong low)
            {
                High = high;
                Low = low;
            }

            public ulong High { get; }
            public ulong Low { get; }

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
