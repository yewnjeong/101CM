using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 3인칭 추적 카메라. 마우스 이동으로 시점 회전 (커서 잠금).
/// Esc → 커서 해제(메뉴는 추후), 화면 클릭 → 다시 잠금.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float pitch = 40f;
    public float yaw = 0f;
    public float distance = 7.5f;
    public float minPitch = 10f;
    public float maxPitch = 70f;
    public float mouseSensitivity = 0.12f;
    public float smooth = 0.08f;
    public float lookHeight = 0.8f;

    [Header("시작 화면 (섬 전체를 천천히 도는 카메라)")]
    public Vector3 titleFocus = new Vector3(0f, 1f, 0f);
    public float titleDistance = 30f;
    public float titlePitch = 32f;
    public float titleOrbitSpeed = 6f;   // 초당 회전 각도

    static CameraFollow inst;
    float titleBlend = -1f;   // 1 = 시작 화면 시점, 0 = 플레이 시점
    float titleYaw;
    float shake;
    Vector3 vel, focus;
    bool focusInit;

    public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

    public static void Shake(float amount)
    {
        if (inst) inst.shake = Mathf.Max(inst.shake, amount);
    }

    public static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Awake() => inst = this;

    void Start()
    {
        bool title = GameManager.I && GameManager.I.OnTitle;
        SetCursorLocked(!title);
    }

    void OnDisable() => SetCursorLocked(false);

    void LateUpdate()
    {
        if (!target) return;
        float dt = Time.deltaTime;

        bool onTitle = GameManager.I && GameManager.I.OnTitle;
        if (titleBlend < 0f) { titleBlend = onTitle ? 1f : 0f; titleYaw = yaw + 180f; }
        titleBlend = Mathf.MoveTowards(titleBlend, onTitle ? 1f : 0f, Time.unscaledDeltaTime / 1.2f);   // 시작하면 1.2초 동안 고양이 쪽으로 내려옴
        if (titleBlend > 0f) titleYaw += titleOrbitSpeed * Time.unscaledDeltaTime;

        var m = Mouse.current;
        // Esc 메뉴는 GameManager가 처리. 메뉴 밖에서 커서가 풀렸으면(창 전환 등) 클릭으로 다시 잠금
        bool inMenu = GameManager.I && (GameManager.I.IsPaused || GameManager.I.IsWon);
        if (m != null && !CursorLocked && !inMenu && m.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        if (m != null && CursorLocked && !onTitle)
        {
            Vector2 d = m.delta.ReadValue();
            yaw += d.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - d.y * mouseSensitivity, minPitch, maxPitch);
        }

        float k = Mathf.SmoothStep(0f, 1f, titleBlend);
        var rot = Quaternion.Euler(Mathf.Lerp(pitch, titlePitch, k), Mathf.LerpAngle(yaw, titleYaw, k), 0f);
        Vector3 focusTarget = target.position + Vector3.up * lookHeight;
        if (!focusInit) { focus = focusTarget; focusInit = true; }
        focus = Vector3.SmoothDamp(focus, focusTarget, ref vel, smooth);
        Vector3 f = Vector3.Lerp(focus, titleFocus, k);

        transform.position = f - rot * Vector3.forward * Mathf.Lerp(distance, titleDistance, k);
        transform.rotation = rot;

        if (shake > 0f)
        {
            transform.position += Random.insideUnitSphere * shake * 0.4f;
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
        }
    }
}
