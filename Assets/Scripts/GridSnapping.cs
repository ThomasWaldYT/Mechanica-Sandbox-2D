// GridSnapping.cs - central snap helpers (pos = 0.25 u, scale = 0.50 u, angle = 90 degrees)
using UnityEngine;

public static class GridSnapping
{
    public const float PosGrid = 0.25f;   // world-units for position
    public const float ScaleGrid = 0.50f; // world-units for size
    public const float AngleSnap = 90f;   // degrees

    public static float RoundPos(float v) => Mathf.Round(v / PosGrid) * PosGrid;
    public static Vector3 SnapPos(Vector3 p)
        => new Vector3(RoundPos(p.x), RoundPos(p.y), p.z);

    public static float SnapScale(float v)
        => Mathf.Max(ScaleGrid, Mathf.Round(v / ScaleGrid) * ScaleGrid);

    public static float SnapAngleDeg(float a)
        => Mathf.Round(a / AngleSnap) * AngleSnap;
}
