// Spawner.cs – Mechanica?Sandbox?2D
// 2025?05?06
//  • Spawning, joint creation, background deselect
//  • Newly spawned part becomes selected
//  • Uses modern FindObjectsByType to avoid deprecation warnings

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public enum JointType { Bolt, Hinge }

public class Spawner : MonoBehaviour
{
    // ????????????????????????????????????????????????
    // SPAWN SETTINGS
    // ????????????????????????????????????????????????

    [Header("Square Settings")]
    [SerializeField] private Color spawnSquareColor = Color.white;
    [SerializeField] private Sprite squareSprite = null;
    [SerializeField] private int squareSortingOrder = 0;

    [Header("Circle Settings")]
    [SerializeField] private Color spawnCircleColor = Color.white;
    [SerializeField] private Sprite circleSprite = null;
    [SerializeField] private int circleSortingOrder = 0;

    // ????????????????????????????????????????????????
    // JOINT SETTINGS
    // ????????????????????????????????????????????????

    [Header("Bolt Settings")]
    [SerializeField] private Color boltColor = Color.yellow;
    [SerializeField] private Sprite boltSprite = null;
    [SerializeField] private float boltSize = 0.20f;
    [SerializeField] private int boltSortingOrder = 1;

    [Header("Hinge Settings")]
    [SerializeField] private Color hingeColor = Color.green;
    [SerializeField] private Sprite hingeSprite = null;
    [SerializeField] private float hingeSize = 0.15f;
    [SerializeField] private int hingeSortingOrder = 2;

    // ????????????????????????????????????????????????
    // OUTLINE COLOURS
    // ????????????????????????????????????????????????

    [Header("Selection Outlines")]
    [SerializeField] private Color selectionBorderMain = new(0f, 1f, 1f, 0.80f);
    [SerializeField] private Color selectionBorderSecondary = new(0f, 1f, 1f, 0.35f);

    // ????????????????????????????????????????????????
    // COMMON / UI
    // ????????????????????????????????????????????????

    [Header("Common Settings")]
    [SerializeField] public float defaultMass = 33f;

    [Header("Cursor Textures")]
    [SerializeField] public Texture2D cursorDefaultTexture = null;
    [SerializeField] public Texture2D cursorDragTexture = null;
    [SerializeField] public Texture2D cursorScaleTexture = null;

    [Header("Freeze UI")]
    public Image freezeIndicator;

    private bool Frozen => Time.timeScale == 0f;

    // ????????????????????????????????????????????????
    // UNITY LIFECYCLE
    // ????????????????????????????????????????????????

