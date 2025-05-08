// Part.cs – selection, dragging, scaling, rotation, context menu, duplicate/rip/delete
// CHANGELOG #10 (2025-05-07):
//   • Fixed “invincible outline” on duplicated objects.
//     – Awake now re?uses an existing Outline child instead of creating a second one.
//     – HideOutline disables every LineRenderer named “Outline” under the part.
//   • No other behaviour changed.

using UnityEngine;
using System.Collections.Generic;

public class Part : MonoBehaviour
{
    // ??????????????????????????? STATIC SELECTION DATA ???????????????????????????
    public static bool IsInteracting { get; private set; }
    private static readonly List<Part> currentGroup = new();
    private static Part mainSelected;
    private static Color mainColour = Color.cyan;
    private static Color secondaryColour = new(0f, 1f, 1f, 0.35f);

    public static void ClearSelection()
    {
        foreach (var p in currentGroup)
        {
            p.HideOutline();
            p.showContextMenu = false;
        }
        currentGroup.Clear();
        mainSelected = null;
    }

    // ??????????????????????????? FREEZE TRACKING ???????????????????????????
    private static bool lastFrozen;
    private bool Frozen => Time.timeScale == 0f;

    // ??????????????????????????? PUBLIC HELPERS ???????????????????????????
    public void SelectAsSingle()
    {
        ClearSelection();
        currentGroup.Add(this);
        mainSelected = this;
        ShowOutline(mainColour);
        showContextMenu = false;
    }
    public void SetCursorTextures(Texture2D move, Texture2D scale, Texture2D def)
    {
        curMove = move; curScale = scale; curDefault = def;
    }
    public void SetSelectionColours(Color main, Color secondary)
    { mainColour = main; secondaryColour = secondary; }

    // ??????????????????????????? INSPECTOR FIELDS ???????????????????????????
    [HideInInspector] public Spawner spawner;

    [Header("Cursor Textures")][SerializeField] private Texture2D curMove;
    [SerializeField] private Texture2D curScale; [SerializeField] private Texture2D curDefault;

    [Header("Mass (affects brightness)")]
    [Range(1, 100)] public float mass = 33f;

    // ??????????????????????????? OUTLINE DATA ???????????????????????????
    private const float LINE_WIDTH = 0.05f; private const int CIRCLE_SEGMENTS = 40;
    private LineRenderer outline;

    // ??????????????????????????? PRIVATE STATE ???????????????????????????
    private Rigidbody2D rb; private Collider2D col; private SpriteRenderer sr;
    private float baseHue, baseSat;

    private enum DragMode { None, Move, ScaleX, ScaleY, ScaleCircle }
    private DragMode dragMode = DragMode.None;
    private Vector3 dragStartMouse; private readonly Dictionary<Part, Vector3> startPos = new();
    private Vector3 groupCentroid;

    // right?click helpers
    private bool rightCandidate; private Vector2 rightStartScreen; private const float RIGHT_DRAG_PIXELS = 4f;

    // context?menu
    private bool showContextMenu; private Vector2 menuGuiPos; private Rect contextMenuRect;

    // pan?cancel (drag elsewhere while menu open)
    private bool panCancelCandidate; private Vector2 panStartScreen;

    private const float EDGE_BAND = 0.15f; private const float ROTATE_STEP = GridSnapping.AngleSnap;
    private float MIN_DIM => GridSnapping.ScaleGrid;

    // connectivity
    private readonly List<Part> connected = new();

    // ??????????????????????????? STATIC UTIL ???????????????????????????
    public static bool IsPointerOverContextMenuArea()
    {
        if (mainSelected == null || !mainSelected.showContextMenu) return false;
        Vector2 guiMouse = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        return mainSelected.contextMenuRect.Contains(guiMouse);
    }

