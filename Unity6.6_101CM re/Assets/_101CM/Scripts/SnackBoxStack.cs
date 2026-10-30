using System.Collections;
using UnityEngine;

/// <summary>과자 상자 더미. 행렬이 충분히 길 때 부딪히면 무너지며 쿠키/사탕이 쏟아진다.</summary>
public class SnackBoxStack : MonoBehaviour
{
    public int requiredFollowers = 3;
    public int dropCount = 5;
    public Transform boxesRoot;
    bool collapsed;

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other) => TryHit(other);

    void TryHit(Collider other)
    {
        if (collapsed) return;
        var gm = GameManager.I;
        if (!gm || !gm.player) return;
        if (!other.GetComponentInParent<CatPlayer>() && !other.GetComponentInParent<Follower>()) return;

        if (gm.player.FollowerCount >= requiredFollowers) Collapse(other.transform.position);
    }

    void Collapse(Vector3 from)
    {
        collapsed = true;
        foreach (var c in GetComponents<Collider>()) if (c.isTrigger) c.enabled = false;
        var gm = GameManager.I;
        Vector3 center = transform.position + Vector3.up * 1.2f;

        foreach (Transform b in boxesRoot)
        {
            var rb = b.gameObject.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.AddExplosionForce(7f, from + Vector3.down * 0.5f, 5f, 0.6f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
            StartCoroutine(ShrinkAway(b, 5f + Random.value));
        }

        for (int i = 0; i < dropCount; i++)
        {
            var t = i % 2 == 0 ? FoodType.Cookie : FoodType.Candy;
            Vector3 off = Random.insideUnitSphere * 0.6f;
            off.y = Mathf.Abs(off.y);
            var f = Food.Create(t, center + off, gm.foodRoot, true);
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
            f.GetComponent<Rigidbody>().linearVelocity = new Vector3(r.x, Random.Range(3.5f, 5.5f), r.y);
        }

        CameraFollow.Shake(0.35f);
        gm.player.Emote(1);
    }

    IEnumerator ShrinkAway(Transform b, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!b) yield break;
        Vector3 s = b.localScale;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            if (!b) yield break;
            b.localScale = s * (1f - t / 0.5f);
            yield return null;
        }
        if (b) Destroy(b.gameObject);
    }
}
