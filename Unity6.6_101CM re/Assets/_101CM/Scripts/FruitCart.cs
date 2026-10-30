using System.Collections;
using UnityEngine;

/// <summary>과일 수레. 행렬이 충분히 길 때 부딪히면 과일이 데굴데굴 굴러간다.</summary>
public class FruitCart : MonoBehaviour
{
    public int requiredFollowers = 6;
    public int fruitCount = 4;
    public GameObject pile;
    public Transform body;
    bool spilled;

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other) => TryHit(other);

    void TryHit(Collider other)
    {
        if (spilled) return;
        var gm = GameManager.I;
        if (!gm || !gm.player) return;
        if (!other.GetComponentInParent<CatPlayer>() && !other.GetComponentInParent<Follower>()) return;

        if (gm.player.FollowerCount >= requiredFollowers) Spill(other.transform.position);
    }

    void Spill(Vector3 from)
    {
        spilled = true;
        foreach (var c in GetComponents<Collider>()) if (c.isTrigger) c.enabled = false;
        if (pile) pile.SetActive(false);
        var gm = GameManager.I;

        Vector3 away = transform.position - from;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        away.Normalize();

        for (int i = 0; i < fruitCount; i++)
        {
            var t = i % 2 == 0 ? FoodType.Apple : FoodType.Orange;
            Vector3 spawn = transform.position + Vector3.up * 1.5f + Random.insideUnitSphere * 0.3f;
            var f = Food.Create(t, spawn, gm.foodRoot, true);
            var rb = f.GetComponent<Rigidbody>();
            Vector3 d = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away;
            rb.linearVelocity = d * Random.Range(4f, 6.5f) + Vector3.up * 2f;
            rb.angularVelocity = Vector3.Cross(Vector3.up, d) * 10f;
        }

        StartCoroutine(Wobble());
        CameraFollow.Shake(0.25f);
        gm.player.Emote(1);
    }

    IEnumerator Wobble()
    {
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float k = 1f - t / 0.8f;
            if (body) body.localRotation = Quaternion.Euler(Mathf.Sin(t * 25f) * 12f * k, 0f, Mathf.Sin(t * 18f) * 8f * k);
            yield return null;
        }
        if (body) body.localRotation = Quaternion.identity;
    }
}
