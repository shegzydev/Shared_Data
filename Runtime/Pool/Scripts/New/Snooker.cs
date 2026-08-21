using SoftFloat;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

public class SnookerGame
{
    public Snooker snooker;

    public SnookerGame(float friction, float gravity)
    {
        snooker = new Snooker(friction, gravity);
    }

    public void Tick(float deltaTime)
    {
        snooker.Tick(deltaTime);
    }

    public (float px, float py, float vx, float vy)[] GetState() => snooker.GetBallData();

    public float GetEnergy() => snooker.GetEnergy();
    public Snooker.Ball[] GetBalls => snooker.GetBalls;

    public void Fire(sfloat power, sfloat dx, sfloat dy) => snooker.Fire(power * dx, power * dy);
}

public class Logger
{
    public static Action<object> Log = _ => { };
}

public class Snooker
{
    public class CollisionResult
    {
        public int indexA;
        public int indexB;
        public sfloat toi;

        public sfloat nx;
        public sfloat ny;

        public bool isEdge;
    }

    public class Circle
    {
        public sfloat px, py, r;
    }

    public class Ball
    {
        public sfloat px, py;
        public sfloat vx, vy;
        public sfloat wx, wy, wz;
        public sfloat r = (sfloat)2.925;

        public bool potted;
        public int number;

        public bool isCue => number == 0;
        public bool is8 => number == 8;
    }

    public class Edge
    {
        public sfloat x1, y1;
        public sfloat x2, y2;

        public int index;
        public sfloat restitution = sfloat.One;

        public sfloat nx, ny;
        public sfloat tx, ty;
        public sfloat edgeLen;
        public sfloat midX, midY;
        public sfloat boundRadius;

        public Edge Precompute()
        {
            sfloat ex = x2 - x1;
            sfloat ey = y2 - y1;

            sfloat edgeLenSq = ex * ex + ey * ey;
            edgeLen = libm.sqrtf(edgeLenSq);
            boundRadius = edgeLen / (sfloat)2;

            tx = ex / edgeLen;
            ty = ey / edgeLen;

            nx = -ty;
            ny = tx;

            midX = (x2 + x1) / (sfloat)2;
            midY = (y2 + y1) / (sfloat)2;

            return this;
        }
    }

    (float x, float y)[] rack = new[]
    {
        (-7.300f, 0.000f),
        (4.930f,  0.000f),
        (5.9426f, -0.5850f),
        (6.9552f,  0.5850f),
        (6.4489f,  0.8775f),
        (5.4363f,  0.2925f),
        (6.9552f, -1.1700f),
        (6.4489f, -0.2925f),
        (5.9426f,  0.000f),
        (6.4489f, -0.8775f),
        (5.9426f,  0.5850f),
        (5.4363f, -0.2925f),
        (6.9552f,  1.1700f),
        (6.9552f,  0.000f),
        (6.4489f,  0.2925f),
        (6.9552f, -0.5850f),
    };

    (float x, float y)[] wallPoints = new (float x, float y)[]
    {
        (-10.16576f,  4.13606758f),
        (-10.1962631f,  4.36928177f),
        (-10.0708855f,  4.64077072f),
        (-9.842305f,   4.81319046f),
        (-9.592929f,   4.87706947f),
        (-9.379472f,   4.842889f),
        (-9.04374f,    4.56546974f),
        (-8.713915f,   4.2f),
        (-0.8523499f,  4.2f),
        (-0.582877254f, 4.578946f),
        (-0.3863626f,  4.885246f),
        (-0.000610685349f, 5.021046f),
        (0.394897127f,  4.877487f),
        (0.665774536f,  4.571879f),
        (0.9585552f,   4.2f),
        (8.813355f,    4.2f),
        (9.270909f,    4.77062263f),
        (9.44801254f,  4.85657768f),
        (9.65386f,     4.8747f),
        (9.856485f,    4.8110775f),
        (10.0359077f,  4.699202f),
        (10.1564705f,  4.512847f),
        (10.192984f,   4.318326f),
        (10.1657059f,  4.11223946f),
        (10.0572441f,  3.90655251f),
        (9.5f,         3.42975769f),
        (9.5f,        -3.496335f),
        (9.999929f,   -3.7869072f),
        (10.12742f,   -3.998754f),
        (10.1934967f, -4.18226128f),
        (10.1659073f, -4.40485f),
        (10.0761917f, -4.58228073f),
        (9.876413f,    -4.777344f),
        (9.622694f,    -4.85973473f),
        (9.36048f,     -4.80198135f),
        (9.139473f,    -4.62731171f),
        (8.73294754f,  -4.2f),
        (0.8587822f,   -4.2f),
        (0.6139451f,   -4.5349823f),
        (0.340668464f, -4.86934052f),
        (0.00178575516f, -4.988961f),
        (-0.349915981f, -4.88016739f),
        (-0.66097765f,  -4.54206352f),
        (-0.9560188f,   -4.2f),
        (-8.812045f,    -4.2f),
        (-9.186239f,    -4.65458679f),
        (-9.35434f,     -4.7953022f),
        (-9.561866f,    -4.86101036f),
        (-9.840787f,    -4.818216f),
        (-10.0255867f,  -4.67388954f),
        (-10.1694206f,  -4.495341f),
        (-10.2218666f,  -4.29965019f),
        (-10.1847176f,  -4.1045002f),
        (-10.0937874f,  -3.87546349f),
        (-9.5f,         -3.4133625f),
        (-9.5f,          3.53429565f),
        (-9.937435f,      3.82531471f),
        (-10.1704132f,    4.131517f),
    };

