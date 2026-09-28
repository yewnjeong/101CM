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
        Vector3 ringRest;
        Quaternion ringRestRot;
        Vector3 armLRest, armRRest;
        Quaternion armLRestRot, armRRestRot;
        float size = 0.5f;
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
            if (ring) { ringRest = ring.localPosition; ringRestRot = ring.localRotation; }
            if (armL) { armLRest = armL.localPosition; armLRestRot = armL.localRotation; }
            if (armR) { armRRest = armR.localPosition; armRRestRot = armR.localRotation; }
            if (body) size = body.localScale.x / 0.78f; // BuildBear: Body x = 0.78s
            ready = true;
        }

        public void Play(EmoteType e, float delay = 0f)
        {
            Setup();
            if (!ready) return;
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
            applied = false;
        }

        void LateUpdate()
        {
            if (!ready) { Setup(); if (!ready) return; }

            float dt = Time.deltaTime;
            if (queued != EmoteType.None)
            {
                queueDelay -= dt;
                if (queueDelay <= 0f) { Current = queued; queued = EmoteType.None; t = 0f; }
            }
            if (Current == EmoteType.None) return;

            t += dt;
            float d = Duration(Current);
            float k = Mathf.Clamp01(t / d);
            float s = size;

            baseRot = visual.rotation;
            applied = true;

            // 팔을 드는 정도: 앞 0.2초 올리고 뒤 0.2초 내린다
            float raise = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((d - t) / 0.2f);
            Vector3 upR = new Vector3(0.36f, 0.42f, 0.06f) * s;
            Vector3 upL = new Vector3(-0.36f, 0.42f, 0.06f) * s;
            Quaternion offset = Quaternion.identity;
            float hop = 0f;

            switch (Current)
            {
                case EmoteType.Wave:
                    if (armR)
                    {
                        armR.localPosition = Vector3.Lerp(armRRest, upR, raise);
                        armR.localRotation = armRRestRot * Quaternion.Euler(0f, 0f, raise * (25f + Mathf.Sin(t * 14f) * 30f));
                    }
                    offset = Quaternion.Euler(0f, 0f, -7f * raise);
                    break;

                case EmoteType.Spin:
                {
                    float ang = Mathf.SmoothStep(0f, 720f, k);
                    offset = Quaternion.Euler(0f, ang, 0f);
                    hop = Mathf.Sin(k * Mathf.PI) * 0.22f * s;
                    if (armR) armR.localPosition = Vector3.Lerp(armRRest, upR, raise);
                    if (armL) armL.localPosition = Vector3.Lerp(armLRest, upL, raise);
                    break;
                }

                case EmoteType.Hop:
                {
                    float ph = k * Mathf.PI * 3f;
                    hop = Mathf.Abs(Mathf.Sin(ph)) * 0.35f * s;
                    float arms = Mathf.Abs(Mathf.Sin(ph)) * raise;
                    if (armR) armR.localPosition = Vector3.Lerp(armRRest, upR, arms * 0.7f);
                    if (armL) armL.localPosition = Vector3.Lerp(armLRest, upL, arms * 0.7f);
                    break;
                }

                case EmoteType.Bow:
                {
                    float bow = Mathf.Sin(k * Mathf.PI);
                    bow = Mathf.Clamp01(bow * 1.4f);
                    offset = Quaternion.Euler(38f * bow, 0f, 0f);
                    hop = -0.05f * s * bow;
                    break;
                }
            }

            visual.rotation = baseRot * offset;
            visual.localPosition = Vector3.up * hop;
            if (ring)
            {
                // 선두 링은 바닥에 평평하게 남긴다(기울거나 뛰어도 바닥을 파고들지 않게)
                ring.rotation = Quaternion.Euler(0f, baseRot.eulerAngles.y, 0f);
                ring.position = transform.position + Vector3.down * (0.49f * s);
            }

            if (t >= d) Current = EmoteType.None;
        }
    }
}
