using System;
using System.Collections.Generic;
using System.Linq;

// Assumes `color` enum (4 values, e.g. Red=0,Green=1,Yellow=2,Blue=3) and
// `LudoObject` (playerCount, positions[]) already exist elsewhere in the project,
// matching the shapes used in the original file.

public class LudoMoveChooser
{
    // ---- Tunable weights (start here when adjusting bot personality) ----
    private static class W
    {
        public const double Progress = 1.0;            // per-square progress value
        public const double HomeStretchBonus = 40.0;    // entering the 51-55 stretch
        public const double FinishedBonus = 1000.0;     // token reaches 56 (home)
        public const double CaptureBase = 900.0;        // flat value of any capture
        public const double CaptureVictimProgress = 6.0;// extra, scaled by victim's lost progress
        public const double ExposureWeight = 700.0;     // * P(captured next opp. turn)
        public const double ThreatWeight = 250.0;       // * P(I can capture next my turn)
        public const double ExitBaseBonus = 60.0;       // getting a token onto the board
        public const double FirstPieceOutBonus = 40.0;  // extra if this is first piece out
    }

    public int LookaheadDepth { get; set; } = 2;  // number of the ROOT player's own future turns to search
    public int BeamWidth { get; set; } = 4;       // candidate moves kept per root decision node

    // Precomputed 2d6 outcomes: (die1, die2, weight out of 36)
    private static readonly (short d1, short d2, int weight)[] DiceCombos = BuildDiceCombos();

    private static (short, short, int)[] BuildDiceCombos()
    {
        var list = new List<(short, short, int)>();
        for (short a = 1; a <= 6; a++)
            for (short b = 1; b <= 6; b++)
                list.Add((a, b, 1)); // keep all 36 explicit; simplest to reason about/debug
        return list.ToArray();
    }

    // P(at least one legal single-die or combo roll equals `gap`), for gap in 1..12,
    // given a fresh 2d6 roll. Used as a fast proxy for "can an opponent/I reach this
    // square next turn". Gaps 1-6 are reachable via either raw die; 7-12 only via the sum.
    private static readonly double[] HitProb = BuildHitProbTable();

    private static double[] BuildHitProbTable()
    {
        var table = new double[13]; // index 0 unused
        int[] sumCounts = new int[13];
        foreach (var (d1, d2, _) in DiceCombos)
        {
            sumCounts[d1]++;
            sumCounts[d2]++; // may double count d1==d2, fine for an approximate table
            int sum = d1 + d2;
            sumCounts[sum]++;
        }
        // Recompute cleanly instead of the double-count above:
        var hitCount = new int[13];
        foreach (var (d1, d2, _) in DiceCombos)
        {
            var reachable = new HashSet<int> { d1, d2, d1 + d2 };
            foreach (var g in reachable)
                if (g >= 1 && g <= 12) hitCount[g]++;
        }
        for (int g = 1; g <= 12; g++) table[g] = hitCount[g] / 36.0;
        return table;
    }

    private static double HitProbability(int gap)
    {
        if (gap < 1 || gap > 12) return 0.0;
        return HitProb[gap];
    }

    // Add this method to the LudoMoveChooser class