    (float x, float y)[] railPoints = new (float x, float y)[]
    {
        (12.0459f, -3.1952f),
        (12.0458f,  2.6585f),
        (12.0094f,  2.7975f),
        (12.0094f,  2.7975f),
        (11.9380f,  2.9052f),
        (11.8586f,  2.9823f),
        (11.8216f,  3.0183f),
        (11.6883f,  3.0976f),
        (11.5476f,  3.1438f),
        (10.6582f,  3.1444f),
        (10.6605f,  2.3452f),
        (11.2371f,  2.3035f),
        (11.3148f,  2.2959f),
        (11.3689f,  2.2581f),
        (11.4012f,  2.2043f),
        (11.4082f,  2.1178f),
        (11.4159f, -4.8886f),
        (12.0427f, -4.8865f),
        (12.0481f, -3.2176f),
    };

    (float x, float y, float r)[] holePoints = new (float x, float y, float radius)[]
    {
        (-9.600f,  4.300f, 0.570f),
        ( 9.600f,  4.300f, 0.570f),
        (-9.600f, -4.270f, 0.570f),
        ( 9.600f, -4.270f, 0.570f),
        ( 0.000f,  4.440f, 0.570f),
        ( 0.000f, -4.410f, 0.570f),
    };

    (sfloat x, sfloat y) dropPosition = ((sfloat)11.15900, (sfloat)2.6800);

    public Action<(Ball ball, Circle hole)> OnHole = _ => { };
    public Action Stopped = () => { };
    public Action<(Ball A, Ball B)> OnBallCollision = _ => { };
    public Action<(Ball A, Edge B)> OnEdgeCollision = _ => { };

    sfloat radius = (sfloat)0.2925;
    sfloat tickDelta = (sfloat)0.01666;//60hz
    sfloat friction;
    sfloat gravity = (sfloat)9.81;

    uint ticksSinceFire = 0;

    //0:cueball
    //1-7:solids
    //8: black
    //9-15: stripes
    Ball[] balls;
    Edge[] edges;
    Circle[] holes;

    public Snooker(float _friction, float _gravity)
    {
        friction = (sfloat)_friction;
        gravity = (sfloat)_gravity;

        balls = new Ball[rack.Length];
        RackBalls();

        holes = new Circle[holePoints.Length];
        for (int i = 0; i < holePoints.Length; i++)
        {
            var holePoint = holePoints[i];
            holes[i] = new Circle
            {
                px = (sfloat)holePoint.x * (sfloat)10,
                py = (sfloat)holePoint.y * (sfloat)10,
                r = (sfloat)holePoint.r * (sfloat)10
            };
        }

        edges = new Edge[wallPoints.Length + railPoints.Length];

        for (int i = 0; i < wallPoints.Length; i++)
        {
            var p1 = wallPoints[i];
            var p2 = wallPoints[(i + 1) % wallPoints.Length];
            edges[i] = new Edge
            {
                x1 = (sfloat)p1.x * (sfloat)10,
                y1 = (sfloat)p1.y * (sfloat)10,
                x2 = (sfloat)p2.x * (sfloat)10,
                y2 = (sfloat)p2.y * (sfloat)10,
                index = i,
                restitution = sfloat.One
            }.Precompute();
        }
        for (int i = 0; i < railPoints.Length; i++)
        {
            var p1 = railPoints[i];
            var p2 = railPoints[(i + 1) % railPoints.Length];
            edges[i + wallPoints.Length] = new Edge
            {
                x1 = (sfloat)p1.x * (sfloat)10,
                y1 = (sfloat)p1.y * (sfloat)10,
                x2 = (sfloat)p2.x * (sfloat)10,
                y2 = (sfloat)p2.y * (sfloat)10,
                index = i + wallPoints.Length,
                restitution = sfloat.Zero
            }.Precompute();
        }

        contactHash = new List<(sfloat nx, sfloat ny)>[balls.Length];
        for (int i = 0; i < contactHash.Length; i++)
        {
            contactHash[i] = new();
        }
    }

