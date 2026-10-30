using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 매 판 시작할 때 FoodSpawnPoint 후보 중에서 구역별로 정해진 개수만큼 무작위로 골라 음식을 배치한다.
/// - 구역마다 '쉬움' 자리를 최소 1개 보장(초반 진입이 막히지 않게)
/// - 얻기 어려운 자리일수록 cm가 높다 (쉬움 2 / 높은 곳 3 / 숨은 곳 3 / 위험 5)
/// - 씬에 미리 놓여 있던 고정 음식은 시작 시 정리한다.
/// </summary>
public class FoodSpawner : MonoBehaviour
{
    [Header("구역별 음식 개수")]
    public int breadCount = 4;
    public int snackCount = 4;
    public int fruitCount = 4;
    public int plazaCount = 3;

    [Header("자리 종류별 cm")]
    public int easyCm = 2;
    public int highCm = 3;
    public int hiddenCm = 3;
    public int dangerCm = 5;

    [Header("규칙")]
    [Tooltip("구역마다 쉬운 자리를 최소 1개 포함")]
    public bool guaranteeEasyPerZone = true;
    [Tooltip("0이면 매 판 랜덤. 숫자를 넣으면 항상 같은 배치(테스트용)")]
    public int fixedSeed = 0;

    public Transform foodRoot;

    void Start()
    {
        // 빌더가 만든 고정 음식 정리
        foreach (var f in new List<Food>(Food.All))
            if (f && !f.physical && f.type != FoodType.LastPiece) Destroy(f.gameObject);

        if (fixedSeed != 0) Random.InitState(fixedSeed);
        if (!foodRoot && GameManager.I) foodRoot = GameManager.I.foodRoot;

        var points = GetComponentsInChildren<FoodSpawnPoint>();
        int total = 0, totalCm = 0;
        foreach (FoodZone z in System.Enum.GetValues(typeof(FoodZone)))
        {
            var list = new List<FoodSpawnPoint>();
            foreach (var p in points) if (p.zone == z) list.Add(p);
            Shuffle(list);

            int want = Mathf.Min(CountFor(z), list.Count);
            var picked = new List<FoodSpawnPoint>();
            if (guaranteeEasyPerZone && want > 0)
            {
                var easy = list.Find(p => p.kind == SpotKind.Easy);
                if (easy) { picked.Add(easy); list.Remove(easy); }
            }
            for (int i = 0; picked.Count < want && i < list.Count; i++) picked.Add(list[i]);

            foreach (var p in picked)
            {
                var f = Food.Create(PickType(z), p.transform.position, foodRoot, false);
                f.cm = CmFor(p.kind);
                total++; totalCm += f.cm;
            }
        }
        Debug.Log($"[FoodSpawner] 음식 {total}개 배치, 합계 {totalCm}cm");
    }

    int CountFor(FoodZone z)
    {
        switch (z)
        {
            case FoodZone.Bread: return breadCount;
            case FoodZone.Snack: return snackCount;
            case FoodZone.Fruit: return fruitCount;
            default: return plazaCount;
        }
    }

    int CmFor(SpotKind k)
    {
        switch (k)
        {
            case SpotKind.High: return highCm;
            case SpotKind.Hidden: return hiddenCm;
            case SpotKind.Danger: return dangerCm;
            default: return easyCm;
        }
    }

    static FoodType PickType(FoodZone z)
    {
        bool a = Random.value < 0.5f;
        switch (z)
        {
            case FoodZone.Bread: return a ? FoodType.Toast : FoodType.Croissant;
            case FoodZone.Snack: return a ? FoodType.Cookie : FoodType.Candy;
            case FoodZone.Fruit: return a ? FoodType.Apple : FoodType.Orange;
            default:
                FoodType[] all = { FoodType.Toast, FoodType.Croissant, FoodType.Cookie, FoodType.Candy, FoodType.Apple, FoodType.Orange };
                return all[Random.Range(0, all.Length)];
        }
    }

    static void Shuffle<T>(List<T> l)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (l[i], l[j]) = (l[j], l[i]);
        }
    }
}
