using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캡슐 기반 고양이 외형 + 걷기 바운스 / 스쿼시 / 감정표현 모션 / 색 변화.
/// 루트(콜라이더·물리)와 분리된 자식 오브젝트로 동작하므로 물리 안정성에 영향 없음.
/// </summary>
public class CatVisual : MonoBehaviour
{
    public Renderer[] tinted;
    public float baseScale = 1f;
    [HideInInspector] public float yaw;        // 월드 기준 바라보는 방향(도)
    [HideInInspector] public float moveSpeed;  // 0~1

    Color currentColor, targetColor;
    bool colorInit;
    MaterialPropertyBlock mpb;
    Animator rigAnimator;
    float breathOffset = -1f;
    float bobPhase, bob, squash, emoteY, emoteTiltX, emoteTiltZ, emoteSpin;

    public static CatVisual Build(Transform parent, Color color, float scale, string name = "Visual")
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * scale;
        var cv = root.gameObject.AddComponent<CatVisual>();
        cv.baseScale = scale;

        var list = new List<Renderer>();
        var body = Palette.Shape(PrimitiveType.Capsule, root, new Vector3(0, 0.5f, 0), new Vector3(0.72f, 0.5f, 0.72f), color, name: "Body");
        list.Add(body.GetComponent<Renderer>());

        var eyeColor = new Color(1f, 0.86f, 0.25f);
        var dark = new Color(0.05f, 0.05f, 0.06f);
        for (int s = -1; s <= 1; s += 2)
        {
            var ear = Palette.Shape(PrimitiveType.Cube, root, new Vector3(0.17f * s, 0.9f, 0.02f), new Vector3(0.2f, 0.2f, 0.09f), color,
                euler: new Vector3(0, 0, 45), name: "Ear");
            list.Add(ear.GetComponent<Renderer>());
            Palette.Shape(PrimitiveType.Sphere, root, new Vector3(0.13f * s, 0.64f, 0.33f), new Vector3(0.17f, 0.19f, 0.08f), eyeColor, emission: 0.35f, name: "Eye");
            Palette.Shape(PrimitiveType.Sphere, root, new Vector3(0.13f * s, 0.64f, 0.365f), new Vector3(0.09f, 0.12f, 0.03f), dark, name: "Pupil");
        }
        Palette.Shape(PrimitiveType.Sphere, root, new Vector3(0, 0.55f, 0.36f), new Vector3(0.06f, 0.04f, 0.03f), new Color(1f, 0.55f, 0.65f), name: "Nose");
        var tail = Palette.Shape(PrimitiveType.Capsule, root, new Vector3(0, 0.38f, -0.42f), new Vector3(0.11f, 0.28f, 0.11f), color,
            euler: new Vector3(-40, 0, 0), name: "Tail");
        list.Add(tail.GetComponent<Renderer>());