    sfloat accumulator;
    public void Tick(float deltaTime)
    {
        accumulator += (sfloat)deltaTime;
        while (accumulator >= tickDelta)
        {
            accumulator -= tickDelta;
            FixedTick();
        }

        CheckPocketed();
    }

    void FixedTick()
    {
        StepSimulation(tickDelta);
        if (rolling) ticksSinceFire++;
    }

    public void ResetCue(bool anywhere = false)
    {
        var p = rack[0];
        balls[0] = new Ball
        {
            px = (sfloat)p.x * (sfloat)10,
            py = (sfloat)p.y * (sfloat)10,
            r = radius * (sfloat)10,
            number = 0
        };

        if (anywhere)
        {
            balls[0].px = sfloat.Zero;
            balls[0].py = sfloat.Zero;
        }
    }

    public void PlaceCue(sfloat x, sfloat y)
    {
        balls[0].px = x;
        balls[0].py = y;
    }

    public void RackBalls()
    {
        for (int i = 0; i < rack.Length; i++)
        {
            var p = rack[i];
            balls[i] = new Ball
            {
                px = (sfloat)p.x * (sfloat)10,
                py = (sfloat)p.y * (sfloat)10,
                r = radius * (sfloat)10,
                number = i
            };
        }
    }

    void ApplyFrictionAll(sfloat dt)
    {
        for (int i = 0; i < balls.Length; i++)
        {
            ApplyFriction(balls[i], dt);
        }

        if (rolling && GetTotalSpeedSqr() == sfloat.Zero)
        {
            Stopped();
            rolling = false;
        }
    }

    void CheckPocketed()
    {
        foreach (var hole in holes)
        {
            foreach (var ball in balls)
            {
                if (ball.potted) continue;

                var dx = ball.px - hole.px;
                var dy = ball.py - hole.py;

                // var dist = libm.sqrtf(dx * dx + dy * dy);
                var dist = (dx * dx + dy * dy);

                var diff = libm.powf(hole.r * (sfloat)1.2 - ball.r, (sfloat)2);

                // if (dist + ball.r <= hole.r * (sfloat)1.2)
                if (dist <= diff)
                {
                    ball.potted = true;

                    ball.px = dropPosition.x * (sfloat)10;
                    ball.py = dropPosition.y * (sfloat)10;

                    var speed = libm.sqrtf(ball.vx * ball.vx + ball.vy * ball.vy);

                    ball.vx = speed;
                    ball.vy = sfloat.Zero;

                    OnHole((ball, hole));
                }
            }
        }
    }

    bool rolling = false;
    public void Fire(sfloat vx, sfloat vy)
    {
        balls[0].vx = vx;
        balls[0].vy = vy;
        rolling = true;
        ticksSinceFire = 0;
    }

    List<(sfloat nx, sfloat ny)>[] contactHash;

    List<CollisionResult> validContacts = new();

    List<CollisionResult> persistentContacts = new();
    List<int> dirtyBalls = new();

    public void StepSimulation(sfloat dt)
    {
        sfloat remaining = dt;
        int maxIterations = 64;
        int iterations = 0;

        persistentContacts.Clear();
        persistentContacts.AddRange(GetContacts(remaining));
        dirtyBalls.Clear();

        while (remaining > (sfloat)0 && iterations < maxIterations)
        {
            iterations++;

            for (int i = 0; i < contactHash.Length; i++)
                contactHash[i].Clear();

            if (persistentContacts.Count == 0)
            {
                AdvanceAll(remaining);
                remaining = (sfloat)0;
                break;
            }

            sfloat minToi = sfloat.MaxValue;

            CollisionResult col = null;
            foreach (var c in persistentContacts)
            {
                if (c.toi > sfloat.Zero && c.toi < minToi)
                {
                    col = c;
                    minToi = c.toi;
                }
            }

            if (col == null || col.toi == sfloat.Zero)
            {
                sfloat epsilon = (sfloat)0.002;
                AdvanceAll(epsilon);
                remaining -= epsilon;
                RebuildContacts(remaining); // cheap fallback path, rare
                continue;
            }

            AdvanceAll(col.toi);
            remaining -= col.toi;

            validContacts.Clear();
            foreach (var c in persistentContacts)
            {
                if (c.toi <= col.toi)
                    validContacts.Add(c);
            }

            // Mark which balls are about to get new velocities
            dirtyBalls.Clear();
            foreach (var c in validContacts)
            {
                dirtyBalls.Add(c.indexA);
                if (!c.isEdge) dirtyBalls.Add(c.indexB);
            }

            ResolveCCDContactsBatch(validContacts);

            // ---- Incremental contact update instead of full GetContacts ----
            UpdateContactsIncremental(col.toi, remaining);
        }

        RemoveOverlaps();

        ApplyGravity(dt);

        ApplyFrictionAll(dt);
    }

