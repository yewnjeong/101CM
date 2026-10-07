using UnityEngine;

namespace CM101
{
    public enum EmoteType { None, Wave, Spin, Hop, Bow }

    /// <summary>
    /// 기분 표현 모션. 곰 외형(Visual)의 팔·몸통에 절차적 애니메이션을 덧입힌다.
    /// 이동/방향 계산은 LeaderController·JellyUnit이 Update에서 하고, 이 컴포넌트가
    /// 프레임 시작(-300)에 기준 자세로 되돌린 뒤 LateUpdate에서 모션을 얹는다 → 방향 계산이 오염되지 않는다.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class JellyEmote : MonoBehaviour
    {
        public EmoteType Current { get; private set; }
        public float Elapsed => t;

        Transform visual, armL, armR, body, ring;
        // 리깅된 모델(Visual/JellyModel)의 팔 뼈. 있으면 뼈를 돌리고, 없으면 예전 팔 구를 움직인다
        Transform model, boneArmL, boneArmR;
        Quaternion boneArmLRest, boneArmRRest;
        // 걷기: 움직이는 동안 팔·다리를 꼼질꼼질 (리깅 모델만)
        Transform boneLegL, boneLegR;
        Quaternion boneLegLRest, boneLegRRest;
        Vector3 lastPos;
        bool hasLastPos;
        float moveAmt, walkPhase;
        [Tooltip("이 속도(u/s)에서 걷기 동작이 가장 크다")] public float walkFullSpeed = 2.0f;
        Vector3 ringRest;
        Quaternion ringRestRot;
        Vector3 armLRest, armRRest;
        Quaternion armLRestRot, armRRestRot;
        float size = 0.5f;
        /// <summary>0보다 크면 몸 크기로 쓴다(리깅 동료는 Body 구가 없어서 JellyUnit이 넣어 준다).</summary>
        [System.NonSerialized] public float sizeOverride;
        bool ready;

        EmoteType queued;
        float queueDelay;
        float t;

        bool applied;
        Quaternion baseRot;

        public static float Duration(EmoteType e)
        {
            switch (e)
            {
                case EmoteType.Wave: return 1.6f;
                case EmoteType.Spin: return 1.1f;
                case EmoteType.Hop: return 1.2f;
                case EmoteType.Bow: return 1.2f;
                default: return 0f;
            }
        }

        public static string Label(EmoteType e)
        {
            switch (e)
            {
                case EmoteType.Wave: return "안녕!";
                case EmoteType.Spin: return "♥";
                case EmoteType.Hop: return "♪";
                case EmoteType.Bow: return "꾸벅";
                default: return "";
            }
        }

        void Setup()
        {
            if (ready) return;
            visual = transform.Find("Visual");
            if (!visual) return;
            armL = visual.Find("ArmL");
            armR = visual.Find("ArmR");
            body = visual.Find("Body");
            ring = visual.Find("LeaderRing");
            model = visual.Find("JellyModel");
            if (model) { boneArmL = FindDeep(model, "Arm.L"); boneArmR = FindDeep(model, "Arm.R"); }
            if (boneArmL) boneArmLRest = boneArmL.localRotation;
            if (boneArmR) boneArmRRest = boneArmR.localRotation;
            if (model) { boneLegL = FindDeep(model, "Leg.L"); boneLegR = FindDeep(model, "Leg.R"); }
            if (boneLegL) boneLegLRest = boneLegL.localRotation;
            if (boneLegR) boneLegRRest = boneLegR.localRotation;
            if (ring) { ringRest = ring.localPosition; ringRestRot = ring.localRotation; }
            if (armL) { armLRest = armL.localPosition; armLRestRot = armL.localRotation; }
            if (armR) { armRRest = armR.localPosition; armRRestRot = armR.localRotation; }
            if (body) size = body.localScale.x / 0.78f; // BuildBear: Body x = 0.78s
            if (sizeOverride > 0f) size = sizeOverride;
            ready = true;
        }

        float flatT = -1f;
        float flatDur = 1.4f;
        public bool IsFlat => flatT >= 0f;

        /// <summary>직원에게 밟혔을 때 납작해지는 모션.</summary>
        public void PlayFlatten(float duration = 1.4f)
        {
            Setup();
            if (!ready) return;
            flatT = 0f;
            flatDur = Mathf.Max(0.6f, duration);
            Current = EmoteType.None;
            queued = EmoteType.None;
        }

        float hop1T = -1f, hop1Delay, hop1H;
        const float Hop1Dur = 0.42f;

        /// <summary>
        /// 제자리 점프 따라 하기: 몸(물리)은 그대로 두고 모습만 한 번 폴짝 뛴다. 줄 순서와 간격이 흐트러지지 않는다.
        /// </summary>
        public void PlayHopOnce(float delay, float height)
        {
            Setup();
            if (!ready || IsFlat) return;
            hop1T = 0f;
            hop1Delay = delay;
            hop1H = height;
        }

        public void Play(EmoteType e, float delay = 0f)
        {
            Setup();
            if (!ready || IsFlat) return;
            if (delay <= 0f) { Current = e; t = 0f; queued = EmoteType.None; }
            else { queued = e; queueDelay = delay; }
        }

        void Update()
        {
            // 프레임 시작: 지난 프레임에 얹은 모션을 걷어내고 기준 자세로 되돌린다
            if (!applied || !visual) return;
            visual.rotation = baseRot;
            visual.localPosition = Vector3.zero;
            if (armL) { armL.localPosition = armLRest; armL.localRotation = armLRestRot; }
            if (armR) { armR.localPosition = armRRest; armR.localRotation = armRRestRot; }
            if (ring) { ring.localPosition = ringRest; ring.localRotation = ringRestRot; }
            if (boneArmL) boneArmL.localRotation = boneArmLRest;
            if (boneArmR) boneArmR.localRotation = boneArmRRest;
            applied = false;
        }

        void LateUpdate()
        {
            if (!ready) { Setup(); if (!ready) return; }
            Walk(Time.deltaTime);

            float dt = Time.deltaTime;
            if (queued != EmoteType.None)
            {
                queueDelay -= dt;
                if (queueDelay <= 0f) { Current = queued; queued = EmoteType.None; t = 0f; }
            }
            bool flat = flatT >= 0f;
            if (flat) { Current = EmoteType.None; hop1T = -1f; } // 납작한 동안은 기분 표현을 하지 않는다
            bool hop1 = hop1T >= 0f;
            if (Current == EmoteType.None && !flat && !hop1) return;

            float s = size;
            baseRot = visual.rotation;
            applied = true;
            Quaternion offset = Quaternion.identity;
            float hop = 0f;
            float liftL = 0f, liftR = 0f; // 팔 뼈를 드는 각도(도). T자 기본 자세에서 위로

            if (Current != EmoteType.None)
            {
                t += dt;
                float d = Duration(Current);
                float k = Mathf.Clamp01(t / d);

                // 팔을 드는 정도: 앞 0.2초 올리고 뒤 0.2초 내린다
                float raise = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((d - t) / 0.2f);
                Vector3 upR = new Vector3(0.36f, 0.42f, 0.06f) * s;
                Vector3 upL = new Vector3(-0.36f, 0.42f, 0.06f) * s;

                switch (Current)
                {
                    case EmoteType.Wave:
                        if (armR)
                        {
                            armR.localPosition = Vector3.Lerp(armRRest, upR, raise);
                            armR.localRotation = armRRestRot * Quaternion.Euler(0f, 0f, raise * (25f + Mathf.Sin(t * 14f) * 30f));
                        }
                        liftR = raise * (70f + Mathf.Sin(t * 14f) * 25f);
                        offset = Quaternion.Euler(0f, 0f, -7f * raise);
                        break;

                    case EmoteType.Spin:
                    {
                        float ang = Mathf.SmoothStep(0f, 720f, k);
                        offset = Quaternion.Euler(0f, ang, 0f);
                        hop = Mathf.Sin(k * Mathf.PI) * 0.22f * s;
                        if (armR) armR.localPosition = Vector3.Lerp(armRRest, upR, raise);
                        if (armL) armL.localPosition = Vector3.Lerp(armLRest, upL, raise);
                        liftL = liftR = raise * 60f;
                        break;
                    }

                    case EmoteType.Hop:
                    {
                        float ph = k * Mathf.PI * 3f;
                        hop = Mathf.Abs(Mathf.Sin(ph)) * 0.35f * s;
                        float arms = Mathf.Abs(Mathf.Sin(ph)) * raise;
                        if (armR) armR.localPosition = Vector3.Lerp(armRRest, upR, arms * 0.7f);
                        if (armL) armL.localPosition = Vector3.Lerp(armLRest, upL, arms * 0.7f);
                        liftL = liftR = arms * 75f;
                        break;
                    }

                    case EmoteType.Bow:
                    {
                        float bow = Mathf.Clamp01(Mathf.Sin(k * Mathf.PI) * 1.4f);
                        offset = Quaternion.Euler(38f * bow, 0f, 0f);
                        hop = -0.05f * s * bow;
                        break;
                    }
                }
                if (t >= d) Current = EmoteType.None;
            }

            if (hop1)
            {
                if (hop1Delay > 0f) hop1Delay -= dt;
                else
                {
                    hop1T += dt;
                    float k = Mathf.Clamp01(hop1T / Hop1Dur);
                    hop += 4f * k * (1f - k) * hop1H; // 포물선 한 번
                    if (hop1T >= Hop1Dur) hop1T = -1f;
                }
            }

            if (flat)
            {
                // 밟힘: 순간 납작 → 납작한 채 부들부들 → 마지막에 뾰잉 하고 원래대로
                flatT += dt;
                const float squashIn = 0.06f, recover = 0.45f, flatY = 0.18f;
                float sy;
                if (flatT < squashIn) sy = Mathf.Lerp(1f, flatY, flatT / squashIn);
                else if (flatT < flatDur - recover) sy = flatY + Mathf.Sin(flatT * 20f) * 0.02f;
                else
                {
                    float r = Mathf.Clamp01((flatT - (flatDur - recover)) / recover);
                    sy = Mathf.Lerp(flatY, 1f, r) + Mathf.Sin(r * Mathf.PI * 2.5f) * (1f - r) * 0.35f;
                }
                sy = Mathf.Max(0.12f, sy);
                float sxz = 1f + Mathf.Clamp01(1f - sy) * 0.75f;
                visual.localScale = Vector3.Scale(visual.localScale, new Vector3(sxz, sy, sxz));
                hop += -0.5f * s * (1f - sy); // 발바닥이 바닥에 붙어 있게
                if (flatT >= flatDur) flatT = -1f;
            }

            visual.rotation = baseRot * offset;
            visual.localPosition = Vector3.up * hop;
            LiftArm(boneArmR, boneArmRRest, liftR, 1f);
            LiftArm(boneArmL, boneArmLRest, liftL, -1f);
            if (ring)
            {
                // 선두 링은 바닥에 평평하게 남긴다(기울거나 뛰어도 바닥을 파고들지 않게)
                ring.rotation = Quaternion.Euler(0f, baseRot.eulerAngles.y, 0f);
                ring.position = transform.position + Vector3.down * (0.49f * s);
            }
        }

        /// <summary>팔 뼈를 모델 정면(Z)축으로 돌려 든다. side +1 = 오른팔(+X), -1 = 왼팔(-X).</summary>
        void LiftArm(Transform bone, Quaternion restLocal, float deg, float side)
        {
            if (!bone || !model || Mathf.Abs(deg) < 0.01f) return;
            Quaternion restWorld = bone.rotation; // 걷기 자세 위에 얹는다
            Quaternion lift = model.rotation * Quaternion.AngleAxis(side * deg, Vector3.forward) * Quaternion.Inverse(model.rotation);
            bone.rotation = lift * restWorld;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindDeep(root.GetChild(i), name);
                if (f) return f;
            }
            return null;
        }

