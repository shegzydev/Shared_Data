using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum TransferProtocol
{
    TCP, UDP
}

public enum TargetGroup
{
    Server, Others, All
}

internal enum ActionType
{
    RPC, Spawn, Despawn, Dispatch, NetEvent, JoinedRoom, RoomFilled
}

internal enum ServerIP
{
    localHost, AWS, Linux, LAN
}

public class CoroutineRunnner
{
    static readonly HashSet<IEnumerator> _activeCoroutines = new();
    public static IEnumerator StartCoroutine(IEnumerator routine)
    {
        _activeCoroutines.Add(routine);
        _ = RunRoutine(routine);
        return routine;
    }

    public static void StopCoroutine(IEnumerator routine)
    {
        _activeCoroutines?.Remove(routine);
    }
    static async Task RunRoutine(IEnumerator routine)
    {
        while (_activeCoroutines.Contains(routine))
        {
            if (!routine.MoveNext()) break;

            object yield = routine.Current;

            if (yield is WaitForSeconds wait)
            {
                await Task.Delay(wait.Milliseconds);
            }
            else if (yield is WaitUntil waitUntil)
            {
                while (!waitUntil.IsDone())
                {
                    await Task.Delay(10);
                }
            }
            else if (yield is null)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    public class WaitForSeconds
    {
        public int Milliseconds { get; }

        public WaitForSeconds(float seconds)
        {
            Milliseconds = (int)(seconds * 1000);
        }

        public WaitForSeconds(TimeSpan timeSpan)
        {
            Milliseconds = timeSpan.Seconds * 1000;
        }
    }
    public class WaitUntil
    {
        private readonly Func<bool> _predicate;
        public WaitUntil(Func<bool> predicate)
        {
            _predicate = predicate;
        }

        public bool IsDone() => _predicate();
    }
}
