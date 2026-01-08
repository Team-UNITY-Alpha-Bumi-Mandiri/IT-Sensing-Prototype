using UnityEngine;

// =========================================
// Controller untuk mengatur ON/OFF toggle overlay
// Bisa dipanggil dari mana saja (SharpeningController, ProjectManager, dll)
// =========================================
public class OverlayToggleController : MonoBehaviour
{
    [Header("References")]
    public ProjectManager projectManager;
    public PropertyPanel propertyPanel;
    public TiffLayerManager tiffLayerManager;

    // =========================================
    // PUBLIC METHODS
    // =========================================

    /// <summary>
    /// Nyalakan semua property toggle di project saat ini
    /// </summary>
    public void EnableAll()
    {
        SetAllProperties(true);
    }

    /// <summary>
    /// Matikan semua property toggle di project saat ini
    /// </summary>
    public void DisableAll()
    {
        SetAllProperties(false);
    }

    /// <summary>
    /// Set semua property ke nilai tertentu
    /// </summary>
    void SetAllProperties(bool value)
    {
        if (projectManager == null) return;
        var proj = projectManager.GetCurrentProject();
        if (proj == null || proj.properties == null) return;

        foreach (var p in proj.properties)
        {
            p.value = value;
        }
        projectManager.Save();

        // Refresh PropertyPanel
        if (propertyPanel != null)
        {
            propertyPanel.ShowProperties(proj.GetProps());
        }

        // Sync TiffLayerManager visibility
        if (tiffLayerManager != null)
        {
            // Trigger property changed untuk setiap layer
            var props = proj.GetProps();
            foreach (var kv in props)
            {
                // Manually notify TiffLayerManager
                tiffLayerManager.OnPropertyToggleExternal(kv.Key, kv.Value);
            }
        }

        Debug.Log($"[OverlayToggleController] Set all properties to {value}");
    }

    /// <summary>
    /// Set property tertentu ke nilai tertentu
    /// </summary>
    public void SetProperty(string name, bool value)
    {
        if (projectManager == null) return;
        var proj = projectManager.GetCurrentProject();
        if (proj == null || proj.properties == null) return;

        var prop = proj.properties.Find(p => p.key == name);
        if (prop != null)
        {
            prop.value = value;
            projectManager.Save();

            // Refresh PropertyPanel
            if (propertyPanel != null)
            {
                propertyPanel.ShowProperties(proj.GetProps());
            }

            // Sync TiffLayerManager
            if (tiffLayerManager != null)
            {
                tiffLayerManager.OnPropertyToggleExternal(name, value);
            }
        }
    }
}
