using System.Collections.Generic;
using SoftFloat;

namespace PoolEngine
{
    public struct SweepHit
    {
        public enum HitType { Circle, Edge }
        public sfloat T;
        public sfloat Distance;

        public sfloat PointX, PointY;
        public sfloat NormalX, NormalY;

        public HitType hitType;
        public int hitObj;

        public override string ToString() => $"T={T:F3}, Point=({PointX:F0}, {PointY:F0})";
    }

    public static class BallSweeper
    {
        static sfloat length((sfloat x, sfloat y) v)
        {
            return libm.sqrtf(v.x * v.x + v.y * v.y);
        }

        static sfloat dot((sfloat x, sfloat y) v1, (sfloat x, sfloat y) v2)
        {
            return v1.x * v2.x + v1.y * v2.y;
        }

        public static (sfloat x, sfloat y) normal((sfloat x, sfloat y) v)
        {
            var len = length(v);
            return (v.x / len, v.y / len);
        }

        static bool isInfinity(sfloat f)
        {
            if (f == sfloat.PositiveInfinity) return true;
            if (f == sfloat.NegativeInfinity) return true;
            return false;
        }

        public static bool Sweep(
            (sfloat x, sfloat y) origin, sfloat radius,
             (sfloat x, sfloat y) direction, sfloat distance,
            Snooker snooker,
            out SweepHit hit)
        {
            hit = default;
            sfloat bestT = sfloat.MaxValue;

            var dirNorm = normal((direction.x, direction.y));

            foreach (var c in snooker.GetBalls)
            {
                if (length((c.px - origin.x, c.py - origin.y)) < radius)
                {
                    continue;
                }

                if (SweepVsBall((origin.x, origin.y), radius, dirNorm, distance, c, out var h) && h.T < bestT)
                {
                    bestT = h.T;
                    hit = h;
                }
            }

            foreach (var e in snooker.GetEdges)
            {
                if (SweepVsEdge((origin.x, origin.y), radius, dirNorm, distance, e, out var h) && h.T < bestT)
                {
                    bestT = h.T;
                    hit = h;
                }
            }

            return bestT <= sfloat.One;
        }

        public static bool SweepVsBall(
            (sfloat x, sfloat y) origin, sfloat radius,
            (sfloat x, sfloat y) direction, sfloat distance,
            Snooker.Ball target,
            out SweepHit hit)
        {
            hit = default;

            sfloat combinedR = radius + target.r;
            sfloat deltaX = origin.x - target.px;
            sfloat deltaY = origin.y - target.py;

            sfloat a = dot(direction, direction);
            sfloat b = (sfloat)2 * (deltaX * direction.x + deltaY * direction.y);
            sfloat c = deltaX * deltaX + deltaY * deltaY - combinedR * combinedR;
            sfloat disc = b * b - (sfloat)4 * a * c;

            if (disc < sfloat.Zero) return false;

            sfloat sqrtDisc = libm.sqrtf(disc);
            sfloat twoA = (sfloat)2 * a;

            sfloat t1 = (-b - sqrtDisc) / twoA;
            sfloat t2 = (-b + sqrtDisc) / twoA;

            sfloat t = sfloat.MaxValue;
            if (t1 >= sfloat.Zero && t1 <= distance) t = t1;
            else if (t2 >= sfloat.Zero && t2 <= distance) t = t2;

            if (t == sfloat.MaxValue || isInfinity(t)) return false;

            sfloat hitPointX = origin.x + direction.x * t;
            sfloat hitPointY = origin.y + direction.y * t;

            var norm = normal((hitPointX - target.px, hitPointY - target.py));

            hit = new SweepHit
            {
                T = t / distance,

                Distance = t,

                PointX = hitPointX,
                PointY = hitPointY,

                NormalX = norm.x,
                NormalY = norm.y,

                hitType = SweepHit.HitType.Circle,
                hitObj = target.number
            };
            return true;
        }

