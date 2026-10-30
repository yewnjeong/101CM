using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 주인공 도둑 고양이. 루트 = Rigidbody + CapsuleCollider(안정적인 이동), 외형은 자식 CatVisual.
/// 이동 경로를 기록해 뒤따르는 새끼 고양이 행렬을 배치한다.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CatPlayer : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float jumpVelocity = 4.5f;   // 약 1m 높이
    [Tooltip("몸 가장자리에서 음식까지의 수평 거리(m)")]
    public float eatRange = 1.0f;
    [Tooltip("음식 높이와 내 높이 차이 허용치(m). 상자·매대 위 음식은 점프하거나 올라가야 닿음")]
    public float eatHeightTolerance = 0.45f;
    public float turnSpeed = 900f;

    [Header("Trail")]
    public float firstGap = 0.9f;
    public float spacing = 0.72f;
    public float followerScale = 0.58f;

    public CatVisual visual;
    public bool InputEnabled { get; set; } = true;
    public int FollowerCount => followers.Count;
    public Food NearbyFood { get; private set; }
    public LostKitten NearbyKitten { get; private set; }

    Rigidbody rb;
    Camera cam;
    Vector3 moveDir, spawnPos;
    bool jumpQueued, grounded;
    float groundY;   // 마지막으로 땅을 밟았던 높이 (점프 중 경로는 이 높이로 기록)
    Coroutine celebrate;
    float invulnUntil, stunUntil, broomSafeUntil;
    public bool IsInvulnerable => Time.time < invulnUntil;

    const float PathStep = 0.12f;
    readonly List<Vector3> path = new List<Vector3>();   // [0] = 가장 최근
    readonly List<Follower> followers = new List<Follower>();
    static PhysicsMaterial noFriction;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (noFriction == null)
            noFriction = new PhysicsMaterial("CatNoFriction")
            {
                dynamicFriction = 0f, staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        GetComponent<CapsuleCollider>().sharedMaterial = noFriction;

        gameObject.layer = 2; // Ignore Raycast → 지면 체크에서 자기 자신 제외
        if (!visual) visual = GetComponentInChildren<CatVisual>();
        spawnPos = transform.position;
        ResetPath();
    }

    void Start() => cam = Camera.main;

    void ResetPath()
    {
        path.Clear();
        groundY = transform.position.y;
        path.Add(TrailPoint());
    }

    public void SetProgress(float p, bool immediate = false)
    {
        var c = Palette.EvaluateProgress(p);
        if (immediate) visual.SetColorImmediate(c); else visual.SetTargetColor(c);
    }

    // ---------------- Input / Update ----------------
    void Update()
    {
        if (GameManager.I && GameManager.I.IsPaused) return;   // Esc 메뉴 열려 있는 동안 입력 무시
        var kb = Keyboard.current;
        Vector2 input = Vector2.zero;

        if (kb != null && InputEnabled)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.spaceKey.wasPressedThisFrame) jumpQueued = true;
            if (kb.eKey.wasPressedThisFrame) TryEat();

            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Emote(0);
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Emote(1);
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Emote(2);
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Emote(3);
        }

        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (cam)
        {
            fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, fwd);
        }
        moveDir = Vector3.ClampMagnitude(fwd * input.y + right * input.x, 1f);

        if (moveDir.sqrMagnitude > 0.01f)
        {
            float target = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            visual.yaw = Mathf.MoveTowardsAngle(visual.yaw, target, turnSpeed * Time.deltaTime);
        }

        Vector3 hv = rb.linearVelocity; hv.y = 0f;
        float speed01 = Mathf.Clamp01(hv.magnitude / moveSpeed);
        visual.moveSpeed = grounded ? speed01 : 0f;

        if (transform.position.y < -8f) Respawn();

        RecordPath();
        UpdateFollowers(speed01);
        UpdateNearbyFood();
    }

    // ---------------- Eat (E) ----------------
    void UpdateNearbyFood()
    {
        // 몸(캡슐 반지름 0.35) 가장자리 기준 수평 거리로 판정 → 진열대 위 음식도 앞에 붙으면 닿음
        // 높이: 음식 중심과 내 몸 아래쪽(발+0.35) 높이 차가 eatHeightTolerance 이내여야 함
        //   → 땅 위 음식은 그냥 먹고, 상자(0.66)·매대(0.8) 위 음식은 점프하거나 올라가야 먹을 수 있음
        const float bodyRadius = 0.35f;
        const float reachY = 0.35f;
        Food best = null;
        LostKitten bestKitten = null;
        float bestD = eatRange + bodyRadius;
        Vector3 me = transform.position;
        if (InputEnabled)
        {
            foreach (var f in Food.All)
            {
                if (!f || !f.CanBeEaten) continue;
                Vector3 fp = f.FocusPoint;
                if (Mathf.Abs(fp.y - (me.y + reachY)) > eatHeightTolerance) continue;
                float d = new Vector2(fp.x - me.x, fp.z - me.z).magnitude;
                if (d < bestD) { bestD = d; best = f; }
            }
            // 흩어진 고양이도 E로 다시 데려옴 (가장 가까운 것 하나)
            foreach (var k in LostKitten.All)
            {
                if (!k || !k.CanRejoin) continue;
                Vector3 kp = k.transform.position;
                if (Mathf.Abs(kp.y - me.y) > eatHeightTolerance + 0.3f) continue;
                float d = new Vector2(kp.x - me.x, kp.z - me.z).magnitude;
                if (d < bestD) { bestD = d; bestKitten = k; best = null; }
            }
        }
        if (best != NearbyFood)
        {
            if (NearbyFood) NearbyFood.SetHighlighted(false);
            if (best) best.SetHighlighted(true);
            NearbyFood = best;
        }
        if (bestKitten != NearbyKitten)
        {
            if (NearbyKitten) NearbyKitten.SetHighlighted(false);
            if (bestKitten) bestKitten.SetHighlighted(true);
            NearbyKitten = bestKitten;
        }
    }

    void TryEat()
    {
        if (NearbyKitten && NearbyKitten.CanRejoin)
        {
            var k = NearbyKitten;
            NearbyKitten = null;
            visual.Squash(0.6f);
            Rejoin(k);
            return;
        }
        if (!NearbyFood) return;
        var f = NearbyFood;
        Vector3 to = f.transform.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.01f) visual.yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        visual.Squash(0.6f);
        f.Eat(this);
    }

    void FixedUpdate()
    {
        grounded = Physics.SphereCast(transform.position + Vector3.up * 0.45f, 0.3f, Vector3.down, out _, 0.25f,
            ~(1 << 2), QueryTriggerInteraction.Ignore);
        if (grounded && rb.linearVelocity.y <= 0.1f) groundY = transform.position.y;
        if (Time.time < stunUntil) { jumpQueued = false; return; } // 카트에 치여 날아가는 중

        var v = rb.linearVelocity;
        Vector3 target = moveDir * moveSpeed;
        Vector3 h = Vector3.MoveTowards(new Vector3(v.x, 0f, v.z), target, (grounded ? 45f : 18f) * Time.fixedDeltaTime);
        v.x = h.x; v.z = h.z;
        if (jumpQueued && grounded)
        {
            v.y = jumpVelocity;
            StartCoroutine(HopWave());
            visual.Squash(0.7f);
        }
        jumpQueued = false;
        rb.linearVelocity = v;
        ApplyLineBlock();
    }

    /// <summary>
    /// 내 행렬 전체를 벽처럼 취급: 고양이 쪽으로 다가가는 속도 성분을 없애서 행렬 사이로 못 지나가게 한다.
    /// 점프로 고양이 머리 위를 넘는 중이면 막지 않음(행렬에 갇혔을 때 빠져나갈 방법).
    /// </summary>
    void ApplyLineBlock()
    {
        const float blockDist = 0.35f + 0.22f + 0.08f;   // 내 반지름 + 고양이 반지름 + 여유
        Vector3 me = rb.position;
        Vector3 v = rb.linearVelocity;
        Vector3 h = new Vector3(v.x, 0f, v.z);
        for (int i = 0; i < followers.Count; i++)
        {
            var f = followers[i];
            if (!f || f.transform.localScale.x < 0.5f) continue;   // 막 생겨나는 중인 고양이는 제외
            Vector3 kp = f.transform.position;
            if (me.y - kp.y > 0.5f) continue;              // 위로 뛰어넘는 중
            Vector3 d = kp - me; d.y = 0f;
            float dist = d.magnitude;
            if (dist > blockDist + 0.25f || dist < 1e-4f) continue;
            Vector3 n = d / dist;
            float into = Vector3.Dot(h, n);
            if (into > 0f) h -= n * into;                   // 고양이 쪽으로 가는 성분 제거
            if (dist < blockDist) h -= n * (blockDist - dist) * 10f;   // 이미 겹쳤으면 살짝 밀어냄
        }
        rb.linearVelocity = new Vector3(h.x, v.y, h.z);
    }

    // ---------------- Shopping cart hit ----------------
    /// <summary>
    /// 카트 충돌. 주인공이 치이면 행렬 끝 3마리, 행렬 중간이 치이면 그 지점 뒤(최대 5마리)가 흩어진다.
    /// </summary>
    public void HitByCart(Vector3 cartPos, Vector3 cartDir, Follower hitFollower)
    {
        if (IsInvulnerable || !InputEnabled) return;
        int start;
        if (hitFollower)
        {
            int idx = followers.IndexOf(hitFollower);
            if (idx < 0) return;
            start = Mathf.Max(idx, followers.Count - 5);
        }
        else start = Mathf.Max(0, followers.Count - 3);

        invulnUntil = Time.time + 1.8f;
        int lostCm = 0, count = 0;
        for (int i = followers.Count - 1; i >= start; i--)
        {
            var f = followers[i];
            followers.RemoveAt(i);
            if (!f) continue;
            lostCm += f.cm;
            count++;
            LostKitten.Scatter(f, cartPos);
        }

        if (!hitFollower)
        {
            Vector3 away = transform.position - cartPos; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = cartDir;
            away.Normalize();
            rb.linearVelocity = away * 7f + Vector3.up * 3.5f;
            stunUntil = Time.time + 0.45f;
        }
        visual.PlayEmote(1);
        visual.Squash(1f);
        StartCoroutine(Blink());
        if (GameManager.I) GameManager.I.OnKittensLost(lostCm, count, this);
    }

    /// <summary>
    /// 마법 빗자루에 쓸림: 주인공을 광장 쪽으로 멀리 날려 보낸다. 행렬은 그대로 따라온다(고양이 손실 없음).
    /// </summary>
    public void SweptByBroom(Vector3 broomPos, Vector3 broomDir)
    {
        if (Time.time < broomSafeUntil || Time.time < stunUntil || !InputEnabled) return;
        broomSafeUntil = Time.time + 2.5f;

        Vector3 away = transform.position - broomPos; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = broomDir;
        away.y = 0f;
        away.Normalize();
        // 빗자루가 쓰는 방향(옆)으로 쓸려 나감 → 광장 카트 길로 날아가 연속으로 맞지 않게
        Vector3 sweep = broomDir; sweep.y = 0f;
        sweep = sweep.sqrMagnitude > 0.01f ? sweep.normalized : away;
        Vector3 d = (away + sweep * 1.3f).normalized;

        rb.linearVelocity = d * 5f + Vector3.up * 6.5f;
        stunUntil = Time.time + 1.0f;
        invulnUntil = Mathf.Max(invulnUntil, Time.time + 1.8f);   // 착지 직후 카트에 연속으로 치이지 않게
        jumpQueued = false;
        visual.PlayEmote(3);   // 빙글빙글 날아감
        visual.Squash(1f);
        StartCoroutine(HopWave());
        if (GameManager.I) GameManager.I.OnSwept(this);
    }

    void Rejoin(LostKitten lk)
    {
        var f = lk.follower;
        lk.follower = null;   // 같은 프레임 중복 합류 방지
        Destroy(lk);
        if (!f) return;
        f.enabled = true;
        if (f.body) f.body.enabled = true;
        followers.Add(f);
        f.Emote(0);
        if (GameManager.I) GameManager.I.OnKittenRejoined(f, this);
    }

    IEnumerator Blink()
    {
        var rends = visual.GetComponentsInChildren<Renderer>();
        while (Time.time < invulnUntil)
        {
            bool on = Mathf.Repeat(Time.time * 10f, 1f) > 0.5f;
            foreach (var r in rends) if (r) r.enabled = on;
            yield return null;
        }
        foreach (var r in rends) if (r) r.enabled = true;
    }

    // ---------------- Trail ----------------
    /// <summary>
    /// 행렬이 따라올 경로 점. 점프로 떠 있는 동안은 높이를 땅 높이로 고정해서
    /// 제자리 점프 시 뒤 고양이들이 머리 위로 올라오지 않게 한다.
    /// </summary>
    Vector3 TrailPoint()
    {
        Vector3 p = transform.position;
        p.y = Mathf.Min(p.y, groundY);
        return p;
    }

    /// <summary>
    /// 겹침 방지: 주인공 몸과 앞쪽 고양이들로부터 최소 거리만큼 수평으로 밀어낸다.
    /// (뒤로 돌거나 제자리에서 맴돌 때 경로가 접혀 고양이가 몸에 파묻히는 문제)
    /// </summary>
    Vector3 Separate(Vector3 pos, int index)
    {
        const float minFromPlayer = 0.7f;
        const float minFromKitten = 0.45f;
        for (int pass = 0; pass < 3; pass++)
        {
            for (int j = 0; j < index; j++)
                if (followers[j]) pos = PushOut(pos, followers[j].transform.position, minFromKitten);
            // 바로 뒤 2마리만 주인공을 비켜 줌(급회전 시 몸에 파묻히지 않게).
            // 나머지 행렬은 비켜 주지 않고 몸 콜라이더로 주인공을 막아서, 행렬 사이로 지나갈 수 없다.
            if (index < 2) pos = PushOut(pos, transform.position, minFromPlayer);
        }
        return pos;
    }

    static readonly Collider[] overlapBuf = new Collider[16];

    /// <summary>
    /// 매대·상자·건물·분수 같은 고정 물체 안으로 파묻히지 않게 수평으로 밀어낸다.
    /// (행렬은 경로를 따라 순간 이동하므로 물리 충돌이 없어서 모서리를 질러가다 파묻힘)
    /// </summary>
    Vector3 PushOutOfWorld(Follower f, Vector3 pos)
    {
        var body = f.body;
        if (!body || f.transform.localScale.x < 0.5f) return pos;
        for (int iter = 0; iter < 2; iter++)
        {
            int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 0.35f, 0.32f, overlapBuf, ~(1 << 2), QueryTriggerInteraction.Ignore);
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                var c = overlapBuf[i];
                if (!c || c == body) continue;
                if (c.attachedRigidbody) continue;   // 카트·빗자루·굴러다니는 음식·무너진 상자 등 움직이는 물체는 제외
                if (Physics.ComputePenetration(body, pos, f.transform.rotation, c, c.transform.position, c.transform.rotation, out var dir, out var dist))
                {
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 1e-4f) continue;   // 바닥(위로 밀어내는 경우)은 무시
                    pos += dir.normalized * dist;
                    moved = true;
                }
            }
            if (!moved) break;
        }
        return pos;
    }

    static Vector3 PushOut(Vector3 pos, Vector3 other, float minDist)
    {
        Vector3 d = pos - other; d.y = 0f;
        float m = d.magnitude;
        if (m >= minDist) return pos;
        if (m < 0.001f) d = Vector3.back; else d /= m;
        Vector3 p = other + d * minDist;
        p.y = pos.y;
        return p;
    }

    /// <summary>점프하면 행렬도 앞에서부터 차례로 콩 뛰어오름 (제자리에서).</summary>
    IEnumerator HopWave()
    {
        for (int i = 0; i < followers.Count; i++)
        {
            yield return new WaitForSeconds(0.05f);
            if (i < followers.Count && followers[i]) followers[i].visual.Hop();
        }
    }

    void RecordPath()
    {
        Vector3 p = TrailPoint();
        if ((p - path[0]).sqrMagnitude < PathStep * PathStep) return;
        path.Insert(0, p);
        int max = Mathf.CeilToInt((firstGap + spacing * (followers.Count + 2)) / PathStep) + 10;
        if (path.Count > max) path.RemoveRange(max, path.Count - max);
    }

    Vector3 SampleTrail(float dist, out Vector3 dir)
    {
        Vector3 prev = TrailPoint();
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 cur = path[i];
            float seg = Vector3.Distance(prev, cur);
            if (seg >= dist && seg > 1e-4f)
            {
                dir = (prev - cur) / seg;
                return Vector3.Lerp(prev, cur, dist / seg);
            }
            dist -= seg;
            prev = cur;
        }
        // 기록된 경로보다 뒤쪽: 바라보는 방향의 반대로 일렬 배치
        dir = Quaternion.Euler(0f, visual.yaw, 0f) * Vector3.forward;
        return prev - dir * dist;
    }

    void UpdateFollowers(float speed01)
    {
        float k = 1f - Mathf.Exp(-18f * Time.deltaTime);
        for (int i = 0; i < followers.Count; i++)
        {
            var f = followers[i];
            if (!f) continue;
            Vector3 target = SampleTrail(firstGap + spacing * i, out var dir);
            Vector3 pos = Vector3.Lerp(f.transform.position, target, k);
            pos = Separate(pos, i);
            // 목표 지점과 멀리 떨어져 있으면(빗자루에 쓸림·리스폰 등) 벽에 걸리지 않게 통과해서 따라잡게 둔다
            if ((target - pos).sqrMagnitude < 1.5f * 1.5f) pos = PushOutOfWorld(f, pos);
            f.transform.position = pos;
            if (dir.sqrMagnitude > 0.001f)
                f.visual.yaw = Mathf.MoveTowardsAngle(f.visual.yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 720f * Time.deltaTime);
            f.visual.moveSpeed = speed01;
        }
    }

    /// <summary>음식을 먹으면 행렬 맨 뒤에 새끼 고양이 생성.</summary>
    public Follower AddFollower(int cm = 0, FoodType sourceType = FoodType.Toast)
    {
        int idx = followers.Count;
        Vector3 pos = SampleTrail(firstGap + spacing * idx, out _);
        var f = Follower.Create(pos, Palette.KittenColorFor(sourceType), followerScale);
        f.cm = cm;
        f.sourceType = sourceType;
        if (sourceType == FoodType.LastPiece) f.UseRainbowModel();   // 마지막 1CM = 무지개 젤리 고양이
        f.visual.yaw = visual.yaw;
        followers.Add(f);
        f.Emote(0);
        return f;
    }

    // ---------------- Emote ----------------
    public void Emote(int id)
    {
        visual.PlayEmote(id);
        StartCoroutine(EmoteWave(id));
    }

    IEnumerator EmoteWave(int id)
    {
        yield return new WaitForSeconds(0.15f);
        for (int i = 0; i < followers.Count; i++)
        {
            if (followers[i]) followers[i].Emote(id);
            yield return new WaitForSeconds(0.06f);
        }
    }

    public void StartCelebration()
    {
        if (celebrate != null) StopCoroutine(celebrate);
        celebrate = StartCoroutine(CelebrateLoop());
    }

    IEnumerator CelebrateLoop()
    {
        int n = 0;
        while (true)
        {
            Emote(n++ % 2 == 0 ? 3 : 0);
            yield return new WaitForSeconds(1.4f);
        }
    }

    void Respawn()
    {
        transform.position = spawnPos;
        rb.linearVelocity = Vector3.zero;
        ResetPath();
        for (int i = 0; i < followers.Count; i++)
            if (followers[i]) followers[i].transform.position = SampleTrail(firstGap + spacing * i, out _);
    }

    // ---------------- Exit ----------------
    void OnTriggerEnter(Collider other) => HandleTrigger(other);
    void OnTriggerStay(Collider other) => HandleTrigger(other);

    void HandleTrigger(Collider other)
    {
        if (other.GetComponent<ExitZone>() && GameManager.I) GameManager.I.TryExit();
    }
}
