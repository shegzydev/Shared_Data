using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

public enum BallType : byte
{
    Solid = 0, Stripes = 1, Cue, Ball8, NIL
}
public class SnookManager : MonoBehaviour
{
    public GameObject potSpawn;

    BallType[] turnType = new BallType[2] { BallType.NIL, BallType.NIL };
    int[] pottedBallCount = new int[2];
    int turn;

    [SerializeField] Ball[] balls;
    public UnityAction CueReset;
    public Action<int> OnTurnChange;
    public Action<int> OnGameEnd;
    public Action<(byte player, byte type)> OnAssign;
    public Action<bool, float> OnSlam;
    public Action OnRerack;

    Vector3[] startPositions;

#if SERVER
    static Vector3[] _balls;
    public Vector3[] GetBalls
    {
        get
        {
            if (_balls == null)
            {
                _balls = new Vector3[15];
            }
            for (int i = 0; i < 15; i++)
            {
                _balls[i] = balls[i].transform.position;
            }
            return _balls;
        }
    }

    public void GetStartPositions()
    {
        startPositions = new Vector3[16];
        for (int i = 0; i < 16; i++)
        {
            startPositions[i] = balls[i].transform.position;
        }
    }

    void Start()
    {
        GetStartPositions();
        ballInHand = true;
        breaking = true;

        foreach (var item in balls)
        {
            item.OnSlam += (flag, speed) =>
            {
                OnSlam?.Invoke(flag, speed);
            };
        }
    }

    public void Push(int index, Vector2 dir)
    {
        balls[index].Push(dir);
    }

#endif

    void Update()
    {
        // TickTimer();
    }

    void Next()
    {
        turn = 1 - turn;
        OnTurnChange?.Invoke(turn);
    }

    public void SetTurn(int _turn)
    {
        turn = _turn;
        OnTurnChange?.Invoke(turn);
    }

    public int GetTurn() => turn;

    public bool GetStopped()
    {
        foreach (var item in balls)
        {
            if (item.transform.position.x > 100) continue;
            if (item.speed > 0.01f) return false;
        }
        return true;
    }

    public bool allBallsStopped = true;
    public async void ScatterBalls()
    {
        if (!allBallsStopped) return;
        ballInHand = false;
        breaking = false;

        PauseTimer();
        foul = false;
        turnPottedSelf = 0;
        turnPottedTotal = 0;
        turnCushion = 0;
        firstHit = BallType.NIL;
        allBallsStopped = false;

        while (!GetStopped()) await Task.Delay(16);

        allBallsStopped = true;
        CheckTurnEvents();
        ResetTimers();
        StartTimer();
    }

    float[] timer = { 30, 30 };
    bool timerRunning = true;
    void ResetTimers()
    {
        timer[0] = 30;
        timer[1] = 30;
    }

    void StartTimer()
    {
        timerRunning = true;
    }

    void PauseTimer()
    {
        timerRunning = false;
    }

    public void TickTimer()
    {
        if (!timerRunning) return;
        timer[turn] -= Time.deltaTime;
        if (timer[turn] <= 0)
        {
            Next();
            ResetTimers();
        }
    }

    public void Rerack()
    {
        CueReset?.Invoke();

        turnType = new BallType[2] { BallType.NIL, BallType.NIL };
        pottedBallCount = new int[2];

        for (int i = 0; i < 16; i++)
        {
            balls[i].transform.position = startPositions[i];
            balls[i].ResetBall();
        }

        breaking = true;
        OnRerack?.Invoke();
    }

    public float[] GetTimerValues => timer;

    int hasWon = -1;
    bool foul = false;
    int turnCushion = 0;
    int turnPottedSelf = 0;
    int turnPottedTotal = 0;
    BallType firstHit = BallType.NIL;
    int potted;

    bool potted8 = false;

    public void RegisterPot(Ball ball)
    {
        var type = ball.type;
        if (type == BallType.Cue || type == BallType.Ball8)
        {
            if (type == BallType.Cue)
            {
                RegisterFoul();//scratch
            }
            if (type == BallType.Ball8)
            {
                potted8 = true;
                // if (turnType[turn] != BallType.Ball8)
                // {
                //     //Debug.Log($"Player{turn} lost");
                //     //turn loses
                //     OnGameEnd?.Invoke(1 - turn);
                // }
                // else
                // {
                //     if (type == firstHit)
                //     {
                //         //Debug.Log($"Player{turn} wins");
                //         OnGameEnd?.Invoke(turn);
                //     }
                //     else
                //     {
                //         //Debug.Log($"Player{turn} lost");
                //         OnGameEnd?.Invoke(1 - turn);
                //     }
                // }
            }
            return;
        }

        if (potted == 0)
        {
            turnType[turn] = type;
            turnType[1 - turn] = (BallType)(1 - (int)type);
            //Debug.Log($"Player {turn} is {type}");
            OnAssign?.Invoke(((byte)turn, (byte)type));
        }

        pottedBallCount[(int)type]++;
        potted++;
        if (type == turnType[turn]) turnPottedSelf++;
        turnPottedTotal++;

        if (turnType[turn] != BallType.Ball8 && pottedBallCount[(int)turnType[turn]] == 7)
        {
            turnType[turn] = BallType.Ball8;
        }

        if (turnType[1 - turn] != BallType.Ball8 && pottedBallCount[(int)turnType[1 - turn]] == 7)
        {
            turnType[1 - turn] = BallType.Ball8;
        }
    }

