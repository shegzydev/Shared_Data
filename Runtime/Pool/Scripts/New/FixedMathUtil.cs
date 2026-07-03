using System;

namespace FixedMath
{
    public static class FixedMathUtil
    {
        public static Fixed64 Abs(Fixed64 v) => v.Raw < 0 ? -v : v;
        public static Fixed64 Min(Fixed64 a, Fixed64 b) => a.Raw < b.Raw ? a : b;
        public static Fixed64 Max(Fixed64 a, Fixed64 b) => a.Raw > b.Raw ? a : b;
        public static Fixed64 Clamp(Fixed64 v, Fixed64 lo, Fixed64 hi) => Max(lo, Min(hi, v));
        public static Fixed64 Clamp01(Fixed64 v) => Clamp(v, Fixed64.Zero, Fixed64.One);

        public static Fixed64 Lerp(Fixed64 a, Fixed64 b, Fixed64 t) => a + (b - a) * t;

        public static Fixed64 Floor(Fixed64 v)
        {
            long raw = v.Raw;
            long mask = Fixed64.ONE_RAW - 1;
            long floored = raw & ~mask;
            // if (raw < 0 && (raw & mask) != 0) floored -= Fixed64.ONE_RAW;
            return Fixed64.FromRaw(floored);
        }

        public static Fixed64 Ceil(Fixed64 v)
        {
            var f = Floor(v);
            return f == v ? f : f + Fixed64.One;
        }

        public static Fixed64 Round(Fixed64 v) => Floor(v + Fixed64.Half);

        public static Fixed64 Sign(Fixed64 v) => v.Raw > 0 ? Fixed64.One : (v.Raw < 0 ? -Fixed64.One : Fixed64.Zero);

        public static Fixed64 Pow2(int exponent) => Fixed64.FromRaw(exponent >= 0 ? Fixed64.ONE_RAW << exponent : Fixed64.ONE_RAW >> -exponent);

        /// <summary>Deterministic exponential decay smoothing (e.g. for friction), t in [0,1].</summary>
        public static Fixed64 MoveTowards(Fixed64 current, Fixed64 target, Fixed64 maxDelta)
        {
            Fixed64 diff = target - current;
            if (Abs(diff) <= maxDelta) return target;
            return current + Sign(diff) * maxDelta;
        }
    }
}