    private void Start()
    {
        if (cursorDefaultTexture != null)
        {
            Vector2 hs = new(cursorDefaultTexture.width * 0.5f,
                              cursorDefaultTexture.height * 0.5f);
            Cursor.SetCursor(cursorDefaultTexture, hs, CursorMode.Auto);
        }
        if (freezeIndicator) freezeIndicator.enabled = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F)) ToggleFreeze();

        // Left?click empty space ? deselect
        if (Frozen && Input.GetMouseButtonDown(0))
        {
            if (Physics2D.OverlapPoint(GetWorldMouse()) == null)
                Part.ClearSelection();
        }

        if (!Frozen) return;

        if (Input.GetKeyDown(KeyCode.S)) SpawnSquare();
        if (Input.GetKeyDown(KeyCode.C)) SpawnCircle();
        if (Input.GetKeyDown(KeyCode.B)) CreateJointAtMouse(JointType.Bolt);
        if (Input.GetKeyDown(KeyCode.H)) CreateJointAtMouse(JointType.Hinge);
    }

    // ????????????????????????????????????????????????
    // SPAWNING
    // ????????????????????????????????????????????????

    private void SpawnSquare()
    {
        Vector3 pos = GridSnapping.SnapPos(GetWorldMouse());

        GameObject partObj = new("Part");
        partObj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        partObj.transform.localScale = Vector3.one;
        partObj.transform.parent = transform;

        partObj.AddComponent<BoxCollider2D>();               // collider first
        FinalisePartObject(partObj, squareSprite, spawnSquareColor, squareSortingOrder);
    }

    private void SpawnCircle()
    {
        Vector3 pos = GridSnapping.SnapPos(GetWorldMouse());

        GameObject partObj = new("Part");
        partObj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        partObj.transform.localScale = Vector3.one;
        partObj.transform.parent = transform;

        partObj.AddComponent<CircleCollider2D>();            // collider first
        FinalisePartObject(partObj, circleSprite, spawnCircleColor, circleSortingOrder);
    }

    private void FinalisePartObject(GameObject partObj, Sprite sprite,
                                    Color col, int order)
    {
        // Sprite
        SpriteRenderer sr = partObj.AddComponent<SpriteRenderer>();
        sr.color = col; sr.sprite = sprite; sr.sortingOrder = order;

        // Physics
        Rigidbody2D rb = partObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f; rb.mass = defaultMass;

        // Behaviour
        Part part = partObj.AddComponent<Part>();
        part.spawner = this;
        part.mass = defaultMass;
        part.SetCursorTextures(cursorDragTexture, cursorScaleTexture, cursorDefaultTexture);
        part.SetSelectionColours(selectionBorderMain, selectionBorderSecondary);

        // Newly spawned part becomes the active selection
        part.SelectAsSingle();
    }

    // ????????????????????????????????????????????????
    // JOINT CREATION
    // ????????????????????????????????????????????????

    private void CreateJointAtMouse(JointType type)
    {
        if (!Frozen) return;

        Vector3 snapPos = GridSnapping.SnapPos(GetWorldMouse());
        const float detectRadius = 0.05f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(snapPos, detectRadius);

        var parts = new List<Part>();
        foreach (Collider2D h in hits)
            if (h.TryGetComponent(out Part p) && !parts.Contains(p))
                parts.Add(p);

        if (parts.Count != 2) return;
        if (IsConnectionAtPoint(snapPos)) return;

        Part a = parts[0]; Part b = parts[1];

        GameObject conn = new(type.ToString());
        conn.transform.position = snapPos;
        conn.transform.SetParent(b.transform, true);

        SpriteRenderer sr = conn.AddComponent<SpriteRenderer>();
        if (type == JointType.Bolt)
        {
            sr.color = boltColor; sr.sprite = boltSprite; sr.sortingOrder = boltSortingOrder;
        }
        else
        {
            sr.color = hingeColor; sr.sprite = hingeSprite; sr.sortingOrder = hingeSortingOrder;
        }

        Vector3 desired = type == JointType.Bolt ? Vector3.one * boltSize
                                                 : Vector3.one * hingeSize;
        Vector3 parentScale = b.transform.lossyScale;
        conn.transform.localScale = new(desired.x / parentScale.x,
                                        desired.y / parentScale.y, 1f);

        Rigidbody2D rbA = a.GetComponent<Rigidbody2D>();
        if (type == JointType.Bolt)
        {
            var j = b.gameObject.AddComponent<FixedJoint2D>();
            j.connectedBody = rbA; j.enableCollision = false; j.anchor = conn.transform.localPosition;
        }
        else
        {
            var j = b.gameObject.AddComponent<HingeJoint2D>();
            j.connectedBody = rbA; j.enableCollision = false; j.anchor = conn.transform.localPosition;
        }

        a.AddConnectedPart(b); b.AddConnectedPart(a);
    }

    private static bool IsConnectionAtPoint(Vector3 wp)
    {
        const float eps2 = 1e-6f;
        // modern, non?deprecated API
        foreach (var j in Object.FindObjectsByType<AnchoredJoint2D>(FindObjectsSortMode.None))
        {
            if ((j.transform.TransformPoint(j.anchor) - wp).sqrMagnitude < eps2) return true;
        }
        return false;
    }

    // ????????????????????????????????????????????????
    // UTILITY
    // ????????????????????????????????????????????????

    private void ToggleFreeze()
    {
        Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
        if (freezeIndicator) freezeIndicator.enabled = Frozen;
    }

    private Vector3 GetWorldMouse()
    {
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        wp.z = 0f; return wp;
    }
}
