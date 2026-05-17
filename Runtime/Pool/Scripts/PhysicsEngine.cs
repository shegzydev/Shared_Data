using System;
using System.Collections.Generic;

namespace PhysicsEngine
{
    public class PhysicsParameters
    {
        public const int scale = 1000;
        public const int solverIterations = 5;

        public const long restitution = 1;
        public const long friction = 995;

        public const long tickRate = 250;
        public const long tickMs = 1000 / tickRate;
    };

    public class Logger
    {
        public static Action<string> Log = _ => { };
    }

    class PhysicsMath
    {
        public static long ISqrt(long n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException();
            if (n == 0) return 0;
            long x = (long)Math.Sqrt(n); // initial estimate only
                                         // Converge to floor(sqrt(n))
            while (x * x > n) x--;
            while ((x + 1) * (x + 1) <= n) x++;
            return x;
        }
    }

    public class Vector2
    {
        public long X { get; set; }
        public long Y { get; set; }

        public Vector2() { }

        public Vector2(double x, double y)
        {
            X = (long)(x * PhysicsParameters.scale);
            Y = (long)(y * PhysicsParameters.scale);
        }

        public Vector2(long x, long y)
        {
            X = x;
            Y = y;
        }

        public double GetX() => X / (double)PhysicsParameters.scale;
        public double GetY() => Y / (double)PhysicsParameters.scale;

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 v, double f) => new Vector2((long)(v.X * f), (long)(v.Y * f));
        public static Vector2 operator *(double f, Vector2 v) => new Vector2((long)(v.X * f), (long)(v.Y * f));
        public static Vector2 operator /(Vector2 v, double f) => new Vector2((long)(v.X / f), (long)(v.Y / f));

        public long Dot(Vector2 other) => X * other.X + Y * other.Y;

