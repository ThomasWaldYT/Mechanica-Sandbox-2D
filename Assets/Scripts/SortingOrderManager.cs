// SortingOrderManager.cs
// Centralised render?stack controller with selectable auto?bring behaviour.
//
// NEW IN CHANGELOG #42 (2025?05?15)
//   • Auto?bring behaviour can now be toggled on/off at runtime via
//     SortingOrderManager.SetAutoBring(bool).
//   • Added SendToBack(IEnumerable<Part>) to mirror BringToFront().
//   • BringToFront() early?exits when auto?bring is disabled.
//
// • Every newly?spawned SpriteRenderer still gets an ever?increasing
//   sorting?order via GetNext().
// • Joints can “reserve” space with EnsureAtLeast().
//
using System.Collections.Generic;
using UnityEngine;

public static class SortingOrderManager
{
    private static int nextOrder = 0;          // upwards?growing stack
    private static int backOrder = -1;         // downwards?growing stack

    // Toggleable global flag (true by default)
    public static bool AutoBringEnabled { get; private set; } = true;

    // ---------------------------------------------------------------- init
    static SortingOrderManager()
    {
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            nextOrder = Mathf.Max(nextOrder, sr.sortingOrder);
    }

    // ---------------------------------------------------------------- API
    public static void SetAutoBring(bool enabled) => AutoBringEnabled = enabled;

    public static int GetNext() => ++nextOrder;

    public static void EnsureAtLeast(int order)
    {
        if (order >= nextOrder) nextOrder = order;
    }

    /// <summary>Lift <paramref name="parts"/> to the very front (largest
    /// sorting orders), preserving their internal relative offsets. Early
    /// exits if <see cref="AutoBringEnabled"/> is false.</summary>
    public static void BringToFront(IEnumerable<Part> parts)
    {
        if (!AutoBringEnabled || parts == null) return;

        var list = new List<Part>(new HashSet<Part>(parts));
        list.Sort((a, b) => GetMainOrder(a).CompareTo(GetMainOrder(b))); // bottom?top

        int baseOrder = nextOrder + 1;
        foreach (Part p in list)
        {
            int curMain = GetMainOrder(p);
            int delta = baseOrder - curMain;

            foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder += delta;
            foreach (var lr in p.GetComponentsInChildren<LineRenderer>())
                lr.sortingOrder += delta;

            baseOrder++; // leave a gap per part
        }
        nextOrder = baseOrder;
    }

    /// <summary>Push <paramref name="parts"/> to the very back (smallest
    /// sorting orders), preserving internal relative offsets.</summary>
    public static void SendToBack(IEnumerable<Part> parts)
    {
        if (parts == null) return;

        var list = new List<Part>(new HashSet<Part>(parts));
        list.Sort((a, b) => GetMainOrder(b).CompareTo(GetMainOrder(a))); // top?bottom

        int baseOrder = backOrder; // negative / growing more negative
        foreach (Part p in list)
        {
            int curMain = GetMainOrder(p);
            int delta = baseOrder - curMain; // delta is negative or zero

            foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder += delta;
            foreach (var lr in p.GetComponentsInChildren<LineRenderer>())
                lr.sortingOrder += delta;

            baseOrder--; // leave gap per part towards ??
        }
        backOrder = baseOrder; // remember new deepest value
    }

    // ---------------------------------------------------------------- helpers
    private static int GetMainOrder(Part p)
    {
        var sr = p ? p.GetComponent<SpriteRenderer>() : null;
        return sr ? sr.sortingOrder : 0;
    }
}
