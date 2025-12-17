using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
//using UnityEngine;
// using Newtonsoft.Json.Linq;
using System.Linq;

public enum WordNetEvent : byte
{
    Here, ChooseLetter, RemoveLetter, CheckWord, ClientStateUpdate,
    RequestLetters, ReceiveLeters, UpdateTimer, EndGame, Sync, Correct, Wrong
}

public enum LogType
{
    Log, Error, Warning, UI
}

public struct Letter
{
    public char character;
    public bool chosen;
    public int pos;
    public Letter(char _character)
    {
        pos = -1;
        chosen = false;
        character = _character;
    }

    public static implicit operator char(Letter letter)
    {
        return letter.character;
    }

    public static explicit operator string(Letter letter)
    {
        return letter.character.ToString();
    }
}

public class WordGame
{
    public static Action<LogType, string> LogAction;

    public static Action<long, int, bool> OnCheckWord;

    public static Action<long, int, DrawData[]> OnStateUpdate;

    public static Action<long, float> OnTimerUpdate;
    /// <summary>
    /// Room ID, Winner, FoundWords
    /// </summary>
    public static Action<long, int, byte[]> OnGameEnd;

    private Random _random = new Random();

    long roomID;
    int lastTime;
    float time = 120;
    int players = 2;
    bool endgame;

    public char[] GetLetters(int player)
    {
        var array = new char[6];
        for (int i = 0; i < letters[player].Length; i++)
        {
            array[i] = letters[player][i];
        }
        return array;
    }
    public string[] GetWords() => chosenWords.ToArray();

    char[] lettersAvailable = null;
    public static HashSet<string> Words = null;
    static HashSet<string> chosenWords = new();

    public static Dictionary<char, byte> letterScores = new Dictionary<char, byte>
    {
        { 'a', 1 }, { 'b', 3 }, { 'c', 3 }, { 'd', 2 }, { 'e', 1 },
        { 'f', 4 }, { 'g', 2 }, { 'h', 4 }, { 'i', 1 }, { 'j', 8 },
        { 'k', 5 }, { 'l', 1 }, { 'm', 3 }, { 'n', 1 }, { 'o', 1 },
        { 'p', 3 }, { 'q', 9 }, { 'r', 1 }, { 's', 1 }, { 't', 1 },
        { 'u', 1 }, { 'v', 4 }, { 'w', 4 }, { 'x', 8 }, { 'y', 4 },
        { 'z', 9 }
    };

    Dictionary<string, int>[] foundWords;
    List<Letter>[] formedWord;
    Letter[][] letters;
    StringBuilder[] word;
    int[] scores;

    public WordGame(long roomId)
    {
        roomID = roomId;
        endgame = false;
    }

    public void Init()
    {
        letters = new Letter[players][];
        word = new StringBuilder[players];
        formedWord = new List<Letter>[players];
        scores = new int[players];
        foundWords = new Dictionary<string, int>[players];

        for (int i = 0; i < players; i++)
        {
            letters[i] = new Letter[6] { new('e'), new('t'), new('r'), new('c'), new('p'), new('a') };
            word[i] = new StringBuilder();
            formedWord[i] = new List<Letter>();
            foundWords[i] = new Dictionary<string, int>();
        }

        if (Words != null)
        {
            ComputeAvailableLetters();
            for (int i = 0; i < players; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    letters[i][j] = new(lettersAvailable[j]);
                }
                letters[i] = letters[i].OrderBy(x => ((float)_random.Next() / int.MaxValue)).ToArray();
            }
        }

        LogAction?.Invoke(LogType.Log, $"a char is of size {sizeof(char)}");

