using System;
using System.Collections.Generic;
using System.Linq;

public class LudoMoveSpoofer
{
    bool findKill = false;
    bool foundKill = false;

    private readonly GuaranteedRoller guaranteedRoller;
    private readonly Random rng;
    private color lastColor;

    public int RigThreshold { get; set; } = 95;

    public LudoMoveSpoofer(int hitsPerBatch, int batchSize)
    {
        rng = new Random();
        guaranteedRoller = new GuaranteedRoller(hitsPerBatch, batchSize);
    }

    public bool TryGetAdvantageousRoll(LudoObject game, int turnIndex, out short a, out short b)
    {
        foundKill = false;

        var numPlayers = game.playerCount;
        var positions = game.positions;

        var OwnedColors = new List<color>();
        for (short c = 0; c < 4; c++)
        {
            if (c % numPlayers == turnIndex % numPlayers) OwnedColors.Add((color)c);
        }

        bool anyOnBoard = OwnedColors.Any(c =>
        {
            int baseIndex = (int)c * 4;
            for (int p = 0; p < 4; p++)
            {
                var pos = positions[baseIndex + p];
                if (pos >= 0 && pos < 56) return true;
            }
            return false;
        });

        short bestA = 1, bestB = 1;
        int bestScore = int.MinValue;

        for (short da = 1; da <= 6; da++)
        {
            for (short db = 1; db <= 6; db++)
            {
                short[] candidateDice = { da, db, (short)(da + db) };
                var best = BestMoveFor(candidateDice, positions, OwnedColors);
                int score = best?.score ?? int.MinValue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestA = da;
                    bestB = db;
                }
            }
        }

        if (bestScore >= RigThreshold)
        {
            a = bestA;
            b = bestB;
            return true;
        }

        if (!anyOnBoard)
        {
            // Nothing in play yet - nudge a single 6 to get started, but let the other die
            // land naturally rather than forcing a double.
            a = 6;
            b = (short)rng.Next(1, 7);
            return true;
        }

