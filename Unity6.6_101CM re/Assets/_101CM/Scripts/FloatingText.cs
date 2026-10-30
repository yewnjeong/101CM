using UnityEngine;

/// <summary>"+5cm", "우당탕!" 같이 떠오르며 사라지는 텍스트.</summary>
public class FloatingText : MonoBehaviour
{
    TextMesh tm;
    Color baseColor;
    float age;
    const float Life = 1.1f;

    public static void Spawn(Vector3 pos, string text, Color color, float height = 0.5f)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = pos;
        go.AddComponent<Billboard>();
        var ft = go.AddComponent<FloatingText>();
        ft.tm = Palette.Text(go.transform, text, Vector3.zero, height, color);
        ft.baseColor = color;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += Vector3.up * Time.deltaTime * 1.4f;
        var c = baseColor;
        c.a = 1f - Mathf.Clamp01((age - 0.6f) / (Life - 0.6f));
        tm.color = c;
        float s = age < 0.15f ? Mathf.Lerp(0.5f, 1.15f, age / 0.15f) : Mathf.Lerp(1.15f, 1f, (age - 0.15f) * 4f);
        transform.localScale = Vector3.one * s;
        if (age > Life) Destroy(gameObject);
    }
}