    public bool TryGetAdvantageousRoll(LudoObject game, int turnIndex, out short a, out short b)
    {
        int numPlayers = game.playerCount;
        var positions = game.positions;

        var rootOwned = GetOwnedColors(turnIndex, numPlayers);

        // Check if any piece is on the board
        bool anyOnBoard = rootOwned.Any(c =>
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
        double bestScore = double.NegativeInfinity;
        (short diceIndex, short pieceIndex)? bestMove = null;

        // Evaluate all 36 dice combinations
        for (short da = 1; da <= 6; da++)
        {
            for (short db = 1; db <= 6; db++)
            {
                short[] candidateDice = { da, db, (short)(da + db) };

                // Find the best move for this dice combination using the full lookahead
                var candidates = EnumerateCandidateMoves(positions, candidateDice, rootOwned);
                if (candidates.Count == 0)
                {
                    // No legal moves with this dice combination
                    continue;
                }

                // Evaluate each candidate with lookahead
                (short d, short p, double score)? bestForThisRoll = null;
                foreach (var c in candidates)
                {
                    var newPos = SimulateMove(positions, c.baseIndex, c.pieceIndex, candidateDice[c.diceIndex], rootOwned, out _);
                    double futureValue = Expectimax(newPos, (turnIndex + 1) % numPlayers, numPlayers,
                                                     LookaheadDepth - 1, turnIndex, rootOwned);

                    double immediate = ScoreMove(positions, c.baseIndex, c.pieceIndex, candidateDice[c.diceIndex], rootOwned);
                    double total = immediate + futureValue;

                    if (bestForThisRoll == null || total > bestForThisRoll.Value.score)
                        bestForThisRoll = ((short)c.diceIndex, (short)(c.baseIndex + c.pieceIndex), total);
                }

                if (bestForThisRoll.HasValue && bestForThisRoll.Value.score > bestScore)
                {
                    bestScore = bestForThisRoll.Value.score;
                    bestA = da;
                    bestB = db;
                    bestMove = (bestForThisRoll.Value.d, bestForThisRoll.Value.p);
                }
            }
        }

        // If no legal moves exist for any dice combination
        if (bestMove == null)
        {
            a = 0;
            b = 0;
            return false;
        }

        // Check if the best roll meets or exceeds a threshold for forced rolls
        // Using a threshold concept similar to the spoofer's RigThreshold
        // (Note: The spoofer's scoring is integer-based; here we use the existing evaluation system)

        // Use a dynamic threshold based on the board state and lookahead depth
        double threshold = 50.0; // Base threshold - can be tuned

        // If no pieces on board, nudge a 6 to get started
        if (!anyOnBoard)
        {
            // Try to get a 6 as one of the dice
            if (bestA != 6 && bestB != 6)
            {
                // Check if any 6-containing combination gives a decent score
                for (short da = 1; da <= 6; da++)
                {
                    for (short db = 1; db <= 6; db++)
                    {
                        if (da == 6 || db == 6)
                        {
                            short[] candidateDice = { da, db, (short)(da + db) };
                            var candidates = EnumerateCandidateMoves(positions, candidateDice, rootOwned);
                            if (candidates.Count > 0)
                            {
                                // Found a 6 that can move something
                                a = da;
                                b = db;
                                return true;
                            }
                        }
                    }
                }
            }
        }

        // If the best score is above threshold, return the advantageous roll
        if (bestScore >= threshold)
        {
            a = bestA;
            b = bestB;
            return true;
        }

        // Otherwise, don't interfere - let normal dice roll happen
        a = 0;
        b = 0;
        return false;
    }

    public (short diceIndex, short pieceIndex)? ChooseBestMove(LudoObject game, int turnIndex, short[] localDice)
    {
        int numPlayers = game.playerCount;
        var rootOwned = GetOwnedColors(turnIndex, numPlayers);

        var candidates = EnumerateCandidateMoves(game.positions, localDice, rootOwned);
        if (candidates.Count == 0) return null;

        // Rank root's own candidates with full recursive lookahead.
        (short d, short p, double score)? best = null;
        foreach (var c in candidates)
        {
            var newPos = SimulateMove(game.positions, c.baseIndex, c.pieceIndex, localDice[c.diceIndex], rootOwned, out _);
            double futureValue = Expectimax(newPos, (turnIndex + 1) % numPlayers, numPlayers,
                                             LookaheadDepth - 1, turnIndex, rootOwned);

            // Immediate move quality (captures, exposure fixed this instant, etc.) plus discounted future.
            double immediate = ScoreMove(game.positions, c.baseIndex, c.pieceIndex, localDice[c.diceIndex], rootOwned);
            double total = immediate + futureValue;

            if (best == null || total > best.Value.score)
                best = ((short)c.diceIndex, (short)(c.baseIndex + c.pieceIndex), total);
        }

        return best == null ? null : (best.Value.d, best.Value.p);
    }

    // ---------------- Core recursive search ----------------

    // Returns board value from the ROOT player's perspective, after simulating forward.
    // Only the root player's turns get full recursive branching (bounded by BeamWidth);
    // other players are modeled as single-ply greedy maximizers of their OWN board value,
    // which keeps a 4-player search tractable while still capturing real threats/captures.
    private double Expectimax(short[] positions, int actingPlayerIndex, int numPlayers,
                              int depthRemaining, int rootPlayerIndex, List<color> rootOwned)
    {
        if (depthRemaining < 0) return EvaluateBoard(positions, rootOwned);

        double expected = 0.0;
        foreach (var (d1, d2, weight) in DiceCombos)
        {
            short[] dice = { d1, d2, (short)(d1 + d2) };
            var actingOwned = GetOwnedColors(actingPlayerIndex, numPlayers);
            var candidates = EnumerateCandidateMoves(positions, dice, actingOwned);

            double resultValue;
            if (candidates.Count == 0)
            {
                // No legal move: pass to next player, same depth budget consumed.
                resultValue = depthRemaining == 0
                    ? EvaluateBoard(positions, rootOwned)
                    : Expectimax(positions, (actingPlayerIndex + 1) % numPlayers, numPlayers,
                                 actingPlayerIndex == rootPlayerIndex ? depthRemaining - 1 : depthRemaining,
                                 rootPlayerIndex, rootOwned);
            }
            else if (actingPlayerIndex == rootPlayerIndex)
            {
                // Root's own turn within the lookahead: keep searching, beam-limited.
                var ranked = candidates
                    .Select(c => (c, immediate: ScoreMove(positions, c.baseIndex, c.pieceIndex, dice[c.diceIndex], actingOwned)))
                    .OrderByDescending(x => x.immediate)
                    .Take(BeamWidth)
                    .ToList();

                double bestVal = double.NegativeInfinity;
                foreach (var (c, imm) in ranked)
                {
                    var np = SimulateMove(positions, c.baseIndex, c.pieceIndex, dice[c.diceIndex], actingOwned, out _);
                    double val = imm + Expectimax(np, (actingPlayerIndex + 1) % numPlayers, numPlayers,
                                                   depthRemaining - 1, rootPlayerIndex, rootOwned);
                    if (val > bestVal) bestVal = val;
                }
                resultValue = bestVal;
            }
            else
            {
                // Opponent turn: cheap greedy self-interest, no further branching search.
                var bestForThem = candidates
                    .Select(c => (c, selfEval: ScoreMove(positions, c.baseIndex, c.pieceIndex, dice[c.diceIndex], actingOwned)))
                    .OrderByDescending(x => x.selfEval)
                    .First();

                var np = SimulateMove(positions, bestForThem.c.baseIndex, bestForThem.c.pieceIndex,
                                       dice[bestForThem.c.diceIndex], actingOwned, out _);
                resultValue = Expectimax(np, (actingPlayerIndex + 1) % numPlayers, numPlayers,
                                          depthRemaining, rootPlayerIndex, rootOwned);
            }

            expected += (weight / 36.0) * resultValue;
        }
        return expected;
    }

    // ---------------- Move enumeration / simulation ----------------

    private struct MoveCandidate { public int baseIndex; public int pieceIndex; public int diceIndex; }

    private List<MoveCandidate> EnumerateCandidateMoves(short[] positions, short[] dice, List<color> ownedColors)
    {
        var result = new List<MoveCandidate>();
        foreach (var ownedColor in ownedColors)
        {
            int baseIndex = (int)ownedColor * 4;
            for (short d = 0; d < 3; d++)
            {
                short steps = dice[d];
                if (steps == 0) continue;
                for (int p = 0; p < 4; p++)
                {
                    short pos = positions[baseIndex + p];
                    if (!CanMove(pos, steps, isCombo: d == 2)) continue;
                    result.Add(new MoveCandidate { baseIndex = baseIndex, pieceIndex = p, diceIndex = d });
                }
            }
        }
        return result;
    }

    private bool CanMove(short pos, short steps, bool isCombo)
    {
        if (pos >= 56) return false;
        if (pos == -1) return steps == 6 && !isCombo;
        if (pos + steps > 56) return false;
        if (pos + steps == 56 && pos < 51) return false;
        return true;
    }

    // Applies a move, including the capture rule: capturer -> home (56), victim -> base (-1).
    private short[] SimulateMove(short[] positions, int baseIndex, int pieceIndex, short steps,
                                  List<color> ownedColors, out bool captured)
    {
        var np = (short[])positions.Clone();
        short pos = np[baseIndex + pieceIndex];
        short dest = pos == -1 ? (short)0 : (short)Math.Min(pos + steps, 56);

        captured = false;
        if (dest < 51)
        {
            color myColor = (color)(baseIndex / 4);
            short myAbs = AbsolutePos(dest, myColor);
            for (int i = 0; i < np.Length; i++)
            {
                color otherColor = (color)(i / 4);
                if (ownedColors.Contains(otherColor)) continue;
                short opos = np[i];
                if (opos == -1 || opos >= 51) continue;
                if (AbsolutePos(opos, otherColor) == myAbs)
                {
                    np[i] = -1;       // victim back to base
                    captured = true;
                }
            }
        }
        if (captured) dest = 56; // capturer instantly finishes

        np[baseIndex + pieceIndex] = dest;
        return np;
    }

    private short AbsolutePos(short pos, color col) => (short)((pos + 1 + (short)col * 13) % 52);

    private List<color> GetOwnedColors(int playerIndex, int numPlayers)
    {
        var owned = new List<color>();
        for (short c = 0; c < 4; c++)
            if (c % numPlayers == playerIndex % numPlayers) owned.Add((color)c);
        return owned;
    }

    // ---------------- Scoring ----------------

    // Immediate, single-move heuristic score (used both standalone and as the beam-ranking
    // signal inside the lookahead). Captures the "obvious" value of taking this move now.
    private double ScoreMove(short[] positions, int baseIndex, int pieceIndex, short steps, List<color> ownedColors)
    {
        short pos = positions[baseIndex + pieceIndex];
        var np = SimulateMove(positions, baseIndex, pieceIndex, steps, ownedColors, out bool captured);
        short dest = np[baseIndex + pieceIndex];

        double score = 0;

        if (pos >= 0 && pos < 51 && dest > pos)
            score += (dest - pos) * W.Progress;

        if (pos == -1 && dest >= 0)
        {
            score += W.ExitBaseBonus;
            int activeOwnedPieces = CountActiveOwnedPieces(positions, ownedColors);
            if (activeOwnedPieces == 0) score += W.FirstPieceOutBonus;
        }

        if (dest >= 56)
        {
            score += W.FinishedBonus;
        }
        else if (dest >= 51)
        {
            score += W.HomeStretchBonus;
        }

        if (captured)
        {
            // Victim's lost progress: find how far along the captured token(s) were.
            // (SimulateMove already reset them to -1 in np, so measure from `positions`.)
            double victimLoss = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                color otherColor = (color)(i / 4);
                if (ownedColors.Contains(otherColor)) continue;
                if (positions[i] >= 0 && positions[i] < 51 && np[i] == -1)
                    victimLoss += positions[i];
            }
            score += W.CaptureBase + victimLoss * W.CaptureVictimProgress;
        }
        else if (dest >= 0 && dest < 51)
        {
            double exposure = ExpectedCaptureProbability(dest, (color)(baseIndex / 4), positions, ownedColors);
            score -= exposure * W.ExposureWeight;

            double threat = ExpectedThreatProbability(dest, (color)(baseIndex / 4), positions, ownedColors);
            score += threat * W.ThreatWeight;
        }

        return score;
    }

