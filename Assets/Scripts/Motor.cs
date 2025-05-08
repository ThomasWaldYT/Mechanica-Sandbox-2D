// Motor.cs – runtime selection UI for motorised hinges
// CHANGELOG #3 (2025-05-07)
//   • Menu now closes on zoom (mouse?wheel scroll).
//   • Outline colour matches normal part selection colour.
//   • Outline is shown only while the context menu is open.
//   • Increased menu width so "Counter-Clockwise" fits.

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class Motor : MonoBehaviour
{
    // STATIC SELECTION TRACKING
    private static readonly List<Motor> current = new();
    private static Motor mainSelected;

    public static void ClearSelection()
    {
        foreach (var m in current)
        {
            if (m) { m.HideOutline(); m.showContextMenu = false; }
        }
        current.Clear();
        mainSelected = null;
    }

    public static bool IsPointerOverContextMenuArea()
    {
        if (mainSelected == null || !mainSelected.showContextMenu) return false;
        Vector2 guiMouse = new(Input.mousePosition.x,
                               Screen.height - Input.mousePosition.y);
        return mainSelected.contextMenuRect.Contains(guiMouse);
    }

    // RUNTIME / LINKS
    private HingeJoint2D joint;     // hinge on parent part
    private LineRenderer outline;
    private CircleCollider2D col;

    // context menu
    private bool showContextMenu;
    private Vector2 menuGuiPos;
    private Rect contextMenuRect;

    // right?click helpers
    private bool rightCandidate;
    private Vector2 rightStartScreen;
    private const float RIGHT_DRAG_PIXELS = 4f;

    private const int CIRCLE_SEGMENTS = 36;
    private const float UI_SCALE = 2f;
    private const float LINE_WIDTH = 0.05f;

    private bool Frozen => Time.timeScale == 0f;

    // INITIALISATION
    public void Init(HingeJoint2D j) => joint = j;

    private void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;   // visual scale is applied by Spawner

        BuildOutline();
        HideOutline();
    }

    private void Update()
    {
        // close menu if user zooms
        if (showContextMenu && Input.mouseScrollDelta.y != 0f)
            showContextMenu = false;

        // ensure outline only shows while menu is open
        if (outline.enabled && !showContextMenu)
            HideOutline();

        HandleRightClick();
    }

    // RIGHT?CLICK HANDLING
    private void HandleRightClick()
    {
        if (!Frozen) return;

        if (Input.GetMouseButtonDown(1) && col.OverlapPoint(GetWorldMouse()))
        {
            rightCandidate = true;
            rightStartScreen = Input.mousePosition;
        }
        if (rightCandidate && Input.GetMouseButton(1) &&
            Vector2.Distance(Input.mousePosition, rightStartScreen) > RIGHT_DRAG_PIXELS)
            rightCandidate = false;

        if (Input.GetMouseButtonUp(1) && rightCandidate && col.OverlapPoint(GetWorldMouse()))
        {
            if (!showContextMenu)
            {
                Part.ClearSelection();
                ClearSelection();

                // highlight connected parts
                if (joint && joint.attachedRigidbody)
                {
                    Part root = joint.attachedRigidbody.GetComponent<Part>();
                    if (root) root.SelectGroup(false);
                }

                current.Add(this);
                mainSelected = this;

                menuGuiPos = new(Input.mousePosition.x,
                                 Screen.height - Input.mousePosition.y);
                showContextMenu = true;
                ShowOutline();
            }
        }

        // close on right?click outside
        if (showContextMenu && Input.GetMouseButtonDown(1) &&
            !col.OverlapPoint(GetWorldMouse()))
            showContextMenu = false;
    }

    // GUI
    private void OnGUI()
    {
        if (!showContextMenu || !Frozen) return;

        GUIStyle lblStyle = new(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * UI_SCALE) };
        GUIStyle btnStyle = new(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * UI_SCALE) };

        // widened menu so "Counter-Clockwise" fits
        float W0 = 220f, LINE_H0 = 22f, SLIDER_H0 = 18f, PAD0 = 8f;

        float W = W0 * UI_SCALE;
        float SLIDER_H = SLIDER_H0 * UI_SCALE;
        float LINE_H = LINE_H0 * UI_SCALE;
        float PAD = PAD0 * UI_SCALE;

        float menuH = PAD * 2 + lblStyle.lineHeight + SLIDER_H + 12f * UI_SCALE +
                      LINE_H + 4f * UI_SCALE;

        Rect bgR = new(menuGuiPos.x + 20f * UI_SCALE,
                       menuGuiPos.y - menuH * 0.5f, W, menuH);
        contextMenuRect = bgR;

        // background box
        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        GUI.Box(bgR, GUIContent.none);
        GUI.color = prev;

        // controls
        float y = bgR.y + PAD;

        Rect labelRect = new(bgR.x + PAD, y, W - PAD * 2, lblStyle.lineHeight);
        y += lblStyle.lineHeight;

        Rect sliderRect = new(bgR.x + PAD, y, W - PAD * 2, SLIDER_H);
        y += SLIDER_H + 12f * UI_SCALE;

        Rect toggleRect = new(bgR.x + PAD, y, W - PAD * 2, LINE_H);

        float absSpeed = Mathf.Abs(joint.motor.motorSpeed);
        GUI.Label(labelRect, $"Speed {absSpeed:0}", lblStyle);

        float newAbs = GUI.HorizontalSlider(sliderRect, absSpeed, 0f, 360f);
        bool dirty = !Mathf.Approximately(newAbs, absSpeed);

        bool cw = joint.motor.motorSpeed < 0f;
        bool newCw = GUI.Toggle(toggleRect, cw,
                                cw ? "Clockwise" : "Counter-Clockwise", btnStyle);
        if (newCw != cw) dirty = true;

        // apply changes
        if (dirty)
        {
            JointMotor2D m = joint.motor;
            m.motorSpeed = newCw ? -newAbs : newAbs;
            m.maxMotorTorque = Mathf.Infinity;
            joint.motor = m;
        }

        // close if clicked elsewhere
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

    // OUTLINE
    private void BuildOutline()
    {
        outline = new GameObject("Outline").AddComponent<LineRenderer>();
        outline.transform.SetParent(transform, false);
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.startWidth = outline.endWidth = LINE_WIDTH;
        outline.material = new Material(Shader.Find("Sprites/Default"));
        outline.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder + 50;

        float r = 0.5f;
        outline.positionCount = CIRCLE_SEGMENTS;
        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float a = i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
            outline.SetPosition(i,
                new(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
    }

    private void ShowOutline()
    {
        if (!outline) return;
        outline.startColor = outline.endColor = Spawner.SelectionBorderMain;
        outline.enabled = true;
    }

    private void HideOutline()
    {
        if (outline) outline.enabled = false;
    }

    // UTILITY
    private Vector3 GetWorldMouse()
    {
        Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        wp.z = 0f;
        return wp;
    }
}
