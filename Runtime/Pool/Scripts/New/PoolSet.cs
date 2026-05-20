using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum PoolNetEvents : byte
{
    Ready, TurnSwitch, Balls, Shoot, Timer, EndGame, Aim, State, Assign, CueSet, Rerack, Foul, Scratch
}

namespace PhysicsEngine
{
    enum State
    {
        Breaking, Open, GameOver
    }

    public enum BallInHandRule
    {
        None,
        BehindHeadstring,
        Anywhere
    }

    public class PoolSet
    {
        PoolPhysics physics = new PoolPhysics();

        public Circle cueBall = new Circle(-73.000, 0);
        public List<Circle> balls = new List<Circle>();
        public List<Edge> edges = new List<Edge>();
        public List<Circle> holes = new List<Circle>();

        bool scratch;
        bool port8;
        bool edgeHit;
        bool pocketedOwnBall;
        bool pocketedAnyBall;
        Circle firstHit;
        Circle calledPocket;
        Circle actualPocket;

        int turn;
        State gameState;

        float[] timers = { 30, 30 };

        bool ballInHand;
        bool breaking;

        public bool getBallInHand => ballInHand;
        public bool getBreaking => breaking;

        HashSet<Circle>[] targets = new HashSet<Circle>[2]
        {
            new(),
            new()
        };

        HashSet<Circle>[] targetType = new HashSet<Circle>[2]
        {
            new(),
            new()
        };

        HashSet<Circle> solids, stripes;
        List<Circle> potted = new();
        bool acceptCollisions;
        bool assigned = false;

        public PoolSet(int turn = 0)
        {
            this.turn = turn;
        }

        public void Init()
        {
            OnBreak += _ => { ballInHand = true; breaking = true; };
            OnFoul += _ => { ballInHand = true; };
            OnScratch += _ => { ballInHand = true; };
            OnFire += _ => { ballInHand = false; breaking = false; };
            OnTurnChanged += _ => { };

            timerRunning = true;

            OnBreak.Invoke(turn);

            physics = new PoolPhysics();

            // Add walls
            for (int i = 0; i < MetaData.wallPoints.Length; i++)
            {
                var edge = new Edge
                {
                    P1 = MetaData.wallPoints[i],
                    P2 = MetaData.wallPoints[(i + 1) % MetaData.wallPoints.Length],
                    Bouncy = true,
                    tag = "edge"
                };
                edges.Add(edge);
                physics.AddEdge(edge);
            }

            // Add rails (non-bouncy)
            for (int i = 0; i < MetaData.railPoints.Length; i++)
            {
                var edge = new Edge
                {
                    P1 = MetaData.railPoints[i],
                    P2 = MetaData.railPoints[(i + 1) % MetaData.railPoints.Length],
                    Bouncy = false,
                    tag = "rail"
                };
                edges.Add(edge);
                physics.AddEdge(edge);
            }

            // Add object balls
            for (int i = 0; i < MetaData.rack.Length; i++)
            {
                var ball = new Circle(MetaData.rack[i].X, MetaData.rack[i].Y)
                {
                    tag = (i < 7) ? "solids" : ((i > 7) ? "stripes" : "black")
                };
                balls.Add(ball);
                physics.AddCircle(ball);
            }

            // Add holes
            for (int i = 0; i < MetaData.holes.Length; i++)
            {
                holes.Add(MetaData.holes[i]);
                physics.AddHole(MetaData.holes[i]);
            }

            physics.AddCircle(cueBall);

            HandleEvents();
            RackUp(false);

            OnTurnChanged(turn);
        }

        public void HandleEvents()
        {
            physics.OnHole = data =>
            {
                HoleHandler(data);
                potted.Add(data.ball);
            };

            physics.OnBallCollision = data =>
            {
                if (!acceptCollisions) return;
                if (data.A == cueBall || data.B == cueBall)
                {
                    firstHit ??= data.A == cueBall ? data.B : data.A;
                }
            };

            physics.OnEdgeCollision = data =>
            {
                if (!acceptCollisions) return;
                edgeHit = true;
            };

            physics.Stopped = () =>
            {
                StopHandler();

                OnStop();

                OnStateUpdate();
            };
        }

