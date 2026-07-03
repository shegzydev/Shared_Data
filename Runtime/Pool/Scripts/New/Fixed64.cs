using System;
using System.Runtime.CompilerServices;

namespace FixedMath
{
    /// <summary>Deterministic Q32.32 fixed-point number.</summary>
    public struct Fixed64 : IEquatable<Fixed64>, IComparable<Fixed64>
    {
        public const int FRAC_BITS = 32;
        public const long ONE_RAW = 1L << FRAC_BITS;

        public long Raw;

        public static readonly Fixed64 Zero = FromRaw(0);
        public static readonly Fixed64 One = FromRaw(ONE_RAW);
        public static readonly Fixed64 Half = FromRaw(ONE_RAW >> 1);
        public static readonly Fixed64 Pi = FromDouble(Math.PI);
        public static readonly Fixed64 TwoPi = FromDouble(Math.PI * 2);
        public static readonly Fixed64 HalfPi = FromDouble(Math.PI / 2);
        public static readonly Fixed64 Epsilon = FromRaw(1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed64 FromRaw(long raw) { Fixed64 f; f.Raw = raw; return f; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed64 FromInt(int v) => FromRaw((long)v << FRAC_BITS);

        public static Fixed64 FromDouble(double v) => FromRaw((long)Math.Round(v * ONE_RAW));

        public double ToDouble() => Raw / (double)ONE_RAW;
        public int ToInt() => (int)(Raw >> FRAC_BITS);
        public float ToFloat() => (float)ToDouble();

        // ---- 64x64 -> 128 helpers (portable, no UInt128 dependency) ----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Mul64x64_128(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong aLo = a & 0xFFFFFFFFUL, aHi = a >> 32;
            ulong bLo = b & 0xFFFFFFFFUL, bHi = b >> 32;

            ulong lolo = aLo * bLo;
            ulong lohi = aLo * bHi;
            ulong hilo = aHi * bLo;
            ulong hihi = aHi * bHi;

            ulong mid = lohi + hilo;
            ulong midCarry = mid < lohi ? 1UL << 32 : 0UL; // overflow of 64-bit add -> carries into high

            ulong lowPart = lolo + (mid << 32);
            ulong carryOut = lowPart < lolo ? 1UL : 0UL;

            hi = hihi + (mid >> 32) + midCarry + carryOut;
            lo = lowPart;
        }

        // Shift right a 128-bit value {hi,lo} by n bits (0<n<128), return low 64 bits of result.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong ShiftRight128(ulong hi, ulong lo, int n)
        {
            if (n == 0) return lo;
            if (n < 64) return (lo >> n) | (hi << (64 - n));
            return hi >> (n - 64);
        }

        public static Fixed64 operator *(Fixed64 a, Fixed64 b)
        {
            bool neg = (a.Raw < 0) ^ (b.Raw < 0);
            ulong ua = (ulong)Math.Abs(a.Raw);
            ulong ub = (ulong)Math.Abs(b.Raw);
            Mul64x64_128(ua, ub, out ulong hi, out ulong lo);
            ulong resultLo = ShiftRight128(hi, lo, FRAC_BITS);
            long result = (long)resultLo;
            return FromRaw(neg ? -result : result);
        }

        public static Fixed64 operator /(Fixed64 a, Fixed64 b)
        {
            if (b.Raw == 0) throw new DivideByZeroException("Fixed64 division by zero");
            bool neg = (a.Raw < 0) ^ (b.Raw < 0);
            ulong ua = (ulong)Math.Abs(a.Raw);
            ulong ub = (ulong)Math.Abs(b.Raw);
            // (ua << 32) / ub, computed via 128-bit numerator
            ulong numHi = ua >> 32;
            ulong numLo = ua << 32;
            ulong result = Div128by64(numHi, numLo, ub);
            long r = (long)result;
            return FromRaw(neg ? -r : r);
        }

        // 128-bit numerator / 64-bit denominator -> 64-bit quotient, via binary long division.
        static ulong Div128by64(ulong numHi, ulong numLo, ulong den)
        {
            if (numHi == 0) return numLo / den;
            ulong quotient = 0;
            ulong remainder = 0;
            for (int i = 127; i >= 0; i--)
            {
                remainder = (remainder << 1) | (ulong)(i >= 64 ? (numHi >> (i - 64)) & 1UL : (numLo >> i) & 1UL);
                if (remainder >= den)
                {
                    remainder -= den;
                    if (i < 64) quotient |= (1UL << i);
                }
            }
            return quotient;
        }

        public static Fixed64 operator +(Fixed64 a, Fixed64 b) => FromRaw(a.Raw + b.Raw);
        public static Fixed64 operator -(Fixed64 a, Fixed64 b) => FromRaw(a.Raw - b.Raw);
        public static Fixed64 operator -(Fixed64 a) => FromRaw(-a.Raw);
        public static Fixed64 operator %(Fixed64 a, Fixed64 b) => FromRaw(a.Raw % b.Raw);

        public static bool operator ==(Fixed64 a, Fixed64 b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed64 a, Fixed64 b) => a.Raw != b.Raw;
        public static bool operator <(Fixed64 a, Fixed64 b) => a.Raw < b.Raw;
        public static bool operator >(Fixed64 a, Fixed64 b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed64 a, Fixed64 b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed64 a, Fixed64 b) => a.Raw >= b.Raw;

        public static implicit operator Fixed64(int v) => FromInt(v);
        public static explicit operator Fixed64(double v) => FromDouble(v);
        public static explicit operator double(Fixed64 v) => v.ToDouble();
        public static explicit operator int(Fixed64 v) => v.ToInt();
        public static explicit operator float(Fixed64 v) => v.ToFloat();

        public bool Equals(Fixed64 other) => Raw == other.Raw;
        public override bool Equals(object obj) => obj is Fixed64 f && Equals(f);
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Fixed64 other) => Raw.CompareTo(other.Raw);
        public override string ToString() => ToDouble().ToString("F6");

