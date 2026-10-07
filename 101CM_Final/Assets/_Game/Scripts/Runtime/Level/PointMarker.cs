using UnityEngine;

namespace CM101.Level
{
    public enum PointKind { Start, ZoneSign, Supply, FinalJelly, Exit }

    /// <summary>시작·구역 안내판(ZS1~5, 구역 입구에서 여기가 어디인지 알려 준다)·보충 지점(SP1~5)·최종 젤리·출구 지점.</summary>
    public class PointMarker : MonoBehaviour
    {
        public string id;
        public ZoneId zone;
        public PointKind kind;

        void OnDrawGizmos()
        {
            switch (kind)
            {
                case PointKind.ZoneSign: Gizmos.color = new Color(0.3f, 0.6f, 1f); break;
                case PointKind.Supply: Gizmos.color = new Color(0.3f, 0.95f, 0.5f); break;
                case PointKind.FinalJelly: Gizmos.color = new Color(1f, 0.4f, 0.6f); break;
                default: Gizmos.color = Color.white; break;
            }
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
            if (kind == PointKind.FinalJelly)
                Gizmos.DrawWireSphere(transform.position, 6f); // 레벨 문서: 최종 젤리 주변 6타일에는 새 규칙 금지
        }
    }
}
