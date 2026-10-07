using UnityEngine;

namespace CM101.Level
{
    /// <summary>에디터 검토용 라벨 묶음. 플레이를 시작하면 숨긴다.</summary>
    public class HideInPlay : MonoBehaviour
    {
        void Awake() { gameObject.SetActive(false); }
    }
}