    HashSet<int> checkedDirty = new();
    void UpdateContactsIncremental(sfloat elapsed, sfloat remaining)
    {
        // 1. Drop consumed/stale contacts, age the rest, drop anything touching a dirty ball
        for (int i = persistentContacts.Count - 1; i >= 0; i--)
        {
            var c = persistentContacts[i];

            bool touchesDirty = dirtyBalls.Contains(c.indexA) ||
                                 (!c.isEdge && dirtyBalls.Contains(c.indexB));

            if (touchesDirty)
            {
                persistentContacts.RemoveAt(i);
                continue;
            }

            c.toi -= elapsed;
            if (c.toi < sfloat.Zero) c.toi = sfloat.Zero; // consumed this step, still might refire
            persistentContacts[i] = c;
        }

        // 2. Recompute contacts only for dirty balls against everything else
        checkedDirty.Clear();
        foreach (int i in dirtyBalls)
        {
            if (checkedDirty.Contains(i)) continue;

            checkedDirty.Add(i);

            Ball a = balls[i];
            if (a.potted) continue;

            for (int j = 0; j < balls.Length; j++)
            {
                if (j == i) continue;
                Ball b = balls[j];
                if (b.potted) continue;

                // avoid duplicate pair (i,j)/(j,i): only add once
                if (j > i || !dirtyBalls.Contains(j)) // if both dirty, only lower index adds it
                {
                    if (GetTimeOfImpactBall(a, b, out sfloat toi) && toi < remaining)
                    {
                        int lo = Math.Min(i, j), hi = Math.Max(i, j);
                        persistentContacts.Add(new CollisionResult
                        { indexA = lo, indexB = hi, toi = toi, isEdge = false });
                    }
                }
            }

            sfloat maxTravel = libm.sqrtf(a.vx * a.vx + a.vy * a.vy) * remaining + a.r;
            for (int e = 0; e < edges.Length; e++)
            {
                Edge b = edges[e];

                sfloat dx = a.px - b.midX;
                sfloat dy = a.py - b.midY;
                sfloat reach = maxTravel + b.boundRadius;
                if (dx * dx + dy * dy > reach * reach) continue;

                if (GetTimeOfImpactEdge(a, b, out sfloat toi) && toi < remaining)
                {
                    persistentContacts.Add(new CollisionResult
                    { indexA = i, indexB = e, toi = toi, isEdge = true });
                }
            }
        }
    }

    void RebuildContacts(sfloat remaining)
    {
        persistentContacts.Clear();
        persistentContacts.AddRange(GetContacts(remaining));
    }

    /*public void StepSimulation(sfloat dt)
    {
        sfloat remaining = dt;

        int maxIterations = 8;
        int iterations = 0;

        while (remaining > (sfloat)0 && iterations < maxIterations)
        {
            iterations++;

            ApplyGravity(remaining);

            var contacts = GetContacts(remaining);

            for (int i = 0; i < contactHash.Length; i++)
                contactHash[i].Clear();

            if (contacts.Count == 0)
            {
                AdvanceAll(remaining);
                remaining = (sfloat)0;
                break;
            }

            contacts.Sort((a, b) => a.toi.CompareTo(b.toi));

            CollisionResult col = contacts[0];
            foreach (var contact in contacts)
            {
                if (contact.toi > sfloat.Zero)
                {
                    col = contact;
                    break;
                }
            }

            if (col.toi == sfloat.Zero)
            {
                sfloat epsilon = (sfloat)0.002;
                AdvanceAll(epsilon);
                remaining -= epsilon;
                continue;
            }

            AdvanceAll(col.toi);
            remaining -= col.toi;

            validContacts.Clear();
            sfloat nonzero = sfloat.Zero;
            foreach (var contact in contacts)
            {
                if (contact.toi > nonzero && nonzero > sfloat.Zero) break;
                validContacts.Add(contact);
                nonzero = contact.toi;
            }

            ResolveCCDContactsBatch(validContacts);
        }

        RemoveOverlaps();
    }*/

