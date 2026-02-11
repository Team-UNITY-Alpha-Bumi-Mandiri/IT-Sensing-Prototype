using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Collections;

[ExecuteAlways]
public class NavbarManager : MonoBehaviour
{
    [System.Serializable]
    public class MenuItem
    {
        public string title;
        public List<SubMenuItem> subItems = new List<SubMenuItem>();
    }

    [System.Serializable]
    public class SubMenuItem
    {
        public string title;
        public bool isEnabled = true;
        public System.Action onClickAction;
    }

    [Header("Settings")]
    public float navbarHeight = 40f;
    public float dropdownWidth = 220f;
    public float dropdownItemHeight = 35f;
    
    [Header("Colors")]
    public Color navbarBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark Grey
    public Color itemNormalColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Make sure it's opaque to show the "button"
    public Color itemHoverColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Lighter Grey
    public Color dropdownBackgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color textNormalColor = Color.white;
    public Color textDisabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    public Color textHoverColor = new Color(1f, 0.8f, 0.2f, 1f); // Gold-ish for hover

    [Header("Font")]
    public TMP_FontAsset fontAsset;
    public float fontSize = 14f;

    private List<MenuItem> menuStructure = new List<MenuItem>();
    private GameObject navbarPanel;
    private GameObject currentOpenDropdown;
    private Coroutine closeDropdownCoroutine;

    private Sprite _defaultSprite;
    private Sprite GetDefaultSprite()
    {
        if (_defaultSprite == null)
        {
            // Create a simple 2x2 white texture
            Texture2D tex = new Texture2D(2, 2);
            Color[] colors = new Color[4];
            for(int i=0; i<4; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            _defaultSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
        }
        return _defaultSprite;
    }

    void Start()
    {
        // 1. Ensure we are under a Canvas (Auto-fix if possible)
        if (GetComponentInParent<Canvas>() == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                transform.SetParent(canvas.transform, false);
            }
            else
            {
                // Create one if we can't find it
                GameObject canvasObj = new GameObject("Canvas");
                canvasObj.layer = LayerMask.NameToLayer("UI");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasObj.transform, false);
            }
        }

        // Ensure we have a RectTransform
        if (GetComponent<RectTransform>() == null)
        {
            gameObject.AddComponent<RectTransform>();
        }

        // Reset scale just in case
        transform.localScale = Vector3.one;
        RectTransform selfRT = GetComponent<RectTransform>();
        if (selfRT != null)
        {
            selfRT.anchorMin = Vector2.zero;
            selfRT.anchorMax = Vector2.one;
            selfRT.offsetMin = Vector2.zero;
            selfRT.offsetMax = Vector2.zero;
        }

