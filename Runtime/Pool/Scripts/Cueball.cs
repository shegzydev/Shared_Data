using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class Cueball : Ball
{
    float angle = 0;
    [SerializeField] float sensitivity = 0.15f, force = 10f;
    Vector3 pos;

    LineRenderer hitLine;
    LineRenderer reflectionLine;
    LineRenderer normalLine;
    LineRenderer ballLine;

    [Range(-1f, 1f)] public float english;
    [Range(-1f, 1f)] public float spin;//follow/drag
    public float spinIntensity = 20;

    public Transform cueStick;
    public Transform hand;
    public Transform grabbedBall;
#if CLIENT
    public GameObject powerBar;
#endif
    // [SerializeField] SpinKnob spinKnob;
    [SerializeField] Transform Lines;

    Vector3 hitpoint;
    Vector3 hitDir;

    Vector3 start;
    bool canPlay;

    public Action OnShoot;
    public Action<Vector3> OnPlaceCue;

    float XInput;
    public void SetInput(float X)
    {
        if (grabbingBall) return;
        XInput = X;
    }
    public void SetAngle(float _angle)
    {
        angle = _angle;
    }
    public float aimAngle => angle;

    public Vector3 aimDir => hitDir;

    public bool getPlay() => canPlay;
    public void SetPlay(bool play)
    {
        canPlay = play;
    }

    protected override void Init()
    {
        angle = Mathf.PI / 2;
        start = transform.position;

        Material mat = new Material(Shader.Find("Sprites/Default"));

        hitLine = Lines.GetChild(0).gameObject.AddComponent<LineRenderer>();
        reflectionLine = Lines.GetChild(1).gameObject.AddComponent<LineRenderer>();
        normalLine = Lines.GetChild(2).gameObject.AddComponent<LineRenderer>();
        ballLine = Lines.GetChild(3).gameObject.AddComponent<LineRenderer>();

        hitLine.material = reflectionLine.material = normalLine.material = ballLine.material = mat;

        normalLine.endWidth = normalLine.startWidth / 2;
        reflectionLine.endWidth = reflectionLine.startWidth / 2;
        ballLine.startWidth = ballLine.endWidth = ballLine.endWidth / 3f;

        normalLine.numCapVertices = 10;
        reflectionLine.numCapVertices = 10;
        hitLine.numCapVertices = 10;

        ballLine.loop = true;
        ballLine.numCornerVertices = 3;

        snookManager.CueReset = ResetCueball;
        canPlay = true;
    }

    protected override void Tick()
    {
        if (canPlay) angle += XInput * sensitivity;
        hitDir = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0);

        var perp = new Vector3(-hitDir.y, hitDir.x);
        hitpoint = transform.position + perp * english * radius;

        ComputeInput(hitDir);

        TraceHitBall(hitDir);

        GrabCueBall();

        if (hand)
        {
            hand.gameObject.SetActive(ballInHand);
            hand.position = grabbingBall ? grabbingPosition : transform.position;
        }

        if (grabbedBall)
        {
            grabbedBall.position = grabbingPosition;
        }
    }

    protected override void FixedTick()
    {
        if (!canPlay)
        {
            spin -= spin * 0.90f * Time.fixedDeltaTime;
            if (Mathf.Abs(spin) < 0.01) spin = 0;

            english -= english * 0.90f * Time.fixedDeltaTime;
            if (Mathf.Abs(english) < 0.01) english = 0;

            rb.linearVelocity += (Vector2)hitDir * spinIntensity * spin * Time.fixedDeltaTime;
        }
    }

    public static List<(Vector3 pos, Vector3 dir)> points = new();
    public void Aim(Vector3 _dir)
    {
        angle = -Vector2.SignedAngle(Vector3.up, _dir) * Mathf.Deg2Rad;
        hitDir = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0);

        points.Add((transform.position, hitDir));

        cueStick.rotation = Quaternion.FromToRotation(cueStick.right, hitDir) * cueStick.rotation;
    }

    void LateUpdate()
    {
        cueStick.position = transform.position;
        cueStick.rotation = Quaternion.FromToRotation(cueStick.right, hitDir) * cueStick.rotation;
        cueStick.gameObject.SetActive(canPlay && stickActive && !grabbingBall);
#if CLIENT
        Lines.gameObject.SetActive(canPlay && stickActive && !grabbingBall);
        powerBar.SetActive(canPlay && meterActive);
#endif
    }

    public bool stickActive;
    public bool meterActive;
    public bool ballInHand;
    public bool breaking;
    [SerializeField] bool grabbingBall;
    Vector2 grabbingPosition = Vector3.one * 5000;

    public void Shoot(Vector3 _dir, Vector2 _spin, float _power)
    {
        angle = -Vector2.SignedAngle(Vector3.up, _dir) * Mathf.Deg2Rad;
        hitDir = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0);

        canPlay = false;

        spin = _spin.y;
        english = _spin.x;

        ballHits = 0;

        rb.AddForceAtPosition(hitDir * force * _power, hitpoint, ForceMode2D.Impulse);

        snookManager.ScatterBalls();
        WaitTillBallStop();

        OnShoot?.Invoke();
    }

    void GrabCueBall()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var projection = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var dist = Vector2.Distance(projection, transform.position);

            if (ballInHand && dist < radius * 5)
            {
                grabbingBall = true;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (grabbingBall)
            {
                var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

                pos.x = Mathf.Clamp(pos.x, -95f + radius, (breaking ? -48f : 95f) - radius);
                pos.y = Mathf.Clamp(pos.y, -42f + radius, 42f - radius);

                grabbingPosition = pos;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (grabbingBall)
            {
                bool canDrop = true;
                var balls = FindObjectsByType<Ball>(FindObjectsSortMode.None);
                foreach (var ball in balls)
                {
                    if (ball == this) continue;
                    if (Vector2.Distance(grabbingPosition, ball.transform.position) < radius)
                    {
                        canDrop = false; break;
                    }
                }
                if (canDrop)
                {
                    OnPlaceCue?.Invoke(grabbingPosition);
                }
            }
            grabbingBall = false;
            grabbingPosition.x = grabbingPosition.y = 5000;
        }
    }

    void TraceHitBall(Vector3 dir)
    {
        if (!canPlay)
        {
            normalLine.positionCount = 0;
            reflectionLine.positionCount = 0;
            hitLine.positionCount = 0;
            ballLine.positionCount = 0;
            return;
        }

        var hit = Physics2D.CircleCast(transform.position, radius, dir);
        if (hit)
        {
            Color phantomColor = Color.white;
            pos = transform.position + dir * hit.distance;
            hitLine.positionCount = 2;
            hitLine.SetPositions(new Vector3[] { transform.position, pos });

            Ball ball;
            if (ball = hit.collider.GetComponent<Ball>())
            {
                var normal = -hit.normal;
                normalLine.positionCount = 2;
                normalLine.SetPositions(new Vector3[] { hit.transform.position, hit.transform.position + ((Vector3)normal * 30 * Vector2.Dot(dir, normal)) });

                var tangent = new Vector3(normal.y, -normal.x) * Vector3.Cross(dir, new Vector3(normal.x, normal.y, 0)).z;
                reflectionLine.positionCount = 2;
                reflectionLine.SetPositions(new Vector3[] { pos, pos + tangent * 30 });

                if (ball.type != snookManager.PreferredBallType && snookManager.PreferredBallType != BallType.NIL)
                {
                    reflectionLine.positionCount = 0;
                    normalLine.positionCount = 0;
                    phantomColor = Color.red;
                }
            }
            else
            {
                var reflection = Vector3.Reflect(dir, hit.normal);
                reflectionLine.positionCount = 2;
                reflectionLine.SetPositions(new Vector3[] { pos, pos + reflection * 30 });
                normalLine.positionCount = 0;
            }

            DrawPhantomBall(32, phantomColor);
        }
    }

    void ComputeInput(Vector3 dir)
    {
        if (!canPlay) return;
        // if (ShootInput)
        // {
        //     Shoot(dir, aimSpin);
        // }
    }

    void DrawPhantomBall(int edges, Color color)
    {
        ballLine.endColor = ballLine.startColor = color;
        ballLine.positionCount = edges;
        Vector3[] points = new Vector3[edges];
        float step = 2 * Mathf.PI / edges;
        for (int i = 0; i < edges; i++)
        {
            points[i] = pos + new Vector3(Mathf.Cos(step * i), Mathf.Sin(step * i)) * radius;
        }
        ballLine.SetPositions(points);
    }

    void ResetCueball()
    {
        transform.position = start;
        GetComponent<CircleCollider2D>().sharedMaterial = new PhysicsMaterial2D { bounciness = 1, friction = 0 };
        rb.gravityScale = 0;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0;
        potted = false;
    }

    public void ResetGravity()
    {
        rb.gravityScale = 0;
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(pos, radius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(hitpoint, radius / 10);
    }

    async void WaitTillBallStop()
    {
        while (!snookManager.allBallsStopped)
        {
            await Task.Yield();
        }
        canPlay = true;
    }

    int ballHits;

    protected override void OnBallCollision(Ball ball)
    {
        base.OnBallCollision(ball);
        if (ballHits == 0)
        {
            snookManager.CheckFirstHit(ball.type);
        }
        ballHits++;
    }

    protected override void OnCushionCollision()
    {
        if (ballHits < 1) return;
        snookManager.RegisterCushion();
    }

    public float getSpin => spin * english;

    public Vector2 aimSpin;
}