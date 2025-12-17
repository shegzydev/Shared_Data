using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Ball : MonoBehaviour
{
    protected Rigidbody2D rb;
    [SerializeField] protected SnookManager snookManager;
    public static float radius => 0.0585f * 100 / 2;
    GameObject potSpawn;
    [SerializeField] BallType ballType;
    public bool potted { get; protected set; }

#if CLIENT
    public static event Action<bool, float> OnSlam;
#elif SERVER
    public event Action<bool, float> OnSlam;
#endif
#if CLIENT
    [SerializeField] GameObject UIView;
#endif

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        potSpawn = snookManager.potSpawn;
        Init();
    }

    public void ResetBall()
    {
        rb.gravityScale = 0;
        rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
#if CLIENT
        if (UIView) UIView.SetActive(transform.position.x < 100);
#endif
        Tick();
    }

    void FixedUpdate()
    {
        if (rb.linearVelocity.magnitude < 1f)
        {
            rb.linearVelocity = Vector2.zero;
        }
        FixedTick();
    }

    void LateUpdate()
    {

    }

    protected virtual void Init() { }
    protected virtual void Tick() { }
    protected virtual void FixedTick() { }
    protected virtual void OnCushionCollision()
    {
        snookManager.RegisterCushion();
    }
    protected virtual void OnBallCollision(Ball ball) { }

    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.transform.tag == "Pocket")
        {
            if ((collision.transform.position - transform.position).magnitude - ((CircleCollider2D)collision).radius < -(radius / 3))
            {
                Debug.Log("potted");
                transform.position = potSpawn.transform.position;
                rb.linearVelocity = transform.right * rb.linearVelocity.magnitude;
                rb.gravityScale = 10f;
                GetComponent<CircleCollider2D>().sharedMaterial = null;
                snookManager.RegisterPot(this);
                potted = true;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider is CircleCollider2D)//Ball
        {
            OnBallCollision(collision.gameObject.GetComponent<Ball>());
            OnSlam?.Invoke(true, collision.relativeVelocity.magnitude);
        }
        else//Cushion
        {
            OnCushionCollision();
            OnSlam?.Invoke(false, collision.relativeVelocity.magnitude);
        }
    }

#if SERVER
    public void Push(Vector2 dir)
    {
        // rb.linearVelocity += rb.linearVelocity * dir * Time.fixedDeltaTime;
        if (rb.linearVelocity.magnitude < 1) return;
        rb.position += dir * Time.fixedDeltaTime;
    }
#endif

    public BallType type => ballType;

    public float speed => rb.linearVelocity.magnitude;

    public View ballViewData => new View { velocity = rb.linearVelocity, position = rb.position };
    public void SetBallFromViewData(Vector3 vel, Vector3 pos)
    {
        rb.linearVelocity = vel;
        rb.position = pos;
    }
    public struct View
    {
        public Vector3 velocity, position;
    }
}