        void HoleHandler((Circle ball, Circle hole) data)
        {
            data.ball.Center = MetaData.dropPosition;
            data.ball.Velocity = new Vector2(200000, -200000);
            data.ball.IsPocketed = true;

            if (data.ball == cueBall)
            {
                scratch = true;
                return;
            }

            if (data.ball == balls[7])
            {
                port8 = true;
                actualPocket = data.hole;
                return;
            }

            bool isSolid = solids.Contains(data.ball);
            bool isStripe = stripes.Contains(data.ball);

            if (!assigned)
            {
                if (isSolid)
                {
                    targetType[turn] = new HashSet<Circle>(solids);
                    targetType[1 - turn] = new HashSet<Circle>(stripes);

                    targets[turn] = solids;
                    targets[1 - turn] = stripes;

                    OnAssign.Invoke((turn, 0));
                }
                else if (isStripe)
                {
                    targetType[turn] = new HashSet<Circle>(stripes);
                    targetType[1 - turn] = new HashSet<Circle>(solids);

                    targets[turn] = stripes;
                    targets[1 - turn] = solids;

                    OnAssign.Invoke((turn, 1));
                }
                assigned = true;
            }

            pocketedAnyBall = true;

            if (targets[turn].Contains(data.ball))
            {
                pocketedOwnBall = true;
            }

            if (solids.Contains(data.ball)) solids.Remove(data.ball);
            if (stripes.Contains(data.ball)) stripes.Remove(data.ball);
        }

