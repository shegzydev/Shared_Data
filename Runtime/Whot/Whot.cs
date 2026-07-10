using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public enum WhotNetEvents : byte
{
    Play, Pick, Call, Turn, Timer, GameOver, Message, State, StateSync, DROP20, CallSuit, Ready
}

public class Whot
{
    public struct DrawData
    {
        public Card[][] hands;
        public Card stack;
        public int deck;
    }

    public enum Suit
    {
        Cross, Square, Star, Circle, Triangle, Whot, NIL
    }

    public class Card
    {
        public Suit Suit { get; }
        public int Number { get; }

        public Card(Suit suit, int number)
        {
            Suit = suit;
            Number = number;
        }

        public static implicit operator int(Card card) => card.Number;

        public byte[] ToBytes()
        {
            var suit = BitConverter.GetBytes((int)this.Suit);
            var num = BitConverter.GetBytes(this.Number);
            return suit.Concat(num).ToArray();
        }

        public static Card BuildCard(byte[] data)
        {
            Card card = new Card((Suit)BitConverter.ToInt32(data, 0), BitConverter.ToInt32(data, 4));
            return card;
        }

        public override string ToString() => $"{Suit:g} {Number}";
        public static int ByteSize => 8;
    }

    class CardDeck
    {
        private static readonly int[] CircleNumbers = { 1, 2, 3, 4, 5, 7, 8, 10, 11, 12, 13, 14 };
        private static readonly int[] TriangleNumbers = { 1, 2, 3, 4, 5, 7, 8, 10, 11, 12, 13, 14 };
        private static readonly int[] CrossNumbers = { 1, 2, 3, 5, 7, 10, 11, 13, 14 };
        private static readonly int[] SquareNumbers = { 1, 2, 3, 5, 7, 10, 11, 13, 14 };
        private static readonly int[] StarNumbers = { 1, 2, 3, 4, 5, 7, 8 };
        private const int WhotNumber = 20;

        public static Card[] CreateDeck()
        {
            List<Card> list = new List<Card>(54);

            foreach (var n in CircleNumbers) list.Add(new Card(Suit.Circle, n));
            foreach (var n in TriangleNumbers) list.Add(new Card(Suit.Triangle, n));
            foreach (var n in CrossNumbers) list.Add(new Card(Suit.Cross, n));
            foreach (var n in SquareNumbers) list.Add(new Card(Suit.Square, n));
            foreach (var n in StarNumbers) list.Add(new Card(Suit.Star, n));

            for (int i = 0; i < 5; i++)
                list.Add(new Card(Suit.Whot, WhotNumber));

            do { list.Shuffle(); } while (list[0] == 20);

            return list.ToArray();
        }
    }

    Card[] cards;

    int numplayers;
    int turn;

    Queue<int> deck;
    Stack<int> played;
    List<int>[] hands;

    int toPickByNext = 1;
    float timer = 22, lastTime = 0;
    bool endGame;
    bool initting;

    Suit NextSuit = Suit.NIL;
    bool waitingForSuitSelect;

    public event Action<int, int> OnPassMessage;
    public event Action<int, byte[]> OnEndGame;
    public event Action<int> OnTurnUpdate;
    public event Action<float> OnTimerUpdate;
    public event Action<(int handPosition, int player)> OnPlay;
    public event Action<(Card card, int player, int cardIndex)> OnUpdateCall;

    public event Action<(Card card, int id, int player, bool market)> OnPickCard;
    public event Action<(int player, bool market)> OnServerPickCard;

    public event Action OnSync;
    public event Action<byte[]> OnGameStateUpdate;
    public event Action<DrawData> OnDrawStateUpdate;
    public event Action<int> OnDrop20;
    public event Action<(int suit, int player)> OnCalledSuit;

    HashSet<int> leftPlayers = new();

    public Whot(int numplayers, int turn = 0)
    {
        timer = 22f;
        this.numplayers = numplayers;
        this.turn = turn;
        cards = CardDeck.CreateDeck();
    }