    // ??????????????????????????? INITIALISATION ???????????????????????????
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr) Color.RGBToHSV(sr.color, out baseHue, out baseSat, out _);

        // Re?use existing outline if present (e.g., after Instantiate) to avoid duplicates.
        Transform existing = transform.Find("Outline");
        if (existing != null && existing.GetComponent<LineRenderer>() != null)
        {
            outline = existing.GetComponent<LineRenderer>();
        }
        else
        {
            BuildOutline();
        }

        HideOutline();
    }

    private void Start()
    {
        rb.mass = mass;
        UpdateBrightness();
    }

    // ??????????????????????????? UPDATE LOOP ???????????????????????????
    private void Update()
    {
        if (!Frozen && lastFrozen) ClearSelection();
        lastFrozen = Frozen;

        if (showContextMenu && Input.mouseScrollDelta.y != 0f) showContextMenu = false;

        col.enabled = !showContextMenu;

        HandleRightClick();
        if (showContextMenu && Input.GetMouseButtonDown(1) && !col.OverlapPoint(GetWorldMouse())) showContextMenu = false;
        HandlePanCancel();
        HandleGlobalRotationShortcut();
    }

    // ??????????????????????????? GLOBAL R KEY ???????????????????????????
    private void HandleGlobalRotationShortcut()
    {
        if (!Frozen || dragMode != DragMode.None) return;
        if (mainSelected != this) return;
        if (!Input.GetKeyDown(KeyCode.R)) return;
        RotateCurrentGroup();
        Physics2D.SyncTransforms();
    }

    private static void RotateCurrentGroup()
    {
        if (currentGroup.Count == 0) return;
        Vector3 pivot = Vector3.zero;
        foreach (var p in currentGroup) pivot += p.transform.position;
        pivot /= currentGroup.Count;
        Quaternion q = Quaternion.Euler(0, 0, ROTATE_STEP);
        foreach (var p in currentGroup)
        {
            Vector3 off = p.transform.position - pivot;
            p.transform.position = pivot + q * off;
            p.transform.rotation *= q;
        }
    }

    // ??????????????????????????? RIGHT?CLICK HANDLING ???????????????????????????
    private void HandleRightClick()
    {
        if (Input.GetMouseButtonDown(1) && col.OverlapPoint(GetWorldMouse()))
        {
            rightCandidate = true;
            rightStartScreen = Input.mousePosition;
        }
        if (rightCandidate && Input.GetMouseButton(1) &&
            Vector2.Distance(Input.mousePosition, rightStartScreen) > RIGHT_DRAG_PIXELS)
            rightCandidate = false;

        if (Input.GetMouseButtonUp(1) && rightCandidate && col.OverlapPoint(GetWorldMouse()))
        {
            if (!showContextMenu)
            {
                ClearSelection();
                SelectGroup(true);
                menuGuiPos = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            }
        }

        if (Input.GetMouseButtonUp(1)) rightCandidate = false;
    }

    // ??????????????????????????? PAN?CANCEL ???????????????????????????
    private void HandlePanCancel()
    {
        if (!showContextMenu) return;
        if (Input.GetMouseButtonDown(1))
        {
            panCancelCandidate = true;
            panStartScreen = Input.mousePosition;
        }
        if (panCancelCandidate && Input.GetMouseButton(1) &&
            Vector2.Distance(Input.mousePosition, panStartScreen) > RIGHT_DRAG_PIXELS)
        {
            showContextMenu = false;
            panCancelCandidate = false;
        }
        if (Input.GetMouseButtonUp(1)) panCancelCandidate = false;
    }

    // ??????????????????????????? LEFT?CLICK DRAG / EDIT ???????????????????????????
    private void OnMouseDown()
    {
        if (!Frozen || !Input.GetMouseButtonDown(0)) return;
        ClearSelection();
        SelectGroup(false);
        BeginDrag();
    }

    private void OnMouseDrag()
    {
        if (Frozen && Input.GetMouseButton(0)) ContinueDrag();
    }

    private void OnMouseUp()
    {
        if (Input.GetMouseButtonUp(0)) EndDrag();
    }

    private void OnMouseOver()
    {
        if (!Frozen) return;
        ApplyCursor(DetectMode(GetWorldMouse()) == DragMode.Move ? curMove : curScale);
    }

    private void OnMouseExit() => ApplyCursor(curDefault);

    // ??????????????????????????? SELECTION HELPERS ???????????????????????????
    public void SelectGroup(bool showSlider)
    {
        ClearSelection();
        var grp = GetGroup();
        currentGroup.AddRange(grp);
        mainSelected = this;
        foreach (var p in grp)
        {
            p.ShowOutline(p == this ? mainColour : secondaryColour);
            p.showContextMenu = (p == this) && showSlider;
        }
    }

    // ??????????????????????????? DRAGGING LOGIC ???????????????????????????
    private void BeginDrag()
    {
        IsInteracting = true;
        dragMode = DetectMode(GetWorldMouse());
        var grp = GetGroup();
        startPos.Clear();
        groupCentroid = Vector3.zero;
        foreach (var p in grp)
        {
            startPos[p] = p.transform.position;
            groupCentroid += p.transform.position;
        }
        groupCentroid /= grp.Count;
        dragStartMouse = GetWorldMouse();
    }

    private void ContinueDrag()
    {
        Vector3 curMouse = GetWorldMouse();
        Vector3 rawDelta = curMouse - dragStartMouse;

        // rotate while dragging
        if (Input.GetKeyDown(KeyCode.R))
        {
            RotateSelectionDuringDrag();

            List<Part> keys = new(startPos.Keys);
            foreach (Part k in keys)
                startPos[k] = k.transform.position;

            dragStartMouse = curMouse;
            Physics2D.SyncTransforms();
            return;
        }

        // scaling
        if (dragMode == DragMode.ScaleX || dragMode == DragMode.ScaleY || dragMode == DragMode.ScaleCircle)
        {
            HandleScaling(curMouse);
            Physics2D.SyncTransforms();
            return;
        }

        // translation
        Vector3 liveCentroid = GetCurrentCentroid();
        Vector3 snappedDelta = startPos.Count > 1
            ? GridSnapping.SnapPos(liveCentroid + rawDelta) - liveCentroid
            : GridSnapping.SnapPos(startPos[this] + rawDelta) - startPos[this];

        foreach (var kv in startPos)
            kv.Key.transform.position = kv.Value + snappedDelta;

        Physics2D.SyncTransforms();
    }

    private void EndDrag()
    {
        IsInteracting = false;
        dragMode = DragMode.None;
        startPos.Clear();
    }

    private void RotateSelectionDuringDrag()
    {
        Vector3 pivot = GetCurrentCentroid();
        Quaternion q = Quaternion.Euler(0, 0, ROTATE_STEP);
        foreach (var kv in startPos)
        {
            Vector3 off = kv.Key.transform.position - pivot;
            kv.Key.transform.position = pivot + q * off;
            kv.Key.transform.rotation *= q;
        }
    }

    private Vector3 GetCurrentCentroid()
    {
        Vector3 sum = Vector3.zero;
        foreach (var kv in startPos) sum += kv.Key.transform.position;
        return sum / startPos.Count;
    }

    // ??????????????????????????? SCALING ???????????????????????????
    private void HandleScaling(Vector3 worldMouse)
    {
        if (dragMode == DragMode.ScaleCircle)
        {
            float r = Mathf.Clamp(Vector3.Distance(worldMouse, transform.position), MIN_DIM * 0.5f, 999f);
            transform.localScale = Vector3.one * GridSnapping.SnapScale(r * 2f);
            return;
        }

        Vector3 dir = dragMode == DragMode.ScaleX ? transform.right : transform.up;
        float proj = Mathf.Abs(Vector3.Dot(worldMouse - transform.position, dir));
        float snap = GridSnapping.SnapScale(Mathf.Clamp(proj * 2f, MIN_DIM, 999f));
        Vector3 ls = transform.localScale;
        if (dragMode == DragMode.ScaleX) ls.x = snap; else ls.y = snap;
        transform.localScale = ls;
    }

    // ??????????????????????????? MODE DETECTION ???????????????????????????
    private DragMode DetectMode(Vector3 worldMouse)
    {
        if (GetGroup().Count > 1) return DragMode.Move;

        if (col is CircleCollider2D cc)
        {
            float dist = transform.InverseTransformPoint(worldMouse).magnitude;
            return Mathf.Abs(dist - cc.radius) < EDGE_BAND ? DragMode.ScaleCircle : DragMode.Move;
        }

        if (col is BoxCollider2D bc)
        {
            Vector3 local = transform.InverseTransformPoint(worldMouse) - (Vector3)bc.offset;
            float hx = bc.size.x * 0.5f, hy = bc.size.y * 0.5f;
            bool nearX = Mathf.Abs(Mathf.Abs(local.x) - hx) < EDGE_BAND;
            bool nearY = Mathf.Abs(Mathf.Abs(local.y) - hy) < EDGE_BAND;

            if (nearX && !nearY) return DragMode.ScaleX;
            if (nearY && !nearX) return DragMode.ScaleY;
            if (nearX && nearY)
                return Mathf.Abs(Mathf.Abs(local.x) - hx) < Mathf.Abs(Mathf.Abs(local.y) - hy)
                    ? DragMode.ScaleX : DragMode.ScaleY;

            return DragMode.Move;
        }

        return DragMode.Move;
    }

    // ??????????????????????????? CONNECTIVITY ???????????????????????????
    public void AddConnectedPart(Part p)
    {
        if (!connected.Contains(p)) connected.Add(p);
        if (!p.connected.Contains(this)) p.connected.Add(this);

        if (!Frozen) return;

        Part preferred = (mainSelected == this || mainSelected == p) ? mainSelected : this;
        bool keepMenu = preferred != null && preferred.showContextMenu;
        ClearSelection();
        preferred.SelectGroup(keepMenu);
    }

    private List<Part> GetGroup()
    {
        List<Part> grp = new();
        Queue<Part> q = new();
        HashSet<Part> seen = new() { this };
        q.Enqueue(this);

        while (q.Count > 0)
        {
            Part cur = q.Dequeue();
            grp.Add(cur);
            foreach (var nxt in cur.connected)
                if (seen.Add(nxt)) q.Enqueue(nxt);
        }
        return grp;
    }

    // ??????????????????????????? CURSOR & BRIGHTNESS ???????????????????????????
    private const float MIN_BRIGHTNESS = 0.3f; private const float MAX_BRIGHTNESS = 0.7f;

    private void UpdateBrightness()
    {
        if (!sr) return;
        float t = Mathf.InverseLerp(1f, 100f, mass);
        float v = Mathf.Lerp(MAX_BRIGHTNESS, MIN_BRIGHTNESS, t);
        sr.color = Color.HSVToRGB(baseHue, baseSat, v);
    }

    private void ApplyCursor(Texture2D tex) =>
        Cursor.SetCursor(tex, tex ? new Vector2(tex.width * 0.5f, tex.height * 0.5f) : Vector2.zero,
            CursorMode.Auto);

    // ??????????????????????????? CONTEXT MENU GUI ???????????????????????????
    private void OnGUI()
    {
        if (!showContextMenu || !Frozen) return;

        const float UI_SCALE = 2f;
        GUIStyle lblStyle = new(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * UI_SCALE) };
        GUIStyle btnStyle = new(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * UI_SCALE) };

        float W0 = 180f, LINE_H0 = 22f, SLIDER_H0 = 18f, PAD0 = 8f;
        float W = W0 * UI_SCALE, LINE_H = LINE_H0 * UI_SCALE, SLIDER_H = SLIDER_H0 * UI_SCALE, PAD = PAD0 * UI_SCALE;
        float menuH = PAD * 2 + lblStyle.lineHeight + SLIDER_H + 12f * UI_SCALE + 3 * (LINE_H + 4f * UI_SCALE);

        Rect bgR = new(menuGuiPos.x + 20f * UI_SCALE, menuGuiPos.y - menuH * 0.5f, W, menuH);
        contextMenuRect = bgR;

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        GUI.Box(bgR, GUIContent.none);
        GUI.color = prev;

        float y = bgR.y + PAD;
        Rect labelRect = new(bgR.x + PAD, y, W - PAD * 2, lblStyle.lineHeight);
        y += lblStyle.lineHeight;

        Rect sliderRect = new(bgR.x + PAD, y, W - PAD * 2, SLIDER_H);
        y += SLIDER_H + 12f * UI_SCALE;

        Rect dupRect = new(bgR.x + PAD, y, W - PAD * 2, LINE_H);
        y += LINE_H + 4f * UI_SCALE;

        Rect ripRect = new(bgR.x + PAD, y, W - PAD * 2, LINE_H);
        y += LINE_H + 4f * UI_SCALE;

        Rect delRect = new(bgR.x + PAD, y, W - PAD * 2, LINE_H);

        GUI.Label(labelRect, $"Mass {mass:0}", lblStyle);

        float nm = GUI.HorizontalSlider(sliderRect, mass, 1f, 100f);
        if (!Mathf.Approximately(nm, mass))
        {
            mass = nm;
            rb.mass = mass;
            UpdateBrightness();
        }

        if (GUI.Button(dupRect, "Duplicate", btnStyle))
        {
            DuplicateGroup();
            showContextMenu = false;
        }
        if (GUI.Button(ripRect, "Rip", btnStyle))
        {
            Rip();
            showContextMenu = false;
        }
        if (GUI.Button(delRect, "Delete", btnStyle))
        {
            DeleteGroup();
            return;
        }

        Event ev = Event.current;
        if (ev.isMouse && ev.type == EventType.MouseDown && ev.button == 0 && !bgR.Contains(ev.mousePosition))
        {
            showContextMenu = false;
            ev.Use();
        }
        if (ev.isMouse && ev.type == EventType.MouseDown && ev.button == 1 && bgR.Contains(ev.mousePosition))
            ev.Use();
    }

    // ??????????????????????????? DUPLICATE / RIP / DELETE ???????????????????????????
    private void DuplicateGroup()
    {
        List<Part> grp = GetGroup();

        Bounds bb = new(grp[0].transform.position, Vector3.zero);
        foreach (var p in grp) bb.Encapsulate(p.col.bounds);
        float offsetX = bb.size.x + 1f;
        Vector3 offset = new(offsetX, 0f, 0f);

        Dictionary<Rigidbody2D, Rigidbody2D> bodyMap = new();
        Dictionary<Part, Part> partMap = new();

        foreach (var p in grp)
        {
            GameObject cloneObj = Instantiate(p.gameObject, p.transform.position + offset, p.transform.rotation,
                p.transform.parent);
            Part cp = cloneObj.GetComponent<Part>();
            partMap[p] = cp;
            bodyMap[p.rb] = cp.rb;

            cp.HideOutline();
            cp.showContextMenu = false;
            cp.connected.Clear();
        }

        foreach (var kv in partMap)
        {
            foreach (AnchoredJoint2D j in kv.Value.GetComponents<AnchoredJoint2D>())
            {
                if (j.connectedBody && bodyMap.TryGetValue(j.connectedBody, out var newB))
                    j.connectedBody = newB;
            }
        }

        foreach (var kv in partMap)
        {
            foreach (AnchoredJoint2D j in kv.Value.GetComponents<AnchoredJoint2D>())
            {
                if (j.connectedBody)
                    kv.Value.AddConnectedPart(j.connectedBody.GetComponent<Part>());
            }
        }

        if (partMap.TryGetValue(this, out var newMain))
        {
            ClearSelection();
            newMain.SelectGroup(false);
        }
    }

    private void Rip()
    {
        Rigidbody2D myRb = rb;

        foreach (AnchoredJoint2D j in Object.FindObjectsByType<AnchoredJoint2D>(FindObjectsSortMode.None))
        {
            if (j == null) continue;
            bool involves = j.attachedRigidbody == myRb || j.connectedBody == myRb;
            if (!involves) continue;

            foreach (Transform ch in j.gameObject.transform)
            {
                if ((ch.name == "Bolt" || ch.name == "Hinge" || ch.name == "Motor") &&
                    (ch.position - j.transform.TransformPoint(j.anchor)).sqrMagnitude < 1e-4f)
                {
                    Destroy(ch.gameObject);
                    break;
                }
            }
            Destroy(j);
        }

        foreach (var other in new List<Part>(connected))
        {
            connected.Remove(other);
            other.connected.Remove(this);
        }
    }

    private void DeleteGroup()
    {
        List<Part> grp = GetGroup();
        foreach (var p in grp) p.Rip();
        foreach (var p in grp) Destroy(p.gameObject);
        ClearSelection();
    }

    // ??????????????????????????? OUTLINE ???????????????????????????
    private void BuildOutline()
    {
        outline = new GameObject("Outline").AddComponent<LineRenderer>();
        outline.transform.SetParent(transform, false);
        outline.useWorldSpace = false;
        outline.loop = true;

        outline.startWidth = outline.endWidth = LINE_WIDTH;
        outline.material = new Material(Shader.Find("Sprites/Default"));
        outline.sortingOrder = sr ? sr.sortingOrder + 50 : 50;
        outline.numCornerVertices = 2;
        RecalculateOutline();
    }

    private void LateUpdate()
    {
        if (outline.enabled && transform.hasChanged)
        {
            RecalculateOutline();
            transform.hasChanged = false;
        }
        UpdateJointSprites();
    }

    private void RecalculateOutline()
    {
        if (col == null) return;

        if (col is CircleCollider2D c)
        {
            outline.positionCount = CIRCLE_SEGMENTS;
            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                float a = i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
                outline.SetPosition(i, new(Mathf.Cos(a) * c.radius, Mathf.Sin(a) * c.radius, 0f));
            }
        }
        else if (col is BoxCollider2D b)
        {
            Vector2 off = b.offset, h = b.size * 0.5f;
            outline.positionCount = 4;
            outline.SetPosition(0, new Vector3(off.x - h.x, off.y - h.y, 0f));
            outline.SetPosition(1, new Vector3(off.x - h.x, off.y + h.y, 0f));
            outline.SetPosition(2, new Vector3(off.x + h.x, off.y + h.y, 0f));
            outline.SetPosition(3, new Vector3(off.x + h.x, off.y - h.y, 0f));
        }
        else
        {
            var bb = col.bounds;
            Vector3 l = transform.InverseTransformPoint(bb.min);
            Vector3 h2 = transform.InverseTransformPoint(bb.max);
            outline.positionCount = 4;
            outline.SetPosition(0, new Vector3(l.x, l.y, 0f));
            outline.SetPosition(1, new Vector3(l.x, h2.y, 0f));
            outline.SetPosition(2, new Vector3(h2.x, h2.y, 0f));
            outline.SetPosition(3, new Vector3(h2.x, l.y, 0f));
        }
    }

    private void ShowOutline(Color c)
    {
        outline.startColor = outline.endColor = c;
        outline.enabled = true;
    }

    private void HideOutline()
    {
        if (outline) outline.enabled = false;

        // Ensure any stray Outline renderers duplicated via Instantiate are also disabled.
        foreach (LineRenderer lr in GetComponentsInChildren<LineRenderer>())
            if (lr != outline && lr.gameObject.name == "Outline")
                lr.enabled = false;
    }

    // ??????????????????????????? JOINT SPRITE FIX ???????????????????????????
    private void UpdateJointSprites()
    {
        foreach (Transform ch in transform)
        {
            if (ch.name == "Bolt")
            {
                Vector3 ps = transform.lossyScale;
                ch.localScale = new(Spawner.BoltVisualSize / ps.x, Spawner.BoltVisualSize / ps.y, 1f);
                ch.localRotation = Quaternion.identity;
            }
            else if (ch.name == "Hinge")
            {
                Vector3 ps = transform.lossyScale;
                ch.localScale = new(Spawner.HingeVisualSize / ps.x, Spawner.HingeVisualSize / ps.y, 1f);
                ch.localRotation = Quaternion.identity;
            }
            else if (ch.name == "Motor")
            {
                Vector3 ps = transform.lossyScale;
                ch.localScale = new(Spawner.MotorVisualSize / ps.x, Spawner.MotorVisualSize / ps.y, 1f);
                ch.localRotation = Quaternion.identity;
            }
        }
    }

    // ??????????????????????????? UTILITY ???????????????????????????
    private Vector3 GetWorldMouse()
    {
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        wp.z = 0f;
        return wp;
    }
}
