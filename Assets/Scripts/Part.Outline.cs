// Part.Outline.cs – outline drawing & joint?sprite rescale
// CHANGELOG #6 (2025?05?13)
//   • Added proper 3?edge outline for right?triangles (PolygonCollider2D).

using UnityEngine;

public partial class Part
{
    // ---------------------------------------------------- OUTLINE
    private void BuildOutline()
    {
        outline = new GameObject("Outline").AddComponent<LineRenderer>();
        outline.transform.SetParent(transform, false);
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.startWidth = outline.endWidth = LINE_WIDTH;
        outline.material = new Material(Shader.Find("Sprites/Default"));
        outline.sortingOrder = sr ? sr.sortingOrder + 50 : 50;
        outline.numCornerVertices = 2;
        RecalculateOutline();
    }

    private void LateUpdate()
    {
        if (outline.enabled && transform.hasChanged)
        {
            RecalculateOutline();
            transform.hasChanged = false;
        }
        UpdateJointSprites();
    }

    private void RecalculateOutline()
    {
        if (col == null) return;

        // Circle
        if (col is CircleCollider2D c)
        {
            outline.positionCount = CIRCLE_SEGMENTS;
            for (int i = 0; i < CIRCLE_SEGMENTS; i++)
            {
                float a = i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
                outline.SetPosition(i,
                    new Vector3(Mathf.Cos(a) * c.radius,
                                Mathf.Sin(a) * c.radius,
                                0f));
            }
            return;
        }

        // Box
        if (col is BoxCollider2D b)
        {
            Vector2 off = b.offset, h = b.size * 0.5f;
            outline.positionCount = 4;
            outline.SetPosition(0, new Vector3(off.x - h.x, off.y - h.y, 0f));
            outline.SetPosition(1, new Vector3(off.x - h.x, off.y + h.y, 0f));
            outline.SetPosition(2, new Vector3(off.x + h.x, off.y + h.y, 0f));
            outline.SetPosition(3, new Vector3(off.x + h.x, off.y - h.y, 0f));
            return;
        }

        // Right?triangle
        if (col is PolygonCollider2D pc && pc.points.Length == 3)
        {
            outline.positionCount = 3;
            Vector2 off = pc.offset;
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = pc.points[i] + off;
                outline.SetPosition(i, new Vector3(p.x, p.y, 0f));
            }
            return;
        }

        // Fallback – bounding box
        Bounds bb = col.bounds;
        Vector3 l = transform.InverseTransformPoint(bb.min);
        Vector3 h2 = transform.InverseTransformPoint(bb.max);
        outline.positionCount = 4;
        outline.SetPosition(0, new Vector3(l.x, l.y, 0f));
        outline.SetPosition(1, new Vector3(l.x, h2.y, 0f));
        outline.SetPosition(2, new Vector3(h2.x, h2.y, 0f));
        outline.SetPosition(3, new Vector3(h2.x, l.y, 0f));
    }

    private void ShowOutline(Color c)
    {
        outline.startColor = outline.endColor = c;
        outline.sortingOrder = sr ? sr.sortingOrder + (this == mainSelected ? 60 : 50) : 50;
        outline.enabled = true;
    }

    private void HideOutline()
    {
        if (outline) outline.enabled = false;

        foreach (LineRenderer lr in GetComponentsInChildren<LineRenderer>())
            if (lr != outline && lr.gameObject.name == "Outline")
                lr.enabled = false;
    }

    // ?????????????????????????????????????????????????????????????????????????????
    // JOINT SPRITE FIX
    // ?????????????????????????????????????????????????????????????????????????????
    private void UpdateJointSprites()
    {
        foreach (Transform ch in transform)
        {
            if (ch.name == "Bolt")
            {
                Vector3 ps = transform.lossyScale;
                ch.localScale = new(Spawner.BoltVisualSize / ps.x, Spawner.BoltVisualSize / ps.y, 1f);
                ch.localRotation = Quaternion.identity;
            }
            else if (ch.name == "Hinge")
            {
                Vector3 ps = transform.lossyScale;
                ch.localScale = new(Spawner.HingeVisualSize / ps.x, Spawner.HingeVisualSize / ps.y, 1f);
                ch.localRotation = Quaternion.identity;
            }
            else if (ch.name == "Motor")
            {
                Vector3 ps = transform.lossyScale;
                ch.localScale = new(Spawner.MotorVisualSize / ps.x, Spawner.MotorVisualSize / ps.y, 1f);
                ch.localRotation = Quaternion.identity;
            }
        }
    }
}
