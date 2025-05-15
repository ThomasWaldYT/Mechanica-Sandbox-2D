// Spawner.cs – spawns parts & joints
// CHANGELOG #8 (2025-05-15)
//   • Joint creation (Bolt / Hinge / Motor) now requires that *both* parts
//     involved are already selected.  Only parts under the cursor that return
//     IsSelected() are considered; if fewer than two such parts are found the
//     hot?key does nothing.  When multiple selected parts overlap, the top two
//     by sorting order are still chosen.
//   • No other behaviour changed.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public enum JointType { Bolt, Hinge, Motor }

public class Spawner : MonoBehaviour
{
    /* =======================================================================
     *  STRUCTS – DESIGN?STATE SNAPSHOT
     * ===================================================================== */
    private struct PartSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public PartSnapshot(Vector3 p, Quaternion r, Vector3 s)
        {
            position = p;
            rotation = r;
            scale = s;
        }
    }

    private readonly Dictionary<Part, PartSnapshot> savedScene = new();
    private readonly List<Part> savedSelection = new();

    /* =======================================================================
     *  VISUALS
     * ===================================================================== */
    [Header("Square Settings")]
    [SerializeField] private Color spawnSquareColor = Color.white;
    [SerializeField] private Sprite squareSprite = null;

    [Header("Circle Settings")]
    [SerializeField] private Color spawnCircleColor = Color.white;
    [SerializeField] private Sprite circleSprite = null;

    [Header("Triangle Settings")]
    [SerializeField] private Color spawnTriangleColor = Color.white;
    [SerializeField] private Sprite triangleSprite = null;

    [Header("Bolt Settings")]
    [SerializeField] private Color boltColor = Color.yellow;
    [SerializeField] private Sprite boltSprite = null;
    [SerializeField] private float boltSize = 0.20f;

    [Header("Hinge Settings")]
    [SerializeField] private Color hingeColor = Color.green;
    [SerializeField] private Sprite hingeSprite = null;
    [SerializeField] private float hingeSize = 0.15f;

    [Header("Motor Settings")]
    [SerializeField] private Color motorColor = Color.cyan;
    [SerializeField] private Sprite motorSprite = null;
    [SerializeField] private float motorSize = 0.18f;
    [SerializeField] private float motorDefaultSpeed = 90f;
    [SerializeField] private bool motorClockwiseDefault = false;

    public static float BoltVisualSize { get; private set; }
    public static float HingeVisualSize { get; private set; }
    public static float MotorVisualSize { get; private set; }

    [Header("Selection Outlines")]
    [SerializeField] private Color selectionBorderMain = new Color(0f, 1f, 1f, 0.80f);
    [SerializeField] private Color selectionBorderSecondary = new Color(0f, 1f, 1f, 0.35f);
    public static Color SelectionBorderMain { get; private set; }

    [Header("Disable?Collision Outlines")]
    [SerializeField] private Color noCollisionOutline = Color.magenta;
    [SerializeField] private Color noCollisionOutlineExternal = Color.yellow;
    public static Color NoCollisionOutline { get; private set; }
    public static Color NoCollisionOutlineExternal { get; private set; }

    [Header("Common Settings")]
    [SerializeField] public float defaultMass = 33f;

    [Header("Cursor Textures")]
    [SerializeField] public Texture2D cursorDefaultTexture = null;
    [SerializeField] public Texture2D cursorDragTexture = null;
    [SerializeField] public Texture2D cursorScaleTexture = null;

    [Header("Freeze UI")]
    public Image freezeIndicator;

    [Header("Auto Bring?To?Front UI Container")]
    public GameObject autoBringUIParent;

    private Button autoBringToggleButton;
    private TMP_Text autoBringToggleText;

    private bool Frozen => Time.timeScale == 0f;

    /* =======================================================================
     *  UNITY LIFECYCLE
     * ===================================================================== */
    private void Start()
    {
        BoltVisualSize = boltSize;
        HingeVisualSize = hingeSize;
        MotorVisualSize = motorSize;

        SelectionBorderMain = selectionBorderMain;
        NoCollisionOutline = noCollisionOutline;
        NoCollisionOutlineExternal = noCollisionOutlineExternal;

        if (cursorDefaultTexture)
        {
            Vector2 hs = new(cursorDefaultTexture.width * 0.5f,
                             cursorDefaultTexture.height * 0.5f);
            Cursor.SetCursor(cursorDefaultTexture, hs, CursorMode.Auto);
        }

        if (freezeIndicator)
            freezeIndicator.enabled = false;
        if (autoBringUIParent)
            autoBringUIParent.SetActive(false);

        if (autoBringUIParent)
        {
            autoBringToggleButton = autoBringUIParent.GetComponentInChildren<Button>();
            if (autoBringToggleButton)
            {
                autoBringToggleButton.onClick.AddListener(ToggleAutoBring);
                autoBringToggleText = autoBringToggleButton.GetComponentInChildren<TMP_Text>();
                RefreshAutoBringUI();
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
            ToggleFreeze();

        if (Frozen && Input.GetMouseButtonDown(0) &&
            !Part.ChoosingNoCollision &&
            Physics2D.OverlapPoint(GetWorldMouse()) == null &&
            !Part.IsPointerOverContextMenuArea() &&
            !Motor.IsPointerOverContextMenuArea())
        {
            Part.ClearSelection();
            Motor.ClearSelection();
        }

        if (!Frozen || Part.ChoosingNoCollision) return;

        if (Input.GetKeyDown(KeyCode.S)) SpawnSquare();
        if (Input.GetKeyDown(KeyCode.C)) SpawnCircle();
        if (Input.GetKeyDown(KeyCode.T)) SpawnTriangle();
        if (Input.GetKeyDown(KeyCode.B)) CreateJointAtMouse(JointType.Bolt);
        if (Input.GetKeyDown(KeyCode.H)) CreateJointAtMouse(JointType.Hinge);
        if (Input.GetKeyDown(KeyCode.M)) CreateJointAtMouse(JointType.Motor);
    }

    /* =======================================================================
     *  FREEZE / UNFREEZE HANDLING
     * ===================================================================== */
    private void ToggleFreeze()
    {
        bool wasFrozen = Frozen;

        if (wasFrozen)
        {
            SaveDesignState();          // leaving design mode – snapshot scene
            Time.timeScale = 1f;
        }
        else
        {
            Time.timeScale = 0f;
            RestoreDesignState();       // re?entering design mode – restore
        }

        bool nowFrozen = !wasFrozen;

        if (freezeIndicator)
            freezeIndicator.enabled = nowFrozen;
        if (autoBringUIParent)
            autoBringUIParent.SetActive(nowFrozen);
    }

    private void SaveDesignState()
    {
        savedScene.Clear();
        savedSelection.Clear();

        foreach (Part p in Object.FindObjectsByType<Part>(FindObjectsSortMode.None))
        {
            savedScene[p] = new PartSnapshot(p.transform.position,
                                             p.transform.rotation,
                                             p.transform.localScale);

            if (p.IsSelected())
                savedSelection.Add(p);
        }
    }

    private void RestoreDesignState()
    {
        foreach (var kv in savedScene)
        {
            if (!kv.Key) continue;

            kv.Key.transform.SetPositionAndRotation(kv.Value.position, kv.Value.rotation);
            kv.Key.transform.localScale = kv.Value.scale;

            Rigidbody2D rb = kv.Key.GetComponent<Rigidbody2D>();
            if (rb)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        Physics2D.SyncTransforms();

        Part.ClearSelection();
        if (savedSelection.Count > 0)
            Part.SelectGroup(savedSelection, false);
    }

    private void ToggleAutoBring()
    {
        SortingOrderManager.SetAutoBring(!SortingOrderManager.AutoBringEnabled);
        RefreshAutoBringUI();
    }

    private void RefreshAutoBringUI()
    {
        if (autoBringToggleText)
            autoBringToggleText.text = SortingOrderManager.AutoBringEnabled ? "On" : "Off";
    }

    /* =======================================================================
     *  PART SPAWN HELPERS
     * ===================================================================== */
    private void SpawnSquare() =>
        SpawnGenericPart(squareSprite, spawnSquareColor,
                         () => new GameObject("Part").AddComponent<BoxCollider2D>());

    private void SpawnCircle() =>
        SpawnGenericPart(circleSprite, spawnCircleColor,
                         () => new GameObject("Part").AddComponent<CircleCollider2D>());

    private void SpawnTriangle() =>
        SpawnGenericPart(triangleSprite, spawnTriangleColor, () =>
        {
            PolygonCollider2D pc = new GameObject("Part").AddComponent<PolygonCollider2D>();
            pc.points = new Vector2[]
            {
                new(-0.5f, -0.5f),
                new( 0.5f, -0.5f),
                new(-0.5f,  0.5f)
            };
            return pc;
        });

    private void SpawnGenericPart(Sprite sprite, Color colour,
                                  System.Func<Collider2D> colliderFactory)
    {
        Vector3 pos = GridSnapping.SnapPos(GetWorldMouse());

        Collider2D col = colliderFactory.Invoke();
        GameObject obj = col.gameObject;
        obj.transform.SetPositionAndRotation(pos, Quaternion.identity);
        obj.transform.localScale = Vector3.one;
        obj.transform.parent = transform;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.color = colour;
        sr.sprite = sprite;
        sr.sortingOrder = SortingOrderManager.GetNext();

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.mass = defaultMass;

        Part p = obj.AddComponent<Part>();
        p.spawner = this;
        p.mass = defaultMass;
        p.SetCursorTextures(cursorDragTexture, cursorScaleTexture, cursorDefaultTexture);
        p.SetSelectionColours(selectionBorderMain, selectionBorderSecondary);

        SortingOrderManager.BringToFront(new[] { p });
        p.SelectAsSingle();
    }

    /* =======================================================================
     *  JOINT CREATION
     * ===================================================================== */
    private void CreateJointAtMouse(JointType type)
    {
        Vector3 snapPos = GridSnapping.SnapPos(GetWorldMouse());
        const float detectRadius = 0.05f;

        /* ------------------------------------------------------------------
         * Collect ONLY *selected* parts overlapping the cursor.  Both ends
         * must be pre?selected or we refuse to create the joint.             */
        Collider2D[] hits = Physics2D.OverlapCircleAll(snapPos, detectRadius);
        List<Part> parts = new();
        foreach (Collider2D h in hits)
        {
            if (h.TryGetComponent(out Part pt) &&
                pt.IsSelected() &&                 // NEW: must be selected
                !parts.Contains(pt))
            {
                parts.Add(pt);
            }
        }

        /* Need at least two selected parts under the cursor. */
        if (parts.Count < 2 || IsConnectionAtPoint(snapPos)) return;

        /* Pick the top two by sorting order. */
        parts.Sort((p1, p2) =>
        {
            int so1 = p1.GetComponent<SpriteRenderer>().sortingOrder;
            int so2 = p2.GetComponent<SpriteRenderer>().sortingOrder;
            return so2.CompareTo(so1);
        });

        Part a = parts[0];
        Part b = parts[1];

        /* ------------------------------------------------------------------ */
        GameObject conn = new GameObject(type.ToString());
        conn.transform.position = snapPos;
        conn.transform.SetParent(b.transform, true);

        SpriteRenderer sr = conn.AddComponent<SpriteRenderer>();
        float visSize = 0f;

        switch (type)
        {
            case JointType.Bolt:
                sr.color = boltColor; sr.sprite = boltSprite; visSize = boltSize; break;
            case JointType.Hinge:
                sr.color = hingeColor; sr.sprite = hingeSprite; visSize = hingeSize; break;
            case JointType.Motor:
                sr.color = motorColor; sr.sprite = motorSprite; visSize = motorSize; break;
        }

        sr.sortingOrder = Mathf.Max(
            a.GetComponent<SpriteRenderer>().sortingOrder,
            b.GetComponent<SpriteRenderer>().sortingOrder) + 1;
        SortingOrderManager.EnsureAtLeast(sr.sortingOrder);

        Vector3 ps = b.transform.lossyScale;
        conn.transform.localScale = new(visSize / ps.x, visSize / ps.y, 1f);

        Rigidbody2D rbA = a.GetComponent<Rigidbody2D>();

        if (type == JointType.Bolt)
        {
            FixedJoint2D j = b.gameObject.AddComponent<FixedJoint2D>();
            j.connectedBody = rbA; j.enableCollision = false;
            j.anchor = conn.transform.localPosition;
        }
        else
        {
            HingeJoint2D j = b.gameObject.AddComponent<HingeJoint2D>();
            j.connectedBody = rbA; j.enableCollision = false;
            j.anchor = conn.transform.localPosition;

            if (type == JointType.Motor)
            {
                j.useMotor = true;
                JointMotor2D m = j.motor;

                const float LEVER_ARM = 0.5f;
                float defaultTorque = defaultMass * 9.81f * LEVER_ARM * 1.2f;

                m.maxMotorTorque = defaultTorque;
                m.motorSpeed = motorClockwiseDefault ? -motorDefaultSpeed : motorDefaultSpeed;
                j.motor = m;

                CircleCollider2D cc = conn.AddComponent<CircleCollider2D>();
                cc.isTrigger = true; cc.radius = 0.5f;

                Motor ui = conn.AddComponent<Motor>(); ui.Init(j);
            }
        }

        JointVisual jv = conn.AddComponent<JointVisual>();
        jv.Init(a.GetComponent<SpriteRenderer>(), b.GetComponent<SpriteRenderer>());

        a.AddConnectedPart(b);
        b.AddConnectedPart(a);
    }

    private static bool IsConnectionAtPoint(Vector3 wp)
    {
        const float eps2 = 1e-6f;
        foreach (AnchoredJoint2D j in Object.FindObjectsByType<AnchoredJoint2D>(FindObjectsSortMode.None))
            if ((j.transform.TransformPoint(j.anchor) - wp).sqrMagnitude < eps2)
                return true;
        return false;
    }

    /* =======================================================================
     *  UTILITY
     * ===================================================================== */
    private Vector3 GetWorldMouse()
    {
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        wp.z = 0f;
        return wp;
    }
}
