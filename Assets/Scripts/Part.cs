using UnityEngine;
using System.Collections.Generic;

public class Part : MonoBehaviour
{
    public Spawner spawner; // Reference to the spawner

    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private Vector3 moveOffset;
    private Vector3 fixedEdgeWorldPos;

    private enum DragMode { None, Move, ResizeLeft, ResizeRight, ResizeTop, ResizeBottom }
    private DragMode currentDragMode = DragMode.None;

    [SerializeField] private float edgeThreshold = 0.15f;
    [SerializeField] private float minWidth = 0.2f;
    [SerializeField] private float minHeight = 0.2f;
    [SerializeField] private float groupRotationSpeed = 5f; // degrees per frame when rotating

    // Mass value, range 1–100; brightness (V) is proportional to mass.
    public float mass = 33f;

    private float currentWidth = 1f;
    private float currentHeight = 1f;
    private bool isFrozenDrag = false;

    // List of parts directly connected to this part.
    private List<Part> connectedParts = new List<Part>();

    // For group dragging, we use this list to store the group (computed on demand).
    private List<Part> dragGroup = new List<Part>();

    private Vector3 lastGroupMousePos;
    private Vector3 lastDragMousePos;

    // For mass slider UI
    private bool showMassSlider = false;
    private float sliderWidth = 500f;
    private float sliderHeight = 100f;

