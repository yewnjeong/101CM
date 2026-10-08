using UnityEngine;
using UnityEngine.InputSystem;

namespace CM101
{
    /// <summary>
    /// 선두 뒤 3인칭 시점. 마우스로 언제든 시점을 돌려 꼬리를 확인한다.
    /// 길이가 늘면 거리를 완만하게 늘리되 최대 거리를 둔다. 벽/선반에 가리면 충돌 보정.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public LeaderController target;
        public float sensitivity = 0.12f;
        public float yaw;
        public float pitch = 28f;
        public float minPitch = 5f, maxPitch = 75f;
        public float baseDistance = 2.0f;    // 2026-10-07: 더 가깝게 (3.2 → 2.45 → 2.0)
        public float distancePerCm = 0.031f;  // 0.05 → 0.0385 → 0.031
        public float maxDistance = 4.7f;     // 7.5 → 5.8 → 4.7
        public float pivotHeight = 0.55f;
        public float collisionRadius = 0.2f;
        [Tooltip("천장이 가까울 때 거리를 줄이기 전에 시점을 이 각도까지 눕힌다")] public float roofMinPitch = 15f;

        float curDist;
        float ceilSmooth;
        float roofSmooth = 90f;
        float followY;
        bool hasFollowY, jumpHold;
        public float Yaw => yaw;

        void Start()
        {
            curDist = baseDistance;
            if (target) yaw = target.transform.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (!target) return;

            if (GameManager.IsPlaying && Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = Mouse.current.delta.ReadValue();
                yaw += d.x * sensitivity;
                pitch = Mathf.Clamp(pitch - d.y * sensitivity, minPitch, maxPitch);
            }

            int len = JellyChain.I ? JellyChain.I.CurrentLength : JellyChain.LeaderLength;
            float want = Mathf.Min(maxDistance, baseDistance + (len - JellyChain.LeaderLength) * distancePerCm);
            curDist = Mathf.Lerp(curDist, want, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));

            // 점프 높이 따라가기 (2026-10-08, 천장 3.9): 진열대 위처럼 천장이 가까운 곳에서 점프하면
            // 카메라가 따라 올라가다 천장에 부딪혀 확 당겨졌다 → 그런 곳에서는 올라가는 동안 카메라 높이를 붙잡아 둔다.
            // 바닥처럼 여유가 있는 곳은 예전처럼 그대로 따라간다.
            Vector3 tp = target.transform.position;
            float cdt = Time.unscaledDeltaTime;
            if (!hasFollowY) { followY = tp.y; hasFollowY = true; }
            if (target.IsGrounded)
            {
                float clear = Physics.SphereCast(tp + Vector3.up * pivotHeight, collisionRadius, Vector3.up, out var rh, 8f,
                    Layers.EnvMask, QueryTriggerInteraction.Ignore) ? rh.distance : 99f;
                jumpHold = clear - target.jumpHeight < curDist * Mathf.Sin(pitch * Mathf.Deg2Rad) + 0.1f;
            }
            bool rising = !target.IsGrounded && tp.y > followY;
            if (rising && jumpHold) { /* 붙잡아 둔다 */ }
            else if (Mathf.Abs(tp.y - followY) < 0.005f) followY = tp.y;
            else followY = Mathf.Lerp(followY, tp.y, 1f - Mathf.Exp(-(tp.y < followY ? 20f : 10f) * cdt));
            Vector3 basePos = new Vector3(tp.x, followY, tp.z);

            // 테이블/팔레트 아래처럼 천장이 낮으면 시점을 낮추고 가까이 붙인다
            float ph = pivotHeight;
            float usePitch = pitch;
            bool lowCeiling = Physics.SphereCast(basePos, collisionRadius * 0.8f, Vector3.up, out var up,
                pivotHeight + 1.2f, Layers.EnvMask, QueryTriggerInteraction.Ignore);
            float ceilT = lowCeiling ? Mathf.InverseLerp(1.4f, 0.5f, up.distance) : 0f; // 1 = 아주 낮은 천장
            ceilSmooth = Mathf.Lerp(ceilSmooth, ceilT, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            if (lowCeiling) ph = Mathf.Min(ph, Mathf.Max(0.05f, up.distance - collisionRadius - 0.05f));
            usePitch = Mathf.Lerp(pitch, Mathf.Min(pitch, 8f), ceilSmooth);

            // 천장(3.9)이 가까울 때(2.0 진열대 위 등): 카메라를 확 당기는 대신 시점을 조금 눕혀 거리를 지킨다 (2026-10-08)
            float roofPitch = 90f;
            Vector3 pv = basePos + Vector3.up * ph;
            if (Physics.SphereCast(pv, collisionRadius, Vector3.up, out var roof, 6f, Layers.EnvMask, QueryTriggerInteraction.Ignore))
            {
                float clear = roof.distance - 0.05f;
                if (clear < curDist)
                    roofPitch = Mathf.Max(roofMinPitch, Mathf.Asin(Mathf.Clamp01(clear / Mathf.Max(0.01f, curDist))) * Mathf.Rad2Deg);
            }
            roofSmooth = Mathf.Lerp(roofSmooth, roofPitch, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            usePitch = Mathf.Min(usePitch, roofSmooth);

            Vector3 pivot = basePos + Vector3.up * ph;
            Vector3 back = Quaternion.Euler(usePitch, yaw, 0f) * Vector3.back;
            float dist = curDist;
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, curDist, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.4f, hit.distance - 0.05f);

            transform.position = pivot + back * dist;
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }
    }
}
