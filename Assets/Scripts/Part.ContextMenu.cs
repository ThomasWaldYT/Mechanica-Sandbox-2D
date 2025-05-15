// Part.ContextMenu.cs – right?click GUI, duplicate, rip, disable collisions, delete
// CHANGELOG #29 (2025?05?13)
//   • Delete now respects multi?selection:
//       – When two or more parts are selected, “Delete” removes *all* selected
//         parts together with every part in their respective connectivity
//         groups.
//       – Single?selection behaviour is unchanged.

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public partial class Part
{
    // ?????????????????????????????????????????????????????????????? GUI ????????????????????????????????????????????????????????????? //
    private void OnGUI()
    {
        /* ==== CHOOSE?MODE OVERLAY ==== */
        if (choosingNoCollision && Frozen && chooseSource == this)
        {
            GUIStyle hdr = new GUIStyle(GUI.skin.label)
            { fontSize = 90, alignment = TextAnchor.MiddleCenter };
            GUIStyle btnLarge = new GUIStyle(GUI.skin.button)
            { fontSize = 72 };

            Rect rText = new Rect(0, 20, Screen.width, 120);
            GUI.Label(rText, "Choose which parts to disable collisions with:", hdr);

            float btnW = 360f, btnH = 120f;
            Rect rBtn = new Rect(Screen.width * 0.5f - btnW * 0.5f, 160, btnW, btnH);
            if (GUI.Button(rBtn, "Done", btnLarge))
            {
                CancelChooseMode();      // collision states already updated live
            }
        }

        /* ==== CONTEXT MENU ==== */
        if (!showContextMenu || !Frozen) return;
        bool multiSelect = currentGroup.Count > 1;

        const float UI_SCALE = 2f;
        GUIStyle lblStyle = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.RoundToInt(20 * UI_SCALE) };
        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        { fontSize = Mathf.RoundToInt(20 * UI_SCALE) };

        float W0 = 220f, LINE_H0 = 22f, SLIDER_H0 = 18f, PAD0 = 8f;

        bool showRipBtn = multiSelect ? currentGroup.Any(p => p.GetGroup().Count > 1)
                                      : GetGroup().Count > 1;
        bool showDisableBtn = true;

        int lineCount = 2;                               // Duplicate + Delete
        if (showRipBtn) lineCount++;
        if (showDisableBtn) lineCount++;
        lineCount += 2;                                  // new Bring / Send buttons

        float W = W0 * UI_SCALE;
        float LINE_H = LINE_H0 * UI_SCALE;
        float SLIDER_H = SLIDER_H0 * UI_SCALE;
        float PAD = PAD0 * UI_SCALE;

        float menuH = PAD * 2 +
                      lblStyle.lineHeight +
                      SLIDER_H + 12f * UI_SCALE +
                      lineCount * (LINE_H + 4f * UI_SCALE);

        Rect bgR = new Rect(menuGuiPos.x + 20f * UI_SCALE,
                            menuGuiPos.y - menuH * 0.5f,
                            W, menuH);
        contextMenuRect = bgR;

        Color prevCol = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        GUI.Box(bgR, GUIContent.none);
        GUI.color = prevCol;

        float y = bgR.y + PAD;

        /* ==== MASS SLIDER ==== */
        Rect labR = new Rect(bgR.x + PAD, y, W - PAD * 2, lblStyle.lineHeight);
        y += lblStyle.lineHeight;
        Rect sliR = new Rect(bgR.x + PAD, y, W - PAD * 2, SLIDER_H);
        y += SLIDER_H + 12f * UI_SCALE;

        GUI.Label(labR, $"Mass {mass:0}", lblStyle);
        float newMass = GUI.HorizontalSlider(sliR, mass, 1f, 100f);

        if (!Mathf.Approximately(newMass, mass))
        {
            if (multiSelect)
            {
                foreach (Part p in currentGroup)
                {
                    p.mass = newMass;
                    p.rb.mass = newMass;
                    p.UpdateBrightness();
                }
            }
            else
            {
                mass = newMass;
                rb.mass = newMass;
                UpdateBrightness();
            }
        }

        /* ==== DUPLICATE ==== */
        Rect dupR = new Rect(bgR.x + PAD, y, W - PAD * 2, LINE_H);
        y += LINE_H + 4f * UI_SCALE;

        if (GUI.Button(dupR, "Duplicate", btnStyle))
        {
            if (multiSelect) DuplicateSelection();
            else DuplicateGroup();

            showContextMenu = false;
            return;
        }

        /* ==== RIP ==== */
        if (showRipBtn)
        {
            Rect ripR = new Rect(bgR.x + PAD, y, W - PAD * 2, LINE_H);
            y += LINE_H + 4f * UI_SCALE;

            if (GUI.Button(ripR, "Rip", btnStyle))
            {
                if (multiSelect) RipSelection();
                else Rip();

                showContextMenu = false;
                return;
            }
        }

        /* ==== DISABLE COLLISIONS ==== */
        if (showDisableBtn)
        {
            Rect nocR = new Rect(bgR.x + PAD, y, W - PAD * 2, LINE_H);
            y += LINE_H + 4f * UI_SCALE;

            if (GUI.Button(nocR, "Disable Collisions", btnStyle))
            {
                choosingNoCollision = true;
                chooseSource = this;
                chooseSelected.Clear();

                if (multiSelect)
                {
                    /* intersection of every selected part's noCollision set */
                    HashSet<Part> inter = null;
                    foreach (Part p in currentGroup)
                    {
                        if (inter == null) inter = new HashSet<Part>(p.noCollision);
                        else inter.IntersectWith(p.noCollision);
                    }
                    if (inter != null) chooseSelected.UnionWith(inter);
                }
                else
                {
                    chooseSelected.UnionWith(noCollision);
                }

                /* ---- outline pass ---- */
                foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
                {
                    if (currentGroup.Contains(p)) continue;      // never self / main
                    bool show = chooseSelected.Contains(p);

                    if (multiSelect)
                    {
                        if (show)
                            p.ShowOutline(Spawner.NoCollisionOutlineExternal); // yellow
                        else
                            p.HideOutline();
                    }
                    else
                    {
                        if (show)
                            p.ShowOutline(Spawner.NoCollisionOutline);        // magenta
                        else if (GetGroup().Contains(p))
                            p.ShowOutline(secondaryColour);                    // group?mate
                        else
                            p.HideOutline();
                    }
                }

                showContextMenu = false;
            }
        }

        // ---- BRING TO FRONT ----
        Rect bringR = new Rect(bgR.x + PAD, y, W - PAD * 2, LINE_H);
        y += LINE_H + 4f * UI_SCALE;
        if (GUI.Button(bringR, "Bring to Front", btnStyle))
        {
            IEnumerable<Part> target = multiSelect ? currentGroup : GetGroup();
            SortingOrderManager.BringToFront(target);
            showContextMenu = false;
            return;
        }

        // ---- SEND TO BACK ----
        Rect backR = new Rect(bgR.x + PAD, y, W - PAD * 2, LINE_H);
        y += LINE_H + 4f * UI_SCALE;
        if (GUI.Button(backR, "Send to Back", btnStyle))
        {
            IEnumerable<Part> target = multiSelect ? currentGroup : GetGroup();
            SortingOrderManager.SendToBack(target);
            showContextMenu = false;
            return;
        }

        /* ==== DELETE ==== */
        Rect delR = new Rect(bgR.x + PAD, y, W - PAD * 2, LINE_H);
        y += LINE_H + 4f * UI_SCALE;
        if (GUI.Button(delR, "Delete", btnStyle))
        {
            if (multiSelect) DeleteSelection(); else DeleteGroup();
            return;
        }

        /* ==== outside?click closes menu ==== */
        Event ev = Event.current;
        if (ev.isMouse && ev.type == EventType.MouseDown && ev.button == 0 &&
            !bgR.Contains(ev.mousePosition))
        {
            showContextMenu = false;
            ev.Use();
        }
        if (ev.isMouse && ev.type == EventType.MouseDown && ev.button == 1 &&
            bgR.Contains(ev.mousePosition))
            ev.Use();
    }

    // ???????????????????????????? DUPLICATION HELPERS ???????????????????????? //

    // -------------------------------------------------------------------- multi?select
    private static void DuplicateSelection()
    {
        if (currentGroup.Count == 0) return;

        /*  1) Build connectivity?units, selection?set, and global map  */
        List<List<Part>> units = new();
        HashSet<Part> visited = new();
        HashSet<Part> selectionSet = new();
        Dictionary<Part, Part> globalMap = new();   // original ? clone

        foreach (Part p in currentGroup)
        {
            if (visited.Contains(p)) continue;
            List<Part> grp = p.GetGroup();
            foreach (Part g in grp) { visited.Add(g); selectionSet.Add(g); }
            units.Add(grp);
        }

        /*  2) Clone every unit (physical copy only)  */
        List<Part> newSelection = new();
        foreach (List<Part> unit in units)
            newSelection.AddRange(CloneUnit(unit, globalMap));

        /*  3) Re?create *all* no?collision links on the clones  */
        foreach (Part orig in selectionSet)
        {
            if (!globalMap.TryGetValue(orig, out Part dup)) continue;

            foreach (Part other in orig.noCollision)
            {
                // case A – partner ALSO duplicated
                if (selectionSet.Contains(other))
                {
                    if (globalMap.TryGetValue(other, out Part dupOther))
                        dup.SetCollisionDisabledWith(dupOther, true);
                }
                // case B – partner external to duplication
                else
                {
                    dup.SetCollisionDisabledWith(other, true);
                }
            }
        }

        /*  4) Update selection  */
        ClearSelection();
        SelectGroup(newSelection, false);
    }

    // -------------------------------------------------------------------- single?group
    private void DuplicateGroup()
    {
        List<Part> grp = GetGroup();
        if (grp.Count == 0) return;

        Dictionary<Part, Part> globalMap = new();
        List<Part> clones = CloneUnit(grp, globalMap);   // physical copy

        /*  copy collision rules (skip links pointing to the originals)  */
        foreach (Part orig in grp)
        {
            Part dup = globalMap[orig];

            foreach (Part other in orig.noCollision)
            {
                if (grp.Contains(other))
                {
                    dup.SetCollisionDisabledWith(globalMap[other], true); // clone?clone
                }
                else
                {
                    dup.SetCollisionDisabledWith(other, true);            // clone?external
                }
            }
        }

        /*  select new leader matching the context?click origin  */
        if (globalMap.TryGetValue(this, out Part newMain))
        {
            ClearSelection();
            newMain.SelectAsSingle();
        }
    }

    // -------------------------------------------------------------------- core cloning
    /// <summary>
    /// Clone every part in <paramref name="unit"/>, remap joints, and register
    /// the <c>original ? clone</c> pairs in <paramref name="globalMap"/>.  
    /// Returns the freshly created clones.
    /// </summary>
    private static List<Part> CloneUnit(List<Part> unit, Dictionary<Part, Part> globalMap)
    {
        if (unit == null || unit.Count == 0) return new();

        /*  place the duplicate just to the right of the unit’s AABB  */
        Bounds bb = new(unit[0].transform.position, Vector3.zero);
        foreach (Part p in unit) bb.Encapsulate(p.col.bounds);
        float offsetX = bb.size.x + 1f;
        Vector3 offset = new(offsetX, 0f, 0f);

        Dictionary<Rigidbody2D, Rigidbody2D> bodyMap = new();
        List<Part> clones = new();

        /*  ---- clone gameObjects ----  */
        foreach (Part p in unit)
        {
            GameObject cloneObj = Object.Instantiate(p.gameObject,
                                                     p.transform.position + offset,
                                                     p.transform.rotation,
                                                     p.transform.parent);

            Part cp = cloneObj.GetComponent<Part>();
            clones.Add(cp);

            globalMap[p] = cp;              // register globally
            bodyMap[p.rb] = cp.rb;

            cp.HideOutline();
            cp.showContextMenu = false;
            cp.connected.Clear();            // rebuild later
        }

        /*  ---- remap joints ----  */
        foreach (Part clone in clones)
        {
            foreach (AnchoredJoint2D j in clone.GetComponents<AnchoredJoint2D>())
            {
                if (j.connectedBody && bodyMap.TryGetValue(j.connectedBody, out var nb))
                    j.connectedBody = nb;
            }
        }

        /*  ---- rebuild connectivity ----  */
        foreach (Part clone in clones)
        {
            foreach (AnchoredJoint2D j in clone.GetComponents<AnchoredJoint2D>())
            {
                if (j.connectedBody)
                    clone.AddConnectedPart(j.connectedBody.GetComponent<Part>());
            }
        }

        return clones;
    }

    private void Rip()
    {
        // capture current connectivity group BEFORE breaking joints
        List<Part> groupBefore = GetGroup();

        // -------- remove joints & connections --------------------------------
        Rigidbody2D myRb = rb;

        foreach (AnchoredJoint2D j in
                 Object.FindObjectsByType<AnchoredJoint2D>(FindObjectsSortMode.None))
        {
            if (!j) continue;
            bool involves = j.attachedRigidbody == myRb || j.connectedBody == myRb;
            if (!involves) continue;

            foreach (Transform ch in j.transform)
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

        foreach (Part other in new List<Part>(connected))
        {
            connected.Remove(other);
            other.connected.Remove(this);
        }

        // -------- restore collisions with former group only ------------------
        foreach (Part other in new List<Part>(noCollision))
        {
            if (groupBefore.Contains(other))
                SetCollisionDisabledWith(other, false);         // re?enable
            // external partners stay disabled (keep yellow outline)
        }

        // -------- selection update -------------------------------------------
        bool wasSel = currentGroup.Contains(this) || mainSelected == this;
        if (wasSel)
        {
            ClearSelection();
            SelectAsSingle();
        }
    }

    // ??????????????????????????????????? RIP & DELETE HELPERS ??????????????????????????????????? //
    /// <summary>
    /// Rips every currently selected part from its original group while
    /// preserving any joint whose *both* ends are selected.
    /// </summary>
    private static void RipSelection()
    {
        if (currentGroup.Count == 0) return;

        HashSet<Part> selection = new(currentGroup);

        // Cache each selected part’s pre?rip group for collision restore
        Dictionary<Part, List<Part>> before = new();
        foreach (Part p in selection) before[p] = p.GetGroup();

        /*  ?? Break joints that cross the selection boundary ?? */
        foreach (AnchoredJoint2D j in
                 Object.FindObjectsByType<AnchoredJoint2D>(FindObjectsSortMode.None))
        {
            if (!j) continue;

            Part a = j.attachedRigidbody ? j.attachedRigidbody.GetComponent<Part>() : null;
            Part b = j.connectedBody ? j.connectedBody.GetComponent<Part>() : null;
            if (!a || !b) continue;

            bool aSel = selection.Contains(a);
            bool bSel = selection.Contains(b);

            // keep joints entirely inside or entirely outside the selection
            if (aSel == bSel) continue;

            // remove visual sprite (Bolt / Hinge / Motor) if it matches anchor
            foreach (Transform ch in j.transform)
            {
                if ((ch.name == "Bolt" || ch.name == "Hinge" || ch.name == "Motor") &&
                    (ch.position - j.transform.TransformPoint(j.anchor)).sqrMagnitude < 1e-4f)
                {
                    Object.Destroy(ch.gameObject);
                    break;
                }
            }
            Object.Destroy(j);

            // update connectivity lists
            if (a.connected.Contains(b)) a.connected.Remove(b);
            if (b.connected.Contains(a)) b.connected.Remove(a);
        }

        /*  ?? Restore collisions with ex?group?mates that were *not* selected ?? */
        foreach (Part p in selection)
        {
            foreach (Part other in new List<Part>(p.noCollision))
            {
                if (!selection.Contains(other) && before[p].Contains(other))
                    p.SetCollisionDisabledWith(other, false);
            }
        }

        /*  ?? Refresh selection visuals ?? */
        UpdateSelectionVisuals();
    }

    /// <summary>
    /// Deletes every selected part *and* all parts in their respective
    /// connectivity groups, then clears the selection.
    /// </summary>
    private static void DeleteSelection()
    {
        if (currentGroup.Count == 0) return;

        /* 1) Build the union of every connectivity group represented
              in the current selection. */
        HashSet<Part> toDelete = new();
        foreach (Part sel in currentGroup)
            foreach (Part g in sel.GetGroup())
                toDelete.Add(g);

        /* 2) Restore collisions that are internal to the parts being removed
              (they are irrelevant post?deletion but keeps Rip() logic tidy). */
        foreach (Part p in toDelete)
            foreach (Part o in new List<Part>(p.noCollision))
                if (toDelete.Contains(o))
                    p.SetCollisionDisabledWith(o, false);

        /* 3) Break joints & connectivity cleanly, then destroy. */
        foreach (Part p in toDelete)
            p.Rip();                       // sever joints / connections safely
        foreach (Part p in toDelete)
            Object.Destroy(p.gameObject);  // remove from scene

        ClearSelection();
    }

    private void DeleteGroup()
    {
        List<Part> grp = GetGroup();
        if (grp.Count == 0) return;

        // restore collisions internal to this group (about to be removed)
        foreach (Part p in grp)
            foreach (Part o in new List<Part>(p.noCollision))
                if (grp.Contains(o))
                    p.SetCollisionDisabledWith(o, false);

        foreach (Part p in grp) p.Rip();
        foreach (Part p in grp) Destroy(p.gameObject);
        ClearSelection();
    }
}
