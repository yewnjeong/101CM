using UnityEngine;

/// <summary>행렬을 이루는 새끼 고양이. 위치는 CatPlayer가 경로 샘플링으로 갱신.</summary>
public class Follower : MonoBehaviour
{
    public CatVisual visual;
    public Collider body;   // 주인공이 행렬 사이로 못 지나가게 막는 몸 콜라이더
    public int cm;
    public FoodType sourceType = FoodType.Toast;   // 이 고양이를 만든 음식 (흩어진 뒤 다시 음식으로 돌아갈 때 사용)   // 이 고양이가 들고 있는 길이(흩어지면 빠지고, 다시 합류하면 돌아옴)
    float age;
    const float PopTime = 0.35f;
    static GameObject kittenModel;

    public static Follower Create(Vector3 pos, Color color, float scale)
    {
        var go = new GameObject("Kitten");
        go.transform.position = pos;
        go.layer = 2;
        var f = go.AddComponent<Follower>();
        f.visual = CatVisual.Build(go.transform, color, scale);
        // 리깅된 새끼 고양이 모델(Resources/KittenModel)이 있으면 캡슐 외형 대신 사용
        if (!kittenModel) kittenModel = Resources.Load<GameObject>("KittenModel");
        if (kittenModel) f.visual.UseModel(kittenModel);

        // 상자/수레와 부딪힘을 감지하기 위한 트리거 (물리적으로 밀지 않음)
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.32f;
        col.center = new Vector3(0f, 0.3f, 0f);
        // 몸 콜라이더(실체): 주인공(다이내믹)은 통과 못 함. 키네마틱이라 벽·매대에는 반응 없으니 위치는 CatPlayer가 밀어냄
        var bodyCol = go.AddComponent<CapsuleCollider>();
        bodyCol.radius = 0.22f;
        bodyCol.height = 0.6f;
        bodyCol.center = new Vector3(0f, 0.3f, 0f);
        f.body = bodyCol;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        go.transform.localScale = Vector3.zero;
        return f;
    }

    void Update()
    {
        if (age >= PopTime) return;
        age += Time.deltaTime;
        transform.localScale = Vector3.one * Ease.OutBack(age / PopTime);
    }

    public void Emote(int id) => visual.PlayEmote(id);

    static GameObject rainbowModel;
    /// <summary>마지막 1CM 고양이: 주인공 모양의 무지개 젤리 모델로 교체 (색 덮어쓰기 없음).</summary>
    public void UseRainbowModel()
    {
        if (!rainbowModel) rainbowModel = Resources.Load<GameObject>("RainbowKittenModel");
        if (!rainbowModel) return;
        var old = visual.transform.Find("Model");
        if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
        visual.UseModel(rainbowModel);
        visual.tinted = new Renderer[0];
    }
}