        public static bool SweepVsEdge(
            (sfloat x, sfloat y) origin, sfloat radius,
            (sfloat x, sfloat y) direction, sfloat distance,
            Snooker.Edge edge,
            out SweepHit hit)
        {
            hit = default;
            sfloat bestT = sfloat.MaxValue;
            SweepHit bestHit = default;

            sfloat ex = edge.x2 - edge.x1;
            sfloat ey = edge.y2 - edge.y1;
            sfloat edgeLenSq = ex * ex + ey * ey;

            if (edgeLenSq > sfloat.Epsilon)
            {
                sfloat nx = -ey;
                sfloat ny = ex;
                var norm = normal((nx, ny));

                sfloat side = norm.x * (origin.x - edge.x1) + norm.y * (origin.y - edge.y1);
                if (side < sfloat.Zero) norm = (-norm.x, -norm.y);

                sfloat dOrigin = norm.x * (origin.x - edge.x1) + norm.y * (origin.y - edge.y1);
                sfloat dDir = dot(norm, direction);

                if (dDir < sfloat.Zero)
                {
                    sfloat tHit = (dOrigin - radius) / -dDir;

                    if (tHit >= sfloat.Zero && tHit <= distance)
                    {
                        sfloat hitPointX = origin.x + direction.x * tHit;
                        sfloat hitPointY = origin.y + direction.y * tHit;

                        sfloat proj = ex * (hitPointX - edge.x1) + ey * (hitPointY - edge.y1);

                        if (proj >= sfloat.Zero && proj <= edgeLenSq)
                        {
                            sfloat T = tHit / distance;
                            if (T < bestT)
                            {
                                bestT = T;
                                bestHit = new SweepHit
                                {
                                    T = T,
                                    Distance = tHit,
                                    PointX = hitPointX,
                                    PointY = hitPointY,
                                    NormalX = norm.x,
                                    NormalY = norm.y,
                                    hitType = SweepHit.HitType.Edge,
                                    hitObj = edge.index
                                };
                            }
                        }
                    }
                }
            }

            CheckEndpoint(origin, radius, direction, distance, (edge.x1, edge.y1), ref bestT, ref bestHit);
            CheckEndpoint(origin, radius, direction, distance, (edge.x2, edge.y2), ref bestT, ref bestHit);

            if (bestT <= sfloat.One)
            {
                hit = bestHit;
                return true;
            }

            return false;
        }

        private static void CheckEndpoint(
            (sfloat x, sfloat y) origin, sfloat radius,
            (sfloat x, sfloat y) direction, sfloat distance,
            (sfloat x, sfloat y) endpoint,
            ref sfloat bestT, ref SweepHit bestHit)
        {
            sfloat dx = origin.x - endpoint.x;
            sfloat dy = origin.y - endpoint.y;

            sfloat a = dot(direction, direction);
            sfloat b = (sfloat)2 * (dx * direction.x + dy * direction.y);
            sfloat c = dx * dx + dy * dy - radius * radius;
            sfloat disc = b * b - (sfloat)4 * a * c;

            if (disc < sfloat.Zero) return;

            sfloat sqrtDisc = libm.sqrtf(disc);
            sfloat twoA = (sfloat)2 * a;

            sfloat t1 = (-b - sqrtDisc) / twoA;
            sfloat t2 = (-b + sqrtDisc) / twoA;

            sfloat t = sfloat.MaxValue;
            if (t1 >= sfloat.Zero && t1 <= distance) t = t1;
            else if (t2 >= sfloat.Zero && t2 <= distance) t = t2;

            if (t == sfloat.MaxValue || isInfinity(t)) return;

            sfloat T = t / distance;
            if (T >= bestT) return;

            sfloat hitPointX = origin.x + direction.x * t;
            sfloat hitPointY = origin.y + direction.y * t;

            var norm = normal((hitPointX - endpoint.x, hitPointY - endpoint.y));

            bestT = T;

            var obj = bestHit.hitObj;
            bestHit = new SweepHit
            {
                T = T,
                Distance = t,
                PointX = hitPointX,
                PointY = hitPointY,
                NormalX = norm.x,
                NormalY = norm.y,
                hitType = SweepHit.HitType.Edge,
                hitObj = obj
            };
        }
    }
}