using UnityEngine;

/// <summary>
/// 혼자 떠다니며 바닥을 쓰는 마법 빗자루 (방해 요소).
/// 정해진 구간을 왕복하며 쓸고, 끝에서 잠깐 멈춰 빙글 돈다.
/// 주인공이나 행렬에 닿으면 주인공(과 따라오는 행렬)을 통째로 멀리 쓸어 날려 버린다.
/// 고양이를 잃지는 않고, 위치와 시간을 잃는 벌칙.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MagicBroom : MonoBehaviour
{
    public Vector3[] waypoints;
    public float speed = 2.2f;
    public float pauseAtEnds = 1.2f;
    public Transform visual;      // 흔들림/회전 연출용
    public Transform sparkles;    // 주위를 도는 반짝이

    Rigidbody rb;
    int target = 1, dir = 1;
    float pauseUntil, swing;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        if (waypoints != null && waypoints.Length > 0) rb.position = transform.position = waypoints[0];
    }

    bool Paused => Time.time < pauseUntil;

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length < 2 || Paused) return;
        Vector3 pos = rb.position, goal = waypoints[target];
        Vector3 to = goal - pos;
        float step = speed * Time.fixedDeltaTime;
        if (to.magnitude <= step)
        {
            rb.MovePosition(goal);
            if (target + dir < 0 || target + dir >= waypoints.Length)
            {
                dir = -dir;
                pauseUntil = Time.time + pauseAtEnds;
            }
            target += dir;
            return;
        }
        Vector3 d = to.normalized;
        rb.MovePosition(pos + d * step);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(d), 240f * Time.fixedDeltaTime));
    }

    void Update()
    {
        float t = Time.time;
        if (visual)
        {
            float bob = Mathf.Sin(t * 3f) * 0.08f;
            if (Paused)
            {
                // 끝에서 잠깐 빙글빙글
                visual.localRotation = Quaternion.Euler(0f, (t * 540f) % 360f, 0f);
                visual.localPosition = new Vector3(0f, 0.15f + bob, 0f);
            }
            else
            {
                // 좌우로 쓱싹쓱싹
                swing += Time.deltaTime * 9f;
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(swing) * 25f);
                visual.localPosition = new Vector3(0f, bob, 0f);
            }
        }
        if (sparkles) sparkles.localRotation = Quaternion.Euler(0f, t * 120f, 0f);
    }

    void OnTriggerEnter(Collider other) => Hit(other);
    void OnTriggerStay(Collider other) => Hit(other);

    void Hit(Collider other)
    {
        var gm = GameManager.I;
        if (!gm || !gm.player || gm.IsWon) return;
        if (other.GetComponentInParent<LostKitten>()) return;
        bool isCat = other.GetComponentInParent<CatPlayer>();
        if (!isCat)
        {
            var f = other.GetComponentInParent<Follower>();
            isCat = f && f.enabled;
        }
        if (isCat) gm.player.SweptByBroom(transform.position, transform.forward);
    }
}
