using System;
using System.Collections.Generic;
using System.Linq;

/*public class LudoMoveEvaluator
{
    bool findKill = false;
    bool foundKill = false;

    private readonly PercentageWeightedRandom killChecker;
    private readonly PercentageWeightedRandom homeChecker;
    private readonly Random rng;
    private color lastColor;

    public int RigThreshold { get; set; } = 95;

    public LudoMoveEvaluator(int hitsPerBatch, int batchSize)
    {
        rng = new Random();
        killChecker = new PercentageWeightedRandom(hitsPerBatch, batchSize);
        homeChecker = new PercentageWeightedRandom(2, 10);
    }

    public bool TryGoodRoll(LudoObject game, int turnIndex, out short a, out short b)
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
            score += homeChecker.CheckTrue() ? 2000 : 0;
        }  // reaching home
        else if (dest >= 51)
        {
            score += (pos >= 51) ? 10 : 30;
        }// reaching the safe home stretch

        if (pos >= 0 && pos < 51 && IsExposed(pos, pieceColor, allPositions, OwnedColors, out int gap0))
        {
            score += 150; // mild bonus for moving a piece that's currently sitting in capture range
        }

        if (dest < 51 && CapturesOpponent(dest, pieceColor, allPositions, OwnedColors))
        {
            if (!foundKill) { findKill = killChecker.CheckTrue(); foundKill = true; }
            score += findKill ? 2500 : 10;
        }
        else
        {
            if (dest >= 0 && dest < 51 && IsExposed(dest, pieceColor, allPositions, OwnedColors, out int gap1))
            {
                score -= 10 * (12 - gap1);
            }

            if (dest >= 0 && dest < 51 && behindOpponent(dest, pieceColor, allPositions, OwnedColors, out int closeGap))
            {
                score += 20 * (12 - closeGap);
            }
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
            if (oppPos >= 51) continue;

            if (oppPos >= 0)
            {
                short oppAbs = AbsolutePos(oppPos, otherColor);
                gap = (myAbs - oppAbs + 52) % 52;
                if (gap > 0 && gap <= 12) return true;
            }
            else if (oppPos < 0)
            {
                oppPos = 0;
                short oppAbs = AbsolutePos(oppPos, otherColor);
                gap = (myAbs - oppAbs + 52) % 52;
                if (gap >= 0 && gap <= 12) return true;
            }
        }

        return false;
    }

    private bool behindOpponent(short pos, color pieceColor, short[] allPositions, List<color> OwnedColors, out int gap)
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
            if (oppPos >= 51) continue;

            if (oppPos == -1) oppPos = 0;

            short oppAbs = AbsolutePos(oppPos, otherColor);
            gap = ((oppAbs + 52) - myAbs) % 52;
            if (gap > 0 && gap <= 12) return true;
        }

        return false;
    }

    private short AbsolutePos(short pos, color col)
    {
        return (short)((pos + 1 + (short)col * 13) % 52);
    }
}
*/

public class LudoMoveEvaluator
{
    private readonly PercentageWeightedRandom killChecker;
    private readonly PercentageWeightedRandom homeChecker;
    private readonly PercentageWeightedRandom rigGateChecker;
    private readonly PercentageWeightedRandom mistakeChecker;
    private readonly Random rng;
    private color lastColor;

    public int RigThreshold { get; set; } = 95;

    public LudoMoveEvaluator(int hitsPerBatch, int batchSize)
    {
        rng = new Random();
        killChecker = new PercentageWeightedRandom(hitsPerBatch, batchSize);
        homeChecker = new PercentageWeightedRandom(2, 10);
        // Even when a "rig-worthy" roll exists, don't always take it - let good
        // rolls look like luck rather than a guarantee. ~70% of eligible turns.
        rigGateChecker = new PercentageWeightedRandom(7, 10);
        // Small chance to play the runner-up move instead of the strict best,
        // so the bot doesn't read as mechanically perfect every single turn.
        mistakeChecker = new PercentageWeightedRandom(1, 10);
    }

