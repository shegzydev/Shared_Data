using System;
using System.Collections.Generic;

namespace PhysicsEngine
{
    public struct SweepHit
    {
        public enum HitType { Circle, Edge }
        public double T;
        public double Distance;
        public Vector2 Point;
        public Vector2 Normal;
        public HitType hitType;
        public PhysicsObj hitObj;

        public override string ToString() => $"T={T:F3}, Point=({Point.X:F0}, {Point.Y:F0})";
    }

    public static class CircleSweeper
    {
        public static bool Sweep(
            Vector2 origin, double radius,
            Vector2 direction, double distance,
            List<Circle> circles,
            List<Edge> edges,
            out SweepHit hit)
        {
            hit = default;
            double bestT = double.MaxValue;
            Vector2 dirNorm = direction.Normalized();

            foreach (var c in circles)
            {
                if ((c.Center - origin).Magnitude() < radius)
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

            return bestT <= 1.0;
        }

        public static bool SweepVsCircle(
            Vector2 origin, double radius,
            Vector2 direction, double distance,
            Circle target,
            out SweepHit hit)
        {
            hit = default;

            double combinedR = radius + target.Radius;
            double deltaX = origin.X - target.Center.X;
            double deltaY = origin.Y - target.Center.Y;

            double a = direction.Dot(direction);
            double b = 2 * (deltaX * direction.X + deltaY * direction.Y);
            double c = deltaX * deltaX + deltaY * deltaY - combinedR * combinedR;
            double disc = b * b - 4 * a * c;

            if (disc < 0) return false;

            double sqrtDisc = Math.Sqrt(disc);
            double twoA = 2 * a;

            double t1 = (-b - sqrtDisc) / twoA;
            double t2 = (-b + sqrtDisc) / twoA;

            double t = double.MaxValue;
            if (t1 >= 0 && t1 <= distance) t = t1;
            else if (t2 >= 0 && t2 <= distance) t = t2;

            if (t == double.MaxValue || double.IsInfinity(t)) return false;

            Vector2 hitPoint = origin + direction * t;
            Vector2 normal = (hitPoint - target.Center).Normalized();

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
            Vector2 origin, double radius,
            Vector2 direction, double distance,
            Edge edge,
            out SweepHit hit)
        {
            hit = default;
            double bestT = double.MaxValue;
            SweepHit bestHit = default;

            double ex = edge.P2.X - edge.P1.X;
            double ey = edge.P2.Y - edge.P1.Y;
            double edgeLenSq = ex * ex + ey * ey;

            if (edgeLenSq > 1e-10)
            {
                double nx = -ey;
                double ny = ex;
                Vector2 normal = new Vector2(nx, ny).Normalized();

                double side = normal.X * (origin.X - edge.P1.X) + normal.Y * (origin.Y - edge.P1.Y);
                if (side < 0) normal = new Vector2(-normal.X, -normal.Y);

                double dOrigin = normal.X * (origin.X - edge.P1.X) + normal.Y * (origin.Y - edge.P1.Y);
                double dDir = normal.Dot(direction);

                if (dDir < 0)
                {
                    double tHit = (dOrigin - radius) / -dDir;

                    if (tHit >= 0 && tHit <= distance && !double.IsInfinity(tHit))
                    {
                        Vector2 hitPoint = origin + direction * tHit;

                        double proj = ex * (hitPoint.X - edge.P1.X) + ey * (hitPoint.Y - edge.P1.Y);
                        if (proj >= 0 && proj <= edgeLenSq)
                        {
                            double T = tHit / distance;
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

            if (bestT <= 1.0)
            {
                hit = bestHit;
                return true;
            }

            return false;
        }

        private static void CheckEndpoint(
            Vector2 origin, double radius,
            Vector2 direction, double distance,
            Vector2 endpoint,
            ref double bestT, ref SweepHit bestHit)
        {
            double dx = origin.X - endpoint.X;
            double dy = origin.Y - endpoint.Y;

            double a = direction.Dot(direction);
            double b = 2 * (dx * direction.X + dy * direction.Y);
            double c = dx * dx + dy * dy - radius * radius;
            double disc = b * b - 4 * a * c;

            if (disc < 0) return;

            double sqrtDisc = Math.Sqrt(disc);
            double twoA = 2 * a;

            double t1 = (-b - sqrtDisc) / twoA;
            double t2 = (-b + sqrtDisc) / twoA;

            double t = double.MaxValue;
            if (t1 >= 0 && t1 <= distance) t = t1;
            else if (t2 >= 0 && t2 <= distance) t = t2;

            if (t == double.MaxValue || double.IsInfinity(t)) return;

            double T = t / distance;
            if (T >= bestT) return;

            Vector2 hitPoint = origin + direction * t;
            Vector2 normal = (hitPoint - endpoint).Normalized();

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