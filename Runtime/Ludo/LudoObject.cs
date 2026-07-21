using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public enum LudoNetEvent : byte
{
    TurnSwitch, Choose, Play, StateUpdate, Roll, EndGame, Timer, Spin, Ready
}

public enum color : short
{
    B, R, G, Y
}

public class LudoObject
{
    class PieceObject
    {
        short start;
        public short pos { get; private set; }
        public bool safe => pos == -1 || pos >= 51;
        color col;

        public PieceObject(color col)
        {
            pos = -1;
            start = (short)((1 + ((short)col * 13)) % 52);
            this.col = col;
        }

        public void begin()
        {
            pos = (sbyte)start;
        }

        public short MoveSteps(short steps, bool isCombo = false)
        {
            short count = 0;

            if (pos == -1)
            {
                if (steps == 6 && !isCombo)
                {
                    pos = 0;
                    count++;
                }
            }
            else
            {
                for (int i = 0; i < steps; i++)
                {
                    if (!Move()) break;
                    count++;
                }
            }

            // if (!safe)
            // {
            //     if (ludoObject.CheckCollision(col, getGeneralPos()))
            //         SetPos(56);
            // }

            return count;
        }

        bool Move()
        {
            if (pos >= 56) return false;
            pos++;
            return true;
        }

        public void SetPos(short _pos)
        {
            pos = _pos;
        }

        public void End()
        {
            SetPos(56);
        }

        public short getAbsolutePos()
        {
            return (short)((pos + 1 + (short)col * 13) % 52);
        }
    }

    (byte x, byte y)[] mainpath = {
        //Main Path
        (6,0), (6,1), (6,2), (6,3), (6,4), (6,5),
        (5,6), (4,6), (3,6), (2,6), (1,6), (0,6),
        (0,7),
        (0,8), (1,8), (2,8), (3,8), (4,8), (5,8),
        (6,9), (6,10), (6,11), (6,12), (6,13), (6,14),
        (7,14),
        (8,14), (8,13), (8,12), (8,11), (8,10), (8,9),
        (9,8), (10,8), (11,8), (12,8), (13,8), (14,8),
        (14,7),
        (14, 6), (13, 6), (12,6), (11,6), (10, 6), (9,6),
        (8,5), (8,4), (8,3), (8,2), (8,1), (8,0),
        (7,0),
    };

    (byte x, byte y)[] home = {
        (7,1), (7,2), (7,3), (7,4), (7,5), (7,6),
        (1,7), (2,7), (3,7), (4,7), (5,7), (6,7),
        (7,13),(7,12),(7,11),(7,10), (7,9), (7,8),
        (13,7), (12,7), (11,7), (10,7), (9,7), (8,7)
    };

    Dictionary<color, PieceObject[]> gamePieces;

    public Action<(short pieceIndex, short start, short end, short steps)> OnPlay;
    public Action<short> OnTurnsSwitch;
    public Action<byte[]> OnStateUpdate;
    public Action OnReady;
    public Action<short[]> OnRollDice;
    public Action OnSpinDice;

    public Action<short> OnEndGame;
    public Action<float> OnTimerUpdate;

    public short turn { get; private set; }
    short numPlayers;

    public short playerCount => numPlayers;

    float lastTimer;
    float timer = 20;

    int doubleStreak = 0;
    int[] sixStreak;

    Random rand = new Random();

    LudoMoveSpoofer ludoMoveSpoofer = new();

    public LudoObject(short numPlayers)
    {
        this.numPlayers = numPlayers;
        sixStreak = new int[numPlayers];
    }

    public void Init(int turnIndex)
    {
        Reset();
        turn = (short)turnIndex;
        doubleStreak = 0;
        OnReady?.Invoke();
        OnTurnsSwitch?.Invoke((short)turnIndex);
        OnStateUpdate?.Invoke(BitConverter.GetBytes((short)0).Concat(GetState()).ToArray());
    }

    public void Reset()
    {
        gamePieces = new Dictionary<color, PieceObject[]>();
        for (int i = 0; i < 4; i++)
        {
            var col = (color)i;
            gamePieces.Add(col, new PieceObject[4] { new(col), new(col), new(col), new(col) });
        }
    }