        public long MagnitudeSquared() => X * X + Y * Y;
        public double Magnitude() => Math.Sqrt(MagnitudeSquared());
    }

    public class Edge
    {
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }
    }

    public class Circle
    {
        public Vector2 Velocity { get; set; }
        public Vector2 Center { get; set; }
        public Vector2 PrevCenter { get; set; }
        public long Radius { get; set; }

        public void ApplyFriction()
        {
            Velocity.X = Velocity.X * PhysicsParameters.friction / PhysicsParameters.scale;
            Velocity.Y = Velocity.Y * PhysicsParameters.friction / PhysicsParameters.scale;
        }
    }

    class Solver
    {
        public void Solve(Circle circle, Edge edge)
        {
            // Edge vector and its squared length
            var ex = edge.P2.X - edge.P1.X;
            var ey = edge.P2.Y - edge.P1.Y;
            var edgeLenSq = ex * ex + ey * ey;

            if (edgeLenSq == 0) return; // degenerate edge

            // Project circle center onto the edge line, clamped to [0, edgeLenSq]
            // t = dot(center - start, edge) / edgeLenSq  — we keep as numerator/denom
            var cx = circle.Center.X - edge.P1.X;
            var cy = circle.Center.Y - edge.P1.Y;
            var t = cx * ex + cy * ey;
            t = Math.Clamp(t, 0, edgeLenSq);

            // Closest point on segment (scaled by edgeLenSq to stay integer)
            // closest = start + t * edge / edgeLenSq
            var closestX = edge.P1.X + t * ex / edgeLenSq;
            var closestY = edge.P1.Y + t * ey / edgeLenSq;

            // Vector from closest point to circle center
            var nx = circle.Center.X - closestX;
            var ny = circle.Center.Y - closestY;
            var distSq = nx * nx + ny * ny;

            if (distSq == 0 || distSq >= circle.Radius * circle.Radius) return;

            var dist = PhysicsMath.ISqrt(distSq);

            // Reflect velocity along the normal (edges are static — infinite mass)
            // v' = v - 2(v · n̂)n̂
            var velDotNormal = circle.Velocity.X * nx + circle.Velocity.Y * ny;

            if (velDotNormal > 0) return; // moving away from edge

            circle.Velocity.X -= 2 * velDotNormal * nx / distSq;
            circle.Velocity.Y -= 2 * velDotNormal * ny / distSq;

            // Positional correction — push circle out of edge
            var overlap = circle.Radius - dist;
            circle.Center.X += nx * overlap / dist;
            circle.Center.Y += ny * overlap / dist;
        }

        public void Solve(Circle c1, Circle c2)
        {
            var dx = c2.Center.X - c1.Center.X;
            var dy = c2.Center.Y - c1.Center.Y;
            var distSq = dx * dx + dy * dy;

            if (distSq == 0) return;

            var radSum = c1.Radius + c2.Radius;
            if (distSq > radSum * radSum) return;

            var dist = PhysicsMath.ISqrt(distSq);

            // --- Positional correction ---
            // Multiply before divide to avoid truncating small overlaps
            var overlap = radSum - dist;
            c1.Center.X -= dx * overlap / (2 * dist);
            c1.Center.Y -= dy * overlap / (2 * dist);
            c2.Center.X += dx * overlap / (2 * dist);
            c2.Center.Y += dy * overlap / (2 * dist);

            // --- Velocity resolution ---
            // velDotNormal uses raw dx/dy (scaled by dist), so divide by distSq
            // to normalize — the sqrt cancels algebraically
            var rvx = c2.Velocity.X - c1.Velocity.X;
            var rvy = c2.Velocity.Y - c1.Velocity.Y;
            var velDotNormal = rvx * dx + rvy * dy;

            if (velDotNormal > 0) return; // moving apart

            c1.Velocity.X += velDotNormal * dx / distSq;
            c1.Velocity.Y += velDotNormal * dy / distSq;
            c2.Velocity.X -= velDotNormal * dx / distSq;
            c2.Velocity.Y -= velDotNormal * dy / distSq;
        }

        /*public void Solve(Circle c1, Circle c2)
        {
            var dx = c2.Center.X - c1.Center.X;
            var dy = c2.Center.Y - c1.Center.Y;
            var distSq = dx * dx + dy * dy;

            if (distSq == 0) return;

            var radSum = c1.Radius + c2.Radius;
            if (distSq > radSum * radSum) return;

            // Relative velocity dot normal (numerator only — dist cancels later)
            // normal = (dx, dy) / dist, but we defer the division:
            // relVel · normal = (rvx * dx + rvy * dy) / dist
            var rvx = c1.Velocity.X - c2.Velocity.X;
            var rvy = c1.Velocity.Y - c2.Velocity.Y;
            var velDotNormal = rvx * dx + rvy * dy; // × dist (implicit denominator)

            if (velDotNormal > 0) return; // moving apart

            // Integer sqrt — only called once per collision
            var dist = PhysicsMath.ISqrt(distSq);

            // Impulse components: (velDotNormal / dist) * normal / dist
            // = velDotNormal * (dx, dy) / distSq  — no sqrt needed here!
            c1.Velocity.X -= velDotNormal * dx / distSq;
            c1.Velocity.Y -= velDotNormal * dy / distSq;
            c2.Velocity.X += velDotNormal * dx / distSq;
            c2.Velocity.Y += velDotNormal * dy / distSq;

            // Positional correction: overlap = radSum - dist (sqrt needed once)
            var overlap = radSum - dist;
            c1.Center.X -= dx * overlap / (2 * dist);
            c1.Center.Y -= dy * overlap / (2 * dist);
            c2.Center.X += dx * overlap / (2 * dist);
            c2.Center.Y += dy * overlap / (2 * dist);
        }*/
    }

    public class PoolPhysics
    {
        public Action<float> Render = _ => { };

        public List<Circle> circles = new();
        public List<Edge> edges = new();

        public void AddCircle(Circle c) => circles.Add(c);
        public void AddEdge(Edge e) => edges.Add(e);

        Solver solver = new Solver();

        long accumulator = 0;
        long lastTime = 0;

        public PoolPhysics()
        {

        }

        public void Tick()
        {
            if (lastTime == 0)
            {
                lastTime = GetCurrentTimeMs;
                return;
            }

            long now = GetCurrentTimeMs;
            long elapsed = now - lastTime;

            lastTime = now;

            accumulator += elapsed;

            while (accumulator >= PhysicsParameters.tickMs)
            {
                FixedTick();
                accumulator -= PhysicsParameters.tickMs;
            }

            float alpha = (float)accumulator / PhysicsParameters.tickMs;
            Render(alpha);
        }

        private void FixedTick()
        {
            // Move circles
            foreach (var c in circles)
            {
                c.PrevCenter = c.Center;
                c.Center += c.Velocity;
            }

            for (int iter = 0; iter < PhysicsParameters.solverIterations; iter++)
            {
                // Check circle-edge collisions
                foreach (var c in circles)
                    foreach (var e in edges)
                        solver.Solve(c, e);

                // Check circle-circle collisions
                for (int i = 0; i < circles.Count; i++)
                    for (int j = i + 1; j < circles.Count; j++)
                        solver.Solve(circles[i], circles[j]);
            }

            foreach (var c in circles)
            {
                c.ApplyFriction();
            }
        }

        long GetCurrentTimeMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}