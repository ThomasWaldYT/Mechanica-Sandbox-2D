// Part.Input.cs – per-frame Update & mouse handling
// CHANGELOG #50 (2025-05-15)
//   • Selection collapse onto a single part is now **deferred** until the
//     mouse button is released *and* no drag movement occurred. This allows
//     multi-part selections to be dragged together without collapsing
//     immediately on mouse?down. Behaviour when Shift is held or when
//     clicking unselected parts is unchanged.
//   • Implementation notes:
//       – On mouse?down over an already?selected part (without Shift), we now
//         mark the click as a candidate (leftCandidate = true) **but do not**
//         collapse the selection.
//       – BeginDrag() is still invoked immediately so the user can start
//         moving parts right away.
//       – In Part.Dragging.cs, the click candidate is cleared automatically
//         once the cursor moves beyond a tiny threshold, preventing unwanted
//         collapse after a drag.
//
// (Previous changelog #32 retained below for reference.)
// CHANGELOG #32 (2025-05-15)
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
        if (PointerOverAnyMenu()) return; // block selection through UI menus

        Vector3 wMouse = GetWorldMouse();
        bool overThis = col.OverlapPoint(wMouse);
        bool topHere = overThis && IsTopUnderCursor();
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        /* ---------- press ---------- */
        if (Input.GetMouseButtonDown(0) && topHere)
        {
            if (IsSelected() && !shift)
            {
                // Part of an existing multi?selection – prepare for possible
                // collapse on mouse?up, but keep the current selection alive
                // so the user can drag multiple parts immediately.
                leftCandidate = true;      // click candidate (may be cancelled)
                BeginDrag();               // immediate drag with full selection
            }
            else
            {
                leftCandidate = true;      // arm for potential selection on release
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
