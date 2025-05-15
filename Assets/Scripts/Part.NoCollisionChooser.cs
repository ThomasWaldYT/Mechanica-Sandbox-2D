// Part.NoCollisionChooser.cs – choose?mode handling for disabling collisions
// CHANGELOG #28 (2025?05?13)
//   • In choose?mode, clicking a target toggles collisions for *all* currently
//     selected parts at once (multi?select supported).
//   • Selected (main) parts themselves can never disable collisions with one
//     another or themselves while this mode is active.
//   • Outlines updated live to follow the new colour rules (yellow for multi).

using System.Linq;
using UnityEngine;

public partial class Part
{
    private void HandleChooseNoCollisionMode()
    {
        if (!choosingNoCollision || chooseSource == null) return;
        if (this != chooseSource) return;                  // only one driver
        if (!Frozen) { CancelChooseMode(); return; }

        /* ---------- mouse click ---------- */
        if (Input.GetMouseButtonDown(0))
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(GetWorldMouse());
            Part target = null;
            foreach (Collider2D h in hits)
            {
                if (h.TryGetComponent(out Part p) && !currentGroup.Contains(p))
                {
                    target = p;
                    break;
                }
            }
            if (target == null) return;

            bool alreadyDisabledForAll = currentGroup.All(s => s.noCollision.Contains(target));

            /* ---- toggle across EVERY selected part ---- */
            if (alreadyDisabledForAll)
            {
                chooseSelected.Remove(target);
                foreach (Part sel in currentGroup)
                    sel.SetCollisionDisabledWith(target, false);

                target.HideOutline();
            }
            else
            {
                chooseSelected.Add(target);
                foreach (Part sel in currentGroup)
                    sel.SetCollisionDisabledWith(target, true);

                bool multi = currentGroup.Count > 1;
                target.ShowOutline(multi
                    ? Spawner.NoCollisionOutlineExternal   // yellow
                    : Spawner.NoCollisionOutline);         // magenta
            }
        }
    }

    /* ---- unchanged helper ---- */
    private static void CancelChooseMode()
    {
        foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
            p.HideOutline();

        chooseSelected.Clear();
        choosingNoCollision = false;
        chooseSource = null;

        // refresh outlines for normal selection
        UpdateSelectionVisuals();
    }
}