    // Static (no-move) evaluation of a board state — used as the leaf value in lookahead.
    private double EvaluateBoard(short[] positions, List<color> ownedColors)
    {
        double score = 0;
        foreach (var col in ownedColors)
        {
            int baseIndex = (int)col * 4;
            for (int p = 0; p < 4; p++)
            {
                short pos = positions[baseIndex + p];
                if (pos >= 56)
                {
                    score += W.FinishedBonus;
                }
                else if (pos >= 51)
                {
                    score += W.HomeStretchBonus + pos * W.Progress;
                }
                else if (pos >= 0)
                {
                    score += pos * W.Progress;
                    double exposure = ExpectedCaptureProbability(pos, col, positions, ownedColors);
                    score -= exposure * W.ExposureWeight;
                    double threat = ExpectedThreatProbability(pos, col, positions, ownedColors);
                    score += threat * W.ThreatWeight;
                }
            }
        }
        return score;
    }

    private int CountActiveOwnedPieces(short[] positions, List<color> ownedColors)
    {
        int count = 0;
        foreach (var col in ownedColors)
        {
            int baseIndex = (int)col * 4;
            for (int p = 0; p < 4; p++)
                if (positions[baseIndex + p] >= 0 && positions[baseIndex + p] < 56) count++;
        }
        return count;
    }

