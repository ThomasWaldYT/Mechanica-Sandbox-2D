// Part.cs – selection, dragging, scaling, rotation, mass slider
// 2025?05?06  (right?click selection & mass?menu fix)

using UnityEngine;
using System.Collections.Generic;

public class Part : MonoBehaviour
{
    // ????????????????????????????????????????????????
    // STATIC SELECTION DATA
    // ????????????????????????????????????????????????

    public static bool IsInteracting { get; private set; }

    private static readonly List<Part> currentGroup = new();
    private static Part mainSelected = null;
    private static Color mainColour = Color.cyan;
    private static Color secondaryColour = new(0f, 1f, 1f, 0.35f);

    public static void ClearSelection()
    {
        foreach (Part p in currentGroup) p.HideOutline();
        currentGroup.Clear(); mainSelected = null;
    }

    // ????????????????????????????????????????????????
    // PUBLIC HELPERS
    // ????????????????????????????????????????????????

    public void SetCursorTextures(Texture2D move, Texture2D scale, Texture2D def)
    { curMove = move; curScale = scale; curDefault = def; }

    public void SetSelectionColours(Color main, Color secondary)
    { mainColour = main; secondaryColour = secondary; }

    public void SelectAsSingle()
    {
        ClearSelection();
        currentGroup.Add(this); mainSelected = this;
        ShowOutline(mainColour);
    }

    // ????????????????????????????????????????????????
    // INSPECTOR / REFERENCES
    // ????????????????????????????????????????????????

    [HideInInspector] public Spawner spawner;

    [Header("Cursor Textures")]
    [SerializeField] private Texture2D curMove;
    [SerializeField] private Texture2D curScale;
    [SerializeField] private Texture2D curDefault;

    [Header("Mass (affects brightness)")]
    [Range(1, 100)] public float mass = 33f;

    // ????????????????????????????????????????????????
    // OUTLINE
    // ????????????????????????????????????????????????

    private const float LINE_WIDTH = 0.05f;
    private const int CIRCLE_SEGMENTS = 40;
    private LineRenderer outline;

    // ????????????????????????????????????????????????
    // PRIVATE STATE
    // ????????????????????????????????????????????????

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;

    private float baseHue, baseSat;

    private enum DragMode { None, Move, ScaleX, ScaleY, ScaleCircle }
    private DragMode dragMode = DragMode.None;

    private Vector3 dragStartMouse;
    private readonly Dictionary<Part, Vector3> startPos = new();
    private Vector3 groupCentroid;

    // Right?click selection helpers
    private bool rightHeld = false;
    private Vector3 rightStart;
    private const float RIGHT_DRAG_THRESHOLD = 0.2f;

    private bool showMassUI;

    private const float EDGE_BAND = 0.15f;
    private const float ROTATE_STEP = GridSnapping.AngleSnap;
    private float MIN_DIM => GridSnapping.ScaleGrid;
    private bool Frozen => Time.timeScale == 0f;

    // ????????????????????????????????????????????????
    // INITIALISATION
    // ????????????????????????????????????????????????

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        if (sr) Color.RGBToHSV(sr.color, out baseHue, out baseSat, out _);

