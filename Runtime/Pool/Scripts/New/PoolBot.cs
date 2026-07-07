using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FixedMath;
using PhysicsEngine;

public class PoolBot
{
    public struct Vector3
    {
        public double x;
        public double y;
        public double z;

        // Static readonly properties for common vectors
        public static readonly Vector3 zero = new Vector3(0.0, 0, 0);
        public static readonly Vector3 one = new Vector3(1.0, 1, 1);
        public static readonly Vector3 forward = new Vector3(0.0, 0, 1);
        public static readonly Vector3 back = new Vector3(0.0, 0, -1);
        public static readonly Vector3 up = new Vector3(0.0, 1, 0);
        public static readonly Vector3 down = new Vector3(0.0, -1, 0);
        public static readonly Vector3 right = new Vector3(1.0, 0, 0);
        public static readonly Vector3 left = new Vector3(-1.0, 0, 0);

        public Vector3(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Vector3(Fixed64 x, Fixed64 y, Fixed64 z)
        {
            this.x = (double)x;
            this.y = (double)y;
            this.z = (double)z;
        }

        public Vector3(double x, double y)
        {
            this.x = x;
            this.y = y;
            this.z = 0;
        }

        public Vector3(Fixed64 x, Fixed64 y)
        {
            this.x = (double)x;
            this.y = (double)y;
            this.z = 0;
        }

        public Vector3(Vector3 other)
        {
            x = other.x;
            y = other.y;
            z = other.z;
        }

        // Properties
        public double magnitude
        {
            get { return Math.Sqrt(x * x + y * y + z * z); }
        }

        public double sqrMagnitude
        {
            get { return x * x + y * y + z * z; }
        }

        public Vector3 normalized
        {
            get
            {
                double mag = magnitude;
                if (mag > 0)
                    return new Vector3(x / mag, y / mag, z / mag);
                return zero;
            }
        }

        // Indexer for array-like access
        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException("Vector3 index must be 0, 1, or 2");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("Vector3 index must be 0, 1, or 2");
                }
            }
        }

        // Methods
        public void Set(double newX, double newY, double newZ)
        {
            x = newX;
            y = newY;
            z = newZ;
        }

        public void Normalize()
        {
            double mag = magnitude;
            if (mag > 0)
            {
                x /= mag;
                y /= mag;
                z /= mag;
            }
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }

        public string ToString(string format)
        {
            return $"({x.ToString(format)}, {y.ToString(format)}, {z.ToString(format)})";
        }

        public override bool Equals(object obj)
        {
            if (obj is Vector3)
            {
                Vector3 other = (Vector3)obj;
                return x == other.x && y == other.y && z == other.z;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2;
        }

        // Static methods
        public static double Angle(Vector3 from, Vector3 to)
        {
            double dot = Dot(from, to);
            double magProduct = from.magnitude * to.magnitude;
            if (magProduct == 0)
                return 0;

            double cosAngle = dot / magProduct;
            cosAngle = Math.Max(-1, Math.Min(1, cosAngle)); // Clamp to avoid floating point errors
            return Math.Acos(cosAngle) * (180.0 / Math.PI);
        }

        public static double SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
        {
            double angle = Angle(from, to);
            Vector3 cross = Cross(from, to);
            double dot = Dot(cross, axis);
            return angle * Math.Sign(dot);
        }

        public static Vector3 ClampMagnitude(Vector3 vector, double maxLength)
        {
            if (vector.sqrMagnitude > maxLength * maxLength)
            {
                return vector.normalized * maxLength;
            }
            return vector;
        }

        public static double Distance(Vector3 a, Vector3 b)
        {
            double dx = a.x - b.x;
            double dy = a.y - b.y;
            double dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static double Dot(Vector3 lhs, Vector3 rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }

        public static Vector3 Cross(Vector3 lhs, Vector3 rhs)
        {
            return new Vector3(
                lhs.y * rhs.z - lhs.z * rhs.y,
                lhs.z * rhs.x - lhs.x * rhs.z,
                lhs.x * rhs.y - lhs.y * rhs.x
            );
        }

        public static Vector3 Lerp(Vector3 a, Vector3 b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return new Vector3(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t
            );
        }

        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, double t)
        {
            return new Vector3(
                a.x + (b.x - a.x) * t,
                a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t
            );
        }

        public static Vector3 Max(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Math.Max(a.x, b.x),
                Math.Max(a.y, b.y),
                Math.Max(a.z, b.z)
            );
        }

        public static Vector3 Min(Vector3 a, Vector3 b)
        {
            return new Vector3(
                Math.Min(a.x, b.x),
                Math.Min(a.y, b.y),
                Math.Min(a.z, b.z)
            );
        }

        public static Vector3 MoveTowards(Vector3 current, Vector3 target, double maxDistanceDelta)
        {
            double dx = target.x - current.x;
            double dy = target.y - current.y;
            double dz = target.z - current.z;
            double magnitude = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (magnitude <= maxDistanceDelta || magnitude == 0)
                return target;

            return new Vector3(
                current.x + dx / magnitude * maxDistanceDelta,
                current.y + dy / magnitude * maxDistanceDelta,
                current.z + dz / magnitude * maxDistanceDelta
            );
        }

        public static Vector3 Project(Vector3 vector, Vector3 onNormal)
        {
            double dot = Dot(vector, onNormal);
            double sqrMag = onNormal.sqrMagnitude;
            if (sqrMag == 0)
                return zero;

            return onNormal * (dot / sqrMag);
        }

        public static Vector3 Reflect(Vector3 inDirection, Vector3 inNormal)
        {
            double dot = Dot(inDirection, inNormal);
            return new Vector3(
                inDirection.x - 2 * dot * inNormal.x,
                inDirection.y - 2 * dot * inNormal.y,
                inDirection.z - 2 * dot * inNormal.z
            );
        }

        // Operator overloads
        public static Vector3 operator +(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static Vector3 operator -(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static Vector3 operator -(Vector3 a)
        {
            return new Vector3(-a.x, -a.y, -a.z);
        }

        public static Vector3 operator *(Vector3 a, double d)
        {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        public static Vector3 operator *(double d, Vector3 a)
        {
            return new Vector3(a.x * d, a.y * d, a.z * d);
        }

        public static Vector3 operator /(Vector3 a, double d)
        {
            if (d == 0)
                throw new DivideByZeroException("Division by zero in Vector3 division");
            return new Vector3(a.x / d, a.y / d, a.z / d);
        }

        public static bool operator ==(Vector3 lhs, Vector3 rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;
            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
                return false;
            return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
        }

        public static bool operator !=(Vector3 lhs, Vector3 rhs)
        {
            return !(lhs == rhs);
        }
    }

    static Vector3[] holesPositions = new Vector3[]
    {
        new(-91.5700, 38.890, 0),
        new(-91.7600, -38.640, 0),
        new(91.8100, -38.630, 0),
        new(92.0300, 38.560, 0),
        new(0.0, 41.000, 0),
        new(0.0, -41.000, 0)
    };

    struct Ball
    {
        public static readonly double radius = 2.925;
    }

    public static Action<(int player, Vector3[] path)> OnResolvedPath;

    PoolSet pool;
    int player;
    bool win;

    static readonly double TableHeight = 84;
    static readonly double TableWidth = 190;

    public PoolBot(PoolSet _pool, int _player, bool _win)
    {
        win = _win;
        pool = _pool;
        player = _player;
        pool.OnTurnChanged += ComputeShot;
    }

    void ComputeShot(int turn)
    {
        if (turn != player) return;
        Compute(turn);
    }

    public struct F_Shot
    {
        public Vector3 pot;
        public double dev;
        public double dist;
        public Vector3[] path;
    }

    async void Compute(int turn)
    {
        if (turn != player) return;

        Logger.Log("Computing...");

        await Task.Delay(2500);

        var myBalls = new List<Vector3>();

        var solids = new List<Vector3>();
        for (int i = 0; i < 7; i++)
        {
            if ((double)pool.balls[i].Center.X > TableWidth / 2) continue;
            solids.Add(new Vector3(pool.balls[i].Center.X, pool.balls[i].Center.Y, 0));
        }

        var stripes = new List<Vector3>();
        for (int i = 8; i < 15; i++)
        {
            if ((double)pool.balls[i].Center.X > TableWidth / 2) continue;
            stripes.Add(new Vector3(pool.balls[i].Center.X, pool.balls[i].Center.Y, 0));
        }

        var playerTypeTarget = pool.GetPlayerType(player);
        if (playerTypeTarget == 0)
        {
            myBalls = solids; //solids
        }
        else if (playerTypeTarget == 1) //stripes
        {
            myBalls = stripes;
        }
        else if (playerTypeTarget == 2) //ball8
        {
            myBalls.Add(new Vector3(pool.balls[7].Center.X, pool.balls[7].Center.Y, 0));
        }
        else if (playerTypeTarget == 3)
        {
            myBalls.AddRange(solids); myBalls.AddRange(stripes);
        } // unassigned
        Logger.Log("Player target type is " + playerTypeTarget);

        List<F_Shot> cue_shots = new();

        Vector3 cueBallPosition = new Vector3(pool.cueBall.Center.X, pool.cueBall.Center.Y);

        foreach (var ball in myBalls)
        {
            for (int i = 0; i < holesPositions.Length; i++)
            {
                var hole = new Vector3(holesPositions[i]);
                {
                    //Direct Shot
                    var dir = (hole - ball).normalized;
                    var tangentDir = new Vector3(dir.y, -dir.x);
                    if (LineOfSight(ball, hole, out double d))
                    {
                        Logger.Log("obj to hole passed");
                        var _pot = ball - dir.normalized * 2 * Ball.radius;
                        if (LineOfSight(cueBallPosition, _pot, out double dist))
                        {
                            Logger.Log("cue to pot passed");
                            var dotP = Vector3.Dot(dir.normalized, (_pot - cueBallPosition).normalized);
                            var distance = d + dist;

                            if (!HoleRisk(_pot, tangentDir * Vector3.Cross(_pot - cueBallPosition, dir).z, holesPositions))
                            {
                                cue_shots.Add(new() { dev = dotP, dist = distance, pot = _pot, path = new Vector3[4] { cueBallPosition, _pot, ball, hole } });
                            }
                        }

                        Shot[] cueshots = null;
                        if ((cueshots = BankTo(cueBallPosition, _pot)) != null && cueshots.Length > 0)
                        {
                            foreach (var shot in cueshots)
                            {
                                var dotP = Vector3.Dot(dir.normalized, shot.reflectionDirection);
                                var distance = shot.distance + d;

                                if (!HoleRisk(_pot, tangentDir * Vector3.Cross(shot.reflectionDirection, dir).z, holesPositions))
                                {
                                    cue_shots.Add(new() { dev = dotP, dist = distance, pot = shot.hitPosition, path = shot.path.Concat(new Vector3[2] { ball, hole }).ToArray() });
                                }
                            }
                            cueshots = null;
                        }

                        if ((cueshots = DoubleBankTo(cueBallPosition, _pot)) != null)
                        {
                            foreach (var shot in cueshots)
                            {
                                var dotP = Vector3.Dot(dir.normalized, shot.reflectionDirection);
                                var distance = shot.distance + d;

                                if (!HoleRisk(_pot, tangentDir * Vector3.Cross(shot.reflectionDirection, dir).z, holesPositions))
                                {
                                    cue_shots.Add(new() { dev = dotP, dist = distance, pot = shot.hitPosition, path = shot.path.Concat(new Vector3[2] { ball, hole }).ToArray() });
                                }
                            }
                        }
                    }
                    else { Logger.Log("Not Passed ball to hole"); }
                }
                //Bank
                Shot[] primaryShots = null;
                if ((primaryShots = BankTo(ball, hole)) != null)
                {
                    foreach (var p_shot in primaryShots)
                    {
                        if ((p_shot.hitPosition - ball).magnitude < Ball.radius * 4) continue;

                        var hitDir = p_shot.hitDirection;
                        var tangentHitDir = new Vector3(hitDir.y, -hitDir.x, 0);
                        var _pot = ball - hitDir.normalized * 2 * Ball.radius;//Where the balls make contact

                        if (LineOfSight(cueBallPosition, _pot, out double _d))
                        {
                            var dotP = Vector3.Dot(hitDir, (_pot - cueBallPosition).normalized);
                            var distance = p_shot.distance + _d;

                            if (!HoleRisk(_pot, tangentHitDir * Vector3.Cross(_pot - cueBallPosition, hitDir).z, holesPositions))
                            {
                                cue_shots.Add(new() { dev = dotP, dist = distance, pot = _pot, path = new Vector3[2] { cueBallPosition, _pot }.Concat(p_shot.path).ToArray() });
                            }
                        }
                        Shot[] secondaryShots = null;
                        if ((secondaryShots = BankTo(cueBallPosition, _pot)) != null)
                        {
                            foreach (var s_shot in secondaryShots)
                            {
                                var dotP = Vector3.Dot(hitDir, s_shot.reflectionDirection);
                                var distance = p_shot.distance + s_shot.distance;

                                if (!HoleRisk(_pot, tangentHitDir * Vector3.Cross(s_shot.reflectionDirection, hitDir).z, holesPositions))
                                {
                                    cue_shots.Add(new() { dev = dotP, dist = distance, pot = s_shot.hitPosition, path = s_shot.path.Concat(p_shot.path).ToArray() });
                                }
                            }
                            secondaryShots = null;
                        }
                        if ((secondaryShots = DoubleBankTo(cueBallPosition, _pot)) != null)
                        {
                            foreach (var s_shot in secondaryShots)
                            {
                                var dotP = Vector3.Dot(hitDir, s_shot.reflectionDirection);
                                var distance = p_shot.distance + s_shot.distance;

                                if (!HoleRisk(_pot, tangentHitDir * Vector3.Cross(s_shot.reflectionDirection, hitDir).z, holesPositions))
                                {
                                    cue_shots.Add(new() { dev = dotP, dist = distance, pot = s_shot.hitPosition, path = s_shot.path.Concat(p_shot.path).ToArray() });
                                }
                            }
                        }
                    }
                    primaryShots = null;
                }
                //DoubleBank
                if ((primaryShots = DoubleBankTo(ball, hole)) != null)
                {
                    foreach (var p_shot in primaryShots)
                    {
                        if ((p_shot.hitPosition - ball).magnitude < Ball.radius * 4) continue;

                        var hitDir = p_shot.hitDirection;
                        var tangentHitDir = new Vector3(hitDir.y, -hitDir.x);

                        var _pot = ball - hitDir.normalized * 2 * Ball.radius;//Where the balls collide

                        if (LineOfSight(cueBallPosition, _pot, out double _d))
                        {
                            var dotP = Vector3.Dot(hitDir, (_pot - cueBallPosition).normalized);
                            var distance = p_shot.distance + _d;

                            if (!HoleRisk(_pot, tangentHitDir * Vector3.Cross(_pot - cueBallPosition, hitDir).z, holesPositions))
                            {
                                cue_shots.Add(new() { dev = dotP, dist = distance, pot = _pot, path = new Vector3[2] { cueBallPosition, _pot }.Concat(p_shot.path).ToArray() });
                            }
                        }
                        Shot[] secondaryShots = null;
                        if ((secondaryShots = BankTo(cueBallPosition, _pot)) != null)
                        {
                            foreach (var s_shot in secondaryShots)
                            {
                                var dotP = Vector3.Dot(hitDir, s_shot.reflectionDirection);
                                var distance = p_shot.distance + s_shot.distance;

                                if (!HoleRisk(_pot, tangentHitDir * Vector3.Cross(s_shot.reflectionDirection, hitDir).z, holesPositions))
                                {
                                    cue_shots.Add(new() { dev = dotP, dist = distance, pot = s_shot.hitPosition, path = s_shot.path.Concat(p_shot.path).ToArray() });
                                }
                            }
                            secondaryShots = null;
                        }
                        if ((secondaryShots = DoubleBankTo(cueBallPosition, _pot)) != null)
                        {
                            foreach (var s_shot in secondaryShots)
                            {
                                var dotP = Vector3.Dot(hitDir, s_shot.reflectionDirection);
                                var distance = p_shot.distance + s_shot.distance;

                                if (!HoleRisk(_pot, tangentHitDir * Vector3.Cross(s_shot.reflectionDirection, hitDir).z, holesPositions))
                                {
                                    cue_shots.Add(new() { dev = dotP, dist = distance, pot = s_shot.hitPosition, path = s_shot.path.Concat(p_shot.path).ToArray() });
                                }
                            }
                        }
                    }
                    primaryShots = null;
                }
            }
        }

        if (cue_shots.Count == 0)
        {
            foreach (var ball in myBalls)
            {
                if (cue_shots.Count == 0)
                {
                    var dir = (cueBallPosition - ball).normalized;
                    if (LineOfSight(cueBallPosition, ball + dir * Ball.radius * 2, out double d))
                    {
                        cue_shots.Add(new()
                        {
                            pot = ball,
                            dev = 1,
                            dist = d,
                            path = new Vector3[] { cueBallPosition, ball }
                        });
                    }
                    else
                    {
                        Shot[] shots = null;
                        if ((shots = BankTo(cueBallPosition, ball)) != null && shots.Length > 0)
                        {
                            cue_shots.Add(new()
                            {
                                pot = shots[0].hitPosition,
                                dev = 1,
                                dist = shots[0].distance,
                                path = shots[0].path
                            });
                            break;
                        }
                        else if ((shots = DoubleBankTo(cueBallPosition, ball)) != null && shots.Length > 0)
                        {
                            cue_shots.Add(new()
                            {
                                pot = shots[0].hitPosition,
                                dev = 1,
                                dist = shots[0].distance,
                                path = shots[0].path
                            });
                            break;
                        }
                    }
                }
            }
        }

        if (win)
        {
            cue_shots = cue_shots.Where(x => x.dev > 0.64f).ToList();
            cue_shots = cue_shots.OrderByDescending(x => (x.dev * 1000) - (x.dist * 50)).ToList();
        }
        else
        {
            cue_shots = cue_shots.Where(x => x.dev < 0.64f).ToList();
            cue_shots = cue_shots.OrderBy(x => (x.dev * 1000) - (x.dist * 50)).ToList();
        }

        if (cue_shots.Count == 0)
        {
            Logger.Log("no shots");
            cue_shots.Add(new()
            {
                dev = 1,
                dist = TableWidth,
                path = new Vector3[2] { new(-TableWidth / 2, 0), new(TableWidth / 2, 0) },
                pot = Vector3.right * 100000
            });
        }

        OnResolvedPath?.Invoke((player, cue_shots[0].path));

        var fireDir = (cue_shots[0].pot - cueBallPosition).normalized;

        var angle = -Vector3.SignedAngle(Vector3.right, fireDir, Vector3.forward);
        double ang = pool.aimAngle;

        for (int i = 0; i < 100; i++)
        {
            var a = ang + (angle - ang) * (i / 100.0);

            pool.SetAimAngle((float)a);
            pool.SendAim();

            await Task.Delay(16);
        }

        pool.SetAimAngle((float)angle);
        pool.SendAim();

        await Task.Delay(1000);

        var power = cue_shots[0].dist / TableWidth;
        power = Math.Clamp(power, 0.2f, 1f) * 600000;

        pool.Fire(new Vector2Fixed((Fixed64)fireDir.x, (Fixed64)fireDir.y) * (Fixed64)power);
    }

    bool LineOfSight(Vector3 a, Vector3 b, out double dist)
    {
        bool hit = CircleSweeper.Sweep(new Vector2Fixed(a.x, a.y), (Fixed64)Ball.radius, new Vector2Fixed(b.x - a.x, b.y - a.y).Normalized(), (Fixed64)(b - a).magnitude, pool.balls, pool.edges, out var hitInfo);
        dist = (b - a).magnitude;

        if (!hit) return true;

        return !hit;
    }

    bool HoleRisk(Vector3 pos, Vector3 dir, Vector3[] holes)
    {
        foreach (var h in holes)
        {
            bool hit = CircleSweeper.Sweep(new Vector2Fixed(pos.x, pos.y), (Fixed64)Ball.radius, new Vector2Fixed(dir.x, dir.y).Normalized(), (Fixed64)(h - pos).magnitude, pool.balls, pool.edges, out var hitInfo);
            if (!hit) return true;
        }
        return false;
    }

    string[] sides = { "left", "right", "top", "bottom" };
    Vector3[] normals = { Vector3.right, Vector3.left, Vector3.down, Vector3.up };

    public Shot[] BankTo(Vector3 start, Vector3 target)
    {
        var shots = new List<Shot>();
        for (int i = 0; i < sides.Length; i++)
        {
            var mirrorPoint = GetMirrorAcrossCushion(target, sides[i], left, right, top, bottom);

            if ((mirrorPoint - target).magnitude < Ball.radius || Vector3.Dot((mirrorPoint - target).normalized, normals[i]) > 0)
                continue;

            var testDirection = (mirrorPoint - start).normalized;

            bool hit = CircleSweeper.Sweep(new Vector2Fixed(start.x, start.y), (Fixed64)Ball.radius, new Vector2Fixed(testDirection.x, testDirection.y).Normalized(), 200000, pool.balls, pool.edges, out var hitInfo);
            if (!(hit && hitInfo.hitType == SweepHit.HitType.Edge))
                continue;

            /*RaycastHit2D hit;
            if (!((hit = physics2D.CircleCast(start, Ball.radius, testDirection, 200)).transform && !hit.transform.GetComponent<Ball>() && !hit.transform.CompareTag("Pocket")))
                continue;*/

            if (new Vector3(hitInfo.Normal.X, hitInfo.Normal.Y) != normals[i]) continue;

            var testHitPos = start + testDirection * (double)hitInfo.Distance;

            if (LineOfSight(testHitPos, target, out double lineDist))
            {
                shots.Add(new Shot
                {
                    hitDirection = testDirection,
                    hitPosition = testHitPos,
                    reflectionDirection = Vector3.Reflect(testDirection, normals[i]),
                    distance = (double)hitInfo.Distance + lineDist,
                    path = new Vector3[3] { start, testHitPos, target }
                });
            }
        }
        return shots.ToArray();
    }

    public Shot[] DoubleBankTo(Vector3 start, Vector3 target)
    {
        var shots = new List<Shot>();

        for (int i = 0; i < sides.Length; i++)
        {
            var mirrorPoint1 = GetMirrorAcrossCushion(target, sides[i], left, right, top, bottom);

            if ((mirrorPoint1 - target).magnitude < Ball.radius || Vector3.Dot((mirrorPoint1 - target).normalized, normals[i]) > 0)
                continue;

            for (int j = 0; j < sides.Length; j++)
            {
                if (j == i) continue;

                var mirrorPoint2 = GetMirrorAcrossCushion(mirrorPoint1, sides[j], left, right, top, bottom);

                if ((mirrorPoint1 - mirrorPoint2).magnitude < Ball.radius || Vector3.Dot((mirrorPoint2 - mirrorPoint1).normalized, normals[j]) > 0)
                    continue;

                var testDir = (mirrorPoint2 - start).normalized;

                bool hit = CircleSweeper.Sweep(new Vector2Fixed(start.x, start.y), (Fixed64)Ball.radius, new Vector2Fixed(testDir.x, testDir.y).Normalized(), 200000, pool.balls, pool.edges, out var hitInfo);
                if (!(hit && hitInfo.hitType == SweepHit.HitType.Edge))
                    continue;

                /*RaycastHit2D hit;
                if (!((hit = physics2D.CircleCast(start, Ball.radius, testDir, 200, new ContactFilter2D { useTriggers = true })).transform && !hit.transform.GetComponent<Ball>() && !hit.transform.CompareTag("Pocket")))
                    continue;*/

                Vector3 traceStart = start + testDir * (double)hitInfo.Distance;
                var traceDir = Vector3.Reflect(testDir, new Vector3(hitInfo.Normal.X, hitInfo.Normal.Y));

                bool hit2 = CircleSweeper.Sweep(new Vector2Fixed(traceStart.x, traceStart.y), (Fixed64)Ball.radius, new Vector2Fixed(traceDir.x, traceDir.y).Normalized(), 200000, pool.balls, pool.edges, out var hitInfo2);
                if (!(hit2 && hitInfo2.hitType == SweepHit.HitType.Edge))
                    continue;

                /*RaycastHit2D hit2;
                if (!((hit2 = physics2D.CircleCast(traceStart, Ball.radius, traceDir, 200, new ContactFilter2D { useTriggers = true })).transform && !hit2.transform.GetComponent<Ball>() && !hit2.transform.CompareTag("Pocket")))
                    continue;*/

                Vector3 lastPoint = traceStart + traceDir * (double)hitInfo2.Distance;
                if (LineOfSight(lastPoint, target, out double lineDist))
                {
                    shots.Add(new Shot
                    {
                        hitDirection = testDir,
                        reflectionDirection = Vector3.Reflect(traceDir, new Vector3(hitInfo2.Normal.X, hitInfo2.Normal.Y)),
                        hitPosition = traceStart,
                        distance = (double)(hitInfo.Distance + hitInfo2.Distance) + lineDist,
                        path = new Vector3[4] { start, traceStart, lastPoint, target }
                    });
                }
            }
        }
        return shots.ToArray();
    }

    public struct Shot
    {
        public Vector3 hitDirection;
        public Vector3 reflectionDirection;
        public Vector3 hitPosition;
        public double distance;
        public Vector3[] path;
    }

    /*Vector3 GetBankDirection(Vector3 startPos, Vector3 targetPos, string wall,
                         double left = -95.38f, double right = TableWidth - 95.38f, double top = TableHeight - 41.82f, double bottom = -41.82f)
    {
        Vector3 mirrored = targetPos;

        left += Ball.radius;
        bottom += Ball.radius;
        top -= Ball.radius;
        right -= Ball.radius;

        switch (wall)
        {
            case "top":
                mirrored.y = top + (top - targetPos.y);
                break;
            case "bottom":
                mirrored.y = bottom - (targetPos.y - bottom);
                break;
            case "left":
                mirrored.x = left - (targetPos.x - left);
                break;
            case "right":
                mirrored.x = right + (right - targetPos.x);
                break;
        }

        return (mirrored - startPos).normalized;
    }*/

    double left = (-TableWidth / 2) + Ball.radius;
    double right = (TableWidth / 2) - Ball.radius;
    double top = (TableHeight / 2) - Ball.radius;
    double bottom = (-TableHeight / 2) + Ball.radius;

    Vector3 GetMirrorAcrossCushion(Vector3 targetPos, string wall, double left, double right, double top, double bottom)
    {
        Vector3 mirrored = targetPos;
        switch (wall)
        {
            case "top":
                mirrored.y = top + (top - targetPos.y);
                break;
            case "bottom":
                mirrored.y = bottom - (targetPos.y - bottom);
                break;
            case "left":
                mirrored.x = left - (targetPos.x - left);
                break;
            case "right":
                mirrored.x = right + (right - targetPos.x);
                break;
        }

        return mirrored;
    }

    ~PoolBot()
    {
        pool.OnTurnChanged -= ComputeShot;
    }
}