        cv.tinted = list.ToArray();
        cv.SetColorImmediate(color);
        return cv;
    }

    /// <summary>캡슐 외형을 숨기고 리깅된 모델 프리팹으로 교체 (색 변화는 모델 렌더러에 적용).</summary>
    public void UseModel(GameObject prefab)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        var obj = Instantiate(prefab, transform, false);
        obj.name = "Model";
        var list = new List<Renderer>();
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) list.Add(r);
        tinted = list.ToArray();
        ApplyColor();
    }

    // ---------------- Color ----------------
    public void SetColorImmediate(Color c)
    {
        currentColor = targetColor = c;
        colorInit = true;
        ApplyColor();
    }

    public void SetTargetColor(Color c)
    {
        if (!colorInit) { SetColorImmediate(c); return; }
        targetColor = c;
    }

    void ApplyColor()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        foreach (var r in tinted)
        {
            if (!r) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", currentColor);
            mpb.SetColor("_Color", currentColor);
            r.SetPropertyBlock(mpb);
        }
    }

    // ---------------- Motion ----------------
    public void Squash(float amount = 1f) => squash = Mathf.Max(squash, amount);

    float hopT = -1f;
    bool hasRig;
    /// <summary>제자리 작은 점프 (감정표현 모션과 별개로 겹쳐짐).</summary>
    public void Hop() { hopT = 0f; Squash(0.4f); }

    /// <summary>0: ♥ 두 번 콩콩 / 1: ! 깜짝 점프 / 2: ? 갸웃 / 3: ♪ 빙글</summary>
    public void PlayEmote(int id)
    {
        if (id == 0)
        {
            if (!rigAnimator) rigAnimator = GetComponentInChildren<Animator>();
            if (rigAnimator && rigAnimator.runtimeAnimatorController) rigAnimator.SetTrigger("Wave");
        }
        hasRig = false;
        foreach (var rw in GetComponentsInChildren<RigWalker>())
        {
            rw.PlayEmote(id, id == 3 ? 0.9f : 0.75f);
            hasRig = true;
        }
        StopAllCoroutines();
        emoteY = emoteTiltX = emoteTiltZ = emoteSpin = 0f;
        StartCoroutine(EmoteRoutine(id));
    }

    IEnumerator EmoteRoutine(int id)
    {
        float dur = id == 3 ? 0.9f : 0.75f, t = 0f;
        if (id == 1) Squash(0.8f);
        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            switch (id)
            {
                case 0: emoteY = Mathf.Abs(Mathf.Sin(n * Mathf.PI * 2f)) * 0.35f; break;
                case 1: emoteY = Mathf.Sin(n * Mathf.PI) * 0.5f; emoteTiltX = -Mathf.Sin(n * Mathf.PI) * 15f; break;
                case 2:
                {
                    // 갸웃: 몸통 가운데를 축으로 살짝(12°) 기울여 잠깐 멈췄다가 돌아옴 (좌우로 흔들지 않게)
                    float k = n < 0.25f ? Mathf.SmoothStep(0f, 1f, n / 0.25f)
                            : n < 0.7f ? 1f
                            : Mathf.SmoothStep(1f, 0f, (n - 0.7f) / 0.3f);
                    // 리깅 모델은 머리만 갸웃하므로 몸은 아주 조금만
                    emoteTiltZ = k * (hasRig ? 4f : 12f);
                    break;
                }
                case 3: emoteSpin = Mathf.SmoothStep(0f, 360f, n); emoteY = Mathf.Sin(n * Mathf.PI) * 0.3f; break;
            }
            yield return null;
        }
        emoteY = emoteTiltX = emoteTiltZ = emoteSpin = 0f;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (colorInit && !Approximately(currentColor, targetColor))
        {
            currentColor = Color.Lerp(currentColor, targetColor, 1f - Mathf.Exp(-3f * dt));
            ApplyColor();
        }

        if (moveSpeed > 0.05f)
        {
            // 걷기 출렁임: 느리고 낮게, 부드러운 곡선(통통 튀지 않게)
            bobPhase += dt * (5f + 3f * moveSpeed);
            float wave = 0.5f - 0.5f * Mathf.Cos(bobPhase * 2f);
            bob = wave * (0.015f + 0.03f * moveSpeed);
        }
        else
        {
            bob = Mathf.MoveTowards(bob, 0f, dt);
            bobPhase = 0f;
        }

        squash = Mathf.MoveTowards(squash, 0f, dt * 4f);
        if (breathOffset < 0f) breathOffset = Random.value * 10f;
        float breathe = Mathf.Sin(Time.time * 3f + breathOffset) * 0.02f;
        float sx = 1f + squash * 0.25f, sy = 1f - squash * 0.3f + breathe;
        transform.localScale = new Vector3(sx, sy, sx) * baseScale;
        float hopY = 0f;
        if (hopT >= 0f)
        {
            hopT += dt;
            const float hopDur = 0.4f;
            if (hopT >= hopDur) hopT = -1f;
            else hopY = Mathf.Sin(hopT / hopDur * Mathf.PI) * 0.7f;
        }
        var rot = Quaternion.Euler(emoteTiltX, yaw + emoteSpin, emoteTiltZ);
        // 기울기는 발이 아니라 몸통 가운데를 축으로 (위쪽이 크게 휘둘리지 않게)
        Vector3 pivot = Vector3.up * (0.5f * baseScale);
        Vector3 tiltOffset = pivot - rot * pivot;
        transform.localPosition = new Vector3(0f, (bob + emoteY + hopY) * baseScale, 0f) + tiltOffset;
        transform.localRotation = rot;
    }

    static bool Approximately(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.003f;
}
