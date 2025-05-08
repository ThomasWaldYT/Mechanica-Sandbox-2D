// Spawner.cs – spawning parts & joints
// CHANGELOG #12 (2025-05-07)
//   • Added static property SelectionBorderMain so other scripts (e.g. Motor)
//     can use the normal selection colour for outlines.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// JOINT TYPES
public enum JointType { Bolt, Hinge, Motor }

// SPAWNER
public class Spawner : MonoBehaviour
{
    // PART VISUALS
    [Header("Square Settings")]
    [SerializeField] private Color spawnSquareColor = Color.white;
    [SerializeField] private Sprite squareSprite = null;
    [SerializeField] private int squareSortingOrder = 0;

    [Header("Circle Settings")]
    [SerializeField] private Color spawnCircleColor = Color.white;
    [SerializeField] private Sprite circleSprite = null;
    [SerializeField] private int circleSortingOrder = 0;

    // BOLT
    [Header("Bolt Settings")]
    [SerializeField] private Color boltColor = Color.yellow;
    [SerializeField] private Sprite boltSprite = null;
    [SerializeField] private float boltSize = 0.20f;
    [SerializeField] private int boltSortingOrder = 1;

    // HINGE
    [Header("Hinge Settings")]
    [SerializeField] private Color hingeColor = Color.green;
    [SerializeField] private Sprite hingeSprite = null;
    [SerializeField] private float hingeSize = 0.15f;
    [SerializeField] private int hingeSortingOrder = 2;

    // MOTOR
    [Header("Motor Settings")]
    [SerializeField] private Color motorColor = Color.cyan;
    [SerializeField] private Sprite motorSprite = null;
    [SerializeField] private float motorSize = 0.18f;
    [SerializeField] private int motorSortingOrder = 2;
    [Tooltip("Default absolute speed in °/s when a motor is first spawned")]
    [SerializeField] private float motorDefaultSpeed = 90f;
    [Tooltip("True = clockwise (-ve speed)  False = counter?clockwise (+ve)")]
    [SerializeField] private bool motorClockwiseDefault = false;

    // expose visual diameters so other scripts can keep sprites round
    public static float BoltVisualSize { get; private set; }
    public static float HingeVisualSize { get; private set; }
    public static float MotorVisualSize { get; private set; }

    // expose main selection colour for other scripts
    public static Color SelectionBorderMain { get; private set; }

    // SELECTION OUTLINES
    [Header("Selection Outlines")]
    [SerializeField] private Color selectionBorderMain = new Color(0f, 1f, 1f, 0.80f);
    [SerializeField] private Color selectionBorderSecondary = new Color(0f, 1f, 1f, 0.35f);

    // COMMON / UI
    [Header("Common Settings")]
    [SerializeField] public float defaultMass = 33f;

    [Header("Cursor Textures")]
    [SerializeField] public Texture2D cursorDefaultTexture = null;
    [SerializeField] public Texture2D cursorDragTexture = null;
    [SerializeField] public Texture2D cursorScaleTexture = null;

    [Header("Freeze UI")]
    public Image freezeIndicator;

    private bool Frozen => Time.timeScale == 0f;

    private void Start()
    {
        BoltVisualSize = boltSize;
        HingeVisualSize = hingeSize;
        MotorVisualSize = motorSize;

        // make main selection colour globally accessible
        SelectionBorderMain = selectionBorderMain;

        if (cursorDefaultTexture)
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

        // clear selection when clicking empty space (but not on any context menu)
        if (Frozen && Input.GetMouseButtonDown(0) &&
            Physics2D.OverlapPoint(GetWorldMouse()) == null &&
            !Part.IsPointerOverContextMenuArea() &&
            !Motor.IsPointerOverContextMenuArea())
        {
            Part.ClearSelection();
            Motor.ClearSelection();
        }

        if (!Frozen) return;

        if (Input.GetKeyDown(KeyCode.S)) SpawnSquare();
        if (Input.GetKeyDown(KeyCode.C)) SpawnCircle();
        if (Input.GetKeyDown(KeyCode.B)) CreateJointAtMouse(JointType.Bolt);
        if (Input.GetKeyDown(KeyCode.H)) CreateJointAtMouse(JointType.Hinge);
        if (Input.GetKeyDown(KeyCode.M)) CreateJointAtMouse(JointType.Motor);
    }