    public async void Init()
    {
        initting = true;

        deck = new Queue<int>();
        played = new Stack<int>();

        for (int i = 0; i < cards.Length; i++)
        {
            deck.Enqueue(i);
        }

        played.Push(deck.Dequeue());
        OnUpdateCall?.Invoke((cards[played.Peek()], 0, -1));

        var dealCards = numplayers > 2 ? 5 : 6;

        hands = new List<int>[numplayers];
        for (int i = 0; i < numplayers; i++)
        {
            hands[i] = new();
            for (int j = 0; j < dealCards; j++)
            {
                int picked = deck.Dequeue();
                hands[i].Add(picked);
                // OnPickCard?.Invoke((cards[picked], picked, i));
            }
        }

        await Task.Delay(7500);

        OnTurnUpdate?.Invoke(turn);
    }

    public void Init(byte[] cardData)
    {
        cards = LoadCards(cardData);

        deck = new Queue<int>();
        played = new Stack<int>();

        for (int i = 0; i < cards.Length; i++)
        {
            deck.Enqueue(i);
        }

        played.Push(deck.Dequeue());
        OnUpdateCall?.Invoke((cards[played.Peek()], 0, -1));

        var dealCards = numplayers > 2 ? 5 : 6;

        hands = new List<int>[numplayers];
        for (int i = 0; i < numplayers; i++)
        {
            hands[i] = new();
            for (int j = 0; j < dealCards; j++)
            {
                int picked = deck.Dequeue();
                hands[i].Add(picked);
                OnPickCard?.Invoke((cards[picked], picked, i, false));
            }
        }

        OnTurnUpdate?.Invoke(turn);
    }

    public bool Play(int player, int handIndex)
    {
        if (player != turn)
        {
            return false;
        }

        int card = hands[player][handIndex];
        int last = played.Peek();
        bool is20 = cards[card] == 20;

        if (!is20)
        {
            if (toPickByNext > 1)
            {
                if (cards[last].Number == 2 && cards[card].Number != 2) return false;
                if (cards[last].Number == 5 && cards[card].Number != 5) return false;
            }

            if (NextSuit == Suit.NIL)
            {
                bool cardsUnmatch = cards[last].Suit != cards[card].Suit
                    && cards[last].Number != cards[card].Number;

                if (cardsUnmatch)
                {
                    return false;
                }
            }
            else
            {
                if (cards[card].Suit != NextSuit)
                {
                    return false;
                }
            }
        }
        else
        {
            toPickByNext = 1;
        }

        played.Push(card);
        OnUpdateCall?.Invoke((cards[card], player, handIndex));

        hands[player].RemoveAt(handIndex);
        OnPlay?.Invoke((handIndex, player));

        if (HandEmpty())
        {
            endGame = true;
            byte[] count = CountCards(out int winner);
            OnEndGame?.Invoke(player, count);
            return true;
        }

        if (hands[player].Count == 2)
        {
            OnPassMessage?.Invoke(3, turn);
        }
        if (hands[player].Count == 1)
        {
            OnPassMessage?.Invoke(4, turn);
        }

        if (cards[card] == 1)
        {
            //if (HasFollowUpForHold(cards[card].Suit))
            HoldOn();
            OnPassMessage?.Invoke(5, turn);
            //else
            //Pick(turn);
            // NextTurn();
        }
        else if (cards[card] == 2)
        {
            toPickByNext = 2;
            OnPassMessage?.Invoke(0, turn);
            NextTurn();
        }
        // else if (cards[card] == 5)
        // {
        //     toPickByNext = 3;
        //     OnPassMessage?.Invoke(1, turn);
        //     NextTurn();
        // }
        else if (cards[card] == 8)
        {
            // if (!NextPlayerHas8())
            // {
            Skip();
            OnPassMessage?.Invoke(6, turn);
            // }
            // else
            //     NextTurn();
        }
        else if (cards[card] == 14)
        {
            OnPassMessage?.Invoke(2, turn);
            PickGeneral(player);
        }
        else if (is20)
        {
            CoroutineRunnner.StartCoroutine(WaitForNextSuit());
            OnDrop20?.Invoke(player);
        }
        else
        {
            NextTurn();
        }

        NextSuit = Suit.NIL;
        OnGameStateUpdate?.Invoke(GetState());
        return true;
    }

