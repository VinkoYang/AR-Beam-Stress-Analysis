using System.IO;
using ARBeam;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the shared UI assets in Assets/ARBeam/UI: static TMP font assets for Inter Regular / Medium / SemiBold /
/// Bold, a 9-sliced rounded-rectangle sprite, a circle sprite, and the UiTheme asset that references them.
/// Re-runnable. Existing font assets are kept (scenes reference them by GUID) unless rebuildFonts is true, which
/// replaces them and breaks those references until BeamUiStyler is run again.
///
/// The font atlases are static (pre-filled with <see cref="Characters"/>) so Play mode never rewrites them; add
/// characters here and re-run if a label needs a glyph that is missing.
/// </summary>
public static class UiThemeBuilder
{
    const string Root = "Assets/ARBeam/UI";
    const string FontDir = Root + "/Fonts";
    public const string ThemePath = Root + "/UiTheme.asset";

    /// <summary>Printable ASCII plus the symbols the beam UIs use (Greek, units, arrows, typographic dashes).</summary>
    public static readonly string Characters = BuildCharacters();

    static string BuildCharacters()
    {
        var sb = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) sb.Append(c);
        sb.Append("°±²³⁴·×÷−–—…‹›«»←↑→↓↔✓•≈≤≥∞ΔΣΩδθσεμτπφ’‘“”€");
        return sb.ToString();
    }

    [MenuItem("ARBeam/UI/Build Theme Assets")]
    static void BuildMenu() => Build();

    public static string Build(bool rebuildFonts = false)
    {
        Directory.CreateDirectory(Root);
        var log = new System.Text.StringBuilder();

        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.regular = BuildFont("Inter-Regular", rebuildFonts, log);
        theme.medium = BuildFont("Inter-Medium", rebuildFonts, log);
        theme.semiBold = BuildFont("Inter-SemiBold", rebuildFonts, log);
        theme.bold = BuildFont("Inter-Bold", rebuildFonts, log);

        // <b> and <font-weight> on the regular face use the real Inter weights (table index = weight/100 − 1).
        theme.regular.fontWeightTable[4].regularTypeface = theme.medium;
        theme.regular.fontWeightTable[5].regularTypeface = theme.semiBold;
        theme.regular.fontWeightTable[6].regularTypeface = theme.bold;
        EditorUtility.SetDirty(theme.regular);

        theme.roundedRect = BuildSprite("RoundedRect", 64, 16, new Vector4(20, 20, 20, 20));
        theme.circle = BuildSprite("Circle", 64, 32, Vector4.zero);

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        log.Append($"Theme: {ThemePath}");
        Debug.Log("[UiThemeBuilder]\n" + log);
        return log.ToString();
    }

    public static UiTheme LoadOrBuild()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        if (theme == null || theme.regular == null || theme.roundedRect == null)
        {
            Build();
            theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        }

        return theme;
    }

    static TMP_FontAsset BuildFont(string name, bool rebuild, System.Text.StringBuilder log)
    {
        string ttfPath = $"{FontDir}/{name}.ttf";
        string assetPath = $"{FontDir}/{name} SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null) throw new FileNotFoundException("Font not found", ttfPath);

        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null && !rebuild)
        {
            log.Append($"{assetPath}: kept").Append('\n');
            return existing;
        }

        if (existing != null) AssetDatabase.DeleteAsset(assetPath);

        var asset = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, false);
        asset.name = name + " SDF";
        asset.TryAddCharacters(Characters, out string missing);
        asset.atlasPopulationMode = AtlasPopulationMode.Static;

        AssetDatabase.CreateAsset(asset, assetPath);
        asset.atlasTextures[0].name = name + " Atlas";
        asset.material.name = name + " Material";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        log.Append($"{assetPath}: {asset.characterTable.Count} glyphs");
        if (!string.IsNullOrEmpty(missing)) log.Append($", missing '{missing}'");
        log.Append('\n');
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    }

    /// <summary>White anti-aliased rounded square (radius = size/2 gives a circle), saved as a sprite.</summary>
    static Sprite BuildSprite(string name, int size, float radius, Vector4 border)
    {
        string path = $"{Root}/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded square inset by half a pixel; one pixel of anti-aliasing.
                float px = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius);
                float py = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius);
                float outside = new Vector2(Mathf.Max(px, 0), Mathf.Max(py, 0)).magnitude + Mathf.Min(Mathf.Max(px, py), 0);
                float alpha = Mathf.Clamp01(radius - outside + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