    // PART SPAWN HELPERS
    private void SpawnSquare()
    {
        Vector3 pos = GridSnapping.SnapPos(GetWorldMouse());

        GameObject partObj = new("Part");
        partObj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        partObj.transform.localScale = Vector3.one;
        partObj.transform.parent = transform;

        partObj.AddComponent<BoxCollider2D>();
        FinalisePartObject(partObj, squareSprite, spawnSquareColor, squareSortingOrder);
    }

    private void SpawnCircle()
    {
        Vector3 pos = GridSnapping.SnapPos(GetWorldMouse());

        GameObject partObj = new("Part");
        partObj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        partObj.transform.localScale = Vector3.one;
        partObj.transform.parent = transform;

        partObj.AddComponent<CircleCollider2D>();
        FinalisePartObject(partObj, circleSprite, spawnCircleColor, circleSortingOrder);
    }

    private void FinalisePartObject(GameObject obj, Sprite sprite, Color col, int order)
    {
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.color = col;
        sr.sprite = sprite;
        sr.sortingOrder = order;

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.mass = defaultMass;

        Part p = obj.AddComponent<Part>();
        p.spawner = this;
        p.mass = defaultMass;
        p.SetCursorTextures(cursorDragTexture, cursorScaleTexture, cursorDefaultTexture);
        p.SetSelectionColours(selectionBorderMain, selectionBorderSecondary);
        p.SelectAsSingle();
    }

    // JOINT CREATION
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

        if (parts.Count != 2 || IsConnectionAtPoint(snapPos)) return;

        Part a = parts[0], b = parts[1];

        // visual sprite
        GameObject conn = new(type.ToString());
        conn.transform.position = snapPos;
        conn.transform.SetParent(b.transform, true);

        SpriteRenderer sr = conn.AddComponent<SpriteRenderer>();
        float worldSize = 0f;

        if (type == JointType.Bolt)
        {
            sr.color = boltColor;
            sr.sprite = boltSprite;
            sr.sortingOrder = boltSortingOrder;
            worldSize = boltSize;
        }
        else if (type == JointType.Hinge)
        {
            sr.color = hingeColor;
            sr.sprite = hingeSprite;
            sr.sortingOrder = hingeSortingOrder;
            worldSize = hingeSize;
        }
        else // Motor
        {
            sr.color = motorColor;
            sr.sprite = motorSprite;
            sr.sortingOrder = motorSortingOrder;
            worldSize = motorSize;
        }

        // keep sprite round even when parent is scaled
        Vector3 ps = b.transform.lossyScale;
        conn.transform.localScale = new(worldSize / ps.x, worldSize / ps.y, 1f);

        // physics joint
        Rigidbody2D rbA = a.GetComponent<Rigidbody2D>();

        if (type == JointType.Bolt)
        {
            var j = b.gameObject.AddComponent<FixedJoint2D>();
            j.connectedBody = rbA;
            j.enableCollision = false;
            j.anchor = conn.transform.localPosition;
        }
        else
        {
            var j = b.gameObject.AddComponent<HingeJoint2D>();
            j.connectedBody = rbA;
            j.enableCollision = false;
            j.anchor = conn.transform.localPosition;

            if (type == JointType.Motor)
            {
                j.useMotor = true;
                JointMotor2D m = j.motor;
                m.maxMotorTorque = Mathf.Infinity;
                m.motorSpeed = motorClockwiseDefault ? -motorDefaultSpeed : motorDefaultSpeed;
                j.motor = m;

                // clickable sprite and context menu
                CircleCollider2D cc = conn.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = 0.5f;

                Motor ui = conn.AddComponent<Motor>();
                ui.Init(j);
            }
        }

        // connectivity
        a.AddConnectedPart(b);
        b.AddConnectedPart(a);
    }

    private static bool IsConnectionAtPoint(Vector3 wp)
    {
        const float eps2 = 1e-6f;
        foreach (var j in Object.FindObjectsByType<AnchoredJoint2D>(FindObjectsSortMode.None))
            if ((j.transform.TransformPoint(j.anchor) - wp).sqrMagnitude < eps2) return true;
        return false;
    }

    // UTILITY
    private void ToggleFreeze()
    {
        Time.timeScale = Time.timeScale == 0f ? 1f : 0f;
        if (freezeIndicator) freezeIndicator.enabled = Frozen;
    }

    private Vector3 GetWorldMouse()
    {
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        wp.z = 0f;
        return wp;
    }
}
