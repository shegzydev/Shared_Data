using System;
using System.Collections.Generic;

namespace PhysicsEngine
{
    public class PhysicsParameters
    {
        public const double SCALE = 1000.0; // Scale factor (1 unit = 0.001 in real world)
        public const int solverIterations = 20;
        public const double restitution = 1.0;
        public const double friction = 0.997;
        public const double tickRate = 60.0;
        public const double tickMs = 1000.0 / tickRate;
        public const double maxTickMS = tickMs * 4;
        public const double dt = tickMs / 1000;
        public const double minVelocity = 300.0;       // 0.01 * SCALE
        public const double ballRadius = 2925.0;      // 2.925 * 1000

        public static bool circleCollided, edgeCollided = false;
    }

    public class Logger
    {
        public static Action<object> Log = _ => { };
    }

    public struct Vector2
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Vector2(double x, double y) { X = x; Y = y; }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 v, double f) => new Vector2(v.X * f, v.Y * f);
        public static Vector2 operator *(double f, Vector2 v) => new Vector2(v.X * f, v.Y * f);
        public static Vector2 operator /(Vector2 v, double d) => new Vector2(v.X / d, v.Y / d);

        public double Dot(Vector2 other) => X * other.X + Y * other.Y;
        public double MagnitudeSquared() => X * X + Y * Y;
        public double Magnitude() => Math.Sqrt(MagnitudeSquared());

        public Vector2 Normalized()
        {
            double mag = Magnitude();
            if (mag < 1e-10) return new Vector2(0, 0);
            return new Vector2(X / mag, Y / mag);
        }

        public static Vector2 Zero => new Vector2(0, 0);

        public override string ToString() => $"({X:F0}, {Y:F0})";

        // Convert to real-world units (divide by SCALE)
        public Vector2 ToReal() => new Vector2(X / PhysicsParameters.SCALE, Y / PhysicsParameters.SCALE);
    }

    public class PhysicsObj
    {
        public string tag = "";
    }

    public class Edge : PhysicsObj
    {
        public Vector2 P1 { get; set; }
        public Vector2 P2 { get; set; }
        public bool Bouncy { get; set; } = true;

        public Vector2 GetNormal()
        {
            double nx = -(P2.Y - P1.Y);
            double ny = P2.X - P1.X;
            return new Vector2(nx, ny).Normalized();
        }

        public Vector2 ClosestPoint(Vector2 point)
        {
            double ex = P2.X - P1.X;
            double ey = P2.Y - P1.Y;
            double lenSq = ex * ex + ey * ey;
            if (lenSq < 1e-10) return P1;

            double t = ((point.X - P1.X) * ex + (point.Y - P1.Y) * ey) / lenSq;
            t = Math.Max(0, Math.Min(1, t));

            return new Vector2(P1.X + t * ex, P1.Y + t * ey);
        }

        public Vector2 ClosestPointUnClamped(Vector2 point)
        {
            double ex = P2.X - P1.X;
            double ey = P2.Y - P1.Y;
            double lenSq = ex * ex + ey * ey;
            if (lenSq < 1e-10) return P1;

            double t = ((point.X - P1.X) * ex + (point.Y - P1.Y) * ey) / lenSq;

            return new Vector2(P1.X + t * ex, P1.Y + t * ey);
        }

        public double proj(Vector2 point)
        {
            double ex = P2.X - P1.X;
            double ey = P2.Y - P1.Y;

            double lenSq = ex * ex + ey * ey;

            if (lenSq < 1e-10) return 0;

            double t = ((point.X - P1.X) * ex + (point.Y - P1.Y) * ey) / lenSq;

            return t;
        }

        public double DistanceToPoint(Vector2 point)
        {
            Vector2 closest = ClosestPoint(point);
            double dx = point.X - closest.X;
            double dy = point.Y - closest.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public class Circle : PhysicsObj
    {
        public Vector2 Velocity { get; set; } = Vector2.Zero;
        public Vector2 Accel { get; set; } = Vector2.Zero;
        public Vector2 Center { get; set; } = Vector2.Zero;
        public Vector2 PrevCenter { get; set; } = Vector2.Zero;
        public double Radius { get; set; } = PhysicsParameters.ballRadius;
        public bool IsPocketed { get; set; } = false;

        public Circle() { }

        public Circle(double x, double y, double radius = -1)
        {
            Center = new Vector2(x, y);
            PrevCenter = Center;
            Radius = radius > 0 ? radius : PhysicsParameters.ballRadius;
            Velocity = Vector2.Zero;
        }

        public void ApplyFriction(double dt)
        {
            Velocity = Velocity * (1.0 / (1.0 + dt * PhysicsParameters.friction));
            if (Velocity.Magnitude() < PhysicsParameters.minVelocity)
                Velocity = Vector2.Zero;
        }

        public void ApplyGravity(double Y)
        {
            Accel = new Vector2(Accel.X, Y);
        }

        public override string ToString() => $"Center=({Center.X:F0}, {Center.Y:F0}), Vel=({Velocity.X:F0}, {Velocity.Y:F0})";
    }

    /*
    public class Solver
    {
        public void Solve(Circle circle, Edge edge, Action<(Circle, Edge)> onCollision)
        {
            Vector2 closest = edge.ClosestPoint(circle.Center);

            double dx = circle.Center.X - closest.X;
            double dy = circle.Center.Y - closest.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist >= circle.Radius) return;

            onCollision?.Invoke((circle, edge));

            Vector2 normal;
            if (dist < 1e-10)
                normal = edge.GetNormal();
            else
                normal = new Vector2(dx / dist, dy / dist);

            double overlap = circle.Radius - dist;
            circle.Center = circle.Center + normal * overlap;

            if (!edge.Bouncy)
            {
                circle.Velocity = Vector2.Zero;
                return;
            }

            double velDotNormal = circle.Velocity.Dot(normal);
            if (velDotNormal > 0) return;

            circle.Velocity = circle.Velocity - (1 + PhysicsParameters.restitution) * velDotNormal * normal;
        }

        public void Solve(Circle a, Circle b, Action<(Circle, Circle)> onCollision)
        {
            double dx = b.Center.X - a.Center.X;
            double dy = b.Center.Y - a.Center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double minDist = a.Radius + b.Radius;

            if (dist >= minDist) return;

            onCollision?.Invoke((a, b));

            Vector2 normal;
            if (dist < 1e-10)
                normal = new Vector2(1, 0);
            else
                normal = new Vector2(dx / dist, dy / dist);

            double overlap = minDist - dist;
            Vector2 correction = normal * (overlap * 0.5);
            a.Center = a.Center - correction;
            b.Center = b.Center + correction;

            Vector2 rv = b.Velocity - a.Velocity;
            double velAlong = rv.Dot(normal);

            if (velAlong > 0) return;

            double e = PhysicsParameters.restitution;
            double impulse = -(1 + e) * velAlong / 2.0;

            a.Velocity = a.Velocity - normal * impulse;
            b.Velocity = b.Velocity + normal * impulse;
        }
    }
    */

    /*
    public class Solver
    {
        private const double CCD_EPSILON = 1e-8;

        public void Solve(Circle circle, Edge edge, Action<(Circle, Edge)> onCollision)
        {
            // Standard discrete collision check first
            Vector2 closest = edge.ClosestPoint(circle.Center);
            double dx = circle.Center.X - closest.X;
            double dy = circle.Center.Y - closest.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist >= circle.Radius) return;

            onCollision?.Invoke((circle, edge));

            Vector2 normal;
            if (dist < 1e-10)
                normal = edge.GetNormal();
            else
                normal = new Vector2(dx / dist, dy / dist);

            double overlap = circle.Radius - dist;
            circle.Center = circle.Center + normal * overlap;

            if (!edge.Bouncy)
            {
                circle.Velocity = Vector2.Zero;
                return;
            }

            double velDotNormal = circle.Velocity.Dot(normal);
            if (velDotNormal > 0) return;

            circle.Velocity = circle.Velocity - (1 + PhysicsParameters.restitution) * velDotNormal * normal;
        }

        public void Solve(Circle a, Circle b, Action<(Circle, Circle)> onCollision)
        {
            // Standard discrete collision check first
            double dx = b.Center.X - a.Center.X;
            double dy = b.Center.Y - a.Center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double minDist = a.Radius + b.Radius;

            if (dist >= minDist) return;

            onCollision?.Invoke((a, b));

            Vector2 normal;
            if (dist < 1e-10)
                normal = new Vector2(1, 0);
            else
                normal = new Vector2(dx / dist, dy / dist);

            double overlap = minDist - dist;
            Vector2 correction = normal * (overlap * 0.5);
            a.Center = a.Center - correction;
            b.Center = b.Center + correction;

            Vector2 rv = b.Velocity - a.Velocity;
            double velAlong = rv.Dot(normal);

            if (velAlong > 0) return;

            double e = PhysicsParameters.restitution;
            double impulse = -(1 + e) * velAlong / 2.0;

            a.Velocity = a.Velocity - normal * impulse;
            b.Velocity = b.Velocity + normal * impulse;
        }

        // New CCD method for circle-circle collision
        public bool SolveCCD(Circle a, Circle b, double deltaTime, Action<(Circle, Circle)> onCollision)
        {
            Vector2 relativeVelocity = b.Velocity - a.Velocity;
            Vector2 relativePosition = b.Center - a.Center;
            double minDist = a.Radius + b.Radius;

            // Quadratic equation coefficients: at² + bt + c = 0
            double a_coeff = relativeVelocity.Dot(relativeVelocity);
            double b_coeff = 2 * relativePosition.Dot(relativeVelocity);
            double c_coeff = relativePosition.Dot(relativePosition) - minDist * minDist;

            // Check discriminant
            double discriminant = b_coeff * b_coeff - 4 * a_coeff * c_coeff;

            if (discriminant < CCD_EPSILON || a_coeff < CCD_EPSILON)
            {
                return false;
            }

            double sqrtDiscriminant = Math.Sqrt(discriminant);
            double t1 = (-b_coeff - sqrtDiscriminant) / (2 * a_coeff);
            double t2 = (-b_coeff + sqrtDiscriminant) / (2 * a_coeff);

            // Find first valid collision time in [0, deltaTime]
            double collisionTime = -1;
            if (t1 >= 0 && t1 <= deltaTime) collisionTime = t1;
            else if (t2 >= 0 && t2 <= deltaTime) collisionTime = t2;

            if (collisionTime < 0 || collisionTime > deltaTime) return false;

            // Move to collision point
            Vector2 oldPositionA = a.Center;
            Vector2 oldPositionB = b.Center;
            Vector2 oldVelocityA = a.Velocity;
            Vector2 oldVelocityB = b.Velocity;

            a.Center = a.Center + a.Velocity * collisionTime;
            b.Center = b.Center + b.Velocity * collisionTime;

            // Handle collision at the exact moment of impact
            onCollision?.Invoke((a, b));

            // Compute collision response
            Vector2 collisionNormal = (b.Center - a.Center).Normalized();
            Vector2 rv = b.Velocity - a.Velocity;
            double velAlong = rv.Dot(collisionNormal);

            if (velAlong < 0)
            {
                double e = PhysicsParameters.restitution;
                double impulse = -(1 + e) * velAlong / 2.0;
                a.Velocity = a.Velocity - collisionNormal * impulse;
                b.Velocity = b.Velocity + collisionNormal * impulse;
            }

            // Move remaining time after collision
            double remainingTime = deltaTime - collisionTime;
            if (remainingTime > CCD_EPSILON)
            {
                a.Center = a.Center + a.Velocity * remainingTime;
                b.Center = b.Center + b.Velocity * remainingTime;
            }

            return true;
        }

        // New CCD method for circle-edge collision
        public bool SolveCCD(Circle circle, Edge edge, double deltaTime, Action<(Circle, Edge)> onCollision)
        {
            Vector2 edgeStart = edge.P1;
            Vector2 edgeEnd = edge.P2;
            Vector2 edgeVector = edgeEnd - edgeStart;
            double edgeLengthSquared = edgeVector.Dot(edgeVector);

            if (edgeLengthSquared < CCD_EPSILON) return false;

            Vector2 edgeDirection = edgeVector / Math.Sqrt(edgeLengthSquared);

            // Project circle center onto edge line
            Vector2 toStart = circle.Center - edgeStart;
            double projection = toStart.Dot(edgeDirection);

            // Clamp projection to edge segment
            double t = Math.Max(0, Math.Min(1, projection / Math.Sqrt(edgeLengthSquared)));
            Vector2 closestPoint = edgeStart + edgeDirection * (t * Math.Sqrt(edgeLengthSquared));

            // Check if circle is moving towards the edge
            Vector2 relativeVelocity = circle.Velocity;
            Vector2 toClosest = circle.Center - closestPoint;
            double distToClosest = toClosest.Magnitude();

            if (distToClosest < circle.Radius)
            {
                // Already colliding, handle discrete collision
                Solve(circle, edge, onCollision);
                return true;
            }

            // Solve quadratic for collision time
            Vector2 velocityDir = relativeVelocity.Normalized();
            double a_coeff = relativeVelocity.Dot(relativeVelocity);
            double b_coeff = 2 * toClosest.Dot(relativeVelocity);
            double c_coeff = distToClosest * distToClosest - circle.Radius * circle.Radius;

            double discriminant = b_coeff * b_coeff - 4 * a_coeff * c_coeff;

            if (discriminant < CCD_EPSILON || a_coeff < CCD_EPSILON)
            {
                return false;
            }

            double sqrtDiscriminant = Math.Sqrt(discriminant);
            double t1 = (-b_coeff - sqrtDiscriminant) / (2 * a_coeff);
            double t2 = (-b_coeff + sqrtDiscriminant) / (2 * a_coeff);

            double collisionTime = -1;
            if (t1 >= 0 && t1 <= deltaTime) collisionTime = t1;
            else if (t2 >= 0 && t2 <= deltaTime) collisionTime = t2;

            if (collisionTime < 0 || collisionTime > deltaTime) return false;

            // Move to collision point
            Vector2 oldCenter = circle.Center;
            circle.Center = circle.Center + circle.Velocity * collisionTime;

            // Handle collision
            onCollision?.Invoke((circle, edge));

            // Compute collision normal and response
            Vector2 newClosest = edge.ClosestPoint(circle.Center);
            Vector2 normal = (circle.Center - newClosest).Normalized();

            double velDotNormal = circle.Velocity.Dot(normal);
            if (velDotNormal < 0)
            {
                if (!edge.Bouncy)
                {
                    circle.Velocity = Vector2.Zero;
                }
                else
                {
                    circle.Velocity = circle.Velocity - (1 + PhysicsParameters.restitution) * velDotNormal * normal;
                }
            }

            // Move remaining time
            double remainingTime = deltaTime - collisionTime;
            if (remainingTime > CCD_EPSILON)
            {
                circle.Center = circle.Center + circle.Velocity * remainingTime;
            }

            // Final position adjustment to prevent penetration
            newClosest = edge.ClosestPoint(circle.Center);
            Vector2 finalToClosest = circle.Center - newClosest;
            double finalDist = finalToClosest.Magnitude();

            if (finalDist < circle.Radius)
            {
                double overlap = circle.Radius - finalDist;
                circle.Center = circle.Center + normal * overlap;
            }

            return true;
        }

        // Helper method to choose between discrete and CCD based on velocity
        public void SolveWithCCD(Circle a, Circle b, double deltaTime, Action<(Circle, Circle)> onCollision)
        {
            double relativeSpeed = (b.Velocity - a.Velocity).Magnitude();
            double minDist = a.Radius + b.Radius;
            double timeToCollision = minDist / (relativeSpeed + CCD_EPSILON);

            // Use CCD if objects are moving fast enough to potentially tunnel
            if (relativeSpeed > 0 && timeToCollision < deltaTime)
            {
                SolveCCD(a, b, deltaTime, onCollision);
            }
            else
            {
                Solve(a, b, onCollision);
            }
        }

        public void SolveWithCCD(Circle circle, Edge edge, double deltaTime, Action<(Circle, Edge)> onCollision)
        {
            double speed = circle.Velocity.Magnitude();
            double timeToCollision = circle.Radius / (speed + CCD_EPSILON);

            // Use CCD if circle is moving fast enough
            if (speed > 0 && timeToCollision < deltaTime)
            {
                SolveCCD(circle, edge, deltaTime, onCollision);
            }
            else
            {
                Solve(circle, edge, onCollision);
            }
        }
    }
    */

    public class Solver
    {
        public struct CCDHit
        {
            public double TOI;
            public Vector2 normal;
            public bool bouncy;

            public PhysicsObj A;
            public PhysicsObj B;
            public bool bIsCircle;
        };

        const double EPSILON = 1e-8;

        // =========================================================
        // Circle vs Edge CCD
        // =========================================================

        public bool SolveCCD(Circle circle, Edge edge, double dt, out CCDHit hit)
        {
            hit = new CCDHit { TOI = dt };

            Vector2 edgeNormal = edge.GetNormal();
            double startDist = (circle.Center - edge.P1).Dot(edgeNormal);
            double velAlongNormal = circle.Velocity.Dot(edgeNormal);

            // Not approaching edge
            if (Math.Abs(velAlongNormal) < EPSILON) return false;

            double t1 = (circle.Radius - startDist) / velAlongNormal;
            double t2 = (-circle.Radius - startDist) / velAlongNormal;

            var t = Math.Min(t1, t2);

            if (t < 0 || t > dt) return false;

            Vector2 hitPoint = circle.Center + circle.Velocity * t;
            var proj = edge.proj(hitPoint);

            if (proj < 0 || proj > 1)
            {
                //Vertices resolution
                var vA = SolveCCD(circle, new Circle(edge.P1.X, edge.P1.Y, 0), dt, out var hit1);
                var vB = SolveCCD(circle, new Circle(edge.P2.X, edge.P2.Y, 0), dt, out var hit2);

                if (!(vA || vB)) return false;

                if (vA && vB)
                {
                    hit.normal = hit1.TOI < hit2.TOI ? hit1.normal : hit2.normal;
                    hit.TOI = Math.Min(hit1.TOI, hit2.TOI);
                }
                else if (vA)
                {
                    hit.normal = hit1.normal;
                    hit.TOI = hit1.TOI;
                }
                else if (vB)
                {
                    hit.normal = hit2.normal;
                    hit.TOI = hit2.TOI;
                }
                hit.normal *= 1;
            }
            else
            {
                hit.normal = edgeNormal;
                hit.TOI = t;
            }

            hit.bouncy = edge.Bouncy;
            hit.A = circle;
            hit.B = edge;
            hit.bIsCircle = false;

            return true;
        }

        /*
        public bool SolveCCD(Circle circle, Edge edge, double dt, out CCDHit hit)
        {
            hit = new CCDHit { };

            Vector2 start = circle.Center;
            Vector2 end = start + circle.Velocity * dt;

            Vector2 edgeNormal = edge.GetNormal();

            // Signed distances to line
            double startDist = (start - edge.P1).Dot(edgeNormal);
            double endDist = (end - edge.P1).Dot(edgeNormal);

            double denom = startDist - endDist;

            if (Math.Abs(denom) < EPSILON)
                return false;

            // TOI
            double t = (startDist - circle.Radius * Math.Sign(startDist)) / denom;

            if (t < 0.0 || t > 1.0)
                return false;

            Vector2 hitPoint = start + (end - start) * t;

            // Ensure projected point lies on segment
            Vector2 closest = edge.ClosestPoint(hitPoint);
            double distSq = (closest - hitPoint).MagnitudeSquared();

            if (distSq > circle.Radius * circle.Radius)
                return false;

            Vector2 normal = edge.GetNormal();
            if (circle.Velocity.Dot(normal) > 0) normal = normal * -1;

            hit.normal = normal;
            hit.TOI = t * dt;
            hit.bouncy = edge.Bouncy;
            hit.A = circle;
            hit.B = edge;
            hit.bIsCircle = false;

            return true;
        }
        */
        // =========================================================
        // Circle vs Circle CCD
        // =========================================================
        public bool SolveCCD(Circle a, Circle b, double dt, out CCDHit hit)
        {
            hit = new CCDHit { TOI = dt };

            Vector2 relativeStart = b.Center - a.Center;
            Vector2 relativeVelocity = b.Velocity - a.Velocity;

            double radius = a.Radius + b.Radius;

            double A = relativeVelocity.Dot(relativeVelocity);
            double B = 2.0 * relativeStart.Dot(relativeVelocity);
            double C = relativeStart.Dot(relativeStart) - radius * radius;

            if (A < EPSILON)
                return false;

            if (B >= 0)
                return false;

            double discriminant = B * B - 4 * A * C;

            if (discriminant < 0)
                return false;

            double sqrt = Math.Sqrt(discriminant);

            double t0 = (-B - sqrt) / (2 * A);

            if (t0 < 0 || t0 > dt)
                return false;

            // Move to TOI
            Vector2 hitA = a.Center + a.Velocity * t0;
            Vector2 hitB = b.Center + b.Velocity * t0;

            Vector2 normal = (hitB - hitA).Normalized();

            hit.normal = normal;
            hit.TOI = t0;
            hit.bouncy = true;

            hit.A = a;
            hit.B = b;
            hit.bIsCircle = true;

            return true;
        }

        // =========================================================
        // Existing discrete solvers
        // =========================================================

        public void Solve(Circle circle, Edge edge, Action<(Circle, Edge)> onCollision)
        {
            Vector2 closest = edge.ClosestPoint(circle.Center);

            double dx = circle.Center.X - closest.X;
            double dy = circle.Center.Y - closest.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist > circle.Radius) return;

            if (!PhysicsParameters.edgeCollided)
            {
                onCollision?.Invoke((circle, edge));
                PhysicsParameters.edgeCollided = true;
            }

            Vector2 normal;
            if (dist < EPSILON)
                normal = edge.GetNormal();
            else
                normal = new Vector2(dx / dist, dy / dist);

            double overlap = circle.Radius - dist;
            circle.Center = circle.Center + normal * overlap;

            if (!edge.Bouncy)
            {
                var dot = circle.Velocity.Dot(normal);
                circle.Velocity -= normal * dot;
                return;
            }

            double velDotNormal = circle.Velocity.Dot(normal);
            if (velDotNormal > 0) return;

            circle.Velocity =
                circle.Velocity -
                (1 + PhysicsParameters.restitution) *
                velDotNormal * normal;
        }

        public void Solve(Circle a, Circle b, Action<(Circle, Circle)> onCollision)
        {
            double dx = b.Center.X - a.Center.X;
            double dy = b.Center.Y - a.Center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double minDist = a.Radius + b.Radius;

            if (dist > minDist) return;

            if (!PhysicsParameters.circleCollided)
            {
                onCollision?.Invoke((a, b));
                PhysicsParameters.circleCollided = true;
            }

            Vector2 normal;
            if (dist < EPSILON)
                normal = new Vector2(1, 0);
            else
                normal = new Vector2(dx / dist, dy / dist);

            double overlap = minDist - dist;
            Vector2 correction = normal * (overlap * 0.5);

            a.Center -= correction;
            b.Center += correction;

            Vector2 rv = b.Velocity - a.Velocity;
            double velAlong = rv.Dot(normal);

            if (velAlong > 0) return;

            double e = PhysicsParameters.restitution;
            double impulse = -(1 + e) * velAlong / 2.0;

            a.Velocity -= normal * impulse;
            b.Velocity += normal * impulse;
        }

        public bool GetEarliestImpact(List<Circle> circles, List<Edge> edges, double dt, out CCDHit TOIHIt)
        {
            TOIHIt = new CCDHit { TOI = dt * 2 };

            for (int i = 0; i < circles.Count; i++)
            {
                for (int j = i + 1; j < circles.Count; j++)
                {
                    if (SolveCCD(circles[i], circles[j], dt, out var hit) && hit.TOI < TOIHIt.TOI)
                    {
                        TOIHIt = hit;
                    }
                }
            }

            foreach (var c in circles)
            {
                foreach (var e in edges)
                {
                    if (SolveCCD(c, e, dt, out var hit) && hit.TOI < TOIHIt.TOI)
                    {
                        TOIHIt = hit;
                    }
                }
            }

            return TOIHIt.TOI <= dt;
        }
    }

    public class PoolPhysics
    {
        public Action<float> Render = _ => { };
        public Action<(Circle ball, Circle hole)> OnHole = _ => { };
        public Action Stopped = () => { };
        public Action<(Circle A, Circle B)> OnBallCollision = _ => { };
        public Action<(Circle A, Edge B)> OnEdgeCollision = _ => { };

        public List<Circle> circles = new();
        public List<Edge> edges = new();
        public List<Circle> holes = new();

        private Solver solver = new();
        private double accumulator = 0;
        private double lastTime = 0;
        private bool rolling = false;
        private bool wasRolling = false;

        public void AddCircle(Circle c) => circles.Add(c);
        public void AddEdge(Edge e) => edges.Add(e);
        public void AddHole(Circle h) => holes.Add(h);

        public List<Solver.CCDHit> CCDCache = new();

        public void Fire()
        {
            rolling = true;
            wasRolling = true;
        }

        public void Tick()
        {
            double now = GetCurrentTimeMs;
            if (lastTime == 0)
            {
                lastTime = now;
                return;
            }

            double elapsed = now - lastTime;
            lastTime = now;
            accumulator += elapsed;

            while (accumulator >= PhysicsParameters.tickMs)
            {
                FixedTick();
                accumulator -= PhysicsParameters.tickMs;
            }

            float alpha = (float)(accumulator / PhysicsParameters.tickMs);
            Render(alpha);
        }

        private void FixedTick()
        {
            PhysicsParameters.circleCollided = PhysicsParameters.edgeCollided = false;

            double timeRemaining = PhysicsParameters.dt;

            bool hitEarly;

            while (hitEarly = solver.GetEarliestImpact(circles, edges, timeRemaining, out var hit))
            {
                if (hit.TOI > 0)
                {
                    Integrate(hit.TOI);

                    Resolve(hit);

                    timeRemaining -= hit.TOI;
                }
                else
                {
                    break;
                    //Chain Reaction
                }
            }

            if(!hitEarly) Integrate(timeRemaining);

            CheckPocketed();

            AddDrag();
        }

        void RemoveOverlaps()
        {
            for (int iter = 0; iter < PhysicsParameters.solverIterations; iter++)
            {
                foreach (var c in circles)
                {
                    foreach (var e in edges)
                        solver.Solve(c, e, OnEdgeCollision);
                }

                for (int i = 0; i < circles.Count; i++)
                {
                    for (int j = i + 1; j < circles.Count; j++)
                    {
                        solver.Solve(circles[i], circles[j], OnBallCollision);
                    }
                }
            }
        }

        void Integrate(double dt)
        {
            foreach (var c in circles)
            {
                c.PrevCenter = c.Center;
                c.Center = c.Center + c.Velocity * dt;
            }
        }

        void Resolve(Solver.CCDHit hit)
        {
            if (hit.bIsCircle)
            {
                Circle A = (Circle)hit.A;
                Circle B = (Circle)hit.B;

                double aDotN = A.Velocity.Dot(hit.normal);
                double bDotN = B.Velocity.Dot(hit.normal);

                A.Velocity = A.Velocity + (bDotN - aDotN) * hit.normal;
                B.Velocity = B.Velocity + (aDotN - bDotN) * hit.normal;

                A.Center -= hit.normal * 100;
                B.Center += hit.normal * 100;
            }
            else
            {
                Circle A = (Circle)hit.A;
                Edge B = (Edge)hit.B;

                if (!B.Bouncy)
                {
                    var along = A.Velocity.Dot(hit.normal);
                    A.Velocity = A.Velocity - (hit.normal * along);
                }
                else
                {
                    A.Velocity = A.Velocity - (2 * A.Velocity.Dot(hit.normal) * hit.normal);
                }

                A.Center += hit.normal * 100;
            }
        }

        void CheckPocketed()
        {
            foreach (var hole in holes)
            {
                foreach (var ball in circles)
                {
                    if (ball.IsPocketed) continue;

                    double dx = ball.Center.X - hole.Center.X;
                    double dy = ball.Center.Y - hole.Center.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist + ball.Radius <= hole.Radius * 1.2)
                    {
                        ball.IsPocketed = true;
                        OnHole((ball, hole));
                    }
                }
            }

        }

        void AddDrag()
        {
            bool spinning = false;

            foreach (var c in circles)
            {
                c.Velocity = c.Velocity + c.Accel * PhysicsParameters.dt;

                c.ApplyFriction(PhysicsParameters.dt);

                if (!c.IsPocketed & c.Velocity.Magnitude() > PhysicsParameters.minVelocity)
                    spinning = true;
            }

            wasRolling = rolling;
            rolling = spinning;

            if (!rolling && wasRolling)
                Stopped.Invoke();
        }

        private double GetCurrentTimeMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}