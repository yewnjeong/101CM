using UnityEngine;

namespace CM101.Level
{
    /// <summary>
    /// 음식 배치 정보(ID·구역·메모). 실제 음식(FoodItem)과 같은 오브젝트에 붙는다.
    /// ID·종류는 밸런스 시트 '음식배치' B 표와 같다.
    /// </summary>
    public class FoodSpawn : MonoBehaviour
    {
        public string id;
        public ZoneId zone;
        public FoodType kind;
        [Tooltip("음식 모양 20종 (None = 프로토)")] public FoodKind food;
        [Tooltip("자리별 cm. 0이면 FoodType 기본값")] public int cm;
        [Tooltip("창고 튜토리얼 음식은 90cm 총량에서 뺀다")] public bool tutorial;
        [TextArea] public string note;

        public int Cm => cm > 0 ? cm : FoodData.Length(kind);
        /// <summary>음식이 놓인 바닥 높이 지점 (FoodItem은 바닥에서 0.25 떠 있다).</summary>
        public Vector3 GroundPos => transform.position - (GetComponent<FoodItem>() ? Vector3.up * 0.25f : Vector3.zero);

        void OnDrawGizmos()
        {
            Gizmos.color = FoodData.FoodColor(kind, 0);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
