using UnityEngine;

/// <summary>
/// 뼈 회전 각도 제한. 애니메이션/코드가 회전시킨 뒤(LateUpdate) 기본 자세 기준 maxAngle 이내로 되돌린다.
/// 머리를 크게 돌리면 팔 쪽 몸이 같이 끌려가는 문제를 막기 위해 Head 뼈에 사용 (기본 5도).
/// </summary>
[DefaultExecutionOrder(1000)]
public class BoneRotationLimit : MonoBehaviour
{
    public Transform bone;
    [Range(0f, 90f)] public float maxAngle = 5f;
    Quaternion rest;
    bool init;

    void Awake() { if (!bone) bone = transform; rest = bone.localRotation; init = true; }

    void LateUpdate()
    {
        if (!init || !bone) return;
        Quaternion delta = Quaternion.Inverse(rest) * bone.localRotation;
        delta.ToAngleAxis(out float ang, out Vector3 axis);
        if (ang > 180f) ang -= 360f;
        if (Mathf.Abs(ang) > maxAngle)
            bone.localRotation = rest * Quaternion.AngleAxis(Mathf.Sign(ang) * maxAngle, axis);
    }
}
