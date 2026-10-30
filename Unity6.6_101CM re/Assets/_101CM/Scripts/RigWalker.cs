using UnityEngine;

/// <summary>
/// 리깅 고양이 몸짓 (절차적). 리깅 모델 루트에 붙인다.
/// - 걷기: 부모 CatVisual.moveSpeed(0~1)에 맞춰 다리 앞뒤·무릎, 팔 앞뒤+파닥, 엉덩이 뒤뚱, 꼬리 살랑
/// - 감정표현 1~4 (CatVisual.PlayEmote에서 호출): 팔·다리·꼬리·머리가 함께 움직임
///   0 ♥ 콩콩: 한 손 번쩍 흔들기 + 콩콩 뛸 때 다리 접기 + 꼬리 바짝 세워 흔들기
///   1 ! 깜짝: 두 팔 번쩍 + 다리 움츠림 + 꼬리 쭉 + 머리 뒤로
///   2 ? 갸웃: 머리 갸웃 + 한 손 턱 괴기 + 꼬리 물음표 말기
///   3 ♪ 빙글: 두 팔 벌려 파닥 + 다리 번갈아 차기 + 꼬리 빙글 휘두르기
/// 머리 회전은 BoneRotationLimit(5도) 안에서만 움직인다.
/// Animator가 손 흔들기(Wave) 중이면 팔은 Animator에 맡긴다.
/// </summary>
[DefaultExecutionOrder(900)]
public class RigWalker : MonoBehaviour
{
    [Header("Walk")]
    public float legSwing = 42f;
    public float kneeBend = 28f;
    public float armSwing = 30f;
    public float armFlap = 22f;
    public float hipSway = 6f;
    [Header("Tail")]
    public float tailWalk = 22f;
    public float tailIdle = 8f;
    public float tailIdleSpeed = 2.2f;
    [Header("Emote")]
    public float headMax = 5f;

    class B
    {
        public Transform t; public Quaternion rest; public Vector3 side, up, fwd;
        public Vector3 lift, front, right;   // 뼈 끝을 위로 / 앞으로 / 오른쪽으로 보내는 축 (뼈 로컬)
        public Quaternion off = Quaternion.identity;
        public void Add(Vector3 axis, float deg) { if (deg != 0f) off = off * Quaternion.AngleAxis(deg, axis); }
    }
    B legL, legR, kneeL, kneeR, armL, armR, foreL, foreR, hips, head;
    B[] tail;
    CatVisual visual;
    Animator anim;
    float phase, amp, tailT;
    int emote = -1; float emoteT, emoteDur;
    bool headDirty, inited;

    void Start() => Init();

    void Init()
    {
        if (inited) return; inited = true;
        visual = GetComponentInParent<CatVisual>();
        anim = GetComponentInChildren<Animator>();
        legL = Make("UpperLeg.L", "LowerLeg.L"); legR = Make("UpperLeg.R", "LowerLeg.R");
        kneeL = Make("LowerLeg.L", "Foot.L"); kneeR = Make("LowerLeg.R", "Foot.R");
        armL = Make("UpperArm.L", "LowerArm.L"); armR = Make("UpperArm.R", "LowerArm.R");
        foreL = Make("LowerArm.L", null, armL); foreR = Make("LowerArm.R", null, armR);
        hips = Make("Hips", "Spine");
        head = Make("Head", null);
        var list = new System.Collections.Generic.List<B>();
        for (int i = 1; i <= 9; i++)
        {
            var b = Make("Tail.0" + i, "Tail.0" + (i + 1), list.Count > 0 ? list[list.Count - 1] : null);
            if (b == null) break; list.Add(b);
        }
        tail = list.ToArray();
    }