        /// <summary>
        /// 움직이는 동안 팔·다리를 번갈아 흔든다. 다리 = 모델 X축으로 앞뒤, 팔 = Y축으로 앞뒤 + Z축으로 살짝 파닥.
        /// 매 프레임 기본 자세에서 다시 계산하므로 쌓이지 않는다. 기분 표현의 팔 들기는 이 위에 얹힌다.
        /// </summary>
        void Walk(float dt)
        {
            if (!model || (!boneArmL && !boneLegL)) return;
            Vector3 p = transform.position;
            float spd = 0f;
            if (hasLastPos && dt > 0f) { Vector3 d = p - lastPos; d.y = 0f; spd = d.magnitude / dt; }
            lastPos = p; hasLastPos = true;
            if (IsFlat) spd = 0f;
            if (spd < 0.3f) spd = 0f; // 제자리 미세 떨림으로 팔다리가 꼼질거리지 않게 (2026-10-07)
            moveAmt = Mathf.MoveTowards(moveAmt, Mathf.Clamp01(spd / Mathf.Max(0.1f, walkFullSpeed)), dt * 5f);
            if (moveAmt > 0.001f) walkPhase += dt * Mathf.Lerp(6f, 14f, moveAmt);
            float a = moveAmt;
            float s = Mathf.Sin(walkPhase), c = Mathf.Cos(walkPhase * 2f);
            SetLimb(boneLegL, boneLegLRest, Vector3.right, 28f * a * s, Vector3.forward, 0f);
            SetLimb(boneLegR, boneLegRRest, Vector3.right, -28f * a * s, Vector3.forward, 0f);
            SetLimb(boneArmR, boneArmRRest, Vector3.up, 22f * a * s, Vector3.forward, 10f * a * c);
            SetLimb(boneArmL, boneArmLRest, Vector3.up, 22f * a * s, Vector3.forward, -10f * a * c);
        }

        void SetLimb(Transform b, Quaternion restLocal, Vector3 axis1, float deg1, Vector3 axis2, float deg2)
        {
            if (!b) return;
            Quaternion local = Quaternion.AngleAxis(deg2, axis2) * Quaternion.AngleAxis(deg1, axis1);
            Quaternion restWorld = b.parent.rotation * restLocal;
            b.rotation = model.rotation * local * Quaternion.Inverse(model.rotation) * restWorld;
        }
    }
}