        // ---- Square root: Newton-Raphson on the raw Q32.32 integer, deterministic, integer-only ----
        public static Fixed64 Sqrt(Fixed64 x)
        {
            if (x.Raw < 0) throw new ArgumentException("Sqrt of negative Fixed64");
            if (x.Raw == 0) return Zero;

            // We want r such that r*r == x (in fixed-point), i.e. rRaw*rRaw/ONE == xRaw => rRaw = sqrt(xRaw*ONE).
            // Work in ulong; xRaw*ONE can exceed 64 bits, so scale via two Newton passes on doubles-free integers:
            // Initial guess via bit-length shift, then refine using integer Newton iteration on the identity
            // r_{n+1} = (r_n + x/r_n) / 2, all in Fixed64 space (division already 128-bit safe).
            ulong ux = (ulong)x.Raw;
            int bitLen = 64 - System.Numerics.BitOperations.LeadingZeroCount(ux);
            // initial guess: 2^(ceil((bitLen)/2)) scaled to fixed-point domain
            long guessRaw = 1L << Math.Max(1, (bitLen + FRAC_BITS) / 2);
            Fixed64 r = FromRaw(guessRaw);
            for (int i = 0; i < 40; i++)
            {
                Fixed64 next = (r + x / r) * Half;
                if (next.Raw == r.Raw) break;
                r = next;
            }
            return r;
        }

        // ---- CORDIC trig ----
        const int CORDIC_ITERS = 40;
        static readonly long[] AtanTable = BuildAtanTable();
        const double CORDIC_K = 0.6072529350088812561694;

        static long[] BuildAtanTable()
        {
            var t = new long[CORDIC_ITERS];
            for (int i = 0; i < CORDIC_ITERS; i++)
                t[i] = (long)Math.Round(Math.Atan(Math.Pow(2, -i)) * ONE_RAW);
            return t;
        }

        /// <summary>Computes sin and cos simultaneously via CORDIC rotation mode.</summary>
        public static void SinCos(Fixed64 angle, out Fixed64 sin, out Fixed64 cos)
        {
            long t = angle.Raw % (long)(TwoPi.Raw);
            if (t > Pi.Raw) t -= TwoPi.Raw;
            if (t < -Pi.Raw) t += TwoPi.Raw;

            bool negCos = false;
            if (t > HalfPi.Raw) { t = Pi.Raw - t; negCos = true; }
            else if (t < -HalfPi.Raw) { t = -Pi.Raw - t; negCos = true; }

            long x = (long)Math.Round(CORDIC_K * ONE_RAW);
            long y = 0;
            long z = t;

            for (int i = 0; i < CORDIC_ITERS; i++)
            {
                long xShift = x >> i;
                long yShift = y >> i;
                if (z >= 0)
                {
                    long newX = x - yShift;
                    long newY = y + xShift;
                    z -= AtanTable[i];
                    x = newX; y = newY;
                }
                else
                {
                    long newX = x + yShift;
                    long newY = y - xShift;
                    z += AtanTable[i];
                    x = newX; y = newY;
                }
            }

            cos = FromRaw(negCos ? -x : x);
            sin = FromRaw(y);
        }

        public static Fixed64 Sin(Fixed64 angle) { SinCos(angle, out var s, out _); return s; }
        public static Fixed64 Cos(Fixed64 angle) { SinCos(angle, out _, out var c); return c; }
        public static Fixed64 Tan(Fixed64 angle) { SinCos(angle, out var s, out var c); return s / c; }

        /// <summary>CORDIC vector mode: returns atan2(y, x).</summary>
        public static Fixed64 Atan2(Fixed64 fy, Fixed64 fx)
        {
            long x = fx.Raw, y = fy.Raw;
            long extraAngle = 0;
            bool negX = x < 0;
            if (negX)
            {
                long origY = y;
                x = -x;
                y = -y;
                extraAngle = origY >= 0 ? Pi.Raw : -Pi.Raw;
            }
            if (x == 0 && y == 0) return Zero;

            long z = 0;
            for (int i = 0; i < CORDIC_ITERS; i++)
            {
                long xShift = x >> i;
                long yShift = y >> i;
                if (y < 0)
                {
                    long newX = x - yShift;
                    long newY = y + xShift;
                    z -= AtanTable[i];
                    x = newX; y = newY;
                }
                else
                {
                    long newX = x + yShift;
                    long newY = y - xShift;
                    z += AtanTable[i];
                    x = newX; y = newY;
                }
            }
            return FromRaw(z + extraAngle);
        }

        public static Fixed64 Atan(Fixed64 v) => Atan2(v, One);
        public static Fixed64 Asin(Fixed64 v) => Atan2(v, Sqrt(One - v * v));
        public static Fixed64 Acos(Fixed64 v) => HalfPi - Asin(v);
    }
}