    void ResolveCCDContactsBatch(List<CollisionResult> contacts)
    {
        foreach (var contact in contacts)
        {
            if (contact.isEdge)
            {
                ResolveCollisionEdge(balls[contact.indexA], edges[contact.indexB]);
            }
            else
            {
                ResolveCollisionBall(balls[contact.indexA], balls[contact.indexB]);
            }
        }
    }

    void RemoveOverlaps()
    {
        for (int iter = 0; iter < 8; iter++)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                for (int j = i + 1; j < balls.Length; j++)
                {
                    SolveDiscrete(balls[i], balls[j]);
                }
            }

            for (int i = 0; i < balls.Length; i++)
            {
                for (int j = 0; j < edges.Length; j++)
                {
                    SolveDiscrete(balls[i], edges[j]);
                }
            }
        }
    }

    void ApplyGravity(sfloat t)
    {
        for (int i = 0; i < balls.Length; i++)
        {
            Ball a = balls[i];

            if (!a.potted) continue;
            if (contactHash[a.number].Any((n) => n.ny > (sfloat)0.965)) continue;

            a.vy += gravity * t;
        }
    }

    void AdvanceAll(sfloat t)
    {
        for (int i = 0; i < balls.Length; i++)
        {
            Ball bl = balls[i];

            bl.px += bl.vx * t;
            bl.py += bl.vy * t;
        }
    }

    public void SolveDiscrete(Ball ball, Edge edge)
    {
        sfloat ex = edge.x2 - edge.x1;
        sfloat ey = edge.y2 - edge.y1;

        sfloat lengthSquared = edge.edgeLen * edge.edgeLen;

        if (lengthSquared == sfloat.Zero)
        {
            return;
        }

        // Project point onto the line, clamp t to [0, 1] to stay on the segment
        sfloat t = ((ball.px - edge.x1) * ex + (ball.py - edge.y1) * ey) / lengthSquared;
        t = sfloat.Max(sfloat.Zero, sfloat.Min(sfloat.One, t));

        sfloat closestX = edge.x1 + t * ex;
        sfloat closestY = edge.y1 + t * ey;

        sfloat dx = ball.px - closestX;
        sfloat dy = ball.py - closestY;

        sfloat dist = libm.sqrtf(dx * dx + dy * dy);

        if (dist > ball.r) return;

        (sfloat x, sfloat y) normal;

        if (dist < sfloat.Epsilon)
            normal = (ey, -ex);
        else
            normal = (dx / dist, dy / dist);

        sfloat overlap = ball.r - dist;

        ball.px += normal.x * overlap;
        ball.py += normal.y * overlap;

        sfloat velDotNormal = (ball.vx * normal.x + ball.vy * normal.y);
        if (velDotNormal > sfloat.Zero) return;

        ball.vx -= (sfloat.One + edge.restitution) * velDotNormal * normal.x;
        ball.vy -= (sfloat.One + edge.restitution) * velDotNormal * normal.y;

        contactHash[ball.number].Add((normal.x, normal.y));
    }

    public void SolveDiscrete(Ball a, Ball b)
    {
        sfloat dx = b.px - a.px;
        sfloat dy = b.py - a.py;

        sfloat dist = libm.sqrtf(dx * dx + dy * dy);
        sfloat minDist = a.r + b.r;

        if (dist > minDist) return;

        (sfloat x, sfloat y) normal;
        if (dist < sfloat.Epsilon)
            normal = (sfloat.One, sfloat.Zero);
        else
            normal = (dx / dist, dy / dist);

        sfloat overlap = minDist - dist;

        sfloat correctionX = normal.x * overlap * (sfloat)0.5;
        sfloat correctionY = normal.y * overlap * (sfloat)0.5;

        a.px -= correctionX;
        a.py -= correctionY;

        b.px += correctionX;
        b.py += correctionY;

        (sfloat x, sfloat y) rv = (b.vx - a.vx, b.vy - a.vy);
        sfloat velAlong = rv.x * normal.x + rv.y * normal.y;

        if (velAlong > sfloat.Zero) return;

        sfloat e = sfloat.One;
        sfloat impulse = -(sfloat.One + e) * velAlong / (sfloat)2.0;

        a.vx -= normal.x * impulse;
        a.vy -= normal.y * impulse;

        b.vx += normal.x * impulse;
        b.vy += normal.y * impulse;

        contactHash[a.number].Add((-normal.x, -normal.y));
        contactHash[b.number].Add((normal.x, normal.y));
    }

    List<CollisionResult> _contacts = new();
    List<CollisionResult> GetContacts(sfloat dt)
    {
        _contacts.Clear();

        for (int i = 0; i < balls.Length; i++)
        {
            Ball a = balls[i];
            if (a.potted) continue;

            for (int j = i + 1; j < balls.Length; j++)
            {
                Ball b = balls[j];

                if (GetTimeOfImpactBall(a, b, out sfloat toi) && toi < dt)
                {
                    _contacts.Add(new CollisionResult
                    {
                        indexA = i,
                        indexB = j,
                        toi = toi,
                        isEdge = false
                    });
                }
            }

            sfloat maxTravel = libm.sqrtf(a.vx * a.vx + a.vy * a.vy) * dt + a.r;
            for (int j = 0; j < edges.Length; j++)
            {
                Edge b = edges[j];

                sfloat dx = a.px - b.midX;
                sfloat dy = a.py - b.midY;
                sfloat reach = maxTravel + b.boundRadius;
                if (dx * dx + dy * dy > reach * reach) continue;

                if (GetTimeOfImpactEdge(a, b, out sfloat toi) && toi < dt)
                {
                    _contacts.Add(new CollisionResult
                    {
                        indexA = i,
                        indexB = j,
                        toi = toi,
                        isEdge = true
                    });
                }
            }
        }

        return _contacts;
    }

    bool GetTimeOfImpactBall(Ball a, Ball b, out sfloat toi)
    {
        toi = sfloat.Zero;

        sfloat dpx = b.px - a.px;
        sfloat dpy = b.py - a.py;
        sfloat dvx = b.vx - a.vx;
        sfloat dvy = b.vy - a.vy;

        sfloat rSum = a.r + b.r;

        sfloat A = dvx * dvx + dvy * dvy;
        sfloat B = (sfloat)2 * (dpx * dvx + dpy * dvy);
        sfloat C = dpx * dpx + dpy * dpy - rSum * rSum;

        if (C <= sfloat.Zero)
        {
            toi = sfloat.Zero;
            return true;
        }

        if (A == sfloat.Zero)
            return false;

        sfloat discriminant = B * B - (sfloat)4 * A * C;
        if (discriminant < sfloat.Zero)
            return false;

        sfloat sqrtDisc = libm.sqrtf(discriminant);
        sfloat t = (-B - sqrtDisc) / ((sfloat)2 * A);

        if (t < sfloat.Zero)
        {
            sfloat t2 = (-B + sqrtDisc) / ((sfloat)2 * A);
            if (t2 < (sfloat)0)
                return false;
            t = sfloat.Zero;
        }

        toi = t;
        return true;
    }
    bool GetTimeOfImpactEdge(Ball a, Edge edge, out sfloat toi)
    {
        toi = sfloat.MaxValue; // or your sfloat equivalent of "infinity"
        bool found = false;

        // 1. Test against the flat segment (line, clamped to bounds) 
        if (GetTimeOfImpactEdgeLine(a, edge, out sfloat lineToi))
        {
            toi = lineToi;
            found = true;
        }

        // 2. Always also test both endpoints independently
        if (GetTimeOfImpactPoint(a, edge.x1, edge.y1, out sfloat t1) && t1 < toi)
        {
            toi = t1;
            found = true;
        }

        if (GetTimeOfImpactPoint(a, edge.x2, edge.y2, out sfloat t2) && t2 < toi)
        {
            toi = t2;
            found = true;
        }

        return found;
    }

    bool GetTimeOfImpactEdgeLine(Ball a, Edge edge, out sfloat toi)
    {
        toi = (sfloat)0;

        sfloat rx = a.px - edge.x1;
        sfloat ry = a.py - edge.y1;
        sfloat distToLine = rx * edge.nx + ry * edge.ny;
        sfloat velAlongNormal = a.vx * edge.nx + a.vy * edge.ny;

        sfloat sign = distToLine >= (sfloat)0 ? (sfloat)1 : (sfloat)(-1);
        sfloat targetDist = sign * a.r;

        if (velAlongNormal * sign >= (sfloat)0)
        {
            if (sign * distToLine <= a.r)
                toi = (sfloat)0;
            else
                return false;
        }
        else
        {
            sfloat t = (targetDist - distToLine) / velAlongNormal;
            if (t < (sfloat)0) return false;
            toi = t;
        }

        sfloat cx = a.px + a.vx * toi;
        sfloat cy = a.py + a.vy * toi;
        sfloat px = cx - edge.x1;
        sfloat py = cy - edge.y1;
        sfloat tParam = px * edge.tx + py * edge.ty;

        // Only valid if contact point is actually within the segment
        return tParam >= (sfloat)0 && tParam <= edge.edgeLen;
    }
    bool GetTimeOfImpactPoint(Ball a, sfloat px, sfloat py, out sfloat toi)
    {
        toi = (sfloat)0;

        sfloat dpx = px - a.px;
        sfloat dpy = py - a.py;

        sfloat A = a.vx * a.vx + a.vy * a.vy;
        sfloat B = (sfloat)(-2) * (dpx * a.vx + dpy * a.vy);
        sfloat C = dpx * dpx + dpy * dpy - a.r * a.r;

        if (C <= (sfloat)0)
        {
            toi = (sfloat)0;
            return true;
        }

        if (A == (sfloat)0)
            return false;

        sfloat discriminant = B * B - (sfloat)4 * A * C;
        if (discriminant < (sfloat)0)
            return false;

        sfloat sqrtDisc = libm.sqrtf(discriminant);
        sfloat t = (-B - sqrtDisc) / ((sfloat)2 * A);

        if (t < (sfloat)0)
            return false;

        toi = t;
        return true;
    }

    void ResolveCollisionBall(Ball a, Ball b)
    {
        // Vector between centers (from a to b)
        sfloat nx = b.px - a.px;
        sfloat ny = b.py - a.py;

        sfloat distSq = nx * nx + ny * ny;

        // Guard against exact overlap (shouldn't normally happen if TOI is correct)
        if (distSq == (sfloat)0)
        {
            nx = (sfloat)1;
            ny = (sfloat)0;
            distSq = (sfloat)1;
        }

        sfloat dist = libm.sqrtf(distSq);
        nx /= dist;
        ny /= dist;

        // --- Elastic impulse (normal velocity exchange) ---
        sfloat rvx = a.vx - b.vx;
        sfloat rvy = a.vy - b.vy;

        sfloat velAlongNormal = rvx * nx + rvy * ny;

        if (velAlongNormal >= (sfloat)0)
        {
            sfloat impulse = velAlongNormal;

            a.vx -= impulse * nx;
            a.vy -= impulse * ny;
            b.vx += impulse * nx;
            b.vy += impulse * ny;

        }

        // --- Penetration correction (push apart if overlapping) ---
        sfloat rSum = a.r + b.r;
        sfloat penetration = rSum - dist;

        // if (penetration > (sfloat)0)
        // {
        //     a.px -= penetration * nx * (sfloat)0.1;
        //     a.py -= penetration * ny * (sfloat)0.1;

        //     b.px += penetration * nx * (sfloat)0.1;
        //     b.py += penetration * ny * (sfloat)0.1;
        // }

        contactHash[a.number].Add((-nx, -ny));
        contactHash[b.number].Add((nx, ny));

        if (velAlongNormal > sfloat.Epsilon) OnBallCollision((a, b));
    }

    void ResolveCollisionEdge(Ball a, Edge edge)
    {
        // Edge tangent/normal (unit vectors)
        sfloat ex = edge.x2 - edge.x1;
        sfloat ey = edge.y2 - edge.y1;
        sfloat edgeLenSq = ex * ex + ey * ey;

        if (edgeLenSq == (sfloat)0)
            return; // degenerate edge

        sfloat edgeLen = libm.sqrtf(edgeLenSq);
        sfloat tx = ex / edgeLen;
        sfloat ty = ey / edgeLen;

        sfloat nx = -ty;
        sfloat ny = tx;

        // Ball relative to edge start
        sfloat rx = a.px - edge.x1;
        sfloat ry = a.py - edge.y1;

        // Parametric position along the edge, to know if we're hitting the segment body or an endpoint
        sfloat tParam = rx * tx + ry * ty;

        sfloat cxNear, cyNear; // nearest point on the (possibly clamped) edge

        if (tParam < (sfloat)0)
        {
            cxNear = edge.x1;
            cyNear = edge.y1;
        }
        else if (tParam > edgeLen)
        {
            cxNear = edge.x2;
            cyNear = edge.y2;
        }
        else
        {
            cxNear = edge.x1 + tx * tParam;
            cyNear = edge.y1 + ty * tParam;
        }

        // Normal from nearest point to ball center (handles both segment body and rounded endpoints)
        sfloat dx = a.px - cxNear;
        sfloat dy = a.py - cyNear;
        sfloat distSq = dx * dx + dy * dy;

        sfloat dist;
        if (distSq == (sfloat)0)
        {
            // Ball center exactly on the edge (shouldn't normally happen) - fall back to edge normal
            dx = nx;
            dy = ny;
            dist = (sfloat)1;
        }
        else
        {
            dist = libm.sqrtf(distSq);
            dx /= dist;
            dy /= dist;
        }

        // --- Velocity reflection (cushion is immovable, infinite mass) ---
        sfloat velAlongNormal = a.vx * dx + a.vy * dy;

        if (velAlongNormal < (sfloat)0) // moving into the cushion
        {
            sfloat j = -((sfloat)1 + edge.restitution) * velAlongNormal;
            a.vx += j * dx;
            a.vy += j * dy;
        }

        // --- Penetration correction ---
        sfloat penetration = a.r - dist;

        if (penetration > (sfloat)0)
        {
            // a.px += dx * penetration;
            // a.py += dy * penetration;
        }

        contactHash[a.number].Add((dx, dy));

        if (velAlongNormal < -sfloat.Epsilon) OnEdgeCollision((a, edge));
    }

    void ApplyFriction(Ball a, sfloat dt)
    {
        a.vx *= sfloat.One / (sfloat.One + dt * friction);
        a.vy *= sfloat.One / (sfloat.One + dt * friction);

        var speed = a.vx * a.vx + a.vy * a.vy;
        if (speed < (sfloat)1)
        {
            a.vx = sfloat.Zero;
            a.vy = sfloat.Zero;
        }
    }

    private (float px, float py, float vx, float vy)[] state;
    public (float px, float py, float vx, float vy)[] GetBallData()
    {
        if (state == null || state.Length != balls.Length)
            state = new (float px, float py, float vx, float vy)[balls.Length];

        for (int i = 0; i < balls.Length; i++)
        {
            Ball ball = balls[i];
            state[i] = ((float)ball.px, (float)ball.py, (float)ball.vx, (float)ball.vy);
        }

        return state;
    }

    public Ball[] GetBalls => balls;
    public Edge[] GetEdges => edges;

    public float GetEnergy()
    {
        sfloat energy = sfloat.Zero;

        for (int i = 0; i < balls.Length; i++)
        {
            Ball b = balls[i];
            energy += (sfloat)0.5 * (b.vx * b.vx + b.vy * b.vy);
        }

        return (float)energy;
    }

    public sfloat GetTotalSpeedSqr()
    {
        sfloat totalSpeed = sfloat.Zero;

        for (int i = 0; i < balls.Length; i++)
        {
            Ball b = balls[i];
            if (b.potted) continue;

            totalSpeed += b.vx * b.vx + b.vy * b.vy;
        }

        return totalSpeed;
    }

    public int[] GetPotted()
    {
        List<int> potted = new();

        for (int i = 0; i < balls.Length; i++)
        {
            Ball b = balls[i];
            if (b.potted) potted.Add(i);
        }

        return potted.ToArray();
    }

    public byte[] GetBallsState()
    {
        if (ticksSinceFire < 2 || !rolling) throw new Exception("Not Ready");

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);

        writer.Write(ticksSinceFire);

        foreach (var ball in balls)
        {
            writer.Write(ball.px.RawValue);
            writer.Write(ball.py.RawValue);
            writer.Write(ball.vx.RawValue);
            writer.Write(ball.vy.RawValue);
        }

        writer.Dispose();
        stream.Dispose();

        return stream.ToArray();
    }

    public void Reconcile(byte[] data)
    {
        if (ticksSinceFire < 2 || !rolling) return;

        using (MemoryStream stream = new MemoryStream(data))
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                uint ticks = reader.ReadUInt32();

                if (ticksSinceFire < ticks) return;

                var diff = ticksSinceFire - ticks;

                foreach (var ball in balls)
                {
                    ball.px = sfloat.FromRaw(reader.ReadUInt32());
                    ball.py = sfloat.FromRaw(reader.ReadUInt32());
                    ball.vx = sfloat.FromRaw(reader.ReadUInt32());
                    ball.vy = sfloat.FromRaw(reader.ReadUInt32());
                }

                ticksSinceFire = ticks;

                for (int i = 0; i < diff; i++)
                {
                    FixedTick();
                }
            }
        }
    }
}
