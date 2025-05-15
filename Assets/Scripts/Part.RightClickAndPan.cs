// Part.RightClickAndPan.cs – RMB context?menu open/close & pan?cancel logic
// CHANGELOG #23 (2025?05?13)
//   • All right?click hit?tests now also check IsTopUnderCursor() so the
//     visible top part always receives the click; lower parts are ignored.
//   • No other behaviour changed.

using UnityEngine;

public partial class Part
{
    // ????????????????????????????????????????????????????????????? RIGHT?CLICK
    private void HandleRightClick()
    {
        // Disable right?click selection while the game is running (live mode)
        if (!Frozen) return;

        /* ---------- press ---------- */
        if (Input.GetMouseButtonDown(1) &&
            col.OverlapPoint(GetWorldMouse()) && IsTopUnderCursor())
        {
            rightCandidate = true;
            rightStartScreen = Input.mousePosition;
        }
        if (rightCandidate && Input.GetMouseButton(1) &&
            Vector2.Distance(Input.mousePosition, rightStartScreen) > RIGHT_DRAG_PIXELS)
            rightCandidate = false;

        /* ---------- release ---------- */
        if (Input.GetMouseButtonUp(1) && rightCandidate &&
            col.OverlapPoint(GetWorldMouse()) && IsTopUnderCursor())
        {
            if (!showContextMenu && !choosingNoCollision)
            {
                bool inCurrentSelection = currentGroup.Contains(this);

                if (!inCurrentSelection)
                {
                    // NEW behaviour – clear selection and select ONLY this part
                    ClearSelection();
                    SelectGroup(new Part[] { this }, true); // opens menu
                }
                else
                {
                    // Keep existing multi?selection; just pop menu for this part
                    foreach (Part p in currentGroup) p.showContextMenu = false;
                    showContextMenu = true;
                    mainSelected = this;
                }

                menuGuiPos = new Vector2(Input.mousePosition.x,
                                         Screen.height - Input.mousePosition.y);
            }
        }

        // reset latch
        if (Input.GetMouseButtonUp(1)) rightCandidate = false;
    }

    // ????????????????????????????????????????????????????????????? PAN?CANCEL
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
}
