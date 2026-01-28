using UnityEngine;
using System.Collections.Generic;

public class GradientManager : MonoBehaviour
{
    public static GradientManager Instance;

    [System.Serializable]
    public class GradientPreset
    {
        public string name;
        public Gradient gradient;
    }

    public List<GradientPreset> presets = new List<GradientPreset>();
    private Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (presets.Count == 0) InitializeDefaults();
    }

    void InitializeDefaults()
    {
        // 1. Grayscale (Default)
        var gray = new Gradient();
        gray.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        presets.Add(new GradientPreset { name = "Grayscale", gradient = gray });

        // 2. Thermal (Blue -> Yellow -> Red)
        var thermal = new Gradient();
        thermal.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(Color.blue, 0f), 
                new GradientColorKey(Color.green, 0.33f), 
                new GradientColorKey(Color.yellow, 0.66f), 
                new GradientColorKey(Color.red, 1f) 
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        presets.Add(new GradientPreset { name = "Thermal", gradient = thermal });

        // 3. NDVI (Red -> Yellow -> Green)
        var ndvi = new Gradient();
        ndvi.SetKeys(
            new GradientColorKey[] { 
                new GradientColorKey(new Color(0.8f, 0, 0), 0f), 
                new GradientColorKey(Color.yellow, 0.5f), 
                new GradientColorKey(new Color(0, 0.6f, 0), 1f) 
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        presets.Add(new GradientPreset { name = "NDVI", gradient = ndvi });
        
        // 4. Viridis-like (Purple -> Blue -> Green -> Yellow)
        var viridis = new Gradient();
        viridis.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.26f, 0.00f, 0.32f), 0.0f),
                new GradientColorKey(new Color(0.28f, 0.35f, 0.54f), 0.33f),
                new GradientColorKey(new Color(0.12f, 0.63f, 0.53f), 0.66f),
                new GradientColorKey(new Color(0.99f, 0.90f, 0.14f), 1.0f)
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        presets.Add(new GradientPreset { name = "Viridis", gradient = viridis });
    }

    public Gradient GetGradient(string name)
    {
        var p = presets.Find(x => x.name == name);
        return p != null ? p.gradient : presets[0].gradient;
    }

    // Helper: Convert Gradient to Texture2D for Shader
    public Texture2D GetGradientTexture(string name, int width = 256)
    {
        if (_textureCache.ContainsKey(name) && _textureCache[name] != null)
            return _textureCache[name];

        var grad = GetGradient(name);
        if (grad == null) return null;

        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        
        for (int i = 0; i < width; i++)
        {
            float t = (float)i / (width - 1);
            tex.SetPixel(i, 0, grad.Evaluate(t));
        }
        tex.Apply();
        
        _textureCache[name] = tex;
        return tex;
    }

    public static Texture2D GradientToTexture(Gradient grad, int width = 256)
    {
        // Static version for UI (non-cached or manual)
        Texture2D tex = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        
        for (int i = 0; i < width; i++)
        {
            float t = (float)i / (width - 1);
            tex.SetPixel(i, 0, grad.Evaluate(t));
        }
        tex.Apply();
        return tex;
    }
}