        a = 0;
        b = 0;
        return false;
    }

    public (short diceIndex, short pieceIndex)? ChooseBestMove(LudoObject game, int turnIndex, short[] localDice)
    {
        foundKill = false;

        int numPlayers = game.playerCount;

        var OwnedColors = new List<color>();
        for (short c = 0; c < 4; c++)
        {
            if (c % numPlayers == turnIndex % numPlayers) OwnedColors.Add((color)c);
        }

        var best = BestMoveFor(localDice, game.positions, OwnedColors);

        if (best != null) lastColor = (color)(best.Value.pieceIndex / 4);
        return best == null ? null : (best.Value.diceIndex, best.Value.pieceIndex);
    }

    private (short diceIndex, short pieceIndex, int score)? BestMoveFor(short[] dice, short[] positions, List<color> OwnedColors)
    {
        var candidates = new List<(short diceIndex, short pieceIndex, int score)>();

        foreach (var ownedColor in OwnedColors)
        {
            int baseIndex = (int)ownedColor * 4;

            for (short d = 0; d < 3; d++)
            {
                short steps = dice[d];
                if (steps == 0) continue;

                for (short p = 0; p < 4; p++)
                {
                    short pos = positions[baseIndex + p];
                    if (!CanMove(pos, steps, isCombo: d == 2)) continue;

                    int score = ScoreMove(pos, steps, positions, ownedColor, OwnedColors);
                    score = (int)(score * (ownedColor == lastColor ? 1f : 1.4f));

                    candidates.Add((d, (short)(baseIndex + p), score));
                }
            }
        }

        if (candidates.Count == 0) return null;

        return candidates.OrderByDescending(c => c.score).First();
    }

    private bool CanMove(short pos, short steps, bool isCombo)
    {
        if (pos >= 56) return false;               // already home
        if (pos == -1) return steps == 6 && !isCombo; // only a solo 6 brings a piece out
        if (pos + steps > 56) return false;
        if (pos + steps == 56 && pos < 51) return false;
        return true;
    }

    private int ScoreMove(short pos, short steps, short[] allPositions, color pieceColor, List<color> OwnedColors)
    {
        short dest = pos == -1 ? (short)0 : (short)Math.Min(pos + steps, 56);

        int score = pos < 51 ? ((dest - Math.Max(pos, (short)0)) * 10) : 0; // reward raw progress

        if (pos < 51 && dest > pos)
        {
            score += dest * 2;
        }

        if (pos == -1)
        {
            // Urgent if this would be the bot's first piece out; a mild nice-to-have
            // otherwise, so it doesn't keep abandoning board progress just to fetch
            // another piece every time a 6 shows up.
            var activeOwnedPieces = CountActiveOwnedPieces(allPositions, OwnedColors);
            score += Math.Max(3 - activeOwnedPieces, 0) * 15;
        }

        if (dest >= 56)
        {
            score += (pos >= 51) ? 15 : 100;
        }  // reaching home
        else if (dest >= 51)
        {
            score += (pos >= 51) ? 10 : 30;
        }// reaching the safe home stretch

        if (dest < 51 && CapturesOpponent(dest, pieceColor, allPositions, OwnedColors))
        {
            if (!foundKill) { findKill = guaranteedRoller.Roll(); foundKill = true; }
            score += findKill ? 250 : 10;
        }

        if (pos >= 0 && pos < 51 && IsExposed(pos, pieceColor, allPositions, OwnedColors, out int gap0))
        {
            score += 150; // mild bonus for moving a piece that's currently sitting in capture range
        }

        if (dest >= 0 && dest < 51 && IsExposed(dest, pieceColor, allPositions, OwnedColors, out int gap1))
        {
            score -= 100 * (12 - gap1);
        }

        return score;
    }

    private int CountActiveOwnedPieces(short[] allPositions, List<color> OwnedColors)
    {
        int count = 0;
        foreach (var ownedColor in OwnedColors)
        {
            int baseIndex = (int)ownedColor * 4;
            for (int p = 0; p < 4; p++)
            {
                short pos = allPositions[baseIndex + p];
                if (pos >= 0 && pos < 56) count++;
            }
        }
        return count;
    }

    private bool CapturesOpponent(short destPos, color pieceColor, short[] allPositions, List<color> OwnedColors)
    {
        short myAbs = AbsolutePos(destPos, pieceColor);

        for (int i = 0; i < allPositions.Length; i++)
        {
            color otherColor = (color)(i / 4);
            if (OwnedColors.Contains(otherColor)) continue;

            short pos = allPositions[i];
            if (pos == -1 || pos >= 51) continue; // in yard or in home stretch = safe

            if (AbsolutePos(pos, otherColor) == myAbs) return true;
        }

        return false;
    }

    private bool IsExposed(short pos, color pieceColor, short[] allPositions, List<color> OwnedColors, out int gap)
    {
        // Rough heuristic: any opponent piece within 12 tiles behind us on the shared path
        // could capture us on their next roll.
        short myAbs = AbsolutePos(pos, pieceColor);
        gap = 0;

        for (int i = 0; i < allPositions.Length; i++)
        {
            color otherColor = (color)(i / 4);
            if (OwnedColors.Contains(otherColor)) continue;

            short oppPos = allPositions[i];
            if (oppPos == -1 || oppPos >= 51) continue;

            short oppAbs = AbsolutePos(oppPos, otherColor);
            gap = (myAbs - oppAbs + 52) % 52;
            if (gap > 0 && gap <= 12) return true;
        }

        return false;
    }

    private short AbsolutePos(short pos, color col)
    {
        return (short)((pos + 1 + (short)col * 13) % 52);
    }
}

public class GuaranteedRoller
{
    private readonly Random _rng = new Random();
    private readonly int _batchSize;
    private readonly int _hitsPerBatch;
    private List<bool> _bag = new();
    private int _index;

    public GuaranteedRoller(int hitsPerBatch, int batchSize)
    {
        _hitsPerBatch = hitsPerBatch;
        _batchSize = batchSize;
        RefillBag();
    }

    private void RefillBag()
    {
        _bag = Enumerable.Repeat(true, _hitsPerBatch)
            .Concat(Enumerable.Repeat(false, _batchSize - _hitsPerBatch))
            .OrderBy(_ => _rng.Next())
            .ToList();
        _index = 0;
    }

    public bool Roll()
    {
        if (_index >= _bag.Count)
            RefillBag();

        return _bag[_index++];
    }
}