        OnStateUpdate?.Invoke(roomID, 0, GetDrawingData());
        OnStateUpdate?.Invoke(roomID, 1, GetDrawingData());
    }

    public void ForceUpdate()
    {
        OnStateUpdate?.Invoke(roomID, 0, GetDrawingData());
        OnStateUpdate?.Invoke(roomID, 1, GetDrawingData());
    }

    public void Tick(float elapsed)
    {
        if (endgame || paused) return;

        lastTime = (int)time;
        time -= elapsed;

        if (lastTime != (int)time)
        {
            OnTimerUpdate?.Invoke(roomID, time);
        }

        if (time <= 0)
        {
            endgame = true;
            OnGameEnd?.Invoke(roomID, GetHighestScorer(), SerializeFoundWords());
        }
    }

    public void StopGame(int loser)
    {
        endgame = true;
        OnGameEnd?.Invoke(roomID, 1 - loser, SerializeFoundWords());
    }

    int GetHighestScorer()
    {
        int highest = 0;
        int chosen = 0;

        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] > highest)
            {
                chosen = i;
                highest = scores[i];
            }
        }

        return chosen;
    }

    public void ChooseLetter(int player, int index)
    {
        if (endgame) return;

        LogAction?.Invoke(LogType.Log, $"chosen: {index}");
        LogAction?.Invoke(LogType.Log, $"{letters[player][index]}");

        letters[player][index].chosen = true;
        letters[player][index].pos = index;

        formedWord[player].Add(letters[player][index]);
        word[player].Append(letters[player][index]);

        LogAction?.Invoke(LogType.Log, word[player].ToString());

        OnStateUpdate?.Invoke(roomID, player, GetDrawingData());
    }

    public void RemoveLetter(int player, int index)
    {
        if (endgame) return;

        var toRemove = formedWord[player][index].pos;
        letters[player][toRemove].chosen = false;

        formedWord[player].RemoveAt(index);
        word[player].Remove(index, 1);

        LogAction?.Invoke(LogType.Log, word[player].ToString());

        OnStateUpdate?.Invoke(roomID, player, GetDrawingData());
    }

    public string GetWord(int player)
    {
        return word[player].ToString();
    }

    public void CheckWord(int player, string wordToCheck)
    {
        if (endgame) return;

        //var currword = word[player].ToString();
        var currword = wordToCheck;
        LogAction?.Invoke(LogType.Log, "checking word" + currword);

        formedWord[player].Clear();
        word[player].Clear();

        for (int i = 0; i < letters[player].Length; i++)
        {
            letters[player][i].chosen = false;
        }

        if (foundWords[player].ContainsKey(currword))
        {
            // OnCheckWord?.Invoke(roomID, player, false);
            LogAction?.Invoke(LogType.Log, $"false: Found word {currword} already!");
        }
        else
        {
            try
            {
                if (Words.Contains(currword))
                {
                    LogAction?.Invoke(LogType.Log, $"true: Found word {currword}");
                    int score = ComputeScore(currword) * 100;
                    scores[player] += score;
                    foundWords[player].Add(currword, score);
                    OnCheckWord?.Invoke(roomID, player, true);
                }
                else
                {
                    LogAction?.Invoke(LogType.Log, $"false: Could not find word {currword}");
                    OnCheckWord?.Invoke(roomID, player, false);
                }
            }
            catch { }
        }

        OnStateUpdate?.Invoke(roomID, player, GetDrawingData());
    }

    int ComputeScore(string word)
    {
        int sum = 0;
        for (int i = 0; i < word.Length; i++)
        {
            sum += letterScores[word[i]];
        }
        return sum;
    }

    bool paused = false;
    public bool IsPaused
    {
        set => paused = value;
        get => paused;
    }

    public struct DrawData
    {
        public Letter[] letters;
        public string word;
        public int score;
    }

    DrawData GetDrawingData(int player)
    {
        var data = new DrawData() { letters = letters[player], word = word[player].ToString() };
        return data;
    }

    DrawData[] GetDrawingData()
    {
        DrawData[] drawData = new DrawData[players];
        for (int i = 0; i < players; i++)
        {
            drawData[i] = new DrawData() { letters = letters[i], word = word[i].ToString(), score = scores[i] };
        }
        return drawData;
    }

    public byte[] GetBytes()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        {
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream);

            binaryWriter.Write(scores.Length);
            for (int i = 0; i < scores.Length; i++)
            {
                binaryWriter.Write(scores[i]);
            }

            binaryWriter.Write(formedWord.Length);
            for (int i = 0; i < formedWord.Length; i++)
            {
                binaryWriter.Write(formedWord[i].Count);

                for (int j = 0; j < formedWord[i].Count; j++)
                {
                    binaryWriter.Write(formedWord[i][j].character);
                    binaryWriter.Write(formedWord[i][j].chosen);
                    binaryWriter.Write(formedWord[i][j].pos);
                }
            }

            binaryWriter.Write(letters.Length);
            for (int i = 0; i < letters.Length; i++)
            {
                binaryWriter.Write(letters[i].Length);

                for (int j = 0; j < letters[i].Length; j++)
                {
                    binaryWriter.Write(letters[i][j].character);
                    binaryWriter.Write(letters[i][j].chosen);
                    binaryWriter.Write(letters[i][j].pos);
                }
            }

            binaryWriter.Write(word.Length);
            for (int i = 0; i < word.Length; i++)
            {
                binaryWriter.Write(word[i].ToString());
            }

            var data = memoryStream.ToArray();
            return data;
        }
    }

    /// <summary>
    /// Called on client side
    /// </summary>
    /// <param name="rawData"></param>
