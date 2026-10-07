using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CM101
{
    /// <summary>
    /// 창고 전신거울 (평면 반사).
    /// 거울 면을 기준으로 메인 카메라와 대칭인 위치에 반사 카메라를 두고, 그 화면을 거울 면(CM101/Mirror 셰이더)에 입힌다.
    /// - 이 오브젝트의 +Z(forward)가 거울 앞쪽(방 쪽)을 향해야 한다. 거울 면은 glass 오브젝트 위치를 지나는 평면.
    /// - 메인 카메라가 거울 앞쪽에 있고, 가까이 있고, 거울이 화면에 보일 때만 반사 카메라를 켠다(그 외에는 은회색).
    /// - 튜토리얼 중 선두가 거울 앞에 오면 기분 표현 안내를 한 번 띄운다.
    /// </summary>
    [DefaultExecutionOrder(10000)] // 카메라(OrbitCamera.LateUpdate)가 움직인 뒤에 따라간다
    public class JellyMirror : MonoBehaviour
    {
        public Renderer glass;
        [Tooltip("반사 화면 세로 해상도")] public int textureHeight = 720;
        [Tooltip("메인 카메라가 이 거리 안에 있을 때만 반사를 그린다")] public float activeDistance = 16f;
        [Tooltip("반사 카메라가 그리는 최대 거리")] public float reflectFar = 30f;
        [Tooltip("거울 면 바로 앞까지 잘라 내는 여유")] public float clipOffset = 0.02f;
        [Tooltip("튜토리얼 중 기분 표현 안내를 띄우는 거리 (0이면 안 띄움)")] public float hintDistance = 2.4f;
        [TextArea] public string hintText = "거울 앞이에요! 1~4를 눌러 기분 표현을 해 보세요";

        Camera reflCam;
        RenderTexture rt;
        MaterialPropertyBlock mpb;
        readonly Plane[] frustum = new Plane[6];
        bool hintShown;
        bool reflecting;

        static readonly int TexId = Shader.PropertyToID("_ReflectionTex");
        static readonly int OnId = Shader.PropertyToID("_MirrorOn");

        public bool IsReflecting => reflecting;

        void OnDisable()
        {
            SetReflecting(false);
        }

        void OnDestroy()
        {
            if (reflCam) Destroy(reflCam.gameObject);
            if (rt) { rt.Release(); Destroy(rt); }
        }

        void LateUpdate()
        {
            if (!glass) return;
            ShowHint();

            var main = Camera.main;
            if (!main) { SetReflecting(false); return; }

            Vector3 n = transform.forward;
            Vector3 p = glass.transform.position;
            Vector3 camPos = main.transform.position;
            float side = Vector3.Dot(camPos - p, n);

            bool want = side > 0.05f && (camPos - p).sqrMagnitude < activeDistance * activeDistance;
            if (want)
            {
                GeometryUtility.CalculateFrustumPlanes(main, frustum);
                want = GeometryUtility.TestPlanesAABB(frustum, glass.bounds);
            }
            if (!want) { SetReflecting(false); return; }

            Ensure(main);

            // 메인 카메라를 거울 면에 대칭으로 옮긴다(회전은 정상 회전 → 셰이더에서 좌우만 뒤집음)
            Vector3 rp = camPos - 2f * side * n;
            Vector3 f = Vector3.Reflect(main.transform.forward, n);
            Vector3 u = Vector3.Reflect(main.transform.up, n);
            reflCam.transform.SetPositionAndRotation(rp, Quaternion.LookRotation(f, u));

            reflCam.fieldOfView = main.fieldOfView;
            reflCam.aspect = main.aspect;
            reflCam.nearClipPlane = main.nearClipPlane;
            reflCam.farClipPlane = reflectFar;
            reflCam.cullingMask = main.cullingMask;
            reflCam.backgroundColor = main.backgroundColor;
            reflCam.clearFlags = main.clearFlags;
            reflCam.depth = main.depth - 1f;
            reflCam.ResetProjectionMatrix();
            // 거울 뒤쪽(벽 속)이 비치지 않게 가까운 면을 거울 면으로 자른다
            reflCam.projectionMatrix = reflCam.CalculateObliqueMatrix(CameraSpacePlane(reflCam, p, n));

            SetReflecting(true);
        }

        Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
        {
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(pos + normal * clipOffset);
            Vector3 cnormal = m.MultiplyVector(normal).normalized;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        void Ensure(Camera main)
        {
            int h = Mathf.Max(64, textureHeight);
            int w = Mathf.Max(64, Mathf.RoundToInt(h * main.aspect));
            if (!rt || rt.width != w || rt.height != h)
            {
                if (rt) { rt.Release(); Destroy(rt); }
                rt = new RenderTexture(w, h, 24, main.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default)
                {
                    name = "MirrorReflection",
                    antiAliasing = 1,
                    useMipMap = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                rt.Create();
                if (reflCam) reflCam.targetTexture = rt;
            }
            if (!reflCam)
            {
                var go = new GameObject("MirrorReflectionCamera");
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(transform, false);
                reflCam = go.AddComponent<Camera>();
                reflCam.enabled = false;
                reflCam.targetTexture = rt;
                reflCam.allowHDR = main.allowHDR;
                reflCam.allowMSAA = false;
                var data = reflCam.GetUniversalAdditionalCameraData();
                data.renderShadows = false;          // 비스듬한 가까운 면에서 그림자 단계가 흔들리지 않게
                data.renderPostProcessing = false;   // 후처리는 메인 카메라에서 한 번만
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorTexture = false;
                data.requiresDepthTexture = false;
            }
        }

        void SetReflecting(bool on)
        {
            if (reflecting == on && mpb != null) return;
            reflecting = on;
            if (reflCam) reflCam.enabled = on;
            if (!glass) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            glass.GetPropertyBlock(mpb);
            mpb.SetFloat(OnId, on ? 1f : 0f);
            if (on && rt) mpb.SetTexture(TexId, rt);
            glass.SetPropertyBlock(mpb);
        }

        void ShowHint()
        {
            if (hintShown || hintDistance <= 0f) return;
            var gm = GameManager.I;
            if (!gm || !gm.TutorialActive || !gm.leader || !GameManager.IsPlaying) return;
            Vector3 d = gm.leader.transform.position - glass.transform.position;
            d.y = 0f;
            if (d.magnitude > hintDistance || Vector3.Dot(d, transform.forward) < 0f) return;
            hintShown = true;
            GameManager.Notify(hintText, MsgKind.Gold, 4f);
        }
    }
}
