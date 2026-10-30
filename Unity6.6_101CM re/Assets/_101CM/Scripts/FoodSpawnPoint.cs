using UnityEngine;

public enum FoodZone { Bread, Snack, Fruit, Plaza }

/// <summary>쉬움 / 높은 곳(점프) / 숨은 곳(탐색) / 위험한 곳(카트·빗자루 경로)</summary>
public enum SpotKind { Easy, High, Hidden, Danger }

/// <summary>
/// 음식이 나타날 수 있는 후보 위치. FoodSpawner가 매 판 이 중 일부를 무작위로 골라 음식을 놓는다.
/// 씬에서 직접 옮기거나 복제(Ctrl+D)해서 후보를 늘릴 수 있다.
/// </summary>
public class FoodSpawnPoint : MonoBehaviour
{
    public FoodZone zone;
    public SpotKind kind;

    public static Color KindColor(SpotKind k)
    {
        switch (k)
        {
            case SpotKind.Easy: return new Color(0.5f, 0.9f, 0.5f);
            case SpotKind.High: return new Color(0.45f, 0.7f, 1f);
            case SpotKind.Hidden: return new Color(0.75f, 0.5f, 1f);
            case SpotKind.Danger: return new Color(1f, 0.4f, 0.4f);
        }
        return Color.white;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = KindColor(kind);
        Vector3 p = transform.position + Vector3.up * 0.35f;
        Gizmos.DrawWireSphere(p, 0.3f);
        Gizmos.DrawLine(transform.position, p);
    }
}