#if CLIENT
    public void Load(int player, byte[] rawData)
    {
        MemoryStream memoryStream = new MemoryStream(rawData);
        BinaryReader binaryReader = new BinaryReader(memoryStream);

        int scoresLength = binaryReader.ReadInt32();
        var _scores = new int[scoresLength];

        for (int i = 0; i < scoresLength; i++)
        {
            var dat = binaryReader.ReadInt32();
            _scores[i] = dat;
        }


        int formedWordLength = binaryReader.ReadInt32();
        var _formedWord = new List<Letter>[formedWordLength];

        for (int i = 0; i < formedWordLength; i++)
        {
            int formedWordLettersLength = binaryReader.ReadInt32();

            _formedWord[i] = new();

            for (int j = 0; j < formedWordLettersLength; j++)
            {
                var character = binaryReader.ReadChar();
                var chosen = binaryReader.ReadBoolean();
                var pos = binaryReader.ReadInt32();

                _formedWord[i].Add(new() { character = character, chosen = chosen, pos = pos });
            }
        }

        int lettersLength = binaryReader.ReadInt32();
        var _letters = new Letter[lettersLength][];

        for (int i = 0; i < lettersLength; i++)
        {
            int lettersLettersLength = binaryReader.ReadInt32();
            _letters[i] = new Letter[lettersLettersLength];

            for (int j = 0; j < lettersLettersLength; j++)
            {
                var character = binaryReader.ReadChar();
                var chosen = binaryReader.ReadBoolean();
                var pos = binaryReader.ReadInt32();

                if (i == player) _letters[i][j] = new() { character = character, chosen = chosen, pos = pos };
            }
        }

        int wordLength = binaryReader.ReadInt32();
        var _word = new StringBuilder[wordLength];
        for (int i = 0; i < wordLength; i++)
        {
            var str = binaryReader.ReadString();
            _word[i] = new StringBuilder();
            if (i == player)
            {
                //_word[i].Clear();
                _word[i].Append(str);
            }
        }

        if (player != GigNet.IDInRoom)
        {
            letters[player] = _letters[player];
            formedWord[player] = _formedWord[player];
            scores[player] = _scores[player];

            word[player].Clear();
            word[player].Append(_word[player].ToString());
        }
        else
        {
            bool chosen = false;

            foreach (var character in _letters[player])
            {
                if (character.chosen)
                {
                    chosen = true;
                    break;
                }
            }

            if (!chosen)
            {
                letters[player] = _letters[player];
                formedWord[player] = _formedWord[player];
                scores[player] = _scores[player];
                word[player].Clear();
                word[player].Append(_word[player].ToString());
                //word[player] = _word[player];
            }
        }

        binaryReader.Close();
        memoryStream.Close();

        OnStateUpdate?.Invoke(roomID, player, GetDrawingData());
    }
#endif
    int count = 0;

    public byte[] GetScoreData()
    {
        using (MemoryStream memoryStream = new MemoryStream())
        using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
        {
            foreach (var item in letterScores)
            {
                binaryWriter.Write(item.Key);
                binaryWriter.Write(item.Value);
            }
            return memoryStream.ToArray();
        }
    }

    public void LoadScoreData(byte[] rawData)
    {
        using (MemoryStream memoryStream = new MemoryStream(rawData))
        using (BinaryReader reader = new BinaryReader(memoryStream))
        {
            try
            {
                while (memoryStream.Position < memoryStream.Length)
                {
                    char key = reader.ReadChar();
                    byte value = reader.ReadByte();
                    letterScores[key] = value;
                }

                OnStateUpdate?.Invoke(roomID, 0, GetDrawingData());
                OnStateUpdate?.Invoke(roomID, 1, GetDrawingData());
            }
            catch (EndOfStreamException)
            {
                LogAction?.Invoke(LogType.Log, "Una don reach stream end!");
            }
        }
    }

    public static void LoadWords(Func<string, HashSet<string>> GetWords)
    {
        if (Words == null) Words = GetWords("words.json");
    }

    void ComputeAvailableLetters()
    {
        Dictionary<char, int> availableLetters = new();

        var wordList = Words.ToArray();
        int start = new System.Random().Next(0, wordList.Length - 100);

        for (int it = start; it < start + 100; it++)
        {
            var _word = wordList[it];

            for (int i = 0; i < _word.Length; i++)
            {
                if (!availableLetters.ContainsKey(_word[i]))
                {
                    availableLetters.Add(_word[i], 0);
                }
                availableLetters[_word[i]]++;
            }

            chosenWords.Add(_word);
        }

        //foreach (var letter in availableLetters)
        //{
        //    Debug.Log("letter: " + letter);
        //}

        lettersAvailable = availableLetters.Keys.ToArray();
    }

    byte[] SerializeFoundWords()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            for (int i = 0; i < players; i++)
            {
                var thewords = foundWords[i].ToArray();
                writer.Write(scores[i]);
                writer.Write(thewords.Length);
                for (int j = 0; j < thewords.Length; j++)
                {
                    writer.Write(thewords[j].Key);
                    writer.Write(thewords[j].Value);
                }
            }
            return stream.ToArray();
        }
    }

#if SERVER
    public int[] GetScores => scores;
#endif
}
