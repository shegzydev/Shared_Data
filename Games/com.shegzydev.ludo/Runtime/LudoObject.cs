using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum NetWorkEvents : byte
{
    TurnSwitch, Choose, Play, StateUpdate, Roll, EndGame, Timer
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
    public Action<short[]> OnRollDice;

    public Action<short> OnEndGame;
    public Action<float> OnTimerUpdate;

    public short turn { get; private set; }
    short numPlayers;

    float lastTimer;
    float timer = 20;

    System.Random rand = new System.Random();

    public LudoObject(short numPlayers)
    {
        this.numPlayers = numPlayers;
        Reset();
    }

    public void Init()
    {
        OnTurnsSwitch?.Invoke(turn);
        OnStateUpdate?.Invoke(BitConverter.GetBytes((short)0).Concat(GetState()).ToArray());
        // OnStateUpdate?.Invoke(GetState());
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
    public void Roll()
    {
        dice[0] = (short)rand.Next(1, 7);
        dice[1] = (short)rand.Next(1, 7);

        dice[2] = (short)(dice[0] + dice[1]);

        doubleSix = dice[2] == 12;

        OnRollDice?.Invoke(dice);

        timer = 20;

        if (!MoveAvailableOnRoll()) SkipTurn();
    }

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

        int color = pieceIndex / 4;
        int entry = pieceIndex % 4;

        var piece = gamePieces[(color)color][entry];

        short start = piece.pos;
        short steps = piece.MoveSteps(dice[chosen], chosen == 2);

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

        if (TurnEnded)
        {
            NextTurn();
            doubleSix = false;
        }

        chosen = -1;
        timer = 20;
    }

    void NextTurn()
    {
        if (!doubleSix) turn++;
        turn %= numPlayers;
        OnTurnsSwitch?.Invoke(turn);
    }

    void SkipTurn()
    {
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
            for (byte i = 0; i < 3; i++)
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