    Transform Find(string n)
    {
        if (string.IsNullOrEmpty(n)) return null;
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    B Make(string name, string tipName, B prev = null)
    {
        var t = Find(name); if (!t) return null;
        var inv = Quaternion.Inverse(t.rotation);
        var tip = Find(tipName);
        Vector3 dir;
        if (tip) dir = (tip.position - t.position).normalized;
        else if (prev != null && prev.t) dir = (t.position - prev.t.position).normalized;
        else dir = t.up;   // 머리 등: 위쪽으로 뻗은 것으로 간주
        if (name == "Head") dir = transform.up;
        return new B
        {
            t = t, rest = t.localRotation,
            side = (inv * transform.right).normalized, up = (inv * transform.up).normalized, fwd = (inv * transform.forward).normalized,
            lift = inv * Toward(dir, transform.up, transform.forward),
            front = inv * Toward(dir, transform.forward, transform.up),
            right = inv * Toward(dir, transform.right, transform.up),
        };
    }

    /// <summary>뼈 방향 dir을 target 쪽으로 돌리는 월드 회전축.</summary>
    static Vector3 Toward(Vector3 dir, Vector3 target, Vector3 fallback)
    {
        var k = Vector3.Cross(dir, target);
        if (k.sqrMagnitude < 1e-4f) k = Vector3.Cross(dir, fallback);
        k.Normalize();
        var test = Quaternion.AngleAxis(10f, k) * dir;
        if (Vector3.Dot(test, target) < Vector3.Dot(dir, target)) k = -k;
        return k;
    }

    /// <summary>감정표현 몸짓 시작 (CatVisual.PlayEmote에서 호출).</summary>
    public void PlayEmote(int id, float duration)
    {
        Init();
        emote = id; emoteT = 0f; emoteDur = Mathf.Max(0.1f, duration);
    }

    /// <summary>에디터 확인용: 특정 걸음 자세로 고정.</summary>
    public void PreviewPose(float ph, float a) { Init(); Reset(); Walk(ph, a); Commit(false, false); }

    /// <summary>에디터 확인용: 감정표현 id의 진행도 n(0~1) 자세로 고정.</summary>
    public void PreviewEmote(int id, float n) { Init(); Reset(); Emote(id, n, false); Commit(false, true); }

    void Reset()
    {
        foreach (var b in All()) if (b != null) b.off = Quaternion.identity;
    }

    System.Collections.Generic.IEnumerable<B> All()
    {
        yield return legL; yield return legR; yield return kneeL; yield return kneeR;
        yield return armL; yield return armR; yield return foreL; yield return foreR;
        yield return hips; yield return head;
        if (tail != null) foreach (var b in tail) yield return b;
    }

    void Walk(float ph, float a)
    {
        float s = Mathf.Sin(ph) * a, c = Mathf.Cos(ph) * a;
        legL?.Add(legL.side, s * legSwing);
        legR?.Add(legR.side, -s * legSwing);
        kneeL?.Add(kneeL.side, Mathf.Max(0f, -Mathf.Sin(ph)) * kneeBend * a);
        kneeR?.Add(kneeR.side, Mathf.Max(0f, Mathf.Sin(ph)) * kneeBend * a);
        hips?.Add(hips.fwd, s * hipSway);
        armL?.Add(armL.up, s * armSwing); armL?.Add(armL.lift, Mathf.Abs(c) * armFlap);
        armR?.Add(armR.up, s * armSwing); armR?.Add(armR.lift, Mathf.Abs(c) * armFlap);
    }

    void Tail(float t, float amp01, float speedBoost)
    {
        if (tail == null) return;
        float tailAmp = Mathf.Lerp(tailIdle, tailWalk, amp01);
        for (int i = 0; i < tail.Length; i++)
            tail[i].Add(tail[i].up, Mathf.Sin(t - i * 0.6f) * tailAmp * (0.5f + 0.15f * i));
    }

    static void Lift(B b, float deg) { if (b != null) b.Add(b.lift, deg); }
    static void Front(B b, float deg) { if (b != null) b.Add(b.front, deg); }
    static void Right(B b, float deg) { if (b != null) b.Add(b.right, deg); }

    void Emote(int id, float n, bool armsByAnimator)
    {
        float e = Mathf.Sin(Mathf.Clamp01(n) * Mathf.PI);                 // 0→1→0
        float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(n * 4f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - n) * 4f)); // 빨리 올라가 유지 후 내려옴
        float hm = headMax;
        switch (id)
        {
            case 0: // ♥ 콩콩: 오른손 번쩍 흔들기
            {
                float hop = Mathf.Abs(Mathf.Sin(n * Mathf.PI * 2f));
                if (!armsByAnimator)
                {
                    // 팔을 너무 들면 큰 머리에 가려지므로 앞쪽으로 내밀어 흔든다
                    Front(armR, 55f * ease); Lift(armR, 35f * ease);
                    Lift(foreR, 30f * ease); Right(foreR, Mathf.Sin(n * Mathf.PI * 8f) * 35f * ease);
                    Front(armL, 15f * hop); Lift(armL, 15f * hop);
                }
                Front(legL, 20f * hop); Front(legR, 20f * hop);
                kneeL?.Add(kneeL.side, 30f * hop); kneeR?.Add(kneeR.side, 30f * hop);
                if (tail != null) for (int i = 0; i < tail.Length; i++)
                { Lift(tail[i], 22f * ease); tail[i].Add(tail[i].up, Mathf.Sin(n * 24f - i * 0.7f) * 22f * ease); }
                Right(head, Mathf.Sin(n * Mathf.PI * 4f) * hm);
                break;
            }
            case 1: // ! 깜짝: 두 팔 번쩍, 다리 움츠림, 꼬리 쭉
            {
                Front(armL, 50f * e); Lift(armL, 35f * e); Front(armR, 50f * e); Lift(armR, 35f * e);
                Lift(foreL, 35f * e); Lift(foreR, 35f * e);
                Front(legL, -12f * e); Front(legR, -12f * e);
                Right(legL, -12f * e); Right(legR, 12f * e);
                kneeL?.Add(kneeL.side, 35f * e); kneeR?.Add(kneeR.side, 35f * e);
                if (tail != null) for (int i = 0; i < tail.Length; i++)
                { Lift(tail[i], (i == 0 ? 45f : 12f) * e); tail[i].Add(tail[i].up, Mathf.Sin(n * 60f) * 4f * e); }
                Front(head, -hm * e);
                hips?.Add(hips.side, -6f * e);
                break;
            }
            case 2: // ? 갸웃: 머리 기울이고 오른손 턱 괴기, 꼬리 물음표
            {
                Right(head, hm * ease);
                Front(armR, 55f * ease); Lift(armR, 20f * ease);
                Lift(foreR, 75f * ease);
                Lift(armL, -12f * ease);
                Front(legR, 12f * ease); Right(legR, 8f * ease);
                hips?.Add(hips.fwd, 5f * ease);
                if (tail != null) for (int i = 0; i < tail.Length; i++)
                { float k = (i + 1f) / tail.Length; Lift(tail[i], 25f * k * ease); tail[i].Add(tail[i].up, 30f * k * ease); }
                break;
            }
            case 3: // ♪ 빙글: 두 팔 벌려 파닥, 다리 번갈아 차기, 꼬리 휘두르기
            {
                float beat = Mathf.Sin(n * Mathf.PI * 6f);
                Lift(armL, 35f * ease); Lift(armR, 35f * ease);
                Front(armL, beat * 35f * ease); Front(armR, -beat * 35f * ease);
                Lift(foreL, 25f * ease); Lift(foreR, 25f * ease);
                Front(legL, beat * 28f * ease); Front(legR, -beat * 28f * ease);
                kneeL?.Add(kneeL.side, Mathf.Max(0f, beat) * 30f * ease); kneeR?.Add(kneeR.side, Mathf.Max(0f, -beat) * 30f * ease);
                if (tail != null) for (int i = 0; i < tail.Length; i++)
                { Lift(tail[i], 15f * ease); tail[i].Add(tail[i].up, Mathf.Sin(n * 18f - i * 0.8f) * 35f * ease); }
                Right(head, beat * hm * ease);
                hips?.Add(hips.fwd, beat * 6f * ease);
                break;
            }
        }
    }

    void Commit(bool skipArms, bool headActive)
    {
        foreach (var b in All())
        {
            if (b == null) continue;
            if (skipArms && (b == armL || b == armR || b == foreL || b == foreR)) continue;
            if (b == head && !headActive) continue;
            b.t.localRotation = b.rest * b.off;
        }
    }

    void LateUpdate()
    {
        float speed = visual ? visual.moveSpeed : 0f;
        float dt = Time.deltaTime;
        amp = Mathf.MoveTowards(amp, speed > 0.05f ? Mathf.Clamp01(0.5f + speed) : 0f, dt * 5f);
        if (amp > 0.001f) phase += dt * (6f + 4f * speed);
        else phase = 0f;

        bool waving = anim && anim.runtimeAnimatorController && anim.GetCurrentAnimatorStateInfo(0).IsName("Wave");
        Reset();

        float walkAmp = amp;
        bool emoting = emote >= 0;
        float n = 0f;
        if (emoting)
        {
            emoteT += dt; n = emoteT / emoteDur;
            if (n >= 1f) { emote = -1; emoting = false; }
            else walkAmp *= 0.3f;   // 감정표현 중에는 걷기 몸짓을 줄임
        }

        Walk(phase, walkAmp);
        tailT += dt * Mathf.Lerp(tailIdleSpeed, 6f + 4f * speed, amp);
        Tail(tailT, amp, 0f);
        if (emoting) Emote(emote, n, waving);

        bool headActive = emoting || headDirty;
        Commit(waving, headActive);
        headDirty = emoting;   // 감정표현이 끝난 다음 프레임에 머리를 기본 자세로 한 번 되돌림
    }
}