    // Base HSV values from the initial color.
    private float baseHue;
    private float baseSat;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = new Vector3(currentWidth, currentHeight, 1);
        if (col is BoxCollider2D box)
        {
            box.size = new Vector2(1, 1);
            box.offset = Vector2.zero;
        }
        Color.RGBToHSV(sr.color, out baseHue, out baseSat, out _);
    }

    private void Start()
    {
        if (spawner != null)
            mass = spawner.defaultMass;
        else
            mass = 33f;
        rb.mass = mass;
        Color newColor = Color.HSVToRGB(baseHue, baseSat, mass / 100f);
        newColor.a = sr.color.a;
        sr.color = newColor;
    }

    // Public method to register a direct connection.
    public void AddConnectedPart(Part other)
    {
        if (!connectedParts.Contains(other))
            connectedParts.Add(other);
    }

    // Recursively get the connected group (direct connections only).
    public List<Part> GetConnectedGroup()
    {
        List<Part> group = new List<Part>();
        HashSet<Part> visited = new HashSet<Part>();
        Queue<Part> queue = new Queue<Part>();
        queue.Enqueue(this);
        visited.Add(this);
        while (queue.Count > 0)
        {
            Part current = queue.Dequeue();
            group.Add(current);
            foreach (Part p in current.connectedParts)
            {
                if (!visited.Contains(p))
                {
                    visited.Add(p);
                    queue.Enqueue(p);
                }
            }
        }
        return group;
    }

    // --- Cursor Handling ---
    private void OnMouseOver()
    {
        Vector3 worldMouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldMouse.z = 0f;
        Vector3 localMouse = transform.InverseTransformPoint(worldMouse);
        float halfLocalSize = 0.5f;
        float distLeft = Mathf.Abs(localMouse.x + halfLocalSize);
        float distRight = Mathf.Abs(localMouse.x - halfLocalSize);
        float distTop = Mathf.Abs(localMouse.y - halfLocalSize);
        float distBottom = Mathf.Abs(localMouse.y + halfLocalSize);
        float minDist = Mathf.Min(distLeft, distRight, distTop, distBottom);
        // Only show scaling cursor if part is single.
        if (minDist < edgeThreshold && GetConnectedGroup().Count == 1)
        {
            if (spawner != null && spawner.cursorScaleSprite != null)
                Cursor.SetCursor(spawner.cursorScaleSprite.texture,
                    new Vector2(spawner.cursorScaleSprite.texture.width / 2, spawner.cursorScaleSprite.texture.height / 2),
                    CursorMode.Auto);
        }
        else
        {
            if (spawner != null && spawner.cursorDragSprite != null)
                Cursor.SetCursor(spawner.cursorDragSprite.texture,
                    new Vector2(spawner.cursorDragSprite.texture.width / 2, spawner.cursorDragSprite.texture.height / 2),
                    CursorMode.Auto);
        }
        if (Input.GetMouseButtonDown(1))
            showMassSlider = !showMassSlider;
    }

    private void OnMouseExit()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
    // --- End Cursor Handling ---

    private void OnGUI()
    {
        if (showMassSlider)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            float guiY = Screen.height - screenPos.y;
            Rect sliderRect = new Rect(screenPos.x - sliderWidth / 2, guiY - 200, sliderWidth, sliderHeight);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !sliderRect.Contains(Event.current.mousePosition))
                showMassSlider = false;
            GUI.Label(new Rect(sliderRect.x, sliderRect.y - 30, sliderWidth, 30), "Mass: " + mass.ToString("F0"), new GUIStyle() { fontSize = 24 });
            float newMass = GUI.HorizontalSlider(sliderRect, mass, 1f, 100f);
            if (!Mathf.Approximately(newMass, mass))
            {
                mass = newMass;
                rb.mass = mass;
                Color updatedColor = Color.HSVToRGB(baseHue, baseSat, mass / 100f);
                updatedColor.a = sr.color.a;
                sr.color = updatedColor;
            }
        }
    }

    private void OnMouseDown()
    {
        currentDragMode = DragMode.None;
        moveOffset = Vector3.zero;
        isFrozenDrag = false;
        if (col != null && !col.enabled)
            col.enabled = true;
        isFrozenDrag = (Time.timeScale == 0f);

        Vector3 worldMouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldMouse.z = 0f;
        lastDragMousePos = worldMouse;

        List<Part> group = GetConnectedGroup();
        if (group.Count > 1)
        {
            dragGroup = group;
            lastGroupMousePos = worldMouse;
        }
        else
        {
            dragGroup.Clear();
            moveOffset = transform.position - worldMouse;
        }

        if (Time.timeScale != 0f)
        {
            if (dragGroup.Count > 0)
            {
                foreach (Part p in dragGroup)
                {
                    Rigidbody2D rbTemp = p.GetComponent<Rigidbody2D>();
                    if (rbTemp != null)
                    {
                        rbTemp.bodyType = RigidbodyType2D.Kinematic;
                        rbTemp.linearVelocity = Vector2.zero;
                        rbTemp.angularVelocity = 0f;
                    }
                }
            }
            else if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        Vector3 localMouse = transform.InverseTransformPoint(worldMouse);
        float halfSize = 0.5f;
        float distLeft = Mathf.Abs(localMouse.x + halfSize);
        float distRight = Mathf.Abs(localMouse.x - halfSize);
        float distTop = Mathf.Abs(localMouse.y - halfSize);
        float distBottom = Mathf.Abs(localMouse.y + halfSize);
        float minDist = Mathf.Min(distLeft, distRight, distTop, distBottom);
        // Allow scaling only if this part is single.
        if (minDist < edgeThreshold && GetConnectedGroup().Count == 1)
        {
            if (minDist == distLeft)
            {
                currentDragMode = DragMode.ResizeLeft;
                fixedEdgeWorldPos = transform.position + Vector3.right * (currentWidth / 2f);
            }
            else if (minDist == distRight)
            {
                currentDragMode = DragMode.ResizeRight;
                fixedEdgeWorldPos = transform.position - Vector3.right * (currentWidth / 2f);
            }
            else if (minDist == distTop)
            {
                currentDragMode = DragMode.ResizeTop;
                fixedEdgeWorldPos = transform.position - Vector3.up * (currentHeight / 2f);
            }
            else if (minDist == distBottom)
            {
                currentDragMode = DragMode.ResizeBottom;
                fixedEdgeWorldPos = transform.position + Vector3.up * (currentHeight / 2f);
            }
        }
        else
        {
            currentDragMode = DragMode.Move;
        }
    }

    private void OnMouseDrag()
    {
        Vector3 worldMouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        worldMouse.z = 0f;

        // If in scaling mode (allowed only for single parts)
        if (currentDragMode == DragMode.ResizeLeft || currentDragMode == DragMode.ResizeRight ||
            currentDragMode == DragMode.ResizeTop || currentDragMode == DragMode.ResizeBottom)
        {
            if (GetConnectedGroup().Count == 1)
            {
                if (currentDragMode == DragMode.ResizeLeft)
                {
                    float oldLeft = transform.position.x - (currentWidth / 2f);
                    float newLeft = worldMouse.x;
                    float delta = oldLeft - newLeft;
                    float newWidth = currentWidth + delta;
                    if (newWidth < minWidth) { delta = minWidth - currentWidth; newWidth = minWidth; }
                    Vector3 newPos = transform.position;
                    newPos.x -= delta / 2f;
                    currentWidth = newWidth;
                    transform.position = newPos;
                    transform.localScale = new Vector3(currentWidth, currentHeight, 1);
                }
                else if (currentDragMode == DragMode.ResizeRight)
                {
                    float oldRight = transform.position.x + (currentWidth / 2f);
                    float newRight = worldMouse.x;
                    float delta = newRight - oldRight;
                    float newWidth = currentWidth + delta;
                    if (newWidth < minWidth) { delta = minWidth - currentWidth; newWidth = minWidth; }
                    Vector3 newPos = transform.position;
                    newPos.x += delta / 2f;
                    currentWidth = newWidth;
                    transform.position = newPos;
                    transform.localScale = new Vector3(currentWidth, currentHeight, 1);
                }
                else if (currentDragMode == DragMode.ResizeTop)
                {
                    float oldTop = transform.position.y + (currentHeight / 2f);
                    float newTop = worldMouse.y;
                    float delta = newTop - oldTop;
                    float newHeight = currentHeight + delta;
                    if (newHeight < minHeight) { delta = minHeight - currentHeight; newHeight = minHeight; }
                    Vector3 newPos = transform.position;
                    newPos.y += delta / 2f;
                    currentHeight = newHeight;
                    transform.position = newPos;
                    transform.localScale = new Vector3(currentWidth, currentHeight, 1);
                }
                else if (currentDragMode == DragMode.ResizeBottom)
                {
                    float oldBottom = transform.position.y - (currentHeight / 2f);
                    float newBottom = worldMouse.y;
                    float delta = oldBottom - newBottom;
                    float newHeight = currentHeight + delta;
                    if (newHeight < minHeight) { delta = minHeight - currentHeight; newHeight = minHeight; }
                    Vector3 newPos = transform.position;
                    newPos.y -= delta / 2f;
                    currentHeight = newHeight;
                    transform.position = newPos;
                    transform.localScale = new Vector3(currentWidth, currentHeight, 1);
                }
            }
        }
        else
        {
            // Apply translation.
            Vector3 translationDelta = worldMouse - lastDragMousePos;
            lastDragMousePos = worldMouse;
            if (dragGroup.Count > 0)
            {
                foreach (Part p in dragGroup)
                    p.transform.position += translationDelta;
                lastGroupMousePos = worldMouse;
            }
            else
            {
                transform.position += translationDelta;
            }

            // Independently, if R is held, apply a fixed rotation.
            if (Input.GetKey(KeyCode.R))
            {
                float rotationDelta = groupRotationSpeed; // fixed angle per frame.
                if (dragGroup.Count > 0)
                {
                    Vector3 groupCenter = Vector3.zero;
                    foreach (Part p in dragGroup)
                        groupCenter += p.transform.position;
                    groupCenter /= dragGroup.Count;
                    foreach (Part p in dragGroup)
                    {
                        Vector3 offset = p.transform.position - groupCenter;
                        offset = Quaternion.Euler(0, 0, rotationDelta) * offset;
                        p.transform.position = groupCenter + offset;
                        p.transform.rotation = p.transform.rotation * Quaternion.Euler(0, 0, rotationDelta);
                    }
                }
                else
                {
                    transform.rotation = transform.rotation * Quaternion.Euler(0, 0, rotationDelta);
                }
            }
        }
    }

    private void OnMouseUp()
    {
        currentDragMode = DragMode.None;
        isFrozenDrag = false;
        if (Time.timeScale != 0f)
        {
            if (dragGroup.Count > 0)
            {
                foreach (Part p in dragGroup)
                {
                    Rigidbody2D rbTemp = p.GetComponent<Rigidbody2D>();
                    if (rbTemp != null)
                    {
                        rbTemp.bodyType = RigidbodyType2D.Dynamic;
                        rbTemp.linearVelocity = Vector2.zero;
                        rbTemp.angularVelocity = 0f;
                    }
                }
            }
            else if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
        dragGroup.Clear();
        if (col != null)
        {
            col.enabled = false;
            Physics2D.SyncTransforms();
            col.enabled = true;
        }
    }

    private void Update()
    {
        if (!Input.GetMouseButton(0) && currentDragMode != DragMode.None)
        {
            currentDragMode = DragMode.None;
            isFrozenDrag = false;
            dragGroup.Clear();
        }
    }

    private void OnJointBreak2D(Joint2D brokenJoint)
    {
        Debug.Log(gameObject.name + " joint broke: " + brokenJoint.name);
    }
}
