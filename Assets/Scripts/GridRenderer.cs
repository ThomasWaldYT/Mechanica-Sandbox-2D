// GridRenderer.cs - camera-following grid that fades with zoom, SRP-safe
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class GridRenderer : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color lineTint = new(1, 1, 1, 0.10f);
    [SerializeField] private int halfLines = 140;      // per axis
    [SerializeField] private float zOffset = -0.01f;   // slight push forward
    [Header("Zoom Fade (orthographic)")]
    [SerializeField] private float maxOrthoSize = 40f;      // brightness -> 0 here
    [SerializeField] private bool fadeWithZoom = true;
    [Header("Only show when frozen?")]
    [SerializeField] private bool onlyWhenFrozen = true;

    private Material mat;
    private Camera cam;
    private float startOrtho;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        startOrtho = cam.orthographic ? cam.orthographicSize : 0f;
        mat = BuildMat();

        Camera.onPostRender += DrawBuiltIn;
        RenderPipelineManager.endCameraRendering += DrawSRP;
    }
    private void OnDisable()
    {
        Camera.onPostRender -= DrawBuiltIn;
        RenderPipelineManager.endCameraRendering -= DrawSRP;
        if (mat) DestroyImmediate(mat);
    }

    private void DrawBuiltIn(Camera c) { if (c == cam) DrawGrid(); }
    private void DrawSRP(ScriptableRenderContext ctx, Camera c)
    { if (c == cam) DrawGrid(); }

    private void DrawGrid()
    {
        if (onlyWhenFrozen && Time.timeScale != 0f) return;
        if (!mat) return;

        Color tint = lineTint;
        if (fadeWithZoom && cam.orthographic)
        {
            float t = Mathf.InverseLerp(startOrtho, maxOrthoSize, cam.orthographicSize);
            tint.a = lineTint.a * (1f - t);
            if (tint.a <= 0.002f) return;
        }

        mat.SetPass(0);
        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);
        GL.Color(tint);

        float g = GridSnapping.PosGrid;
        int n = halfLines;

        Vector3 camPos = cam.transform.position;
        float baseX = Mathf.Floor(camPos.x / g) * g;
        float baseY = Mathf.Floor(camPos.y / g) * g;

        for (int i = -n; i <= n; i++)
        {
            float x = baseX + i * g;
            GL.Vertex3(x, baseY - n * g, zOffset);
            GL.Vertex3(x, baseY + n * g, zOffset);
        }
        for (int j = -n; j <= n; j++)
        {
            float y = baseY + j * g;
            GL.Vertex3(baseX - n * g, y, zOffset);
            GL.Vertex3(baseX + n * g, y, zOffset);
        }

        GL.End();
        GL.PopMatrix();
    }

    private static Material BuildMat()
    {
        var m = new Material(Shader.Find("Hidden/Internal-Colored"));
        m.hideFlags = HideFlags.HideAndDontSave;
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        m.SetInt("_ZWrite", 0);
        m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        m.renderQueue = 5000;
        return m;
    }
}