    short[] dice = new short[3];
    short chosen = -1;
    bool doubleSix = false;
    public async void Roll(Action<(int a, int b)> OnRoll = null)
    {
        OnSpinDice?.Invoke();
        timer = 20;

        await Task.Delay(500);

        if (ludoMoveSpoofer.TryGetAdvantageousRoll(this, turn, out var a, out var b))
        {
            dice[0] = a;
            dice[1] = b;
            dice[2] = (short)(a + b);
        }
        else
        {
            dice[0] = (short)rand.Next(1, 7);
            dice[1] = (short)rand.Next(1, 7);
            dice[2] = (short)(dice[0] + dice[1]);
        }

        if (dice[2] == 12 && sixStreak[turn] > 0)
        {
            int i = rand.Next(2);
            dice[i] -= (short)rand.Next(1, 6);
            dice[2] = (short)(dice[0] + dice[1]);
        }

        doubleSix = dice[2] == 12;
        if (doubleSix)
        {
            sixStreak[turn] = 1000;
            doubleStreak++;
        }

        sixStreak[turn]--;

        OnRoll?.Invoke((dice[0], dice[1]));

        OnRollDice?.Invoke(dice);

        if (doubleStreak >= 3)
        {
            SkipTurn();
            return;
        }

        if (!MoveAvailableOnRoll()) SkipTurn();

        if (numTurnPawnsInPlay(out var inPlay) == 1)
        {
            if (!((dice[0] == 6 || dice[1] == 6) && numTurnPawnsInHome(out var _) > 0))
            {
                chosen = 2;
                Play((short)inPlay[0].index);
            }
        }
    }

    public async void BotRollOverride(short a, short b)
    {
        OnSpinDice?.Invoke();
        timer = 20;

        await Task.Delay(500);

        dice[0] = a;
        dice[1] = b;
        dice[2] = (short)(dice[0] + dice[1]);

        if (dice[2] == 12 && sixStreak[turn] > 0)
        {
            int i = rand.Next(2);
            dice[i] -= (short)rand.Next(1, 6);
            dice[2] = (short)(dice[0] + dice[1]);
        }

        doubleSix = dice[2] == 12;
        if (doubleSix)
        {
            sixStreak[turn] = 1000;
            doubleStreak++;
        }

        sixStreak[turn]--;

        OnRollDice?.Invoke(dice);

        if (doubleStreak >= 3)
        {
            SkipTurn();
            return;
        }

        if (!MoveAvailableOnRoll()) SkipTurn();

        if (numTurnPawnsInPlay(out var inPlay) == 1)
        {
            if (!((dice[0] == 6 || dice[1] == 6) && numTurnPawnsInHome(out var _) > 0))
            {
                chosen = 2;
                Play((short)inPlay[0].index);
            }
        }
    }

    //Client side will call this function
    public void RollOverride(short a, short b)
    {
        dice[0] = a;
        dice[1] = b;
        dice[2] = (short)(dice[0] + dice[1]);
        OnRollDice?.Invoke(dice);
    }

    public bool Choose(short i)
    {
        if (dice[2] == 0) return false;
        chosen = i;
        return true;
    }

    public void Play(short pieceIndex)
    {
        if (chosen == -1) return;

        if (!CanPieceUseDie(pieceIndex, chosen)) return;

        int color = pieceIndex / 4;
        int entry = pieceIndex % 4;

        var piece = gamePieces[(color)color][entry];

        short start = piece.pos;
        short steps = piece.MoveSteps(dice[chosen], chosen == 2);

        bool noneLeft = false;

        if (chosen < 2 && dice[1 - chosen] > 0)
        {
            var avail = numTurnPawnsInPlay(out var inPlay);
            if (avail == 1)
            {
                if (!(dice[1 - chosen] == 6 && numTurnPawnsInHome(out var _) > 0))
                {
                    var piece2 = inPlay[0].obj;
                    short index = (short)inPlay[0].index;

                    short start2 = piece2.pos;
                    short steps2 = piece2.MoveSteps(dice[1 - chosen], false);

                    steps += steps2;
                    // OnPlay?.Invoke((index, start2, piece2.pos, steps2));
                    dice[1 - chosen] = 0;
                }
            }
            else if (avail == 0)
            {
                noneLeft = true;
            }
        }

        if (!piece.safe && CheckCollision((color)color, piece.getAbsolutePos()))
        {
            piece.End();
        }

        OnPlay?.Invoke((pieceIndex, start, piece.pos, steps));

        if (chosen == 2)
        {
            //Debug.Log("chose third value");
            dice = new short[3] { 0, 0, 0 };
        }
        else
        {
            dice[chosen] = 0;
            dice[2] = (short)(dice[0] + dice[1]);
        }

        OnStateUpdate?.Invoke(BitConverter.GetBytes(steps).Concat(GetState()).ToArray());

        if (GameOver())
        {
            OnEndGame?.Invoke(turn);
        }

        if (TurnEnded || noneLeft)
        {
            NextTurn();
            doubleSix = false;
        }

        chosen = -1;
        timer = 20;
    }

