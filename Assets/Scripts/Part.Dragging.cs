// Part.Dragging.cs � drag / scale / rotate interaction
// CHANGELOG #51 (2025-05-15)
//   � Fix: Triangles can now be moved. DetectMode now returns DragMode.Move
//     for clicks inside the triangle interior. Scaling only triggers when
//     the cursor is near one of the three edges (vertical, horizontal, or
//     hypotenuse).

using UnityEngine;
using System.Collections.Generic;

public partial class Part
{
    /* -------------------------------------------------------------------------
     * STATIC
     * ---------------------------------------------------------------------- */
    private static Part dragLeader;

    /* -------------------------------------------------------------------------
     * CONSTANTS
     * ---------------------------------------------------------------------- */
    private const float MIN_START_DIST = 0.01f;      // safeguards circle scaling
    private const float CLICK_CANCEL_EPS2 = 1e-6f;   // ~0.001 world?units squared

    /* -------------------------------------------------------------------------
     * DRAG STATE
     * ---------------------------------------------------------------------- */
    // circle scaling
    private float scaleStartDist = 0f;
    private Vector3 scaleStartScale = Vector3.one;

    // rectangle / triangle?leg axis scaling
    private Vector3 axisStartScale = Vector3.one;

    // triangle hypotenuse
    private Vector2 triStartScale = Vector2.zero;
    private bool triSmallIsX = true;

    // outward?direction signs (axis & hypotenuse)
    private float signXScale = 1f;
    private float signYScale = 1f;

    /* --------------------------------------------------------------------- */
    /* BEGIN DRAG                                                            */
    /* --------------------------------------------------------------------- */
    private void BeginDrag()
    {
        IsInteracting = true;
        dragLeader = this;
        dragMode = DetectMode(GetWorldMouse());
        dragStartMouse = GetWorldMouse();

        /* parts that move together */
        List<Part> grp = new List<Part>();
        if (currentGroup.Count > 0)
        {
            foreach (Part sel in currentGroup)
                foreach (Part g in sel.GetGroup())
                    if (!grp.Contains(g))
                        grp.Add(g);
        }
        else
        {
            grp = GetGroup();
        }
        if (!grp.Contains(this))
            grp.Add(this);

        // Only auto?bring when globally enabled
        if (SortingOrderManager.AutoBringEnabled)
            SortingOrderManager.BringToFront(grp);

        /* cache start positions */
        startPos.Clear();
        groupCentroid = Vector3.zero;
        foreach (Part p in grp)
        {
            startPos[p] = p.transform.position;
            groupCentroid += p.transform.position;
        }
        groupCentroid /= grp.Count;

        /* outward?direction signs */
        Vector3 dirX = transform.right;
        Vector3 dirY = transform.up;
        signXScale = Mathf.Sign(Vector3.Dot(dragStartMouse - transform.position, dirX));
        signYScale = Mathf.Sign(Vector3.Dot(dragStartMouse - transform.position, dirY));
        if (signXScale == 0f) signXScale = 1f;
        if (signYScale == 0f) signYScale = 1f;

        /* ---------- mode?specific prep ---------- */

        // 1) Circle uniform
        if (dragMode == DragMode.ScaleCircle && col is CircleCollider2D)
        {
            scaleStartScale = transform.localScale;
            scaleStartDist = Mathf.Max(Vector3.Distance(dragStartMouse, transform.position),
                                         MIN_START_DIST); // prevent divide?by?zero
        }

        // 2) Triangle hypotenuse (ratio)
        if (dragMode == DragMode.ScaleCircle &&
            col is PolygonCollider2D pc && pc.points.Length == 3)
        {
            triStartScale = transform.localScale;
            triSmallIsX = triStartScale.x <= triStartScale.y;
        }

        // 3) Axis scaling (rect / triangle leg)
        if (dragMode == DragMode.ScaleX || dragMode == DragMode.ScaleY)
            axisStartScale = transform.localScale;
    }

