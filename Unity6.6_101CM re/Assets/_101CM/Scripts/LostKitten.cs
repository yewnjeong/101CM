using System.Collections;
using UnityEngine;

/// <summary>
/// 카트에 치여 행렬에서 떨어져 나간 새끼 고양이.
/// 튕겨 날아가 앉아서 두리번거리고, 주인공이 가까이에서 E를 누르면 다시 행렬에 합류한다.
/// </summary>
public class LostKitten : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<LostKitten> All = new System.Collections.Generic.List<LostKitten>();

    public Follower follower;
    bool landed, highlighted, converting;
    float nextFidget, landTime;

    /// <summary>착지 후 이 시간이 지나면 고양이가 원래 음식으로 돌아간다.</summary>
    public const float RevertDelay = 1f;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
    void OnDestroy()
    {
        All.Remove(this);
        if (follower) follower.transform.localScale = Vector3.one;
    }

    /// <summary>E로 데려올 수 있는 거리면 살짝 커지며 강조.</summary>
    public void SetHighlighted(bool on)
    {
        highlighted = on;
        if (!on && follower) follower.transform.localScale = Vector3.one;
    }

    public bool CanRejoin => landed && follower && !converting;

    public static void Scatter(Follower f, Vector3 from)
    {
        var lk = f.gameObject.AddComponent<LostKitten>();
        lk.follower = f;
        f.enabled = false;
        if (f.body) f.body.enabled = false;   // 흩어진 동안은 길을 막지 않게
        lk.StartCoroutine(lk.Fly(from));
    }

    IEnumerator Fly(Vector3 from)
    {
        Vector3 a = transform.position;
        Vector3 b = FindLandingSpot(a, from);
        follower.visual.moveSpeed = 0f;
        follower.visual.PlayEmote(1);

        float t = 0f, dur = 0.65f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            Vector3 p = Vector3.Lerp(a, b, n);
            p.y += Mathf.Sin(n * Mathf.PI) * 1.8f;
            transform.position = p;
            follower.visual.yaw += 720f * Time.deltaTime;
            yield return null;
        }
        transform.position = b;
        follower.visual.Squash(0.8f);
        landed = true;
        landTime = Time.time;
        nextFidget = Time.time + 0.6f;
    }

    static Vector3 FindLandingSpot(Vector3 a, Vector3 from)
    {
        Vector3 away = a - from; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
        away.Normalize();

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 d = Quaternion.Euler(0f, Random.Range(-70f, 70f) + attempt * 45f, 0f) * away;
            Vector3 b = a + d * Random.Range(2.5f, 4.5f);
            b.y = 0f;
            Vector2 flat = new Vector2(b.x, b.z);
            if (flat.magnitude > 19.5f) { flat = flat.normalized * 19.5f; b = new Vector3(flat.x, 0f, flat.y); }
            // 건물·분수 등과 겹치지 않는 곳
            if (!Physics.CheckSphere(b + Vector3.up * 0.5f, 0.35f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                return b;
        }
        return new Vector3(a.x, 0f, a.z);
    }

    /// <summary>고양이가 퐁 사라지고 그 자리에 원래 음식이 다시 나타난다(E로 다시 먹으면 cm·행렬 복구).</summary>
    IEnumerator RevertToFood()
    {
        converting = true;
        if (highlighted) SetHighlighted(false);
        var f = follower;
        Vector3 s0 = f.transform.localScale;
        float t = 0f;
        while (t < 0.15f)
        {
            t += Time.deltaTime;
            f.transform.localScale = s0 * (1f - t / 0.15f);
            yield return null;
        }
        Vector3 pos = f.transform.position; pos.y = Mathf.Max(0f, pos.y);
        var food = Food.Create(f.sourceType, pos, GameManager.I ? GameManager.I.foodRoot : null, false);
        food.cm = f.cm;
        food.PopIn();
        follower = null;
        Destroy(f.gameObject);
    }

    void Update()
    {
        if (!landed || !follower || converting) return;
        if (Time.time - landTime >= RevertDelay) { StartCoroutine(RevertToFood()); return; }
        if (highlighted) follower.transform.localScale = Vector3.one * (1.2f + Mathf.Sin(Time.time * 8f) * 0.06f);
        var p = GameManager.I ? GameManager.I.player : null;
        if (p)
        {
            Vector3 to = p.transform.position - transform.position;
            if (to.sqrMagnitude > 0.01f)
                follower.visual.yaw = Mathf.MoveTowardsAngle(follower.visual.yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 240f * Time.deltaTime);
        }
        if (Time.time > nextFidget)
        {
            nextFidget = Time.time + Random.Range(1.2f, 2.4f);
            follower.visual.PlayEmote(Random.value < 0.6f ? 2 : 0); // 갸웃 / 콩콩 (날 데려가줘!)
        }
    }
}
