using UnityEngine;

/// <summary>머리 위 말풍선 감정표현 (♥ ! ? ♪).</summary>
public class EmoteBubble : MonoBehaviour
{
    public static readonly string[] Symbols = { "♥", "!", "?", "♪" };
    public static readonly Color[] SymbolColors =
    {
        new Color(1f, 0.35f, 0.5f),
        new Color(1f, 0.55f, 0.1f),
        new Color(0.3f, 0.55f, 1f),
        new Color(0.6f, 0.35f, 0.95f),
    };

    float age, life = 1.4f, scale = 1f;

    public static EmoteBubble Show(Transform target, int id, float height, float scale = 1f) =>
        Show(target, Symbols[id], SymbolColors[id], height, scale);

    public static EmoteBubble Show(Transform target, string symbol, Color color, float height, float scale = 1f)
    {
        var old = target.Find("EmoteBubble");
        if (old) Destroy(old.gameObject);

        var go = new GameObject("EmoteBubble");
        go.transform.SetParent(target, false);
        go.transform.localPosition = Vector3.up * height;
        go.AddComponent<Billboard>();
        var b = go.AddComponent<EmoteBubble>();
        b.scale = scale;

        Palette.Shape(PrimitiveType.Sphere, go.transform, Vector3.zero, new Vector3(0.75f, 0.65f, 0.08f), Color.white, emission: 0.5f, name: "Back");
        Palette.Shape(PrimitiveType.Sphere, go.transform, new Vector3(0.12f, -0.33f, 0f), new Vector3(0.18f, 0.18f, 0.06f), Color.white, emission: 0.5f, name: "Tail");
        Palette.Text(go.transform, symbol, new Vector3(0f, 0f, -0.06f), 0.45f, color);
        go.transform.localScale = Vector3.zero;
        return b;
    }

    void Update()
    {
        age += Time.deltaTime;
        float s;
        if (age < 0.18f) s = Ease.OutBack(age / 0.18f);
        else if (age > life - 0.2f) s = Mathf.Clamp01((life - age) / 0.2f);
        else s = 1f;
        transform.localScale = Vector3.one * s * scale;
        if (age >= life) Destroy(gameObject);
    }
}