    public bool TryGoodRoll(LudoObject game, int turnIndex, out short a, out short b)
    {
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

                // Exploratory: we're only asking "how promising is this dice combo",
                // not actually committing to a move. Must NOT consume killChecker /
                // homeChecker's real luck budget - that's reserved for the move that
                // actually gets played, in ChooseBestMove.
                var best = BestMoveFor(candidateDice, positions, OwnedColors, exploratory: true);
                int score = best?.score ?? int.MinValue;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestA = da;
                    bestB = db;
                }
            }
        }

        // Being eligible to rig isn't the same as rigging: only follow through
        // some of the time so favorable rolls don't show up like clockwork.
        if (bestScore >= RigThreshold && rigGateChecker.CheckTrue())
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
        int numPlayers = game.playerCount;

        var OwnedColors = new List<color>();
        for (short c = 0; c < 4; c++)
        {
            if (c % numPlayers == turnIndex % numPlayers) OwnedColors.Add((color)c);
        }

        // Committed: this is the actual move for the actual roll. Kill/home luck
        // gets decided for real here (once per call - see BestMoveFor).
        var best = BestMoveForWithMistake(localDice, game.positions, OwnedColors);

        if (best != null) lastColor = (color)(best.Value.pieceIndex / 4);
        return best == null ? null : (best.Value.diceIndex, best.Value.pieceIndex);
    }

    private (short diceIndex, short pieceIndex, int score)? BestMoveForWithMistake(
        short[] dice, short[] positions, List<color> OwnedColors)
    {
        var ranked = RankedMovesFor(dice, positions, OwnedColors, exploratory: false);
        if (ranked.Count == 0) return null;
        if (ranked.Count == 1) return ranked[0];

        var top = ranked[0];
        var second = ranked[1];

        // Only "miss" the best move if the runner-up is close enough that a strong
        // human could plausibly have picked it too - never hand back a howler.
        bool closeEnough = second.score >= top.score - 150;
        if (closeEnough && mistakeChecker.CheckTrue())
        {
            return second;
        }

        return top;
    }

    private (short diceIndex, short pieceIndex, int score)? BestMoveFor(
        short[] dice, short[] positions, List<color> OwnedColors, bool exploratory)
    {
        var candidates = RankedMovesFor(dice, positions, OwnedColors, exploratory);
        return candidates.Count == 0 ? null : candidates[0];
    }

    private List<(short diceIndex, short pieceIndex, int score)> RankedMovesFor(
        short[] dice, short[] positions, List<color> OwnedColors, bool exploratory)
    {
        var candidates = new List<(short diceIndex, short pieceIndex, int score)>();

        // "Should we favor a kill / an immediate home run this turn" is decided at
        // most ONCE per call (i.e. once for the final chosen move), not once per
        // capture/home opportunity we happen to scan across. In exploratory mode
        // these never get set - we just assume best case and never touch the RNG.
        bool killDecided = false, killFavored = false;
        bool homeDecided = false, homeFavored = false;

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

                    int score = ScoreMove(
                        pos, steps, positions, ownedColor, OwnedColors, exploratory,
                        ref killDecided, ref killFavored, ref homeDecided, ref homeFavored);
                    score = (int)(score * (ownedColor == lastColor ? 1f : 1.4f));

                    candidates.Add((d, (short)(baseIndex + p), score));
                }
            }
        }

        return candidates.OrderByDescending(c => c.score).ToList();
    }

    private bool CanMove(short pos, short steps, bool isCombo)
    {
        if (pos >= 56) return false;               // already home
        if (pos == -1) return steps == 6 && !isCombo; // only a solo 6 brings a piece out
        if (pos + steps > 56) return false;
        return true;
    }

    private int ScoreMove(
        short pos, short steps, short[] allPositions, color pieceColor, List<color> OwnedColors,
        bool exploratory, ref bool killDecided, ref bool killFavored, ref bool homeDecided, ref bool homeFavored)
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
            bool homeSucceeds;
            if (exploratory)
            {
                // Just probing dice potential - assume the finish lands, but don't
                // burn the real "not every home run happens" gate for a hypothetical.
                homeSucceeds = true;
            }
            else
            {
                if (!homeDecided) { homeFavored = homeChecker.CheckTrue(); homeDecided = true; }
                homeSucceeds = homeFavored;
            }
            score += homeSucceeds ? 2000 : 0;
        }
        else if (dest >= 51)
        {
            score += (pos >= 51) ? 10 : 30;
        } // reaching the safe home stretch

        if (pos >= 0 && pos < 51 && IsExposed(pos, pieceColor, allPositions, OwnedColors, out int gap0))
        {
            score += 150; // mild bonus for moving a piece that's currently sitting in capture range
        }

        if (dest < 51 && CapturesOpponent(dest, pieceColor, allPositions, OwnedColors))
        {
            bool willKill;
            if (exploratory)
            {
                // Just probing dice potential - assume the kill lands, but don't burn
                // the real "not every kill opportunity gets taken" gate for a hypothetical.
                willKill = true;
            }
            else
            {
                if (!killDecided) { killFavored = killChecker.CheckTrue(); killDecided = true; }
                willKill = killFavored;
            }
            score += willKill ? 2500 : 10;
        }
        else
        {
            if (dest >= 0 && dest < 51 && IsExposed(dest, pieceColor, allPositions, OwnedColors, out int gap1))
            {
                score -= 10 * (12 - gap1);
            }

            if (dest >= 0 && dest < 51 && behindOpponent(dest, pieceColor, allPositions, OwnedColors, out int closeGap))
            {
                score += 20 * (12 - closeGap);
            }
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
        // could capture us on their next roll. Pieces still in the yard can't move without
        // first rolling a 6, so they aren't a live threat yet - only pieces already on the
        // board count here.
        short myAbs = AbsolutePos(pos, pieceColor);
        gap = 0;

        for (int i = 0; i < allPositions.Length; i++)
        {
            color otherColor = (color)(i / 4);
            if (OwnedColors.Contains(otherColor)) continue;

            short oppPos = allPositions[i];
            if (oppPos < 0 || oppPos >= 51) continue;

            short oppAbs = AbsolutePos(oppPos, otherColor);
            gap = (myAbs - oppAbs + 52) % 52;
            if (gap > 0 && gap <= 12) return true;
        }

        return false;
    }

    private bool behindOpponent(short pos, color pieceColor, short[] allPositions, List<color> OwnedColors, out int gap)
    {
        // Any opponent piece within 12 tiles ahead of us on the shared path that we
        // could catch up to and capture on our next roll. Pieces still in the yard
        // can't be caught up to since they haven't entered the shared path yet.
        short myAbs = AbsolutePos(pos, pieceColor);
        gap = 0;

        for (int i = 0; i < allPositions.Length; i++)
        {
            color otherColor = (color)(i / 4);
            if (OwnedColors.Contains(otherColor)) continue;

            short oppPos = allPositions[i];
            if (oppPos < 0 || oppPos >= 51) continue;

            short oppAbs = AbsolutePos(oppPos, otherColor);
            gap = ((oppAbs + 52) - myAbs) % 52;
            if (gap > 0 && gap <= 12) return true;
        }

        return false;
    }

    private short AbsolutePos(short pos, color col)
    {
        return (short)((pos + 1 + (short)col * 13) % 52);
    }
}

public class PercentageWeightedRandom
{
    private readonly Random _rng = new Random();
    private readonly int _batchSize;
    private readonly int _hitsPerBatch;
    private List<bool> _bag = new();
    private int _index;

    public PercentageWeightedRandom(int hitsPerBatch, int batchSize)
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

    public bool CheckTrue()
    {
        if (_index >= _bag.Count)
            RefillBag();

        return _bag[_index++];
    }
}