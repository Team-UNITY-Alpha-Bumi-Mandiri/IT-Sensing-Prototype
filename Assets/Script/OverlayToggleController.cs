using UnityEngine;

// ============================================================
// OverlayToggleController - Controller ON/OFF semua overlay
// ============================================================
// Fungsi:
// - EnableAll/DisableAll: Toggle semua property sekaligus
// - SetProperty: Set property tertentu ke nilai tertentu
// Dipanggil dari UI button atau script lain (SharpeningController)
// ============================================================
public class OverlayToggleController : MonoBehaviour
{
    [Header("References")]
    public ProjectManager projectManager;    // Manager project untuk akses properties
    public PropertyPanel propertyPanel;      // Panel UI untuk refresh tampilan
    public TiffLayerManager tiffLayerManager; // Manager layer untuk sync visibility

    // Nyalakan semua property toggle
    public void EnableAll() => SetAllProperties(true);

    // Matikan semua property toggle
    public void DisableAll() => SetAllProperties(false);

    // Set semua property ke nilai tertentu
    void SetAllProperties(bool value)
    {
        var proj = projectManager?.GetCurrentProject();
        if (proj?.properties == null) return;

        // Update semua property
        foreach (var p in proj.properties) p.value = value;
        projectManager.Save();

        // Refresh UI panel
        propertyPanel?.ShowPropertiesWithType(proj.GetProps());

        // Sync visibility ke layer manager
        if (tiffLayerManager != null)
        {
            foreach (var kv in proj.GetProps())
                tiffLayerManager.OnPropertyToggleExternal(kv.Key, kv.Value.value);
        }

        Debug.Log($"[OverlayToggleController] Set all = {value}");
    }

    // Set property tertentu ke nilai tertentu
    // Berguna untuk toggle spesifik dari script lain
    public void SetProperty(string name, bool value)
    {
        var proj = projectManager?.GetCurrentProject();
        if (proj?.properties == null) return;

        var prop = proj.properties.Find(p => p.key == name);
        if (prop == null) return;

        prop.value = value;
        projectManager.Save();

        propertyPanel?.ShowPropertiesWithType(proj.GetProps());
        tiffLayerManager?.OnPropertyToggleExternal(name, value);
    }
}
