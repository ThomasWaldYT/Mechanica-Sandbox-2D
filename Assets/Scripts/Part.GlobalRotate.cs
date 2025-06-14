// Part.GlobalRotate.cs – R key shortcut for rotating a selection when *not* dragging
// CHANGELOG #52 (2025?05?15)
//   • Bug?fix: rotation shortcut now checks Part.IsInteracting instead of the
//     per?part dragMode flag.  This prevents a stale non?None dragMode on the
//     leader part from blocking the shortcut, restoring reliable group
//     rotation while *not* dragging.
//   • No other behaviour changed.

using System.Collections.Generic;
using UnityEngine;

public partial class Part
{
    // ---------------------------------------------------------------- HANDLE R KEY
    private void HandleGlobalRotationShortcut()
    {
        /* 1. Only run in design mode (frozen) and when *no* drag is underway. */
        if (!Frozen || IsInteracting) return;

        /* 2. Key test */
        if (!Input.GetKeyDown(KeyCode.R)) return;
        if (currentGroup.Count == 0) return;

        /* 3. Determine the pivot part that will stay fixed under the cursor. */
        Part leader = mainSelected ? mainSelected : currentGroup[0];
        if (this != leader) return;                // only the leader runs the code once

        RotateCurrentGroupAndSnap(leader);
        Physics2D.SyncTransforms();
    }

    // ---------------------------------------------------------------- CORE LOGIC
    private static void RotateCurrentGroupAndSnap(Part leader)
    {
        /* Gather the full connectivity union of the current selection. */
        HashSet<Part> parts = new();
        foreach (Part sel in currentGroup)
            foreach (Part g in sel.GetGroup())
                parts.Add(g);

        /* --- 1)  Snap the *current* angle to the next 45° increment. -------- */
        float curAngle = leader.transform.eulerAngles.z;
        float nextSnap = NextSnapAngle(curAngle);
        float deltaAng = Mathf.Approximately(nextSnap, curAngle)
                         ? GridSnapping.AngleSnap
                         : nextSnap - curAngle;
        Quaternion dq = Quaternion.Euler(0f, 0f, deltaAng);

        /* --- 2)  Rotate every part around the leader. ---------------------- */
        Vector3 pivot = leader.transform.position;
        foreach (Part p in parts)
        {
            if (p == leader)
            {
                p.transform.rotation *= dq;        // rotate in place
                continue;
            }

            Vector3 off = p.transform.position - pivot;
            p.transform.position = pivot + dq * off;
            p.transform.rotation *= dq;
        }

        /* --- 3)  Snap the leader back onto the 0.25?grid and apply the same
         *         offset to every other part so the whole assembly remains
         *         rigid but perfectly aligned.                                */
        Vector3 snapped = GridSnapping.SnapPos(leader.transform.position);
        Vector3 snapDelta = snapped - leader.transform.position;
        if (snapDelta.sqrMagnitude > 0f)
        {
            foreach (Part p in parts)
                p.transform.position += snapDelta;
        }
    }

    // Return the next positive multiple of GridSnapping.AngleSnap (> angle).  
    private static float NextSnapAngle(float angle)
    {
        float norm = Mathf.Repeat(angle, 360f);
        return (Mathf.Floor(norm / GridSnapping.AngleSnap) + 1f) * GridSnapping.AngleSnap;
    }
}