    // P(some opponent token can land exactly on `pos` next turn) — approximated as
    // 1 - Π(1 - HitProb(gap_i)) across all opponent tokens within range, so multiple
    // threats stack instead of only the nearest one counting.
    private double ExpectedCaptureProbability(short pos, color myColor, short[] positions, List<color> ownedColors)
    {
        short myAbs = AbsolutePos(pos, myColor);
        double survive = 1.0;

        for (int i = 0; i < positions.Length; i++)
        {
            color otherColor = (color)(i / 4);
            if (ownedColors.Contains(otherColor)) continue;
            short oppPos = positions[i];
            if (oppPos >= 51) continue; // in stretch or home: cannot capture

            short effectivePos = oppPos == -1 ? (short)0 : oppPos;
            // A base token can only threaten if it can roll a 6 to exit onto square 0 first
            // (handled loosely here: treat a based token as "at square 0" only if it's their only option).
            if (oppPos == -1) continue; // ignore base tokens for threat purposes; conservative & simpler

            short oppAbs = AbsolutePos(effectivePos, otherColor);
            int gap = (myAbs - oppAbs + 52) % 52;
            if (gap >= 1 && gap <= 12)
                survive *= (1 - HitProbability(gap));
        }
        return 1 - survive;
    }

    // P(this token can capture some opponent on my NEXT turn) — same idea, mirrored.
    private double ExpectedThreatProbability(short pos, color myColor, short[] positions, List<color> ownedColors)
    {
        short myAbs = AbsolutePos(pos, myColor);
        double missAll = 1.0;

        for (int i = 0; i < positions.Length; i++)
        {
            color otherColor = (color)(i / 4);
            if (ownedColors.Contains(otherColor)) continue;
            short oppPos = positions[i];
            if (oppPos == -1 || oppPos >= 51) continue;

            short oppAbs = AbsolutePos(oppPos, otherColor);
            int gap = (oppAbs - myAbs + 52) % 52;
            if (gap >= 1 && gap <= 12)
                missAll *= (1 - HitProbability(gap));
        }
        return 1 - missAll;
    }
}