// Part.Selection.cs – selection & outline logic
// CHANGELOG #35 (2025-05-15)
//   - Fixed outline sorting bug where the newly selected main part’s outline
//     sometimes rendered behind existing outlines.  mainSelected is now set
//     before ShowOutline() is invoked so the correct +60 sorting offset is
//     applied.

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public partial class Part
{
    // ================================================================= CLEAR ALL
    public static void ClearSelection()
    {
        foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
        {
            p.HideOutline();
            p.showContextMenu = false;
        }
        currentGroup.Clear();
        mainSelected = null;
    }

    // ================================================================ CLICK HANDLER
    public void HandleClickSelection(bool shiftHeld)
    {
        if (shiftHeld)
        {
            if (currentGroup.Contains(this)) currentGroup.Remove(this);
            else currentGroup.Add(this);
        }
        else
        {
            currentGroup.Clear();
            currentGroup.Add(this);
        }

        UpdateSelectionVisuals();
    }

    public bool IsSelected() => currentGroup.Contains(this);

    // ================================================================ VISUALS
    private static void UpdateSelectionVisuals()
    {
        HashSet<Part> selected = new(currentGroup);
        bool multi = selected.Count > 1;

        /* ------------------------------------------------------------------
         * Determine the new mainSelected first so ShowOutline() can apply the
         * correct +60 sorting offset for the main selection. */
        mainSelected = selected.Count == 1 ? selected.First() : null;
        /* ------------------------------------------------------------------ */

        /* helper sets */
        HashSet<Part> secondary = new();
        foreach (Part sel in selected)
            foreach (Part g in sel.GetGroup())
                if (!selected.Contains(g)) secondary.Add(g);

        /* outlines */
        foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
        {
            if (selected.Contains(p))
            {
                p.ShowOutline(mainColour);
                continue;
            }

            if (multi)
            {
                bool disabledForAll = selected.All(s => s.noCollision.Contains(p));
                if (disabledForAll)
                {
                    p.ShowOutline(Spawner.NoCollisionOutlineExternal);
                    continue;
                }
            }
            else if (selected.Count == 1)
            {
                Part solo = selected.First();
                if (solo.noCollision.Contains(p))
                {
                    p.ShowOutline(Spawner.NoCollisionOutline);
                    continue;
                }
            }

            if (secondary.Contains(p))
                p.ShowOutline(secondaryColour);
            else
                p.HideOutline();
        }
    }

    // ================================================================ CONTEXT TEST
    public static bool IsPointerOverContextMenuArea()
    {
        if (mainSelected == null || !mainSelected.showContextMenu) return false;
        Vector2 guiMouse = new(Input.mousePosition.x,
                               Screen.height - Input.mousePosition.y);
        return mainSelected.contextMenuRect.Contains(guiMouse);
    }

    // ================================================================ LEGACY HELPERS
    public void SelectAsSingle()
    {
        currentGroup.Clear();
        currentGroup.Add(this);
        UpdateSelectionVisuals();
        showContextMenu = false;
    }

    public void SelectGroup(bool showSlider = false)
    {
        SelectGroup(GetGroup(), showSlider);
    }

    public static void SelectGroup(IEnumerable<Part> parts, bool showSlider = false)
    {
        currentGroup.Clear();
        currentGroup.AddRange(parts.Distinct());
        UpdateSelectionVisuals();

        if (currentGroup.Count > 0)
            currentGroup[0].showContextMenu = showSlider;
    }
}
