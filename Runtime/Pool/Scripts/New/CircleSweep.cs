using System;
using System.Collections.Generic;
using FixedMath;

namespace PhysicsEngine
{
    public struct SweepHit
    {
        public enum HitType { Circle, Edge }
        public Fixed64 T;
        public Fixed64 Distance;
        public Vector2Fixed Point;
        public Vector2Fixed Normal;
        public HitType hitType;
        public PhysicsObj hitObj;

        public override string ToString() => $"T={T:F3}, Point=({Point.X:F0}, {Point.Y:F0})";
    }

    public static class CircleSweeper
    {
        public static bool Sweep(
            Vector2Fixed origin, Fixed64 radius,
            Vector2Fixed direction, Fixed64 distance,
            List<Circle> circles,
            List<Edge> edges,
            out SweepHit hit)
        {
            hit = default;
            Fixed64 bestT = Fixed64.MaxValue;
            Vector2Fixed dirNorm = direction.Normalized();

            foreach (var c in circles)
            {
                if ((c.Center - origin).Length() < radius)
                {
                    continue;
                }

                if (SweepVsCircle(origin, radius, dirNorm, distance, c, out var h) && h.T < bestT)
                {
                    bestT = h.T;
                    hit = h;
                }
            }

            foreach (var e in edges)
            {
                if (SweepVsEdge(origin, radius, dirNorm, distance, e, out var h) && h.T < bestT)
                {
                    bestT = h.T;
                    hit = h;
                }
            }

            return bestT <= Fixed64.One;
        }

        public static bool SweepVsCircle(
            Vector2Fixed origin, Fixed64 radius,
            Vector2Fixed direction, Fixed64 distance,
            Circle target,
            out SweepHit hit)
        {
            hit = default;

            Fixed64 combinedR = radius + target.Radius;
            Fixed64 deltaX = origin.X - target.Center.X;
            Fixed64 deltaY = origin.Y - target.Center.Y;

            Fixed64 a = Vector2Fixed.Dot(direction, direction);
            Fixed64 b = 2 * (deltaX * direction.X + deltaY * direction.Y);
            Fixed64 c = deltaX * deltaX + deltaY * deltaY - combinedR * combinedR;
            Fixed64 disc = b * b - 4 * a * c;

            if (disc < 0) return false;

            Fixed64 sqrtDisc = Fixed64.Sqrt(disc);
            Fixed64 twoA = 2 * a;

            Fixed64 t1 = (-b - sqrtDisc) / twoA;
            Fixed64 t2 = (-b + sqrtDisc) / twoA;

            Fixed64 t = Fixed64.MaxValue;
            if (t1 >= 0 && t1 <= distance) t = t1;
            else if (t2 >= 0 && t2 <= distance) t = t2;

            if (t == Fixed64.MaxValue || Fixed64.IsInfinity(t)) return false;

            Vector2Fixed hitPoint = origin + direction * t;
            Vector2Fixed normal = (hitPoint - target.Center).Normalized();

            hit = new SweepHit
            {
                T = t / distance,
                Distance = t,
                Point = hitPoint,
                Normal = normal,
                hitType = SweepHit.HitType.Circle,
                hitObj = target
            };
            return true;
        }

        public static bool SweepVsEdge(
            Vector2Fixed origin, Fixed64 radius,
            Vector2Fixed direction, Fixed64 distance,
            Edge edge,
            out SweepHit hit)
        {
            hit = default;
            Fixed64 bestT = Fixed64.MaxValue;
            SweepHit bestHit = default;

            Fixed64 ex = edge.P2.X - edge.P1.X;
            Fixed64 ey = edge.P2.Y - edge.P1.Y;
            Fixed64 edgeLenSq = ex * ex + ey * ey;

            if (edgeLenSq > Fixed64.FromDouble(1e-10))
            {
                Fixed64 nx = -ey;
                Fixed64 ny = ex;
                Vector2Fixed normal = new Vector2Fixed(nx, ny).Normalized();

                Fixed64 side = normal.X * (origin.X - edge.P1.X) + normal.Y * (origin.Y - edge.P1.Y);
                if (side < 0) normal = new Vector2Fixed(-normal.X, -normal.Y);

                Fixed64 dOrigin = normal.X * (origin.X - edge.P1.X) + normal.Y * (origin.Y - edge.P1.Y);
                Fixed64 dDir = Vector2Fixed.Dot(normal, direction);

                if (dDir < 0)
                {
                    Fixed64 tHit = (dOrigin - radius) / -dDir;

                    if (tHit >= 0 && tHit <= distance && !Fixed64.IsInfinity(tHit))
                    {
                        Vector2Fixed hitPoint = origin + direction * tHit;

                        Fixed64 proj = ex * (hitPoint.X - edge.P1.X) + ey * (hitPoint.Y - edge.P1.Y);
                        if (proj >= 0 && proj <= edgeLenSq)
                        {
                            Fixed64 T = tHit / distance;
                            if (T < bestT)
                            {
                                bestT = T;
                                bestHit = new SweepHit
                                {
                                    T = T,
                                    Distance = tHit,
                                    Point = hitPoint,
                                    Normal = normal,
                                    hitType = SweepHit.HitType.Edge,
                                    hitObj = edge
                                };
                            }
                        }
                    }
                }
            }

            CheckEndpoint(origin, radius, direction, distance, edge.P1, ref bestT, ref bestHit);
            CheckEndpoint(origin, radius, direction, distance, edge.P2, ref bestT, ref bestHit);

            if (bestT <= Fixed64.One)
            {
                hit = bestHit;
                return true;
            }

            return false;
        }

        private static void CheckEndpoint(
            Vector2Fixed origin, Fixed64 radius,
            Vector2Fixed direction, Fixed64 distance,
            Vector2Fixed endpoint,
            ref Fixed64 bestT, ref SweepHit bestHit)
        {
            Fixed64 dx = origin.X - endpoint.X;
            Fixed64 dy = origin.Y - endpoint.Y;

            Fixed64 a = Vector2Fixed.Dot(direction, direction);
            Fixed64 b = 2 * (dx * direction.X + dy * direction.Y);
            Fixed64 c = dx * dx + dy * dy - radius * radius;
            Fixed64 disc = b * b - 4 * a * c;

            if (disc < 0) return;

            Fixed64 sqrtDisc = Fixed64.Sqrt(disc);
            Fixed64 twoA = 2 * a;

            Fixed64 t1 = (-b - sqrtDisc) / twoA;
            Fixed64 t2 = (-b + sqrtDisc) / twoA;

            Fixed64 t = Fixed64.MaxValue;
            if (t1 >= 0 && t1 <= distance) t = t1;
            else if (t2 >= 0 && t2 <= distance) t = t2;

            if (t == Fixed64.MaxValue || Fixed64.IsInfinity(t)) return;

            Fixed64 T = t / distance;
            if (T >= bestT) return;

            Vector2Fixed hitPoint = origin + direction * t;
            Vector2Fixed normal = (hitPoint - endpoint).Normalized();

            bestT = T;

            var obj = bestHit.hitObj;
            bestHit = new SweepHit
            {
                T = T,
                Distance = t,
                Point = hitPoint,
                Normal = normal,
                hitType = SweepHit.HitType.Edge,
                hitObj = obj
            };
        }
    }
}