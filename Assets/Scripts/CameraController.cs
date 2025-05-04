// CameraController.cs – Mechanica Sandbox 2D
// Handles pan/zoom, but respects Part.IsInteracting (e.g. during slider drags).
// 2025?05?03  CHANGE: panning uses right?mouse button (button 1)

using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 10f;
    public float minOrthographicSize = 1f;
    public float maxOrthographicSize = 100f;

    [Header("Pan Settings")]
    public float panSpeed = 0.5f;
    private Vector3 lastMousePosition;

    [Header("Pan Bounds")]
    public float minX = -50f;
    public float maxX = 50f;
    public float minY = -50f;
    public float maxY = 50f;

    private void Update()
    {
        if (Part.IsInteracting) return;

        HandleZoom();
        HandlePan();
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;

        if (Camera.main.orthographic)
        {
            float newSize = Camera.main.orthographicSize - scroll * zoomSpeed;
            Camera.main.orthographicSize =
                Mathf.Clamp(newSize, minOrthographicSize, maxOrthographicSize);
        }
        else
        {
            float newFOV = Camera.main.fieldOfView - scroll * zoomSpeed;
            Camera.main.fieldOfView = Mathf.Clamp(newFOV, 15f, 90f);
        }

        ClampPosition();
    }

    private void HandlePan()
    {
        // --- Right?mouse drag ---
        if (Input.GetMouseButtonDown(1))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(1))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            delta = Camera.main.ScreenToWorldPoint(delta) -
                    Camera.main.ScreenToWorldPoint(Vector3.zero);

            transform.position -= delta * panSpeed;
            ClampPosition();

            lastMousePosition = Input.mousePosition;
        }
    }

    private void ClampPosition()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);
        transform.position = pos;
    }
}