    /* --------------------------------------------------------------------- */
    /* CONTINUE DRAG                                                         */
    /* --------------------------------------------------------------------- */
    private void ContinueDrag()
    {
        if (this != dragLeader) return;
        Vector3 curMouse = GetWorldMouse();

        /* NEW: cancel pending click?collapse once movement is detected */
        if (leftCandidate &&
            (curMouse - dragStartMouse).sqrMagnitude > CLICK_CANCEL_EPS2)
            leftCandidate = false;

        /* mid?drag rotation with R */
        if (Input.GetKeyDown(KeyCode.R) && currentGroup.Count == 1)
        {
            RotateSelectionDuringDrag();

            // refresh cached positions safely
            foreach (Part p in new List<Part>(startPos.Keys))
                startPos[p] = p.transform.position;

            dragStartMouse = curMouse;
            Physics2D.SyncTransforms();
            return;
        }

        /* scaling allowed only for single isolated part */
        bool canScale =
            currentGroup.Count == 1 && GetGroup().Count == 1 &&
            (dragMode == DragMode.ScaleX ||
             dragMode == DragMode.ScaleY ||
             dragMode == DragMode.ScaleCircle);

        if (canScale)
        {
            HandleScaling(curMouse);
            Physics2D.SyncTransforms();
            return;
        }

        /* translation � keep leader snapped in real?time */
        Vector3 rawDelta = curMouse - dragStartMouse;
        Vector3 snappedDelta =
            GridSnapping.SnapPos(startPos[this] + rawDelta) - startPos[this];

        foreach (var kv in startPos)
            kv.Key.transform.position = kv.Value + snappedDelta;

        Physics2D.SyncTransforms();
    }

    /* --------------------------------------------------------------------- */
    /* END DRAG                                                              */
    /* --------------------------------------------------------------------- */
    private void EndDrag()
    {
        IsInteracting = false;
        dragLeader = null;
        dragMode = DragMode.None;
        startPos.Clear();
    }

    /* --------------------------------------------------------------------- */
    /* ROTATION DURING DRAG (UNCHANGED SINCE #47)                            */
    /* --------------------------------------------------------------------- */
    private void RotateSelectionDuringDrag()
    {
        float curAngle = transform.eulerAngles.z;
        float nextSnap = NextSnapAngle(curAngle);
        float delta = Mathf.Approximately(nextSnap, curAngle)
                         ? ROTATE_STEP
                         : nextSnap - curAngle;

        Quaternion q = Quaternion.Euler(0f, 0f, delta);

        // Pivot is the leader so it stays under the cursor (pre?snap).
        Vector3 pivot = mainSelected.transform.position;

        foreach (var kv in startPos)
        {
            Part p = kv.Key;

            if (p == mainSelected)
            {
                // Leader rotates in place � no positional change.
                p.transform.rotation *= q;
                continue;
            }

            Vector3 off = p.transform.position - pivot;
            p.transform.position = pivot + q * off;
            p.transform.rotation *= q;
        }

        /* Snap the leader to the grid?cell under the cursor */
        Vector3 targetSnap = GridSnapping.SnapPos(GetWorldMouse());
        Vector3 snapOffset = targetSnap - mainSelected.transform.position;
        if (snapOffset.sqrMagnitude > 0f)
        {
            foreach (var kv in startPos.Keys)
                kv.transform.position += snapOffset;
        }
    }

    /* --------------------------------------------------------------------- */
    /* SCALING CORE (unchanged)                                              */
    /* --------------------------------------------------------------------- */
    private void HandleScaling(Vector3 worldMouse)
    {
        /* ===== 1. Triangle hypotenuse (ratio) ===== */
        if (dragMode == DragMode.ScaleCircle &&
            col is PolygonCollider2D pc && pc.points.Length == 3)
        {
            Vector3 dirX = transform.right;
            Vector3 dirY = transform.up;

            float deltaHalfX = Vector3.Dot(worldMouse - dragStartMouse, dirX);
            float deltaHalfY = Vector3.Dot(worldMouse - dragStartMouse, dirY);

            // Skip first frame (avoids unwanted snap)
            if (Mathf.Approximately(deltaHalfX, 0f) &&
                Mathf.Approximately(deltaHalfY, 0f))
                return;

            float rawX = triStartScale.x + deltaHalfX * 2f;
            float rawY = triStartScale.y + deltaHalfY * 2f;

            bool smallIsX = triSmallIsX;
            float startSmall = smallIsX ? triStartScale.x : triStartScale.y;
            float startLarge = smallIsX ? triStartScale.y : triStartScale.x;
            float curSmallRaw = smallIsX ? rawX : rawY;

            float snapSmall = GridSnapping.SnapScale(Mathf.Clamp(curSmallRaw, MIN_DIM, 999f));
            float scaleFactor = snapSmall / Mathf.Max(startSmall, 1e-4f);
            float newLarge = Mathf.Max(startLarge * scaleFactor, MIN_DIM);

            float finalX, finalY;
            if (smallIsX)
            {
                finalX = snapSmall;
                finalY = newLarge;
            }
            else
            {
                finalX = newLarge;
                finalY = snapSmall;
            }

            // Safety � reject non?finite values
            if (!float.IsFinite(finalX) || !float.IsFinite(finalY))
            {
                finalX = finalY = MIN_DIM;
            }

            transform.localScale = new Vector3(finalX, finalY, 1f);
            return;
        }

        /* ===== 2. Circle uniform ===== */
        if (dragMode == DragMode.ScaleCircle && col is CircleCollider2D)
        {
            float curDist = Vector3.Distance(worldMouse, transform.position);
            // Skip first frame (no cursor movement)
            if (Mathf.Approximately(curDist, scaleStartDist))
                return;

            float factor = curDist / Mathf.Max(scaleStartDist, MIN_START_DIST);
            float rawDia = scaleStartScale.x * factor;
            float snapDia = GridSnapping.SnapScale(Mathf.Clamp(rawDia, MIN_DIM, 999f));
            transform.localScale = Vector3.one * snapDia;
            return;
        }

        /* ===== 3. Axis (rect / triangle leg) ===== */
        if (dragMode == DragMode.ScaleX || dragMode == DragMode.ScaleY)
        {
            bool isX = dragMode == DragMode.ScaleX;
            Vector3 dir = isX ? transform.right : transform.up;
            float sign = isX ? signXScale : signYScale;

            float deltaHalf = Vector3.Dot(worldMouse - dragStartMouse, dir) * sign;
            // Skip first frame (avoids 1?unit snap)
            if (Mathf.Approximately(deltaHalf, 0f))
                return;

            float rawFull = (isX ? axisStartScale.x : axisStartScale.y) + deltaHalf * 2f;
            float snap = GridSnapping.SnapScale(Mathf.Clamp(rawFull, MIN_DIM, 999f));

            Vector3 ls = transform.localScale;
            if (isX) ls.x = snap; else ls.y = snap;
            transform.localScale = ls;
            return;
        }
    }

