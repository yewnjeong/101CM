using UnityEngine;

/// <summary>
/// 정해진 경로를 따라 혼자 굴러다니는 쇼핑카트 (방해 요소).
/// 주인공이나 행렬에 부딪히면 행렬 끝 고양이들이 흩어진다.
/// loop=true: 경로를 계속 순환 / false: 끝에서 잠깐 멈췄다가 되돌아감.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ShoppingCart : MonoBehaviour
{
    public Vector3[] waypoints;
    public bool loop = true;
    public float speed = 3.5f;
    public float pauseAtEnds = 0.8f;
    public int startIndex;
    public Transform visual;

    Rigidbody rb;
    int target, dir = 1;
    float pauseUntil, rattle;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        startIndex = Mathf.Clamp(startIndex, 0, waypoints.Length - 1);
        rb.position = transform.position = waypoints[startIndex];
        target = startIndex + 1;
        if (target >= waypoints.Length) { target = loop ? 0 : startIndex - 1; if (!loop) dir = -1; }
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length < 2) return;
        if (Time.time < pauseUntil) return;

        Vector3 pos = rb.position, goal = waypoints[target];
        Vector3 to = goal - pos;
        float step = speed * Time.fixedDeltaTime;
        if (to.magnitude <= step)
        {
            rb.MovePosition(goal);
            Advance();
            return;
        }
        Vector3 d = to.normalized;
        rb.MovePosition(pos + d * step);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(d), 300f * Time.fixedDeltaTime));
    }

    void Advance()
    {
        if (loop)
        {
            target = (target + 1) % waypoints.Length;
            return;
        }
        if (target + dir < 0 || target + dir >= waypoints.Length)
        {
            dir = -dir;
            pauseUntil = Time.time + pauseAtEnds;
        }
        target += dir;
    }

    void Update()
    {
        if (!visual) return;
        bool moving = Time.time >= pauseUntil;
        rattle += Time.deltaTime * (moving ? 28f : 0f);
        visual.localPosition = new Vector3(0f, moving ? Mathf.Abs(Mathf.Sin(rattle)) * 0.035f : 0f, 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, moving ? Mathf.Sin(rattle * 0.5f) * 1.5f : 0f);
    }

    void OnTriggerEnter(Collider other) => Hit(other);
    void OnTriggerStay(Collider other) => Hit(other);

    void Hit(Collider other)
    {
        var gm = GameManager.I;
        if (!gm || !gm.player || gm.IsWon) return;
        if (other.GetComponentInParent<LostKitten>()) return;

        if (other.GetComponentInParent<CatPlayer>())
        {
            gm.player.HitByCart(transform.position, transform.forward, null);
            return;
        }
        var f = other.GetComponentInParent<Follower>();
        if (f && f.enabled) gm.player.HitByCart(transform.position, transform.forward, f);
    }
}