    void NextTurn()
    {
        if (!doubleSix)
        {
            turn++;
            doubleStreak = 0;
        }
        turn %= numPlayers;
        OnTurnsSwitch?.Invoke(turn);
    }

    void SkipTurn()
    {
        doubleStreak = 0;
        doubleSix = false;//Just in case
        dice = new short[3] { 0, 0, 0 };
        chosen = -1;
        timer = 20;
        NextTurn();
    }

    bool GameOver()
    {
        int done = 0;

        for (int i = 0; i < 16; i++)
        {
            short curr_turn = (short)((i / 4) % numPlayers);
            if (turn != curr_turn) continue;
            if (gamePieces[(color)(i / 4)][i % 4].pos >= 56) done++;
        }

        return done == ((numPlayers == 2) ? 8 : 4);
    }

    int numTurnPawnsInPlay(out List<(PieceObject obj, int index)> availablePieces)
    {
        int pawnsInPlay = 0;
        availablePieces = new();

        for (int i = 0; i < 16; i++)
        {
            short curr_turn = (short)((i / 4) % numPlayers);
            if (turn != curr_turn) continue;
            var piece = gamePieces[(color)(i / 4)][i % 4];

            if (piece.pos > -1 && piece.pos < 56)
            {
                pawnsInPlay++;
                availablePieces.Add((piece, i));
            }
        }

        return pawnsInPlay;
    }

    int numTurnPawnsInHome(out List<(PieceObject obj, int index)> homePieces)
    {
        int pawnsInHome = 0;
        homePieces = new();

        for (int i = 0; i < 16; i++)
        {
            short curr_turn = (short)((i / 4) % numPlayers);
            if (turn != curr_turn) continue;
            var piece = gamePieces[(color)(i / 4)][i % 4];

            if (piece.pos == -1)
            {
                pawnsInHome++;
                homePieces.Add((piece, i));
            }
        }

        return pawnsInHome;
    }