    public void RegisterCushion()
    {
        turnCushion++;
    }

    public void RegisterFoul()
    {
        foul = true;
    }

    public void CheckFirstHit(BallType type)
    {
        //Debug.Log($"first hit is {type}");
        firstHit = type;
        if (type != turnType[turn] && turnType[turn] != BallType.NIL)
        {
            RegisterFoul();
        }
    }

    void InHand()
    {
        ballInHand = true;
        CueReset.Invoke();
        Next();
    }

    void CheckTurnEvents()
    {
        if (foul && turnType[turn] == BallType.NIL)
        {
            Rerack();
            Next();
            return;
        }

        if (potted8)
        {
            if (turnType[turn] == BallType.Ball8 || (turnType[turn] != BallType.NIL && pottedBallCount[(int)turnType[turn]] >= 7))
            {
                if (firstHit == BallType.Ball8)
                {
                    OnGameEnd.Invoke(foul ? 1 - turn : turn);
                }
                else if (firstHit == turnType[turn])
                {
                    OnGameEnd.Invoke(turn);
                }
                else
                {
                    OnGameEnd.Invoke(1 - turn);
                }
            }
            else if (turnType[turn] == BallType.NIL)
            {
                Rerack();
                Next();
            }
            else
            {
                OnGameEnd.Invoke(1 - turn);
            }
            return;
        }

        if (firstHit == BallType.NIL)
            RegisterFoul();

        if (foul)
        {
            InHand();
        }
        else
        {
            if (turnPottedSelf > 0)
            {
                OnTurnChange?.Invoke(turn);
            }
            else
            {
                if (turnCushion == 0)
                {
                    //foulf
                    InHand();
                }
                else
                {
                    Next();
                }
            }
        }
    }

    public BallType PreferredBallType => turnType[turn];
    public BallType AIBallType => turnType[1];

    public void SetTimerValues(float[] times)
    {
        timer = times;
    }

    void OnGUI()
    {
        // GUI.Label(new Rect(Screen.width / 2, 0, 100, 50), $"Player{turn}:{turnType[turn]}");
    }

    public byte[] GetBallsViewData()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter binaryWriter = new BinaryWriter(stream))
        {
            for (int i = 0; i < balls.Length; i++)
            {
                var data = balls[i].ballViewData;
                binaryWriter.Write(data.velocity.x);
                binaryWriter.Write(data.velocity.y);
                binaryWriter.Write(data.velocity.z);
                binaryWriter.Write(data.position.x);
                binaryWriter.Write(data.position.y);
                binaryWriter.Write(data.position.z);
            }
            return stream.ToArray();
        }
    }

    public void SetBallsFromView(byte[] rawData)
    {
        using (MemoryStream stream = new MemoryStream(rawData))
        using (BinaryReader binaryReader = new BinaryReader(stream))
        {
            for (int i = 0; i < balls.Length; i++)
            {
                float vx = binaryReader.ReadSingle();
                float vy = binaryReader.ReadSingle();
                float vz = binaryReader.ReadSingle();
                float px = binaryReader.ReadSingle();
                float py = binaryReader.ReadSingle();
                float pz = binaryReader.ReadSingle();

                balls[i].SetBallFromViewData(new Vector3(vx, vy, vz), new Vector3(px, py, pz));
            }
        }
    }

    public byte[] GetState()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write((byte)turnType[0]);
            writer.Write((byte)turnType[1]);
            writer.Write((byte)pottedBallCount[0]);
            writer.Write((byte)pottedBallCount[1]);
            writer.Write((byte)turn);
            writer.Write(ballInHand);
            writer.Write(breaking);
            return stream.ToArray();
        }
    }

    bool ballInHand = true;
    bool breaking = true;

    public void SetState(byte[] data)
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            turnType[0] = (BallType)reader.ReadByte();
            turnType[1] = (BallType)reader.ReadByte();
            pottedBallCount[0] = reader.ReadByte();
            pottedBallCount[1] = reader.ReadByte();
            SetTurn(reader.ReadByte());
            ballInHand = reader.ReadBoolean();
        }
    }
}
