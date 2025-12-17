using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum WhotNetEvents : byte
{
    Play, Pick, Call, Turn, Timer, GameOver, Message, State, StateSync
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
    float timer = 15, lastTime = 0;
    bool endGame;

    public event Action<int, int> OnPassMessage;
    public event Action<int, byte[]> OnEndGame;
    public event Action<int> OnTurnUpdate;
    public event Action<float> OnTimerUpdate;
    public event Action<(int handPosition, int player)> OnPlay;
    public event Action<Card> OnUpdateCall;
    public event Action<(Card card, int id, int player, bool market)> OnPickCard;
    public event Action OnSync;
    public event Action<byte[]> OnGameStateUpdate;
    public event Action<DrawData> OnDrawStateUpdate;
    public event Action<int> OnDrop20;

    public Whot(int numplayers)
    {
        this.numplayers = numplayers;
        cards = CardDeck.CreateDeck();
    }

    public void Init()
    {
        deck = new Queue<int>();
        played = new Stack<int>();

        for (int i = 0; i < cards.Length; i++)
        {
            deck.Enqueue(i);
        }

        played.Push(deck.Dequeue());
        OnUpdateCall?.Invoke(cards[played.Peek()]);

        hands = new List<int>[numplayers];
        for (int i = 0; i < numplayers; i++)
        {
            hands[i] = new();
            for (int j = 0; j < 6; j++)
            {
                int picked = deck.Dequeue();
                hands[i].Add(picked);
                // OnPickCard?.Invoke((cards[picked], picked, i));
            }
        }

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
        OnUpdateCall?.Invoke(cards[played.Peek()]);

        hands = new List<int>[numplayers];
        for (int i = 0; i < numplayers; i++)
        {
            hands[i] = new();
            for (int j = 0; j < 6; j++)
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
        if (player != turn) return false;

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

            if (cards[last].Suit != cards[card].Suit
                && cards[last].Number != cards[card].Number)
            {
                return false;
            }
        }
        else
        {
            toPickByNext = 1;
        }

        played.Push(card);
        OnUpdateCall?.Invoke(cards[card]);

        hands[player].RemoveAt(handIndex);
        OnPlay?.Invoke((handIndex, player));

        if (cards[card] == 1)
        {
            if (HasFollowUpForHold(cards[card].Suit))
                HoldOn();
            else
                NextTurn();
        }
        else if (cards[card] == 2)
        {
            toPickByNext = 2;
            OnPassMessage?.Invoke(0, turn);
            NextTurn();
        }
        else if (cards[card] == 5)
        {
            toPickByNext = 3;
            OnPassMessage?.Invoke(1, turn);
            NextTurn();
        }
        else if (cards[card] == 8)
        {
            if (!NextPlayerHas8())
                Skip();
            else
                NextTurn();
        }
        else if (cards[card] == 14)
        {
            OnPassMessage?.Invoke(2, turn);
            PickGeneral(player);
        }
        else if (is20)
        {
            CoroutineRunnner.StartCoroutine(WaitForNextSuit());
        }
        else
        {
            NextTurn();
        }

        OnGameStateUpdate?.Invoke(GetState());
        return true;
    }


    Suit NextSuit = Suit.NIL;
    IEnumerator WaitForNextSuit()
    {
        NextSuit = Suit.NIL;
        while (NextSuit == Suit.NIL)
        {
            yield return new CoroutineRunnner.WaitForSeconds(0.016f);
        }
    }

    public void ChooseNextSuit(Suit suit)
    {
        NextSuit = suit;
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
        if (player != turn) return;

        for (int i = 0; i < toPickByNext; i++)
        {
            if (deck.TryDequeue(out int picked))
            {
                hands[player].Add(picked);
                OnPickCard?.Invoke((cards[picked], picked, player, false));
            }
        }
        toPickByNext = 1;


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
    }

    public void PickGeneral(int player)
    {
        if (player != turn) return;

        //Debug.Log("General Market");

        for (int i = 0; i < numplayers; i++)
        {
            if (i == turn) continue;
            if (deck.TryDequeue(out int picked))
            {
                hands[i].Add(picked);
                OnPickCard?.Invoke((cards[picked], picked, i, true));
            }
        }

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

    public void UpdateTimer(float delta)
    {
        if (endGame) return;

        lastTime = timer;
        timer -= delta;
        if ((int)timer != (int)lastTime)
        {
            OnTimerUpdate?.Invoke(timer / 15);
        }
        if (timer <= 0)
        {
            Pick(turn);
        }
    }

    public int DeckSize => deck.Count;
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

            return tmpCards;
        }
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