    bool MoveAvailableOnRoll()
    {
        bool pawnInPlay = false;
        bool pawnInHome = false;

        for (int i = 0; i < 16; i++)
        {
            short curr_turn = (short)((i / 4) % numPlayers);
            if (turn != curr_turn) continue;
            var piece = gamePieces[(color)(i / 4)][i % 4];

            if (piece.pos > -1 && piece.pos < 56)
            {
                pawnInPlay = true;
            }
            else if (piece.pos == -1)
            {
                pawnInHome = true;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            var val = dice[i];
            if (i < 2)
            {
                if (val < 6 && pawnInPlay) return true;
                if (val == 6 && (pawnInHome || pawnInPlay)) return true;
            }
            else
            {
                if (pawnInPlay) return true;
            }
        }

        return false;
    }

    // ==================== Pure simulation helpers ====================

    bool CanApply(short pos, short value, bool isCombo)
    {
        if (value == 0 || pos >= 56) return false;
        if (pos == -1) return value == 6 && !isCombo;
        return true;
    }

    // Read-only version of CheckCollision — does NOT mutate game state
    bool WouldCapture(int colorIdx, short pos)
    {
        bool safe = (pos == -1 || pos >= 51);
        if (safe) return false;

        short abs = (short)((pos + 1 + colorIdx * 13) % 52);

        for (int i = 0; i < 16; i++)
        {
            int otherColor = i / 4;
            if (colorIdx % numPlayers == otherColor % numPlayers) continue;

            var other = gamePieces[(color)otherColor][i % 4];
            if (other.safe) continue;
            if (other.getAbsolutePos() == abs) return true;
        }
        return false;
    }

    short SimulatePos(int colorIdx, short pos, short value, bool isCombo)
    {
        if (!CanApply(pos, value, isCombo)) return pos;

        short p = (pos == -1) ? (short)0 : pos;
        if (pos != -1)
        {
            for (int i = 0; i < value && p < 56; i++) p++;
        }

        // Same rule as Play(): landing on an enemy sends THIS piece home too
        if (WouldCapture(colorIdx, p)) p = 56;

        return p;
    }

    bool BothDiceUsableInOrder(int colorIdx, short pos, short firstIdx, short secondIdx)
    {
        if (!CanApply(pos, dice[firstIdx], firstIdx == 2)) return false;
        short afterFirst = SimulatePos(colorIdx, pos, dice[firstIdx], firstIdx == 2);
        return CanApply(afterFirst, dice[secondIdx], secondIdx == 2);
    }

    List<PieceObject> MovablePieces()
    {
        var list = new List<PieceObject>();
        for (int i = 0; i < 16; i++)
        {
            int colorIdx = i / 4;
            if (colorIdx % numPlayers != turn) continue;
            var piece = gamePieces[(color)colorIdx][i % 4];
            if (piece.pos < 56) list.Add(piece);
        }
        return list;
    }

    // ==================== Public API ====================

    // Can this specific piece use this specific die slot right now?
    // Accounts for capture and the "must use both dice" rule when it's the player's last piece.
    public bool CanPieceUseDie(short pieceIndex, short diceIndex)
    {
        if (diceIndex < 0 || diceIndex > 2 || dice[diceIndex] == 0) return false;

        int colorIdx = pieceIndex / 4;
        int entry = pieceIndex % 4;
        if (colorIdx < 0 || colorIdx > 3 || entry < 0 || entry > 3) return false;
        if (colorIdx % numPlayers != turn) return false;

        var piece = gamePieces[(color)colorIdx][entry];
        if (!CanApply(piece.pos, dice[diceIndex], diceIndex == 2)) return false;

        // Forced-order rule: only one movable piece, both individual dice still live
        if (diceIndex != 2 && dice[0] != 0 && dice[1] != 0 && MovablePieces().Count == 1)
        {
            short other = (short)(1 - diceIndex);
            bool thisFirstWorks = BothDiceUsableInOrder(colorIdx, piece.pos, diceIndex, other);
            bool otherFirstWorks = BothDiceUsableInOrder(colorIdx, piece.pos, other, diceIndex);

            // If only playing the OTHER die first lets both get used, this die is blocked as opener
            if (otherFirstWorks && !thisFirstWorks) return false;
        }

        return true;
    }

    // Does this piece have any usable die at all (any of the 3 slots)?
    public bool HasValidMove(short pieceIndex) =>
        CanPieceUseDie(pieceIndex, 0) || CanPieceUseDie(pieceIndex, 1) || CanPieceUseDie(pieceIndex, 2);

    // Does ANY piece belonging to the current player have a legal move for this die?
    public bool HasValidMoveForDie(short diceIndex)
    {
        if (diceIndex < 0 || diceIndex > 2 || dice[diceIndex] == 0) return false;
        for (int i = 0; i < 16; i++)
            if (CanPieceUseDie((short)i, diceIndex)) return true;
        return false;
    }

    public bool CheckCollision(color col, int refPos)
    {
        for (int i = 0; i < 16; i++)
        {
            int color = i / 4;

            if ((int)col % numPlayers == color % numPlayers) continue;

            int entry = i % 4;
            var piece = gamePieces[(color)color][entry];

            if (piece.safe) continue;

            if (refPos == piece.getAbsolutePos())
            {
                //Debug.Log($"Collided with a {color} piece");
                piece.SetPos(-1);
                return true;
            }
        }
        return false;
    }

    public (short x, short y)[] GetPath(color col = color.B)
    {
        (short, short)[] _path = new (short, short)[57];

        int start = 1 + (int)col * 13;
        for (int i = 0; i < 51; i++)
        {
            int pos = (start + i) % 52;
            _path[i] = mainpath[pos];
        }

        for (int i = 0; i < 6; i++)
        {
            _path[51 + i] = home[(int)col * 6 + i];
        }

        return _path;
    }

    public short[] positions
    {
        get
        {
            List<short> poss = new();
            for (int i = 0; i < 4; i++)
            {
                foreach (var piece in gamePieces[(color)i])
                {
                    poss.Add(piece.pos);
                }
            }
            return poss.ToArray();
        }
    }

    public static implicit operator bool(LudoObject myObject)
    {
        return myObject != null;
    }

    public byte[] GetState()
    {
        using (MemoryStream stream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(turn);
                var positionData = positions;
                foreach (var pos in positionData)
                {
                    writer.Write(pos);
                }

                return stream.ToArray();
            }
        }
    }

    public void LoadState(byte[] rawData)
    {
        using (MemoryStream stream = new MemoryStream(rawData))
        {
            BinaryReader reader = new BinaryReader(stream);

            reader.ReadInt16();
            for (byte i = 0; i < 16; i++)
            {
                int color = i / 4;
                int entry = i % 4;
                gamePieces[(color)color][entry].SetPos(reader.ReadInt16());
            }

            reader.Dispose();
        }
    }

    public void SetTurn(short _turn)
    {
        turn = _turn;
        OnTurnsSwitch?.Invoke(turn);
    }

    bool TurnEnded
    {
        get
        {
            short sum = 0;
            for (byte i = 0; i < 2; i++)
            {
                sum += dice[i];
            }
            return sum == 0;
        }
    }

    public void TickTimer(float delta)
    {
        lastTimer = timer;
        timer -= delta;
        if ((int)lastTimer != (int)timer) OnTimerUpdate?.Invoke(timer);
        if (timer <= 0) SkipTurn();
    }
}
