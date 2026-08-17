using System;

namespace Game.Battle.Core.Math
{
    [Serializable]
    public struct FPVector2 : IEquatable<FPVector2>
    {
        public static readonly FPVector2 Zero =
            new FPVector2(FP.Zero, FP.Zero);
        public static readonly FPVector2 Right =
            new FPVector2(FP.One, FP.Zero);
        public static readonly FPVector2 Up =
            new FPVector2(FP.Zero, FP.One);

        public FPVector2(FP x, FP y)
        {
            X = x;
            Y = y;
        }

        public FP X;
        public FP Y;

        public FP SqrMagnitude => X * X + Y * Y;

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

        public static FPVector2 Normalize(FPVector2 value) =>
            value.Normalized;

        public void Normalize()
        {
            this = Normalized;
        }

        public static FPVector2 Abs(FPVector2 value) =>
            new FPVector2(FP.Abs(value.X), FP.Abs(value.Y));

        public static FPVector2 Clamp(
            FPVector2 value,
            FPVector2 min,
            FPVector2 max) =>
            new FPVector2(
                FP.Clamp(value.X, min.X, max.X),
                FP.Clamp(value.Y, min.Y, max.Y));

        public static FPVector2 Rotate(
            FPVector2 value,
            FP degrees)
        {
            FP.SinCos(degrees, out FP sin, out FP cos);
            return new FPVector2(
                value.X * cos - value.Y * sin,
                value.X * sin + value.Y * cos);
        }

        public static FP Dot(FPVector2 left, FPVector2 right) =>
            left.X * right.X + left.Y * right.Y;

        public static FP DistanceSquared(
            FPVector2 left,
            FPVector2 right)
        {
            return (left - right).SqrMagnitude;
        }

        public static FPVector2 operator +(
            FPVector2 left,
            FPVector2 right) =>
            new FPVector2(left.X + right.X, left.Y + right.Y);

        public static FPVector2 operator -(
            FPVector2 left,
            FPVector2 right) =>
            new FPVector2(left.X - right.X, left.Y - right.Y);

        public static FPVector2 operator *(
            FPVector2 value,
            FP scalar) =>
            new FPVector2(value.X * scalar, value.Y * scalar);

        public static FPVector2 operator /(
            FPVector2 value,
            FP scalar) =>
            new FPVector2(value.X / scalar, value.Y / scalar);

        public static bool operator ==(
            FPVector2 left,
            FPVector2 right) => left.Equals(right);

        public static bool operator !=(
            FPVector2 left,
            FPVector2 right) => !left.Equals(right);

        public bool Equals(FPVector2 other) =>
            X == other.X && Y == other.Y;

        public override bool Equals(object obj) =>
            obj is FPVector2 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
