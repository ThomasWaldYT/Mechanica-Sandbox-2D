// Part.Connectivity.cs
// Connectivity graph and no?collision utilities
// CHANGELOG #20 (2025?05?15)
//   • Making a connection between *already?selected* parts no longer clears
//     the current selection. The entire previous selection is preserved.
//   • Original behaviour is kept when neither of the connected parts was
//     selected beforehand.

using UnityEngine;
using System.Collections.Generic;

public partial class Part
{
    // ????????????????????????????????????????????????????????????????????????????
    // CONNECTIVITY
    // ????????????????????????????????????????????????????????????????????????????

    // Clear all disabled?collision links that exist *between* two distinct
    // connectivity groups (g1 and g2). Links internal to each group or with
    // external parts are untouched.
    private static void ResetDisabledCollisionsBetweenGroups(List<Part> g1, List<Part> g2)
    {
        foreach (Part a in g1)
        {
            foreach (Part b in g2)
            {
                // Only act if a?b were previously non?colliding
                if (a.noCollision.Contains(b))
                    a.SetCollisionDisabledWith(b, false); // re?enable
            }
        }
    }

    /// <summary>
    /// Register <paramref name="p"/> as connected to <c>this</c> part.  Handles
    /// group merging, collision rules, and selection refresh.
    /// </summary>
    public void AddConnectedPart(Part p)
    {
        if (p == null || p == this) return;

        /* ------------------------------------------------------------------ */
        /*  Merge connectivity groups and manage collision rules              */
        /* ------------------------------------------------------------------ */

        // Capture groups BEFORE they merge so we can identify inter?group links.
        List<Part> groupA = GetGroup();
        List<Part> groupB = p.GetGroup();

        // If the groups are distinct *now*, they will merge by the time this
        // method completes.  Clear any disabled?collision links BETWEEN them so
        // everything collides normally in the combined group.
        bool groupsAreDistinct = !groupA.Contains(p); // quick check
        if (groupsAreDistinct)
            ResetDisabledCollisionsBetweenGroups(groupA, groupB);

        // ------------------------------------------------------------------
        // Standard bookkeeping
        // ------------------------------------------------------------------
        if (!connected.Contains(p)) connected.Add(p);
        if (!p.connected.Contains(this)) p.connected.Add(this);

        // ALWAYS disable collisions between the *newly* connected pair so the
        // physical joint does not immediately collide with itself.
        if (!noCollision.Contains(p))
            SetCollisionDisabledWith(p, true);

        /* ------------------------------------------------------------------ */
        /*  Selection & context?menu refresh                                  */
        /* ------------------------------------------------------------------ */
        if (!Frozen) return;

        bool eitherPreSelected = currentGroup.Contains(this) || currentGroup.Contains(p);

        if (eitherPreSelected)
        {
            // Preserve the full existing selection; just refresh visuals.
            UpdateSelectionVisuals();
        }
        else
        {
            // Legacy behaviour when neither part was selected.
            Part preferred = (mainSelected == this || mainSelected == p) ? mainSelected : this;
            bool keepMenu = preferred != null && preferred.showContextMenu;

            ClearSelection();

            if (preferred != null)
            {
                SelectGroup(new Part[] { preferred }, keepMenu);
                mainSelected = preferred;
            }
        }
    }

    // Retrieve the full connectivity group containing this part.
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

    // ????????????????????????????????????????????????????????????????????????????
    // NO?COLLISION UTILITIES (unchanged except made internal use above)
    // ????????????????????????????????????????????????????????????????????????????
    private void SetCollisionDisabledWith(Part other, bool disable)
    {
        if (other == null || other == this) return;
        if (disable)
        {
            if (noCollision.Add(other))
            {
                other.noCollision.Add(this);
                Physics2D.IgnoreCollision(col, other.col, true);
            }
        }
        else
        {
            if (noCollision.Remove(other))
            {
                other.noCollision.Remove(this);
                Physics2D.IgnoreCollision(col, other.col, false);
            }
        }
    }

    private void ResetNoCollisions()
    {
        foreach (var o in new List<Part>(noCollision))
            SetCollisionDisabledWith(o, false);
    }
}