    IEnumerator WaitForNextSuit()
    {
        timer = 15;
        NextSuit = Suit.NIL;
        waitingForSuitSelect = true;

        while (NextSuit == Suit.NIL)
        {
            yield return null;
        }

        waitingForSuitSelect = false;
    }

    public void ChooseNextSuit(Suit suit, int player)
    {
        NextSuit = suit;
        NextTurn();
        OnCalledSuit?.Invoke(((int)NextSuit, player));
    }

    bool HasFollowUpForHold(Suit lastSuit)
    {
        var hand = hands[turn];

        for (int i = 0; i < hand.Count; i++)
        {
            if (cards[hand[i]] == 1 || cards[hand[i]].Suit == lastSuit)
            {
                return true;
            }
        }
        return false;
    }

    bool NextPlayerHas8()
    {
        var nextHand = hands[(turn + 1) % numplayers];

        for (int i = 0; i < nextHand.Count; i++)
        {
            if (cards[nextHand[i]] == 8)
            {
                return true;
            }
        }

        return false;
    }

    public void Pick(int player)
    {
        if (player != turn)
        {
            return;
        }

        for (int i = 0; i < toPickByNext; i++)
        {
            if (deck.TryDequeue(out int picked))
            {
                hands[player].Add(picked);
                OnPickCard?.Invoke((cards[picked], picked, player, false));
            }
        }

        toPickByNext = 1;

        OnServerPickCard?.Invoke((player, false));

#if !CLIENT
        if (!DeckEmpty())
        {
            NextTurn();
            OnGameStateUpdate?.Invoke(GetState());
        }
        else
        {
            OnGameStateUpdate?.Invoke(GetState());
            Tender();
        }
#endif
    }

    public void PickGeneral(int player)
    {
        if (player != turn) return;

        for (int i = 0; i < numplayers; i++)
        {
            if (i == turn) continue;
            if (deck.TryDequeue(out int picked))
            {
                hands[i].Add(picked);
                OnServerPickCard?.Invoke((i, true));
                OnPickCard?.Invoke((cards[picked], picked, i, true));
            }
        }

#if !CLIENT

        if (!DeckEmpty())
        {
            HoldOn();
            OnGameStateUpdate?.Invoke(GetState());
        }
        else
        {
            OnGameStateUpdate?.Invoke(GetState());
            Tender();
        }
#endif

    }

    bool DeckEmpty()
    {
        if (deck.Count > 0) return false;
        return true;
    }

    void Tender()
    {
        byte[] count = CountCards(out int winner);
        endGame = true;
        OnEndGame?.Invoke(winner, count);
    }

    byte[] CountCards(out int winner)
    {
        int min = 100000;
        winner = -1;
        byte[] data = new byte[numplayers];

        for (int i = 0; i < numplayers; i++)
        {
            int sum = 0;

            foreach (var entry in hands[i])
            {
                sum += cards[entry].Number;
            }

            if (sum <= min)
            {
                min = sum;
                winner = i;
            }

            data[i] = (byte)sum;
        }

        return data;
    }

    public void HoldOn()
    {
        turn += numplayers;
        turn %= numplayers;
        ValidateTurnChange();
    }

    public void Skip()
    {
        turn += 2;
        turn %= numplayers;
        ValidateTurnChange();
    }

    public void NextTurn()
    {
        //Check if for gameOver First
        if (HandEmpty())
        {
            endGame = true;
            byte[] count = CountCards(out int winner);
            OnEndGame?.Invoke(turn, count);
            return;
        }

        turn++;
        turn %= numplayers;

        ValidateTurnChange();
    }

    bool HandEmpty()
    {
        return hands[turn].Count == 0;
    }
    void ValidateTurnChange()
    {
        timer = 15;
        OnTurnUpdate?.Invoke(turn);
    }

