using System;

namespace Game.Battle.Core.Math
{
    [Serializable]
    public struct FPVector2 : IEquatable<FPVector2>
    {
        /// <summary>
        /// 公开的Zero数据。
        /// </summary>
        public static readonly FPVector2 Zero =
            new FPVector2(FP.Zero, FP.Zero);
        /// <summary>
        /// 公开的Right数据。
        /// </summary>
        public static readonly FPVector2 Right =
            new FPVector2(FP.One, FP.Zero);
        /// <summary>
        /// 公开的Up数据。
        /// </summary>
        public static readonly FPVector2 Up =
            new FPVector2(FP.Zero, FP.One);

        /// <summary>
        /// 创建定点数Vector2实例。
        /// </summary>
        public FPVector2(FP x, FP y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 公开的X数据。
        /// </summary>
        public FP X;
        /// <summary>
        /// 公开的Y数据。
        /// </summary>
        public FP Y;

        /// <summary>
        /// 向调用方提供平方长度。
        /// </summary>
        public FP SqrMagnitude => X * X + Y * Y;

        /// <summary>
        /// 向调用方提供长度。
        /// </summary>
        public FP Magnitude => FP.Sqrt(SqrMagnitude);

        public FPVector2 Normalized
        {
            get
            {
                FP magnitude = Magnitude;
                return magnitude == FP.Zero
                    ? Zero
                    : this / magnitude;
            }
        }

        /// <summary>
        /// 规范化当前数据。
        /// </summary>
        public static FPVector2 Normalize(FPVector2 value) =>
            value.Normalized;

        /// <summary>
        /// 规范化当前数据。
        /// </summary>
        public void Normalize()
        {
            this = Normalized;
        }

        /// <summary>
        /// 计算绝对值。
        /// </summary>
        public static FPVector2 Abs(FPVector2 value) =>
            new FPVector2(FP.Abs(value.X), FP.Abs(value.Y));

        /// <summary>
        /// 将数值限制在指定范围内。
        /// </summary>
        public static FPVector2 Clamp(
            FPVector2 value,
            FPVector2 min,
            FPVector2 max) =>
            new FPVector2(
                FP.Clamp(value.X, min.X, max.X),
                FP.Clamp(value.Y, min.Y, max.Y));

        /// <summary>
        /// 旋转目标对象。
        /// </summary>
        public static FPVector2 Rotate(
            FPVector2 value,
            FP degrees)
        {
            FP.SinCos(degrees, out FP sin, out FP cos);
            return new FPVector2(
                value.X * cos - value.Y * sin,
                value.X * sin + value.Y * cos);
        }

        /// <summary>
        /// 执行计算点积相关逻辑。
        /// </summary>
        public static FP Dot(FPVector2 left, FPVector2 right) =>
            left.X * right.X + left.Y * right.Y;

        /// <summary>
        /// 执行计算距离Squared相关逻辑。
        /// </summary>
        public static FP DistanceSquared(
            FPVector2 left,
            FPVector2 right)
        {
            return (left - right).SqrMagnitude;
        }

        /// <summary>
        /// 执行 + 运算符重载。
        /// </summary>
        public static FPVector2 operator +(
            FPVector2 left,
            FPVector2 right) =>
            new FPVector2(left.X + right.X, left.Y + right.Y);

        /// <summary>
        /// 执行 - 运算符重载。
        /// </summary>
        public static FPVector2 operator -(
            FPVector2 left,
            FPVector2 right) =>
            new FPVector2(left.X - right.X, left.Y - right.Y);

        /// <summary>
        /// 执行 * 运算符重载。
        /// </summary>
        public static FPVector2 operator *(
            FPVector2 value,
            FP scalar) =>
            new FPVector2(value.X * scalar, value.Y * scalar);

        /// <summary>
        /// 执行 / 运算符重载。
        /// </summary>
        public static FPVector2 operator /(
            FPVector2 value,
            FP scalar) =>
            new FPVector2(value.X / scalar, value.Y / scalar);

        /// <summary>
        /// 执行 == 运算符重载。
        /// </summary>
        public static bool operator ==(
            FPVector2 left,
            FPVector2 right) => left.Equals(right);

        /// <summary>
        /// 执行 != 运算符重载。
        /// </summary>
        public static bool operator !=(
            FPVector2 left,
            FPVector2 right) => !left.Equals(right);

        /// <summary>
        /// 比较当前实例与指定对象是否相等。
        /// </summary>
        public bool Equals(FPVector2 other) =>
            X == other.X && Y == other.Y;

        /// <summary>
        /// 比较当前实例与指定对象是否相等。
        /// </summary>
        public override bool Equals(object obj) =>
            obj is FPVector2 other && Equals(other);

        /// <summary>
        /// 获取当前实例的哈希代码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        /// <summary>
        /// 生成当前实例的字符串表示。
        /// </summary>
        public override string ToString() => $"({X}, {Y})";
    }
}
