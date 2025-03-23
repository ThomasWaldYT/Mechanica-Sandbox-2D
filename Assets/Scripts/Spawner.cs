using UnityEngine;
using System.Collections.Generic;

public enum JointType { Bolt, Hinge }

public class Spawner : MonoBehaviour
{
    [SerializeField] private Color spawnColor = Color.white;
    [SerializeField] private Sprite squareSprite = null;

    [SerializeField] private Color boltColor = Color.yellow;
    [SerializeField] private Sprite boltSprite = null;

    [SerializeField] private Color hingeColor = Color.green;       // Hinge color
    [SerializeField] private Sprite hingeSprite = null;            // Hinge sprite
    [SerializeField] private float hingeSize = 0.15f;                // Hinge size
    [SerializeField] private int hingeSortingOrder = 2;              // Hinge sorting order

    [SerializeField] private float boltSize = 0.2f;

    [SerializeField] private int squareSortingOrder = 0;
    [SerializeField] private int boltSortingOrder = 1;

    [SerializeField] public float defaultMass = 33f;

    // New cursor sprites:
    [SerializeField] public Sprite cursorDragSprite = null;
    [SerializeField] public Sprite cursorScaleSprite = null;

    private bool isFrozen = false;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
            SpawnSquare();
        if (Input.GetKeyDown(KeyCode.F))
            ToggleFreeze();
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (Time.timeScale == 0f)
                CreateJointAtMouse(JointType.Bolt);
        }
        if (Input.GetKeyDown(KeyCode.H))
        {
            if (Time.timeScale == 0f)
                CreateJointAtMouse(JointType.Hinge);
        }
    }

    private void SpawnSquare()
    {
        Vector3 spawnPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        spawnPos.z = 0f;
        GameObject square = new GameObject("Part");
        square.transform.position = spawnPos;
        square.transform.localScale = Vector3.one;
        square.transform.parent = transform;
        SpriteRenderer sr = square.AddComponent<SpriteRenderer>();
        sr.color = spawnColor;
        if (squareSprite != null)
            sr.sprite = squareSprite;
        sr.sortingOrder = squareSortingOrder;
        Rigidbody2D rb = square.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        square.AddComponent<BoxCollider2D>();
        square.AddComponent<Part>();
        Part partScript = square.GetComponent<Part>();
        partScript.spawner = this;
        partScript.mass = defaultMass;
        rb.mass = defaultMass;
    }

    private void ToggleFreeze()
    {
        isFrozen = !isFrozen;
        Time.timeScale = isFrozen ? 0f : 1f;
    }

    // Generic joint creation: connects exactly two parts.
    private void CreateJointAtMouse(JointType type)
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Collider2D[] colliders = Physics2D.OverlapPointAll(mousePos);
        List<Part> partsUnderMouse = new List<Part>();
        foreach (Collider2D col in colliders)
        {
            Part part = col.GetComponent<Part>();
            if (part != null && !partsUnderMouse.Contains(part))
                partsUnderMouse.Add(part);
        }
        if (partsUnderMouse.Count < 2)
            return;

        // Connect only the first two parts.
        Part partA = partsUnderMouse[0];
        Part partB = partsUnderMouse[1];

        if (type == JointType.Bolt)
        {
            FixedJoint2D joint = partB.gameObject.AddComponent<FixedJoint2D>();
            joint.connectedBody = partA.GetComponent<Rigidbody2D>();
            joint.enableCollision = false;
            // Create a connector object as a child of partB.
            GameObject connector = new GameObject("Bolt");
            connector.transform.position = mousePos;
            connector.transform.parent = partB.transform;
            SpriteRenderer connSR = connector.AddComponent<SpriteRenderer>();
            connSR.color = boltColor;
            if (boltSprite != null)
                connSR.sprite = boltSprite;
            connSR.sortingOrder = boltSortingOrder;
            connector.transform.localScale = new Vector3(boltSize, boltSize, 1);
            joint.anchor = connector.transform.localPosition;
        }
        else if (type == JointType.Hinge)
        {
            HingeJoint2D joint = partB.gameObject.AddComponent<HingeJoint2D>();
            joint.connectedBody = partA.GetComponent<Rigidbody2D>();
            joint.enableCollision = false;
            GameObject connector = new GameObject("Hinge");
            connector.transform.position = mousePos;
            connector.transform.parent = partB.transform;
            SpriteRenderer connSR = connector.AddComponent<SpriteRenderer>();
            connSR.color = hingeColor;
            if (hingeSprite != null)
                connSR.sprite = hingeSprite;
            connSR.sortingOrder = hingeSortingOrder;
            connector.transform.localScale = new Vector3(hingeSize, hingeSize, 1);
            joint.anchor = connector.transform.localPosition;
        }

        // Register the direct connection between partA and partB.
        partA.AddConnectedPart(partB);
        partB.AddConnectedPart(partA);
    }
}