    public void Ready()
    {
        initting = false;
    }

    public void UpdateTimer(float delta)
    {
        if (endGame) return;

        lastTime = timer;
        timer -= delta;
        OnTimerUpdate?.Invoke(timer / 15);

        if (timer <= 0)
        {
            if (waitingForSuitSelect)
            {
                ChooseNextSuit((Suit)rng.Next(5), turn);
            }
            else
            {
                Pick(turn);
            }
        }
    }

    Random rng = new Random();

    // How many cards to peek at from the deck before picking the best one (used when scanEntireDeck is false)
    const int SwapPeekCount = 3;

    int EvaluateCardValue(Card card, Dictionary<Suit, int> suitCounts, Suit lastSuit, int lastNumber,
    bool considerOtherHands = false, int excludePlayer = -1)
    {
        int score = 0;

        // 1. Immediate playability (biggest weight — can I use this NOW?)
        if (card.Number == lastNumber) score += 150;
        if (card.Suit == lastSuit) score += 100;
        if (card.Number == 20) score += 250; // Whot is always playable

        // 2. Power card bonus (2=pick2, 5=pick3, 8=skip, 1=hold, 14=general market)
        switch (card.Number)
        {
            case 2: score += 90; break;
            case 8: score += 70; break;
            case 1: score += 60; break;
            case 14: score += 50; break; // situational, hurts you sometimes
        }

        // 3. Suit synergy — does this card match a suit I'm already stacked in?
        if (suitCounts.TryGetValue(card.Suit, out int count))
            score += count * 15;

        // 4. Flexibility — low numbers/common numbers appear across more suits,
        // so they're statistically easier to chain later
        if (card.Number <= 5) score += 10;

        // 5. Opponent awareness — penalize suits opponents are close to winning with,
        // reward cards that deny opponents a suit they're clearly stacked in
        if (considerOtherHands)
        {
            for (int p = 0; p < hands.Length; p++)
            {
                if (p == excludePlayer) continue;

                var opponentHand = hands[p];
                int opponentSuitCount = 0;

                for (int i = 0; i < opponentHand.Count; i++)
                {
                    if (cards[opponentHand[i]].Suit == card.Suit)
                        opponentSuitCount++;
                }

                // If an opponent is low on cards AND heavily stacked in this suit,
                // holding/denying this card matters more
                if (opponentHand.Count <= 3 && opponentSuitCount >= 2)
                    score += 40;

                // Opponent close to winning overall — slightly favor any denial card
                if (opponentHand.Count == 1)
                    score += 25;
            }
        }

        return score;
    }

    public bool SwapCard(int player, bool scanEntireDeck = false, bool considerOtherHands = false)
    {
        if (player != turn) return false;
        if (deck.Count == 0) return false;

        var hand = hands[player];
        var lastCard = cards[played.Peek()];

        if (mustSuit != Suit.NIL)
        {
            lastCard = new Card(mustSuit, 0);
        }

        var suitCounts = new Dictionary<Whot.Suit, int>();
        for (int i = 0; i < hand.Count; i++)
        {
            var s = cards[hand[i]].Suit;
            suitCounts[s] = suitCounts.TryGetValue(s, out int c) ? c + 1 : 1;
        }

        int worstLocalIndex = 0;
        int worstScore = int.MaxValue;
        for (int i = 0; i < hand.Count; i++)
        {
            int score = EvaluateCardValue(cards[hand[i]], suitCounts, lastCard.Suit, lastCard.Number, considerOtherHands, player);
            if (score < worstScore)
            {
                worstScore = score;
                worstLocalIndex = i;
            }
        }
        int worstCardIndex = hand[worstLocalIndex];

        // Snapshot deck contents BEFORE any dequeuing happens
        // var deckBeforeSnapshot = deck.ToArray();

        int peekCount = scanEntireDeck ? deck.Count : Math.Min(SwapPeekCount, deck.Count);
        var peeked = new List<int>(peekCount);
        for (int i = 0; i < peekCount; i++)
            peeked.Add(deck.Dequeue());

        int bestPeekIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < peeked.Count; i++)
        {
            int score = EvaluateCardValue(cards[peeked[i]], suitCounts, lastCard.Suit, lastCard.Number, considerOtherHands, player);
            if (score > bestScore)
            {
                bestScore = score;
                bestPeekIndex = i;
            }
        }
        int bestCardIndex = peeked[bestPeekIndex];

