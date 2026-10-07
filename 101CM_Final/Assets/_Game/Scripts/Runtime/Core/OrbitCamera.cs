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

        float curDist;
        float ceilSmooth;
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

            // 테이블/팔레트 아래처럼 천장이 낮으면 시점을 낮추고 가까이 붙인다
            float ph = pivotHeight;
            float usePitch = pitch;
            bool lowCeiling = Physics.SphereCast(target.transform.position, collisionRadius * 0.8f, Vector3.up, out var up,
                pivotHeight + 1.2f, Layers.EnvMask, QueryTriggerInteraction.Ignore);
            float ceilT = lowCeiling ? Mathf.InverseLerp(1.4f, 0.5f, up.distance) : 0f; // 1 = 아주 낮은 천장
            ceilSmooth = Mathf.Lerp(ceilSmooth, ceilT, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            if (lowCeiling) ph = Mathf.Min(ph, Mathf.Max(0.05f, up.distance - collisionRadius - 0.05f));
            usePitch = Mathf.Lerp(pitch, Mathf.Min(pitch, 8f), ceilSmooth);

            Vector3 pivot = target.transform.position + Vector3.up * ph;
            Vector3 back = Quaternion.Euler(usePitch, yaw, 0f) * Vector3.back;
            float dist = curDist;
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, curDist, Layers.EnvMask, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.4f, hit.distance - 0.05f);

            transform.position = pivot + back * dist;
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }
    }
}