        BuildOutline(); HideOutline();
    }

    private void Start() { rb.mass = mass; UpdateBrightness(); }

    // ????????????????????????????????????????????????
    // OUTLINE BUILD / UPDATE
    // ????????????????????????????????????????????????

    private void BuildOutline()
    {
        outline = new GameObject("Outline").AddComponent<LineRenderer>();
        outline.transform.SetParent(transform, false);
        outline.useWorldSpace = false; outline.loop = true;
        outline.startWidth = outline.endWidth = LINE_WIDTH;
        outline.material = new Material(Shader.Find("Sprites/Default"));
        outline.sortingOrder = sr ? sr.sortingOrder + 50 : 50;
        outline.numCornerVertices = 2;
        RecalculateOutline();
    }

    private void LateUpdate()
    {
        if (outline.enabled && transform.hasChanged)
        { RecalculateOutline(); transform.hasChanged = false; }
    }

    private void RecalculateOutline()
    {
        if (col == null) return;

        // Circle
        if (col is CircleCollider2D circle)
        {
            float r = circle.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
            outline.positionCount = CIRCLE_SEGMENTS;
            for (int i = 0; i < CIRCLE_SEGMENTS; ++i)
            {
                float a = i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
                outline.SetPosition(i, new(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            return;
        }

        // Box
        if (col is BoxCollider2D box)
        {
            Vector2 off = box.offset;
            Vector2 half = box.size * 0.5f;
            outline.positionCount = 4;
            outline.SetPosition(0, new(off.x - half.x, off.y - half.y));
            outline.SetPosition(1, new(off.x - half.x, off.y + half.y));
            outline.SetPosition(2, new(off.x + half.x, off.y + half.y));
            outline.SetPosition(3, new(off.x + half.x, off.y - half.y));
            return;
        }

        // Fallback – bounds
        Bounds b = col.bounds;
        Vector3 l = transform.InverseTransformPoint(b.min);
        Vector3 h = transform.InverseTransformPoint(b.max);
        outline.positionCount = 4;
        outline.SetPosition(0, new(l.x, l.y));
        outline.SetPosition(1, new(l.x, h.y));
        outline.SetPosition(2, new(h.x, h.y));
        outline.SetPosition(3, new(h.x, l.y));
    }

    private void ShowOutline(Color c)
    { outline.startColor = outline.endColor = c; outline.enabled = true; }

    private void HideOutline() => outline.enabled = false;

    // ????????????????????????????????????????????????
    // MOUSE INPUT
    // ????????????????????????????????????????????????

    private void OnMouseDown()
    {
        if (!Frozen) return;

        // Right?button ?
        if (Input.GetMouseButtonDown(1))
        {
            rightHeld = true;
            rightStart = GetWorldMouse();
            return;
        }

        // Left?button ?
        if (Input.GetMouseButtonDown(0))
        {
            SelectGroup();
            BeginDrag();
        }
    }

    private void OnMouseDrag()
    {
        if (!Frozen) return;

        // Cancel right?click if user pans
        if (rightHeld && Input.GetMouseButton(1))
        {
            if (Vector3.Distance(GetWorldMouse(), rightStart) > RIGHT_DRAG_THRESHOLD)
                rightHeld = false;
        }

        // Left drag ? transform
        if (Input.GetMouseButton(0)) ContinueDrag();
    }

    private void OnMouseUp()
    {
        // Right?button released (only fires if cursor still over this collider)
        if (Input.GetMouseButtonUp(1) && rightHeld)
        {
            SelectGroup();
            showMassUI = !showMassUI; // toggle mass UI
            rightHeld = false;
        }

        if (Input.GetMouseButtonUp(0)) EndDrag();
    }

    private void OnMouseOver()
    {
        if (!Frozen) return;
        DragMode m = DetectMode(GetWorldMouse());
        ApplyCursor(m == DragMode.Move ? curMove : curScale);
    }

    private void OnMouseExit() { ApplyCursor(curDefault); }

    // ????????????????????????????????????????????????
    // SELECTION
    // ????????????????????????????????????????????????

    private void SelectGroup()
    {
        if (mainSelected == this) return;

        ClearSelection();
        List<Part> grp = GetGroup();
        currentGroup.AddRange(grp);
        mainSelected = this;

        foreach (Part p in grp)
            p.ShowOutline(p == this ? mainColour : secondaryColour);
    }

    // ????????????????????????????????????????????????
    // DRAGGING / TRANSFORM
    // ????????????????????????????????????????????????

    private void BeginDrag()
    {
        IsInteracting = true;
        dragMode = DetectMode(GetWorldMouse());

        var grp = GetGroup();
        startPos.Clear(); groupCentroid = Vector3.zero;
        foreach (Part p in grp)
        { startPos[p] = p.transform.position; groupCentroid += p.transform.position; }
        groupCentroid /= grp.Count;
        dragStartMouse = GetWorldMouse();
    }

    private void ContinueDrag()
    {
        Vector3 curMouse = GetWorldMouse();
        Vector3 rawDelta = curMouse - dragStartMouse;

        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelection();
            foreach (Part p in startPos.Keys) startPos[p] = p.transform.position;
            groupCentroid = GetCurrentCentroid();
            dragStartMouse = curMouse; Physics2D.SyncTransforms(); return;
        }

        if (dragMode == DragMode.ScaleX || dragMode == DragMode.ScaleY || dragMode == DragMode.ScaleCircle)
        { HandleScaling(curMouse); Physics2D.SyncTransforms(); return; }

        Vector3 snappedDelta = startPos.Count > 1
                               ? GridSnapping.SnapPos(groupCentroid + rawDelta) - groupCentroid
                               : GridSnapping.SnapPos(startPos[this] + rawDelta) - startPos[this];

        foreach (var kv in startPos) kv.Key.transform.position = kv.Value + snappedDelta;
        Physics2D.SyncTransforms();
    }

    private void EndDrag()
    { IsInteracting = false; dragMode = DragMode.None; startPos.Clear(); }

    private void RotateSelection()
    {
        Vector3 pivot = GetCurrentCentroid();
        Quaternion q = Quaternion.Euler(0, 0, ROTATE_STEP);

        foreach (var kv in startPos)
        {
            Vector3 off = kv.Key.transform.position - pivot;
            kv.Key.transform.position = pivot + q * off;
            kv.Key.transform.rotation *= q;
        }

        Vector3 drift = pivot - GetCurrentCentroid();
        if (drift.sqrMagnitude > 1e-6f)
            foreach (var kv in startPos) kv.Key.transform.position += drift;
    }

    private Vector3 GetCurrentCentroid()
    {
        Vector3 sum = Vector3.zero;
        foreach (var kv in startPos) sum += kv.Key.transform.position;
        return sum / startPos.Count;
    }

    private void HandleScaling(Vector3 worldMouse)
    {
        if (dragMode == DragMode.ScaleCircle)
        {
            float radius = Mathf.Clamp(Vector3.Distance(worldMouse, transform.position),
                                       MIN_DIM * 0.5f, 999f);
            transform.localScale = Vector3.one * GridSnapping.SnapScale(radius * 2f);
            return;
        }

        Vector3 dir = dragMode == DragMode.ScaleX ? transform.right : transform.up;
        float proj = Vector3.Dot(worldMouse - transform.position, dir);
        float half = Mathf.Clamp(Mathf.Abs(proj), MIN_DIM * 0.5f, 999f);
        float snap = GridSnapping.SnapScale(half * 2f);

        Vector3 ls = transform.localScale;
        if (dragMode == DragMode.ScaleX) ls.x = snap; else ls.y = snap;
        transform.localScale = ls;
    }

    // ????????????????????????????????????????????????
    // MODE DETECTION
    // ????????????????????????????????????????????????

    private DragMode DetectMode(Vector3 worldMouse)
    {
        if (GetGroup().Count > 1) return DragMode.Move;

        // Circle
        if (col is CircleCollider2D circle)
        {
            float dist = (transform.InverseTransformPoint(worldMouse)).magnitude;
            return Mathf.Abs(dist - circle.radius) < EDGE_BAND
                 ? DragMode.ScaleCircle : DragMode.Move;
        }

        // Box
        if (col is BoxCollider2D box)
        {
            Vector3 local = transform.InverseTransformPoint(worldMouse) - (Vector3)box.offset;
            float hx = box.size.x * 0.5f, hy = box.size.y * 0.5f;
            bool nearX = Mathf.Abs(Mathf.Abs(local.x) - hx) < EDGE_BAND;
            bool nearY = Mathf.Abs(Mathf.Abs(local.y) - hy) < EDGE_BAND;

            if (nearX && !nearY) return DragMode.ScaleX;
            if (nearY && !nearX) return DragMode.ScaleY;
            if (nearX && nearY)
                return Mathf.Abs(Mathf.Abs(local.x) - hx) <
                       Mathf.Abs(Mathf.Abs(local.y) - hy)
                     ? DragMode.ScaleX : DragMode.ScaleY;

            return DragMode.Move;
        }

        return DragMode.Move;
    }

    // ????????????????????????????????????????????????
    // CONNECTIVITY
    // ????????????????????????????????????????????????

    private readonly List<Part> connected = new();
    public void AddConnectedPart(Part p) { if (!connected.Contains(p)) connected.Add(p); }

    private List<Part> GetGroup()
    {
        var group = new List<Part>();
        var q = new Queue<Part>(); q.Enqueue(this);
        var seen = new HashSet<Part> { this };

        while (q.Count > 0)
        {
            Part cur = q.Dequeue(); group.Add(cur);
            foreach (Part p in cur.connected) if (seen.Add(p)) q.Enqueue(p);
        }
        return group;
    }

    // ????????????????????????????????????????????????
    // CURSOR & MASS UI
    // ????????????????????????????????????????????????

    private void UpdateBrightness()
    {
        if (!sr) return;
        float v = Mathf.InverseLerp(1f, 100f, mass);
        sr.color = Color.HSVToRGB(baseHue, baseSat, Mathf.Lerp(0.3f, 1f, v));
    }

    private void ApplyCursor(Texture2D tex)
    {
        if (tex == null) { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); return; }
        Cursor.SetCursor(tex, new(tex.width * 0.5f, tex.height * 0.5f), CursorMode.Auto);
    }

    private void OnGUI()
    {
        if (!showMassUI || !Frozen) return;

        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position);
        float gy = Screen.height - sp.y;
        const float W = 250f, H = 20f;
        Rect r = new(sp.x - W * 0.5f, gy - 50f, W, H);

        float newMass = GUI.HorizontalSlider(r, mass, 1f, 100f);
        if (!Mathf.Approximately(newMass, mass))
        { mass = newMass; rb.mass = mass; UpdateBrightness(); }

        GUI.Label(new(r.x, r.y - 18, W, 18),
                  "Mass " + mass.ToString("0"),
                  new GUIStyle { alignment = TextAnchor.MiddleCenter });
    }

    // ????????????????????????????????????????????????
    // UTILITY
    // ????????????????????????????????????????????????

    private Vector3 GetWorldMouse()
    {
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        wp.z = 0f; return wp;
    }
}