        // Only build if it doesn't exist (e.g. if we didn't build it in Editor)
        if (transform.Find("NavbarPanel") == null)
        {
            InitializeMenuData();
            BuildNavbar();
        }
        else
        {
            // If in Editor mode and it exists, we might want to ensure listeners are hooked up if we are entering Play mode.
            // But [ExecuteAlways] runs Start when script reloads or object instantiates.
            
            // If application is playing, we MUST rebuild to hook up events (Action delegates are not serialized)
            if (Application.isPlaying)
            {
                ClearNavbar();
                InitializeMenuData();
                BuildNavbar();
            }
        }
    }

    void Update()
    {
        // Optional: Continuous check in Editor if you want live updates without ContextMenu,
        // but be careful not to spam BuildNavbar.
        // For now, Start() + ContextMenu is sufficient.
        if (!Application.isPlaying)
        {
            // If the panel was deleted manually, rebuild it?
            // if (transform.childCount == 0) BuildNavbar(); 
            // Better not auto-rebuild constantly to avoid conflicts.
        }
    }

    [ContextMenu("Build Navbar")]
    public void GenerateNavbarInEditor()
    {
        // 1. Ensure we are under a Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                Debug.Log("Reparenting NavbarManager to existing Canvas.");
                transform.SetParent(canvas.transform, false);
            }
            else
            {
                Debug.Log("Creating new Canvas for Navbar.");
                GameObject canvasObj = new GameObject("Canvas");
                canvasObj.layer = LayerMask.NameToLayer("UI");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasObj.transform, false);
            }
        }

        // 2. Ensure RectTransform on self
        if (GetComponent<RectTransform>() == null)
        {
            gameObject.AddComponent<RectTransform>();
        }
        
        // Reset Self RectTransform to cover screen or be top-anchored?
        // Actually NavbarManager might just be a container. Let's make it fill screen or behave nicely.
        RectTransform selfRT = GetComponent<RectTransform>();
        selfRT.anchorMin = Vector2.zero;
        selfRT.anchorMax = Vector2.one;
        selfRT.offsetMin = Vector2.zero;
        selfRT.offsetMax = Vector2.zero;
        selfRT.localScale = Vector3.one;

        ClearNavbar();
        InitializeMenuData();
        BuildNavbar();
        
        // Force update of layout
        Canvas.ForceUpdateCanvases();
        
        // 3. Ensure EventSystem exists (for buttons to work)
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            Debug.Log("Created EventSystem.");
        }

        Debug.Log("Navbar Built in Editor.");
    }

    [ContextMenu("Clear Navbar")]
    public void ClearNavbar()
    {
        // Find existing panel
        Transform existing = transform.Find("NavbarPanel");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        // Also clear any lingering dropdowns
        List<GameObject> toDestroy = new List<GameObject>();
        foreach(Transform child in transform)
        {
            if (child.name.StartsWith("Dropdown_"))
            {
                toDestroy.Add(child.gameObject);
            }
        }
        
        foreach(var go in toDestroy)
        {
             if (Application.isPlaying) Destroy(go);
             else DestroyImmediate(go);
        }
        
        navbarPanel = null;
        currentOpenDropdown = null;
    }

    void InitializeMenuData()
    {
        menuStructure.Clear(); // Clear existing to avoid duplicates if called multiple times

        // File
        var fileMenu = new MenuItem { title = "File" };
        fileMenu.subItems.Add(new SubMenuItem { title = "Exit", isEnabled = true, onClickAction = () => { Debug.Log("Exit Clicked"); Application.Quit(); } });
        menuStructure.Add(fileMenu);

        // GNSS
        var gnssMenu = new MenuItem { title = "GNSS" };
        gnssMenu.subItems.Add(new SubMenuItem { title = "GNSS Data Viewer", isEnabled = true });
        gnssMenu.subItems.Add(new SubMenuItem { title = "Pos Data Viewer", isEnabled = true });
        gnssMenu.subItems.Add(new SubMenuItem { title = "GNSS Converter", isEnabled = true });
        gnssMenu.subItems.Add(new SubMenuItem { title = "Static Processing", isEnabled = true });
        gnssMenu.subItems.Add(new SubMenuItem { title = "Geotagging", isEnabled = true });
        gnssMenu.subItems.Add(new SubMenuItem { title = "PPK + Geotagging", isEnabled = true });
        gnssMenu.subItems.Add(new SubMenuItem { title = "TEQC GNSS Data QC", isEnabled = false });
        menuStructure.Add(gnssMenu);

        // IMU
        var imuMenu = new MenuItem { title = "IMU" };
        imuMenu.subItems.Add(new SubMenuItem { title = "File Bin to Log", isEnabled = true });
        imuMenu.subItems.Add(new SubMenuItem { title = "File Log to AHRS", isEnabled = true });
        menuStructure.Add(imuMenu);

        // Images
        var imagesMenu = new MenuItem { title = "Images" };
        imagesMenu.subItems.Add(new SubMenuItem { title = "Geotagging Images", isEnabled = true });
        imagesMenu.subItems.Add(new SubMenuItem { title = "PPK + Geotagging", isEnabled = true });
        imagesMenu.subItems.Add(new SubMenuItem { title = "Images Exif Viewer", isEnabled = true });
        imagesMenu.subItems.Add(new SubMenuItem { title = "Images Calibration", isEnabled = false });
        menuStructure.Add(imagesMenu);

        // Orthomosaic
        var orthoMenu = new MenuItem { title = "Orthomosaic" };
        orthoMenu.subItems.Add(new SubMenuItem { title = "Build Orthomosaic", isEnabled = false });
        orthoMenu.subItems.Add(new SubMenuItem { title = "Ortho Calibration", isEnabled = false });
        orthoMenu.subItems.Add(new SubMenuItem { title = "Tile Builder", isEnabled = false });
        orthoMenu.subItems.Add(new SubMenuItem { title = "Raster Calculator", isEnabled = true });
        orthoMenu.subItems.Add(new SubMenuItem { title = "Raster Classification", isEnabled = true });
        menuStructure.Add(orthoMenu);

        // Vector
        var vectorMenu = new MenuItem { title = "Vector" };
        vectorMenu.subItems.Add(new SubMenuItem { title = "Editor File Pos", isEnabled = false });
        vectorMenu.subItems.Add(new SubMenuItem { title = "Pos To Shapefile", isEnabled = true });
        vectorMenu.subItems.Add(new SubMenuItem { title = "Pos to Geojson", isEnabled = true });
        vectorMenu.subItems.Add(new SubMenuItem { title = "Pos to KML", isEnabled = true });
        vectorMenu.subItems.Add(new SubMenuItem { title = "DEM to Contour", isEnabled = true });
        vectorMenu.subItems.Add(new SubMenuItem { title = "Vector to Editor", isEnabled = false });
        menuStructure.Add(vectorMenu);

        // GeoLiDAR
        var lidarMenu = new MenuItem { title = "GeoLiDAR" };
        lidarMenu.subItems.Add(new SubMenuItem { title = "3D Trajectory Viewer", isEnabled = false });
        lidarMenu.subItems.Add(new SubMenuItem { title = "Trajectory Builder", isEnabled = false });
        lidarMenu.subItems.Add(new SubMenuItem { title = "Pointcloud Generator", isEnabled = false });
        lidarMenu.subItems.Add(new SubMenuItem { title = "TLS Pointcloud Generator", isEnabled = true });
        lidarMenu.subItems.Add(new SubMenuItem { title = "Pointcloud Classification", isEnabled = false });
        lidarMenu.subItems.Add(new SubMenuItem { title = "Pointcloud to Mesh", isEnabled = false });
        lidarMenu.subItems.Add(new SubMenuItem { title = "Mesh To DEM", isEnabled = false });
        menuStructure.Add(lidarMenu);

        // Landcam
        var landcamMenu = new MenuItem { title = "Landcam" };
        landcamMenu.subItems.Add(new SubMenuItem { title = "NDVI Generator", isEnabled = true });
        landcamMenu.subItems.Add(new SubMenuItem { title = "Image Viewer", isEnabled = false });
        menuStructure.Add(landcamMenu);

        // Blue Marine
        var marineMenu = new MenuItem { title = "Blue Marine" };
        marineMenu.subItems.Add(new SubMenuItem { title = "BMI Files Converter", isEnabled = true });
        marineMenu.subItems.Add(new SubMenuItem { title = "TIme Offsite GNS", isEnabled = false });
        marineMenu.subItems.Add(new SubMenuItem { title = "Combine BMI and PPK", isEnabled = true });
        menuStructure.Add(marineMenu);

        // Tools
        var toolsMenu = new MenuItem { title = "Tools" };
        toolsMenu.subItems.Add(new SubMenuItem { title = "Coordinat Converter", isEnabled = true });
        toolsMenu.subItems.Add(new SubMenuItem { title = "GNSS PPK Advanced Setting", isEnabled = true });
        menuStructure.Add(toolsMenu);
    }

    void BuildNavbar()
    {
        // 1. Create Main Navbar Panel
        navbarPanel = new GameObject("NavbarPanel");
        navbarPanel.transform.SetParent(this.transform, false);
        
        // Ensure Layer is UI
        SetLayerRecursively(navbarPanel, LayerMask.NameToLayer("UI"));

        RectTransform rt = navbarPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(0, navbarHeight);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        Image bg = navbarPanel.AddComponent<Image>();
        bg.sprite = GetDefaultSprite();
        bg.type = Image.Type.Sliced;
        bg.color = navbarBackgroundColor;

        HorizontalLayoutGroup hlg = navbarPanel.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(10, 10, 0, 0);
        hlg.spacing = 5;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // 2. Create Items
        foreach (var item in menuStructure)
        {
            CreateNavbarItem(item, navbarPanel.transform);
        }
    }

    void CreateNavbarItem(MenuItem item, Transform parent)
    {
        GameObject itemObj = new GameObject(item.title);
        itemObj.transform.SetParent(parent, false);

        // Background Image
        Image img = itemObj.AddComponent<Image>();
        img.sprite = GetDefaultSprite();
        img.type = Image.Type.Sliced;
        img.color = itemNormalColor;

        Button btn = itemObj.AddComponent<Button>();
        btn.targetGraphic = img;

        LayoutElement le = itemObj.AddComponent<LayoutElement>();
        le.minWidth = 80;
        le.preferredWidth = 100;
        le.flexibleWidth = 0;

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(itemObj.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = item.title;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = fontSize;
        tmp.color = textNormalColor;
        if (fontAsset != null) tmp.font = fontAsset;

        // Adjust width based on text
        tmp.ForceMeshUpdate();
        le.preferredWidth = tmp.preferredWidth + 30; // Padding

        // Add Hover Component
        NavbarHoverHandler hover = itemObj.AddComponent<NavbarHoverHandler>();
        hover.Setup(this, item, img, tmp);
    }

    public void ShowDropdown(MenuItem item, Transform parentBtnTransform)
    {
        CancelCloseDropdown();
        
        // If already open for this item, do nothing
        if (currentOpenDropdown != null && currentOpenDropdown.name == "Dropdown_" + item.title)
            return;

        CloseDropdown(); // Close others

        if (item.subItems.Count == 0) return;

        GameObject dropdownObj = new GameObject("Dropdown_" + item.title);
        dropdownObj.transform.SetParent(this.transform, false);

        RectTransform rt = dropdownObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        // Remove manual height calculation, let ContentSizeFitter handle it
        rt.sizeDelta = new Vector2(dropdownWidth, 0); 
        
        // Position
        RectTransform btnRT = parentBtnTransform as RectTransform;
        Vector3[] corners = new Vector3[4];
        btnRT.GetWorldCorners(corners);
        dropdownObj.transform.position = corners[0]; // Bottom Left corner of button

        // Add Content Size Fitter
        ContentSizeFitter csf = dropdownObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Background Image
        Image img = dropdownObj.AddComponent<Image>();
        img.sprite = GetDefaultSprite();
        img.type = Image.Type.Sliced;
        img.color = dropdownBackgroundColor;

        // Dropdown Item Layout
        VerticalLayoutGroup vlg = dropdownObj.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true; // Let children control their height
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.spacing = 0;

        // Add Hover Handler to Dropdown itself
        DropdownHoverHandler dropdownHover = dropdownObj.AddComponent<DropdownHoverHandler>();
        dropdownHover.Setup(this);

        // Create Sub Items
        foreach (var sub in item.subItems)
        {
            CreateDropdownItem(sub, dropdownObj.transform);
        }

        currentOpenDropdown = dropdownObj;
    }

    void CreateDropdownItem(SubMenuItem sub, Transform parent)
    {
        GameObject itemObj = new GameObject(sub.title);
        itemObj.transform.SetParent(parent, false);

        LayoutElement le = itemObj.AddComponent<LayoutElement>();
        le.minHeight = dropdownItemHeight;
        le.preferredHeight = dropdownItemHeight;

        Image img = itemObj.AddComponent<Image>();
        img.color = Color.clear;

        Button btn = itemObj.AddComponent<Button>();
        btn.targetGraphic = img;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(itemObj.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10, 0);
        textRT.offsetMax = new Vector2(-10, 0);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = sub.title;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.fontSize = fontSize;
        tmp.color = sub.isEnabled ? textNormalColor : textDisabledColor;
        if (fontAsset != null) tmp.font = fontAsset;

        // Hover effect for dropdown item
        EventTrigger trigger = itemObj.AddComponent<EventTrigger>();
        
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerEnter;
        entry.callback.AddListener((data) => {
            CancelCloseDropdown(); // Also cancel close when hovering items
            
            // Hover effect for background always
            img.color = itemHoverColor;

            if (sub.isEnabled) {
                tmp.color = textHoverColor;
            } else {
                // Disabled items keep their disabled color, or maybe slightly brighter if needed
                // But user said "font color is thinner", implying the disabled look.
                // We leave it as textDisabledColor.
            }
        });
        trigger.triggers.Add(entry);

        EventTrigger.Entry exit = new EventTrigger.Entry();
        exit.eventID = EventTriggerType.PointerExit;
        exit.callback.AddListener((data) => {
            img.color = Color.clear;
            tmp.color = sub.isEnabled ? textNormalColor : textDisabledColor;
        });
        trigger.triggers.Add(exit);

        btn.onClick.AddListener(() => {
            if (sub.isEnabled)
            {
                Debug.Log($"Clicked: {sub.title}");
                sub.onClickAction?.Invoke();
                CloseDropdown();
                HandlePopup(sub.title);
            }
        });
    }

    public void StartCloseDropdownTimer()
    {
        if (closeDropdownCoroutine != null) StopCoroutine(closeDropdownCoroutine);
        closeDropdownCoroutine = StartCoroutine(CloseDropdownDelayed());
    }

    public void CancelCloseDropdown()
    {
        if (closeDropdownCoroutine != null) StopCoroutine(closeDropdownCoroutine);
    }

    IEnumerator CloseDropdownDelayed()
    {
        yield return new WaitForSeconds(0.2f); // Short delay
        CloseDropdown();
    }

    public void CloseDropdown()
    {
        if (currentOpenDropdown != null)
        {
            if (Application.isPlaying) Destroy(currentOpenDropdown);
            else DestroyImmediate(currentOpenDropdown);

            currentOpenDropdown = null;
        }
    }

    void HandlePopup(string title)
    {
        Debug.Log("Opening Popup for " + title);
        // Here you would instantiate your Popup Prefab
        // Example: PopupFactory.Create(title);
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}

public class NavbarHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private NavbarManager manager;
    private NavbarManager.MenuItem item;
    private Image bgImage;
    private TextMeshProUGUI text;

    public void Setup(NavbarManager manager, NavbarManager.MenuItem item, Image bgImage, TextMeshProUGUI text)
    {
        this.manager = manager;
        this.item = item;
        this.bgImage = bgImage;
        this.text = text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager == null) return;
        manager.CancelCloseDropdown();
        bgImage.color = manager.itemHoverColor;
        text.color = manager.textHoverColor;
        manager.ShowDropdown(item, transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager == null) return;
        bgImage.color = manager.itemNormalColor;
        text.color = manager.textNormalColor;
        manager.StartCloseDropdownTimer();
    }
}

public class DropdownHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private NavbarManager manager;

    public void Setup(NavbarManager manager)
    {
        this.manager = manager;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager == null) return;
        manager.CancelCloseDropdown();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager == null) return;
        manager.StartCloseDropdownTimer();
    }
}
