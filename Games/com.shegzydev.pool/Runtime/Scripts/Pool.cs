using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum NetEvents : byte
{
    Ready, TurnSwitch, Balls, Shoot, Timer, EndGame, Aim, State, Assign, CueSet, Rerack
}

#if SERVER
public struct StateData
{
    public Vector3 cueBallPosition;
    public Vector3[] objectBallsPositions;
    public Vector3[] holesPositions;
}
#endif


public class Pool : MonoBehaviour
{
    public bool rolling = false;
    [SerializeField] Cueball cueball;
    [SerializeField] SnookManager snookManager;
#if SERVER
    public bool AIWins;
    [SerializeField] Transform[] holePositions;
    [SerializeField] LayerMask ballMask;
#endif
    public Action<int> OnTurnSwitch;
    public Action<int> OnGameEnd;
    public Action<bool, float> OnSlam;
    public Action<(byte player, byte type)> OnAssign;
    public Action OnShoot;
    public Action OnRerack;

    public bool Paused = false;
    void Awake()
    {
        // snookManager.OnTurnChange = OnTurnSwitch;
        snookManager.OnTurnChange = (turn) =>
        {
            OnTurnSwitch?.Invoke(turn);
        };

        snookManager.OnGameEnd = (winner) =>
        {
            OnGameEnd?.Invoke(winner);
        };

        snookManager.OnAssign = data =>
        {
            OnAssign?.Invoke(data);
        };

        snookManager.OnSlam = (flag, speed) =>
        {
            OnSlam?.Invoke(flag, speed);
        };

        snookManager.OnRerack = () =>
        {
            OnRerack?.Invoke();
        };
    }

    void Start()
    {

    }

    void Update()
    {

    }

#if SERVER
    void FixedUpdate()
    {
        if (!AIWins) return;
        var lines = new List<(Vector3 a, Vector3 b)>();

        if (snookManager.AIBallType == BallType.Solid)
        {
            for (int i = 0; i < 7; i++)
            {
                var ball = snookManager.GetBalls[i];
                if (ball.x > 100) continue;
                foreach (var hole in GetHolePositions)
                {
                    lines.Add((hole, ball));
                }
                lines.Add((ball, cueball.transform.position));
            }
            for (int i = 8; i < 15; i++)
            {
                var ball = snookManager.GetBalls[i];
                if (ball.x > 100) continue;
                foreach (var line in lines)
                {
                    var pushDir = LineCircleProximity(line.a, line.b, ball, Ball.radius);
                    snookManager.Push(i, pushDir);
                }
            }
        }
        else if (snookManager.AIBallType == BallType.Stripes)
        {
            for (int i = 8; i < 15; i++)
            {
                var ball = snookManager.GetBalls[i];
                if (ball.x > 100) continue;
                foreach (var hole in GetHolePositions)
                {
                    lines.Add((hole, ball));
                }
                lines.Add((ball, cueball.transform.position));
            }
            for (int i = 0; i < 7; i++)
            {
                var ball = snookManager.GetBalls[i];
                if (ball.x > 100) continue;

                var pushDir = Vector2.zero;
                foreach (var line in lines)
                {
                    pushDir += LineCircleProximity(line.a, line.b, ball, Ball.radius);
                }
                snookManager.Push(i, pushDir);
            }
        }
    }

    Vector2 LineCircleProximity(Vector2 P1, Vector2 P2, Vector2 C, float r)
    {
        Vector2 L = P2 - P1;
        float lineLenSq = Vector2.Dot(L, L);

        if (lineLenSq < 1e-6f)
            return Vector2.zero; // avoid divide-by-zero when points coincide

        float t = Vector2.Dot(C - P1, L) / lineLenSq;
        t = Mathf.Clamp01(t); // <-- ensures projection stays on the segment

        Vector2 Pproj = P1 + t * L;
        Vector2 diff = C - Pproj;
        float dist = diff.magnitude;

        // Proximity value: 1 when on line, 0 when dist >= 2r
        float value = 1f - Mathf.Clamp01(dist / (r * 2f));

        // Push direction: away from line; if perfectly on line, use perpendicular
        Vector2 pushDir;
        if (dist > 1e-6f)
            pushDir = diff.normalized;
        else
            pushDir = new Vector2(L.y, -L.x).normalized; // arbitrary perpendicular

        // Scale by ball radius * proximity value
        return pushDir * Ball.radius * value;
    }


#endif

    public void Shoot(Vector3 dir, Vector2 spin, float power)
    {
        cueball.Shoot(dir, spin, power);
        OnShoot?.Invoke();
        rolling = true;
    }

    public void UpdateTimer(float[] times)
    {
        snookManager.SetTimerValues(times);
    }

    public float[] GetTimes()
    {
        return snookManager.GetTimerValues;
    }

    public bool BallStopped()
    {
        return snookManager.GetStopped();
    }

    public byte[] GetBallData()
    {
        return snookManager.GetBallsViewData();
    }

    public void SetBallsData(byte[] data)
    {
        snookManager.SetBallsFromView(data);
    }

    public byte[] GetStateData()
    {
        return snookManager.GetState();
    }

    public void SetStateData(byte[] data)
    {
        snookManager.SetState(data);
    }

    public void SetCue(Vector3 pos)
    {
        cueball.transform.position = pos;
    }

    public void SetTurn(int turn)
    {
        snookManager.SetTurn(turn);
    }

    public int GetTurn()
    {
        return snookManager.GetTurn();
    }

    public void SetInput(float X)
    {
        cueball.SetInput(X);
    }

    public void SetAimAngle(float angle)
    {
        cueball.SetAngle(angle);
    }

    public void Aim(Vector3 dir)
    {
        cueball.Aim(dir);
    }

    public void Tick()
    {
        if (Paused) return;
        snookManager.TickTimer();
    }

    public bool GetPlay()
    {
        return cueball.getPlay();
    }

    public void SetPlay(bool play)
    {
        cueball.SetPlay(play);
    }

    public void ResetGravity()
    {
        cueball.ResetGravity();
    }

    public float aimAngle => cueball.aimAngle;
    public Vector3 aimDir => cueball.aimDir;
    public Vector2 aimSpin => cueball.aimSpin;

#if SERVER
    public Vector3[] GetHolePositions
    {
        get
        {
            Vector3[] holes = new Vector3[holePositions.Length];
            for (int i = 0; i < holes.Length; i++)
            {
                holes[i] = holePositions[i].position;
            }
            return holes;
        }
    }
    public StateData GetPoolBallsState()
    {
        return new StateData()
        {
            cueBallPosition = cueball.transform.position,
            objectBallsPositions = snookManager.GetBalls,
            holesPositions = GetHolePositions
        };
    }

    public LayerMask GetBallsLayerMask() => ballMask;
    public BallType GetPlayerType => snookManager.PreferredBallType;
#endif
}
