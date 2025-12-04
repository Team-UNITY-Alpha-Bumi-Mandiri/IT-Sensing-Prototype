using UnityEngine;
using UnityEngine.EventSystems;

public class PanAndZoom : MonoBehaviour
{
    public Camera mapCamera;
    public float panSpeed = 0.01f;
    public float zoomSpeed = 2f;
    public float minOrtho = 2f;
    public float maxOrtho = 40f;

    Vector3 lastMousePos;
    bool dragging = false;

    void Update()
    {
        // block if pointer over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // Start dragging
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePos = Input.mousePosition;
            dragging = true;
        }
        if (Input.GetMouseButtonUp(0))
            dragging = false;

        if (dragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            // convert screen delta to world delta
            Vector3 worldDelta = mapCamera.ScreenToWorldPoint(lastMousePos) - mapCamera.ScreenToWorldPoint(lastMousePos + delta);
            mapCamera.transform.position += worldDelta;
            lastMousePos = Input.mousePosition;
        }

        // Zoom (mouse wheel)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            float size = mapCamera.orthographicSize - scroll * zoomSpeed;
            mapCamera.orthographicSize = Mathf.Clamp(size, minOrtho, maxOrtho);
        }
    }
}
