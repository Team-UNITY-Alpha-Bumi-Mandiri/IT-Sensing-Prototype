using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper script untuk actions umum DrawTool
/// Bisa ditaruh di panel toolbar terpisah atau di buttons individual
/// </summary>
public class DrawToolHelper : MonoBehaviour
{
    [Header("References")]
    public DrawTool drawTool;

    [Header("Buttons (Optional - assign jika dibutuhkan)")]
    public Button finishButton; // Manual finish untuk Line/Polygon

    void Start()
    {
        // Setup listeners jika button di-assign
        if (finishButton != null)
            finishButton.onClick.AddListener(OnFinish);
    }

    // ========================================================================
    // PUBLIC METHODS (bisa dipanggil dari UI Button atau script lain)
    // ========================================================================

    public void OnFinish()
    {
        if (drawTool != null)
        {
            // Cek apakah sedang drawing Line atau Polygon
            if (drawTool.IsCurrentlyDrawing())
            {
                // Trigger finish secara manual (alternatif untuk double-click)
                Debug.Log("Gunakan Double-Click untuk finish atau ESC untuk cancel");
                // Atau bisa expose method FinishDrawing() sebagai public
            }
        }
    }

    public void OnCancel()
    {
        if (drawTool != null && drawTool.IsCurrentlyDrawing())
        {
            // Cancel drawing yang sedang berlangsung
            Debug.Log("Gunakan ESC untuk cancel");
        }
    }

    // ========================================================================
    // KEYBOARD SHORTCUTS
    // ========================================================================
    void Update()
    {
        if (drawTool == null) return;

        // Enter = Finish (untuk Line/Polygon)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (drawTool.IsCurrentlyDrawing())
            {
                OnFinish();
            }
        }
    }
}