        if (bestCardIndex == worstCardIndex)
        {
            for (int i = 0; i < peeked.Count; i++)
                deck.Enqueue(peeked[i]);
            return false;
        }

        // var handBeforeSnapshot = new List<int>(hand);

        hand[worstLocalIndex] = bestCardIndex;

        peeked[bestPeekIndex] = worstCardIndex;
        for (int i = 0; i < peeked.Count; i++)
            deck.Enqueue(peeked[i]);

        // LogSwap(player, worstCardIndex, bestCardIndex, handBeforeSnapshot, deckBeforeSnapshot);

        OnGameStateUpdate?.Invoke(GetState());

        return true;
    }

    public int DeckSize => deck != null ? deck.Count : 52;
    public int[] handsSize
    {
        get
        {
            int[] sizes = new int[numplayers];
            for (int i = 0; i < numplayers; i++)
            {
                sizes[i] = hands[i].Count;
            }
            return sizes;
        }
    }

    public byte[] GetState()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            //turn
            writer.Write(turn);

            //cards
            writer.Write(cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                writer.Write(cards[i].ToBytes());
            }

            //deck
            var deckArray = deck.ToArray();
            writer.Write(deckArray.Length);
            for (int i = 0; i < deckArray.Length; i++)
            {
                writer.Write(deckArray[i]);
            }

            //played
            var playedArray = played.ToArray();
            writer.Write(playedArray.Length);
            for (int i = playedArray.Length - 1; i >= 0; i--)
            {
                writer.Write(playedArray[i]);
            }

            //hands
            writer.Write(hands.Length);
            for (int i = 0; i < hands.Length; i++)
            {
                writer.Write(hands[i].Count);
                for (int j = 0; j < hands[i].Count; j++)
                {
                    writer.Write(hands[i][j]);
                }
            }

            //end game
            writer.Write(endGame);

            //Next Suit
            writer.Write((int)NextSuit);

            //nextPicks
            writer.Write(toPickByNext);

            return stream.ToArray();
        }
    }

    public void LoadState(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            // turn
            turn = reader.ReadInt32();

            // cards
            int cardCount = reader.ReadInt32();
            cards = new Card[cardCount];
            for (int i = 0; i < cardCount; i++)
            {
                // If ToBytes() produced a fixed size, e.g. 4 or 8 bytes, you must know that size here.
                // For example: byte[] cardBytes = reader.ReadBytes(Card.ByteSize);
                // But since you have Card.BuildCard(byte[]), I’ll assume it knows how to handle a fixed size chunk.
                // Adjust 'Card.ByteSize' to whatever your Card class expects.
                byte[] cardBytes = reader.ReadBytes(Card.ByteSize);
                cards[i] = Card.BuildCard(cardBytes);
            }

            // deck
            int deckCount = reader.ReadInt32();
            deck = new Queue<int>();
            for (int i = 0; i < deckCount; i++)
            {
                deck.Enqueue(reader.ReadInt32());
            }

            // played
            int playedCount = reader.ReadInt32();
            played = new Stack<int>();
            for (int i = 0; i < playedCount; i++)
            {
                // read in the same order they were written (reverse preserved)
                played.Push(reader.ReadInt32());
            }

            // hands
            int handCount = reader.ReadInt32();
            hands = new List<int>[handCount];
            for (int i = 0; i < handCount; i++)
            {
                int handSize = reader.ReadInt32();
                hands[i] = new List<int>(handSize);
                for (int j = 0; j < handSize; j++)
                {
                    hands[i].Add(reader.ReadInt32());
                }
            }

            // endGame
            endGame = reader.ReadBoolean();

            //Next Suit
            NextSuit = (Suit)reader.ReadInt32();

            //nextPicks
            toPickByNext = reader.ReadInt32();
        }

        OnDrawStateUpdate?.Invoke(GetDrawData());
    }

    public DrawData GetDrawData()
    {
        var data = new DrawData();
        data.stack = cards[played.Peek()];
        data.deck = deck.Count();
        data.hands = new Card[hands.Length][];
        for (int i = 0; i < hands.Length; i++)
        {
            data.hands[i] = new Card[hands[i].Count];
            for (int j = 0; j < hands[i].Count; j++)
            {
                data.hands[i][j] = cards[hands[i][j]];
            }
        }
        return data;
    }

    public byte[] GetCards()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                writer.Write(cards[i].ToBytes());
            }
            writer.Write(turn);
            return stream.ToArray();
        }
    }

    public Card[] LoadCards(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            int cardCount = reader.ReadInt32();
            var tmpCards = new Card[cardCount];
            for (int i = 0; i < cardCount; i++)
            {
                byte[] cardBytes = reader.ReadBytes(Card.ByteSize);
                tmpCards[i] = Card.BuildCard(cardBytes);
            }
            turn = reader.ReadInt32();

            return tmpCards;
        }
    }

    public int getTurn => turn;

    public int lastCard => played.Peek();
    public Suit mustSuit => NextSuit;
    public List<int> currentHand => hands[turn];
    public List<int> getHand(int player) => hands[player];
    public Card[] gameCards => cards;

    public void RemovePlayer(int player)
    {
        leftPlayers.Add(player);
    }
    public void RestorePlayer(int player)
    {
        leftPlayers.Remove(player);
    }

    public void SetTurn(int turn)
    {
        this.turn = turn;
    }

    public int removedPlayers => leftPlayers.Count;

    public struct SwapLogEntry
    {
        public DateTime timestamp;
        public int player;
        public Card cardOut;
        public Card cardIn;
        public Card[] handBefore;
        public Card[] handAfter;
        public Card[] deckBefore;
        public Card[] deckAfter;
    }

    readonly List<SwapLogEntry> swapLog = new();
    public IReadOnlyList<SwapLogEntry> SwapLog => swapLog;

    void LogSwap(int player, int cardOutIndex, int cardInIndex, List<int> handBeforeSnapshot, int[] deckBeforeSnapshot)
    {
        var entry = new SwapLogEntry
        {
            timestamp = DateTime.UtcNow,
            player = player,
            cardOut = cards[cardOutIndex],
            cardIn = cards[cardInIndex],
            handBefore = handBeforeSnapshot.Select(i => cards[i]).ToArray(),
            handAfter = hands[player].Select(i => cards[i]).ToArray(),
            deckBefore = deckBeforeSnapshot.Select(i => cards[i]).ToArray(),
            deckAfter = deck.Select(i => cards[i]).ToArray()
        };

        swapLog.Add(entry);

        Console.WriteLine(
            $"[SWAP] {entry.timestamp:O} player={player} " +
            $"out=({entry.cardOut.Suit},{entry.cardOut.Number}) " +
            $"in=({entry.cardIn.Suit},{entry.cardIn.Number}) " +
            $"hand_before=[{string.Join(",", entry.handBefore.Select(c => $"{c.Suit}:{c.Number}"))}] " +
            $"hand_after=[{string.Join(",", entry.handAfter.Select(c => $"{c.Suit}:{c.Number}"))}] " +
            $"deck_before=[{string.Join(",", entry.deckBefore.Select(c => $"{c.Suit}:{c.Number}"))}] " +
            $"deck_after=[{string.Join(",", entry.deckAfter.Select(c => $"{c.Suit}:{c.Number}"))}]");
    }
}

internal static class ListExtensions
{
    private static readonly System.Random rng = new System.Random();
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T tmp = list[k];
            list[k] = list[n];
            list[n] = tmp;
        }
    }
}
