using System.Collections;
using UnityEngine;

/// <summary>101CM 달성 시 열리는 출구 게이트.</summary>
public class ExitGate : MonoBehaviour
{
    public Transform bars;
    public Collider blocker;
    public Renderer[] glowRenderers;
    public Light gateLight;
    bool open;

    public bool IsOpen => open;

    public void Open()
    {
        if (open) return;
        open = true;
        if (blocker) blocker.enabled = false;
        if (gateLight) gateLight.enabled = true;
        if (glowRenderers != null)
            foreach (var r in glowRenderers)
                if (r) r.sharedMaterial = Palette.Get(new Color(1f, 0.85f, 0.35f), 2f);
        CameraFollow.Shake(0.2f);
        StartCoroutine(Lower());
    }

    IEnumerator Lower()
    {
        if (!bars) yield break;
        Vector3 a = bars.localPosition, b = a + Vector3.down * 3.2f;
        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.deltaTime;
            bars.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t / 1.2f));
            yield return null;
        }
        bars.gameObject.SetActive(false);
    }
}
