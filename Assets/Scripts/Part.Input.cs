// Part.Input.cs – per?frame Update & mouse handling
// CHANGELOG #32 (2025?05?15)
//   • Clicking a part that is already selected *without* Shift now shrinks the
//     selection to that single part before beginning any drag. This lets you
//     quickly collapse a multi?selection to one part with a simple click.
//   • No other behaviour changed.

using UnityEngine;

public partial class Part
{
    // ------------------------------------------------------------------ state
    private bool leftCandidate;           // true after valid press; cleared on up

    // ------------------------------------------------------------------ helper
    /// <summary>
    /// True if the cursor is currently inside *any* context menu (part or motor).
    /// </summary>
    private static bool PointerOverAnyMenu()
        => IsPointerOverContextMenuArea() || Motor.IsPointerOverContextMenuArea();

    // ------------------------------------------------------------------ Update
    private void Update()
    {
        // Disable collider while context menu is over this part so clicks hit UI
        col.enabled = !showContextMenu;

        // Deselect when unfreezing
        if (!Frozen && lastFrozen) Part.ClearSelection();
        lastFrozen = Frozen;

        // Close menus on scroll
        if (showContextMenu && Input.mouseScrollDelta.y != 0f)
            showContextMenu = false;

        // Per?frame interaction handlers
        HandleLeftClick();
        HandleRightClick();
        HandlePanCancel();
        HandleGlobalRotationShortcut();
        HandleChooseNoCollisionMode();
    }

    // ================================================================ LEFT?CLICK
    private void HandleLeftClick()
    {
        if (!Frozen || choosingNoCollision) return;
        if (PointerOverAnyMenu()) return; // NEW: block selection through UI menus

        Vector3 wMouse = GetWorldMouse();
        bool overThis = col.OverlapPoint(wMouse);
        bool topHere = overThis && IsTopUnderCursor();
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /* ---------- press ---------- */
        if (Input.GetMouseButtonDown(0) && topHere)
        {
            // Second click on an already?selected part – now SINGLE?select it
            if (IsSelected() && !shift)
            {
                if (currentGroup.Count > 1)
                    SelectAsSingle();      // collapse to just this part

                BeginDrag();               // immediate drag (unchanged)
            }
            else
            {
                leftCandidate = true;     // arm for potential selection on release
            }
        }

        /* ---------- hold (drag) ---------- */
        if (Input.GetMouseButton(0) && IsInteracting)
            ContinueDrag();

        /* ---------- release ---------- */
        if (Input.GetMouseButtonUp(0))
        {
            if (leftCandidate && topHere)
                HandleClickSelection(shift);

            leftCandidate = false;

            if (IsInteracting)
                EndDrag();
        }
    }

    // =================================================================== CURSOR
    private void OnMouseOver()
    {
        if (!Frozen || choosingNoCollision) return;
        if (PointerOverAnyMenu()) return; // avoid cursor swaps over UI

        if (IsSelected())
            ApplyCursor(DetectMode(GetWorldMouse()) == DragMode.Move ? curMove : curScale);
        else
            ApplyCursor(curDefault);
    }

    private void OnMouseExit()
    {
        if (!choosingNoCollision) ApplyCursor(curDefault);
    }

    // ========================================================== TOP?OF?STACK TEST
    /// <summary>
    /// True iff <c>this</c> is the highest?sorting <see cref="Part"/> whose
    /// collider overlaps the current mouse position.
    /// </summary>
    private bool IsTopUnderCursor()
    {
        Vector2 m = GetWorldMouse();
        Collider2D[] hits = Physics2D.OverlapPointAll(m);

        Part top = null;
        int highestSO = int.MinValue;

        foreach (Collider2D h in hits)
        {
            if (h.TryGetComponent(out Part p))
            {
                int so = p.sr ? p.sr.sortingOrder : 0;
                if (so > highestSO)
                {
                    highestSO = so;
                    top = p;
                }
            }
        }
        return top == this;
    }
}
