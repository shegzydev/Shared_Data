using System;

namespace FixedMath
{
    public struct Vector2Fixed
    {
        public Fixed64 X, Y;
        public Vector2Fixed(Fixed64 x, Fixed64 y) { X = x; Y = y; }

        public static readonly Vector2Fixed Zero = new Vector2Fixed(Fixed64.Zero, Fixed64.Zero);

        public static Vector2Fixed operator +(Vector2Fixed a, Vector2Fixed b) => new Vector2Fixed(a.X + b.X, a.Y + b.Y);
        public static Vector2Fixed operator -(Vector2Fixed a, Vector2Fixed b) => new Vector2Fixed(a.X - b.X, a.Y - b.Y);
        public static Vector2Fixed operator -(Vector2Fixed a) => new Vector2Fixed(-a.X, -a.Y);
        public static Vector2Fixed operator *(Vector2Fixed a, Fixed64 s) => new Vector2Fixed(a.X * s, a.Y * s);
        public static Vector2Fixed operator *(Fixed64 s, Vector2Fixed a) => a * s;
        public static Vector2Fixed operator /(Vector2Fixed a, Fixed64 s) => new Vector2Fixed(a.X / s, a.Y / s);

        public static Fixed64 Dot(Vector2Fixed a, Vector2Fixed b) => a.X * b.X + a.Y * b.Y;
        public static Fixed64 Cross(Vector2Fixed a, Vector2Fixed b) => a.X * b.Y - a.Y * b.X;

        public Fixed64 LengthSquared() => X * X + Y * Y;
        public Fixed64 Length() => Fixed64.Sqrt(LengthSquared());

        public Vector2Fixed Normalized()
        {
            Fixed64 len = Length();
            if (len.Raw == 0) return Zero;
            return this / len;
        }

        public static Vector2Fixed Lerp(Vector2Fixed a, Vector2Fixed b, Fixed64 t) =>
            new Vector2Fixed(FixedMathUtil.Lerp(a.X, b.X, t), FixedMathUtil.Lerp(a.Y, b.Y, t));

        public Vector2Fixed Rotated(Fixed64 angle)
        {
            Fixed64.SinCos(angle, out Fixed64 s, out Fixed64 c);
            return new Vector2Fixed(X * c - Y * s, X * s + Y * c);
        }

        public override string ToString() => $"({X}, {Y})";
    }

    public struct Vector3Fixed
    {
        public Fixed64 X, Y, Z;
        public Vector3Fixed(Fixed64 x, Fixed64 y, Fixed64 z) { X = x; Y = y; Z = z; }

        public static readonly Vector3Fixed Zero = new Vector3Fixed(Fixed64.Zero, Fixed64.Zero, Fixed64.Zero);

        public static Vector3Fixed operator +(Vector3Fixed a, Vector3Fixed b) => new Vector3Fixed(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3Fixed operator -(Vector3Fixed a, Vector3Fixed b) => new Vector3Fixed(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3Fixed operator -(Vector3Fixed a) => new Vector3Fixed(-a.X, -a.Y, -a.Z);
        public static Vector3Fixed operator *(Vector3Fixed a, Fixed64 s) => new Vector3Fixed(a.X * s, a.Y * s, a.Z * s);
        public static Vector3Fixed operator *(Fixed64 s, Vector3Fixed a) => a * s;
        public static Vector3Fixed operator /(Vector3Fixed a, Fixed64 s) => new Vector3Fixed(a.X / s, a.Y / s, a.Z / s);

        public static Fixed64 Dot(Vector3Fixed a, Vector3Fixed b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vector3Fixed Cross(Vector3Fixed a, Vector3Fixed b) => new Vector3Fixed(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        public Fixed64 LengthSquared() => X * X + Y * Y + Z * Z;
        public Fixed64 Length() => Fixed64.Sqrt(LengthSquared());

        public Vector3Fixed Normalized()
        {
            Fixed64 len = Length();
            if (len.Raw == 0) return Zero;
            return this / len;
        }

        public static Vector3Fixed Lerp(Vector3Fixed a, Vector3Fixed b, Fixed64 t) =>
            new Vector3Fixed(FixedMathUtil.Lerp(a.X, b.X, t), FixedMathUtil.Lerp(a.Y, b.Y, t), FixedMathUtil.Lerp(a.Z, b.Z, t));

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