        void StopHandler()
        {
            acceptCollisions = false;

            // ─────────────────────────────────────────────
            // PRIORITY 1: 8-ball pocketed + scratch → always loss
            // ─────────────────────────────────────────────
            if (port8 && scratch)
            {
                Logger.Log("8-ball pocketed + scratch → loss");
                EndGame(player: turn, isWinner: false);
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 2: BREAK SHOT — handle entirely in isolation
            // ─────────────────────────────────────────────
            if (gameState == State.Breaking)
            {
                if (port8)
                {
                    // House rule: re-rack on 8-ball-on-break.
                    // Swap to: EndGame(player: turn, isWinner: true) for BCA rules.
                    Logger.Log("8-ball pocketed on break → re-rack");
                    RackUp();
                    // Same player breaks again (do NOT call Next())
                    ResetParams();
                    return;
                }

                if (scratch)
                {
                    // Scratch on break → opponent gets ball in hand behind headstring
                    Logger.Log("Scratch on break → ball behind headstring");
                    ResetCue(BallInHandRule.BehindHeadstring);
                    OnScratch.Invoke(turn);
                    Next();
                    ResetParams();
                    return;
                }

                // Legal break requires: hit the rack AND (pocket a ball OR hit a rail)
                if (firstHit == null || (!pocketedAnyBall && !edgeHit))
                {
                    Logger.Log("Illegal break (no hit or no rail/pocket) → foul");
                    ResetCue(BallInHandRule.Anywhere);
                    OnFoul.Invoke(turn);
                    Next();
                    ResetParams();
                    return;
                }

                // Legal break — pocketed balls determine group assignment (done elsewhere)
                // Breaker continues only if they pocketed a ball
                if (!pocketedAnyBall)
                {
                    Logger.Log("Legal break, no balls pocketed → next player");
                    Next();
                }
                else
                {
                    Logger.Log("Legal break, ball(s) pocketed → breaker continues");
                    // Groups are assigned externally when balls are pocketed
                }

                gameState = State.Open; // Table is now open
                OnTurnChanged(turn);
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 3: 8-ball pocketed (non-break)
            // ─────────────────────────────────────────────
            if (port8)
            {
                // Must have cleared ALL own balls first
                bool clearedOwn = targets[turn].Count == 0;

                // Called pocket — currently unenforced (correctPocket hardcoded true)
                // TODO: remove the override once calledPocket UI is implemented
                bool correctPocket = calledPocket == actualPocket;
                correctPocket = true; // ← remove this line when called-shot is implemented

                bool legalWin = clearedOwn && correctPocket;
                Logger.Log($"8-ball pocketed → clearedOwn={clearedOwn}, correctPocket={correctPocket} → {(legalWin ? "WIN" : "LOSS")}");
                EndGame(player: turn, isWinner: legalWin);
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 4: Scratch (non-break, non-8-ball)
            // ─────────────────────────────────────────────
            if (scratch)
            {
                Logger.Log("Scratch → ball in hand anywhere");
                ResetCue(BallInHandRule.Anywhere); // After break, scratch is always anywhere
                OnScratch.Invoke(turn);
                Next();
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 5: No first hit → foul (cue ball hit nothing)
            // ─────────────────────────────────────────────
            if (firstHit == null)
            {
                Logger.Log("No first hit → foul");
                ResetCue(BallInHandRule.Anywhere);
                OnFoul.Invoke(turn);
                Next();
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 6: Wrong first hit (only enforced when groups are assigned)
            // ─────────────────────────────────────────────
            if (assigned && !targetType[turn].Contains(firstHit))
            {
                Logger.Log($"Wrong first hit ({firstHit}) → foul");
                ResetCue(BallInHandRule.Anywhere);
                OnFoul.Invoke(turn);
                Next();
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 7: No pocket AND no rail → foul
            // ─────────────────────────────────────────────
            if (!pocketedAnyBall && !edgeHit)
            {
                Logger.Log("No pocket and no rail hit → foul");
                ResetCue(BallInHandRule.Anywhere);
                OnFoul.Invoke(turn);
                Next();
                ResetParams();
                return;
            }

            // ─────────────────────────────────────────────
            // PRIORITY 8: Open table — assign groups if a ball was pocketed
            // ─────────────────────────────────────────────
            if (!assigned && pocketedOwnBall)
            {
                // Group assignment should have already happened in the pocketing event.
                // If not, trigger it here as a fallback.
                Logger.Log("Open table: ball pocketed, groups should now be assigned");
                // AssignGroups(turn); ← call your assignment logic here if not event-driven
            }

            // ─────────────────────────────────────────────
            // PRIORITY 9: Legal shot — determine if turn continues
            // ─────────────────────────────────────────────
            if (!pocketedOwnBall)
            {
                Logger.Log("No own ball pocketed → next player");
                Next();
            }
            else
            {
                Logger.Log("Own ball pocketed → same player continues");
                if (pocketedOwnBall && targets[turn].Count == 0)
                {
                    targetType[turn] = new HashSet<Circle>(balls.Where((x, i) => i == 7));
                }
            }

            OnTurnChanged(turn);
            ResetParams();
        }

        void ResetParams()
        {
            firstHit = null;
            scratch = false;
            edgeHit = false;
            port8 = false;
            pocketedOwnBall = false;
            pocketedAnyBall = false;
            actualPocket = null;
            calledPocket = null;
            timerRunning = true;
        }

        public void RackUp(bool reRack = true)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                balls[i].Center = MetaData.rack[i];
                balls[i].PrevCenter = balls[i].Center;
                balls[i].Velocity = Vector2.Zero;
                balls[i].IsPocketed = false;
            }

            solids = new HashSet<Circle>(balls.Where((x, i) => i < 7));
            stripes = new HashSet<Circle>(balls.Where((x, i) => i > 7));

            gameState = State.Breaking;

            ResetCue(BallInHandRule.None);

            if (reRack) OnReRack.Invoke();

            foreach (var ball in potted)
            {
                ball.IsPocketed = false;
            }

            potted.Clear();
        }

        public void ResetCue(BallInHandRule rule = BallInHandRule.None)
        {
            switch (rule)
            {
                case BallInHandRule.BehindHeadstring:
                    cueBall.Center = new Vector2(MetaData.headStringX, MetaData.cueBallStart.Y);
                    break;

                case BallInHandRule.Anywhere:
                    cueBall.Center = Vector2.Zero;
                    break;

                case BallInHandRule.None:
                default:
                    cueBall.Center = MetaData.cueBallStart;
                    break;
            }

            cueBall.PrevCenter = cueBall.Center;
            cueBall.Velocity = Vector2.Zero;
            cueBall.IsPocketed = false;
            if (potted.Contains(cueBall)) potted.Remove(cueBall);
        }

        public void Update(float deltaTime)
        {
            if (timerRunning && !timerPaused)
            {
                timers[turn] -= deltaTime;
                if (timers[turn] <= 0)
                {
                    ResetTimer();
                    Next();
                }
            }

            foreach (var pottedBall in potted)
            {
                pottedBall.ApplyGravity(-400000);
            }

            physics.Tick();
        }

        public void Fire(Vector2 velocity)
        {
            cueBall.Velocity = velocity;
            cueBall.IsPocketed = false;
            physics.Fire();
            acceptCollisions = true;
            ResetTimer();
            timerRunning = false;
            OnFire((turn, velocity.X, velocity.Y));
        }

        void Next()
        {
            turn = (turn + 1) % 2;
            OnTurnChanged.Invoke(turn);
        }

        public void EndGame(int player, bool isWinner)
        {
            int winner = isWinner ? player : 1 - player;
            Logger.Log($"Player {winner} wins!");
            gameState = State.GameOver;
            OnGameOver?.Invoke(winner);
        }

        bool timerRunning = true;
        void ResetTimer()
        {
            timers[0] = 30;
            timers[1] = 30;
        }

        public bool timerPaused;
        public float[] GetTimes => timers;
        public float aimAngle = 0;
        public bool GetPlay() => !acceptCollisions;
        public int GetTurn() => turn;
        public void SetAimAngle(float val)
        {
            aimAngle = val;
            OnStateUpdate();
        }

        public int GetPlayerType(int player)
        {
            if (!assigned) return 3;
            if (targets[player] == solids && targets[player].Count > 0) return 0;
            if (targets[player] == stripes && targets[player].Count > 0) return 1;
            if (targets[player].Count == 0) return 2;
            return 0;
        }

        public void SendAim()
        {
            OnAim();
        }

        public event Action<int> OnGameOver = _ => { };
        public event Action<int> OnTurnChanged = _ => { };
        public event Action<int> OnScratch = _ => { };
        public event Action<int> OnFoul = _ => { };
        public event Action<(int turn, double X, double Y)> OnFire = _ => { };
        public event Action<(int player, int group)> OnAssign = _ => { };
        public event Action<int> OnBreak = _ => { };
        public event Action OnReRack = () => { };
        public event Action<int> On8BallPocketed = _ => { };
        public event Action OnStateUpdate = () => { };
        public event Action OnAim = () => { };

        public Action OnStop = () => { };

        public byte[] GetState()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte flags = 0;

                if (turn > 0) flags |= 1 << 0;
                if (assigned) flags |= 1 << 1;
                if (timerPaused) flags |= 1 << 2;
                if (acceptCollisions) flags |= 1 << 3;
                if (ballInHand) flags |= 1 << 4;
                if (breaking) flags |= 1 << 5;

                writer.Write(flags);
                writer.Write((byte)gameState);

                writer.Write(aimAngle);
                writer.Write(timers[0]);
                writer.Write(timers[1]);

                for (int i = 0; i < balls.Count; i++)
                {
                    writer.Write(balls[i].Center.X);
                    writer.Write(balls[i].Center.Y);
                    writer.Write(balls[i].PrevCenter.X);
                    writer.Write(balls[i].PrevCenter.Y);
                    writer.Write(balls[i].Velocity.X);
                    writer.Write(balls[i].Velocity.Y);
                    writer.Write(balls[i].IsPocketed);
                }

                writer.Write(cueBall.Center.X);
                writer.Write(cueBall.Center.Y);
                writer.Write(cueBall.PrevCenter.X);
                writer.Write(cueBall.PrevCenter.Y);
                writer.Write(cueBall.Velocity.X);
                writer.Write(cueBall.Velocity.Y);
                writer.Write(cueBall.IsPocketed);

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(targetType[i].Count);
                    foreach (var ball in targetType[i])
                    {
                        writer.Write(balls.IndexOf(ball));
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(targets[i].Count);
                    foreach (var ball in targets[i])
                    {
                        writer.Write(balls.IndexOf(ball));
                    }
                }

                return stream.ToArray();
            }
        }

        public void SetState(byte[] data)
        {
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                var flags = reader.ReadByte();

                turn = (flags >> 0) & 1;
                assigned = ((flags >> 1) & 1) > 0;
                timerPaused = ((flags >> 2) & 1) > 0;
                acceptCollisions = ((flags >> 3) & 1) > 0;
                ballInHand = ((flags >> 4) & 1) > 0;
                breaking = ((flags >> 5) & 1) > 0;

                gameState = (State)reader.ReadByte();
                aimAngle = reader.ReadSingle();
                timers[0] = reader.ReadSingle();
                timers[1] = reader.ReadSingle();

                potted.Clear();
                for (int i = 0; i < balls.Count; i++)
                {
                    balls[i].Center = new Vector2(reader.ReadDouble(), reader.ReadDouble());
                    balls[i].PrevCenter = new Vector2(reader.ReadDouble(), reader.ReadDouble());
                    balls[i].Velocity = new Vector2(reader.ReadDouble(), reader.ReadDouble());
                    balls[i].IsPocketed = reader.ReadBoolean();

                    if (balls[i].IsPocketed) { potted.Add(balls[i]); }
                }

                cueBall.Center = new Vector2(reader.ReadDouble(), reader.ReadDouble());
                cueBall.PrevCenter = new Vector2(reader.ReadDouble(), reader.ReadDouble());
                cueBall.Velocity = new Vector2(reader.ReadDouble(), reader.ReadDouble());
                cueBall.IsPocketed = reader.ReadBoolean();

                if (cueBall.IsPocketed) { potted.Add(cueBall); }

                for (int i = 0; i < 2; i++)
                {
                    targetType[i].Clear();
                    var count = reader.ReadInt32();
                    for (int j = 0; j < count; j++)
                    {
                        targetType[i].Add(balls[reader.ReadInt32()]);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    targets[i].Clear();
                    var count = reader.ReadInt32();
                    for (int j = 0; j < count; j++)
                    {
                        targets[i].Add(balls[reader.ReadInt32()]);
                    }
                }
            }
        }
    }
}