    /* --------------------------------------------------------------------- */
    /* MODE DETECTION                                                        */
    /* --------------------------------------------------------------------- */
    private DragMode DetectMode(Vector3 worldMouse)
    {
        if (currentGroup.Count > 1 || GetGroup().Count > 1)
            return DragMode.Move;

        /* ---------------- Circle ---------------- */
        if (col is CircleCollider2D cc)
        {
            float dist = transform.InverseTransformPoint(worldMouse).magnitude;
            return Mathf.Abs(dist - cc.radius) < EDGE_BAND
                   ? DragMode.ScaleCircle
                   : DragMode.Move;
        }

        /* ---------------- Box ---------------- */
        if (col is BoxCollider2D bc)
        {
            Vector3 local = transform.InverseTransformPoint(worldMouse) - (Vector3)bc.offset;
            float hx = bc.size.x * 0.5f;
            float hy = bc.size.y * 0.5f;
            bool nearX = Mathf.Abs(Mathf.Abs(local.x) - hx) < EDGE_BAND;
            bool nearY = Mathf.Abs(Mathf.Abs(local.y) - hy) < EDGE_BAND;

            if (nearX && !nearY) return DragMode.ScaleX;
            if (nearY && !nearX) return DragMode.ScaleY;
            if (nearX && nearY)
                return Mathf.Abs(Mathf.Abs(local.x) - hx) <
                       Mathf.Abs(Mathf.Abs(local.y) - hy)
                       ? DragMode.ScaleX
                       : DragMode.ScaleY;

            return DragMode.Move;
        }

        /* ---------------- Right?triangle ---------------- */
        if (col is PolygonCollider2D pc && pc.points.Length == 3)
        {
            // Local space (after scale & rotation)
            Vector3 local = transform.InverseTransformPoint(worldMouse) - (Vector3)pc.offset;

            // Edge proximity checks (triangle defined by points:
            // (-0.5,-0.5), (0.5,-0.5), (-0.5,0.5))
            bool nearLeft =
                Mathf.Abs(local.x + 0.5f) < EDGE_BAND &&
                local.y >= -0.5f - EDGE_BAND && local.y <= 0.5f + EDGE_BAND;

            bool nearBottom =
                Mathf.Abs(local.y + 0.5f) < EDGE_BAND &&
                local.x >= -0.5f - EDGE_BAND && local.x <= 0.5f + EDGE_BAND;

            bool nearHyp =
                Mathf.Abs(local.x + local.y) < EDGE_BAND &&
                local.x >= -0.5f - EDGE_BAND && local.x <= 0.5f + EDGE_BAND &&
                local.y >= -0.5f - EDGE_BAND && local.y <= 0.5f + EDGE_BAND;

            if (nearHyp) return DragMode.ScaleCircle; // uniform scale
            if (nearLeft) return DragMode.ScaleX;      // vertical edge
            if (nearBottom) return DragMode.ScaleY;      // horizontal edge

            return DragMode.Move;                        // interior � move
        }

        return DragMode.Move;
    }
}
