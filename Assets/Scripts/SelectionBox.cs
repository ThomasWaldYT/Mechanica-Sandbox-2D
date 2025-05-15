// SelectionBox.cs
// Marquee multi?select with full Shift behaviour – outlines never disappear.
// 2025?05?15  v2.1  (keeps selection visible while Shift?clicking blank space)

using UnityEngine;
using System.Collections.Generic;

public class SelectionBox : MonoBehaviour
{
    private const float DRAG_THRESHOLD = 4f;          // px before a drag counts
    private bool boxCandidate = false;              // pressed but below threshold
    private bool isSelecting = false;              // actively drawing the box
    private Vector2 startScreen;                      // press pos (screen) Y?up
    private Texture2D whiteTex;                       // 1?px GUI texture

    private readonly List<Part> initialSel = new();   // snapshot at press
    private bool keepSelection = false;               // guard outlines while holding

    private bool Frozen => Time.timeScale == 0f;

    private void Awake() => whiteTex = Texture2D.whiteTexture;

    private void Update()
    {
        if (!Frozen) { ResetFlags(); return; }
        if (Part.IsInteracting) return;

        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) ||
                         Input.GetKey(KeyCode.RightShift);

        /* ---------------------------------------------------- press */
        if (Input.GetMouseButtonDown(0) && !PointerOverMenu())
        {
            Vector3 wp = ScreenToWorld(Input.mousePosition);
            bool overSelected = false;

            foreach (Collider2D h in Physics2D.OverlapPointAll(wp))
                if (h.TryGetComponent(out Part p) && p.IsSelected())
                { overSelected = true; break; }

            if (SelectionCount() == 0 || !overSelected)
            {
                boxCandidate = true;
                startScreen = Input.mousePosition;
                keepSelection = shiftHeld;            // guard outlines if Shift

                /* snapshot current selection */
                initialSel.Clear();
                foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
                    if (p.IsSelected()) initialSel.Add(p);
            }
        }

        /* ----------------------------------------------- movement */
        if (boxCandidate && !isSelecting &&
            Vector2.Distance(Input.mousePosition, startScreen) > DRAG_THRESHOLD)
        {
            isSelecting = true;          // we crossed the threshold – start box
        }

        /* ---------------------------------------------------- release */
        if (Input.GetMouseButtonUp(0))
        {
            if (isSelecting)
            {
                ApplyMarquee(shiftHeld);
            }

            ResetFlags();
        }
    }

    /* ================================================================= GUI */
    private void OnGUI()
    {
        if (!isSelecting) return;

        Vector2 cur = Input.mousePosition;
        Vector2 p1 = new(startScreen.x, Screen.height - startScreen.y);
        Vector2 p2 = new(cur.x, Screen.height - cur.y);

        Rect r = new(Mathf.Min(p1.x, p2.x),
                     Mathf.Min(p1.y, p2.y),
                     Mathf.Abs(p2.x - p1.x),
                     Mathf.Abs(p2.y - p1.y));

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0.6f, 1f, 0.15f);  // light cyan, 15?% alpha
        GUI.DrawTexture(r, whiteTex);
        GUI.color = prev;
    }

    /* ============================================================ LATE UPDATE
     * Runs after Spawner may have cleared the selection; instantly restores it
     * so outlines never blink off while holding Shift.                            */
    private void LateUpdate()
    {
        if (!keepSelection) return;
        if (SelectionCount() > 0) return;             // nothing to fix
        if (initialSel.Count == 0) return;

        Part.SelectGroup(initialSel, false);          // silent restore
    }

    /* ============================================================ CORE */
    private void ApplyMarquee(bool shiftHeld)
    {
        Vector3 w0 = ScreenToWorld(startScreen);
        Vector3 w1 = ScreenToWorld(Input.mousePosition);

        Vector2 min = new(Mathf.Min(w0.x, w1.x), Mathf.Min(w0.y, w1.y));
        Vector2 max = new(Mathf.Max(w0.x, w1.x), Mathf.Max(w0.y, w1.y));

        List<Part> hits = new();

        foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
        {
            Collider2D c = p.GetComponent<Collider2D>();
            if (!c) continue;

            Bounds b = c.bounds;
            bool overlap =
                b.min.x <= max.x && b.max.x >= min.x &&
                b.min.y <= max.y && b.max.y >= min.y;

            if (overlap) hits.Add(p);
        }

        List<Part> finalSel = new();
        if (shiftHeld) finalSel.AddRange(initialSel);  // keep originals
        finalSel.AddRange(hits);

        Part.ClearSelection();
        if (finalSel.Count > 0) Part.SelectGroup(finalSel, false);
    }

    /* ============================================================ HELPERS */
    private static Vector3 ScreenToWorld(Vector3 s)
    {
        Vector3 w = Camera.main.ScreenToWorldPoint(s);
        w.z = 0f;
        return w;
    }

    private static int SelectionCount()
    {
        int n = 0;
        foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
            if (p.IsSelected()) n++;
        return n;
    }

    private static bool PointerOverMenu() =>
        Part.IsPointerOverContextMenuArea() || Motor.IsPointerOverContextMenuArea();

    private void ResetFlags()
    {
        boxCandidate = false;
        isSelecting = false;
        keepSelection = false;
        initialSel.Clear();
    }
}
