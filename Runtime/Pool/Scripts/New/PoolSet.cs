using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoftFloat;
// using FixedMath;

public enum PoolNetEvents : byte
{
    Ready, TurnSwitch, Balls, Shoot, Timer, EndGame, Aim, State, Assign, CueSet, Rerack, Foul, Scratch, Rejoin
}

namespace PoolEngine
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
        public Snooker physics;

        // public Circle cueBall = new Circle(-73.000, 0);
        // public List<Circle> balls = new List<Circle>();
        // public List<Edge> edges = new List<Edge>();
        // public List<Circle> holes = new List<Circle>();

        bool scratch;
        bool port8;
        bool edgeHit;
        bool pocketedOwnBall;
        bool pocketedAnyBall;

        int firstHit = -1;

        Snooker.Circle calledPocket;
        Snooker.Circle actualPocket;

        int turn;
        State gameState;

        float[] timers = { 30, 30 };

        bool ballInHand;
        bool breaking;

        public bool getBallInHand => ballInHand;
        public bool getBreaking => breaking;

        public Snooker.Ball cueBall => physics.GetBalls[0];
        public Snooker.Ball[] GetBalls => physics.GetBalls;

        HashSet<int>[] targets = new HashSet<int>[2]
        {
            new(),
            new()
        };

        HashSet<int>[] targetType = new HashSet<int>[2]
        {
            new(),
            new()
        };

        HashSet<int> solids, stripes;

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

            physics = new Snooker(0.5f, -60f);

            HandleEvents();
            RackUp(false);

            OnTurnChanged(turn);
        }

        public void HandleEvents()
        {
            physics.OnHole = data =>
            {
                HoleHandler(data);
                OnPocket();
            };

            physics.OnBallCollision = data =>
            {
                if (!acceptCollisions) return;

                if (data.A.isCue || data.B.isCue)
                {
                    if (firstHit == -1) firstHit = data.A.isCue ? data.B.number : data.A.number;
                }

                if (data.A.potted) return;
                OnBallHit();
            };

            physics.OnEdgeCollision = data =>
            {
                if (!acceptCollisions) return;
                edgeHit = true;
                OnEdgeHit();
            };

            physics.Stopped = () =>
            {
                StopHandler();

                OnStop();

                OnStateUpdate();
            };
        }

        void HoleHandler((Snooker.Ball ball, Snooker.Circle hole) data)
        {
            if (data.ball.isCue)
            {
                scratch = true;
                return;
            }

            if (data.ball.is8)
            {
                port8 = true;
                actualPocket = data.hole;
                return;
            }

            bool isSolid = solids.Contains(data.ball.number);
            bool isStripe = stripes.Contains(data.ball.number);

            if (!assigned)
            {
                if (isSolid)
                {
                    targetType[turn] = new HashSet<int>(solids);
                    targetType[1 - turn] = new HashSet<int>(stripes);

                    targets[turn] = solids;
                    targets[1 - turn] = stripes;

                    OnAssign.Invoke((turn, 0));
                }
                else if (isStripe)
                {
                    targetType[turn] = new HashSet<int>(stripes);
                    targetType[1 - turn] = new HashSet<int>(solids);

                    targets[turn] = stripes;
                    targets[1 - turn] = solids;

                    OnAssign.Invoke((turn, 1));
                }
                assigned = true;
            }

            pocketedAnyBall = true;

            if (targets[turn].Contains(data.ball.number))
            {
                pocketedOwnBall = true;
            }

            if (solids.Contains(data.ball.number)) solids.Remove(data.ball.number);
            if (stripes.Contains(data.ball.number)) stripes.Remove(data.ball.number);
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
                if (firstHit == -1 || (!pocketedAnyBall && !edgeHit))
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
            if (firstHit == -1)
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
                    targetType[turn] = new HashSet<int>(Enumerable.Repeat(8, 1));
                }
            }

            OnTurnChanged(turn);
            ResetParams();
        }

        void ResetParams()
        {
            firstHit = -1;
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
            physics.RackBalls();

            solids = new HashSet<int>(Enumerable.Range(1, 7));
            stripes = new HashSet<int>(Enumerable.Range(9, 7));

            gameState = State.Breaking;

            ResetCue(BallInHandRule.None);

            if (reRack) OnReRack.Invoke();
        }

        public void ResetCue(BallInHandRule rule = BallInHandRule.None)
        {

            switch (rule)
            {
                case BallInHandRule.Anywhere:
                    physics.ResetCue(true);
                    break;

                case BallInHandRule.BehindHeadstring:
                case BallInHandRule.None:
                default:
                    physics.ResetCue();
                    break;
            }

        }

        public void placeCueFromRaw(uint x, uint y)
        {
            physics.PlaceCue(sfloat.FromRaw(x), sfloat.FromRaw(y));
        }

        public void PlaceCue(sfloat x, sfloat y)
        {
            physics.PlaceCue(x, y);
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

            physics.Tick(deltaTime);
        }

        public void Fire(sfloat power, sfloat dx, sfloat dy)
        {
            physics.Fire(power * dx, power * dy);
            acceptCollisions = true;
            ResetTimer();
            timerRunning = false;
            OnFire((turn, (power * dx).RawValue, (power * dy).RawValue));
        }

        public void Fire(sfloat vx, sfloat vy)
        {
            physics.Fire(vx, vy);
            acceptCollisions = true;
            ResetTimer();
            timerRunning = false;
            OnFire((turn, vx.RawValue, vy.RawValue));
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
        public event Action<(int turn, uint X, uint Y)> OnFire = _ => { };
        public event Action<(int player, int group)> OnAssign = _ => { };
        public event Action<int> OnBreak = _ => { };
        public event Action OnReRack = () => { };
        public event Action<int> On8BallPocketed = _ => { };
        public event Action OnStateUpdate = () => { };
        public event Action OnAim = () => { };


        public event Action OnBallHit = () => { };
        public event Action OnEdgeHit = () => { };
        public event Action OnPocket = () => { };

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

                var balls = physics.GetBalls;
                for (int i = 0; i < balls.Length; i++)
                {
                    writer.Write(balls[i].px.RawValue);
                    writer.Write(balls[i].py.RawValue);
                    writer.Write(balls[i].vx.RawValue);
                    writer.Write(balls[i].vy.RawValue);
                    writer.Write(balls[i].potted);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(targetType[i].Count);
                    foreach (var ball in targetType[i])
                    {
                        writer.Write(ball);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(targets[i].Count);
                    foreach (var ball in targets[i])
                    {
                        writer.Write(ball);
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

                var balls = physics.GetBalls;
                for (int i = 0; i < balls.Length; i++)
                {
                    balls[i].px = sfloat.FromRaw(reader.ReadUInt32());
                    balls[i].py = sfloat.FromRaw(reader.ReadUInt32());
                    balls[i].vx = sfloat.FromRaw(reader.ReadUInt32());
                    balls[i].vy = sfloat.FromRaw(reader.ReadUInt32());
                    balls[i].potted = reader.ReadBoolean();
                }

                for (int i = 0; i < 2; i++)
                {
                    targetType[i].Clear();
                    var count = reader.ReadInt32();
                    for (int j = 0; j < count; j++)
                    {
                        targetType[i].Add(reader.ReadInt32());
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    targets[i].Clear();
                    var count = reader.ReadInt32();
                    for (int j = 0; j < count; j++)
                    {
                        targets[i].Add(reader.ReadInt32());
                    }
                }
            }
        }

        public float GetEnergy()
        {
            return physics.GetEnergy();
        }
    }
}