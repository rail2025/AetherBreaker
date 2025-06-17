using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AetherBreaker.UI;

public class TextureManager : IDisposable
{
    private readonly Dictionary<string, IDalamudTextureWrap> bubbleTextures = new();
    private readonly List<IDalamudTextureWrap> backgroundTextures = new();

    public TextureManager()
    {
        LoadBubbleTextures();
        LoadBackgroundTextures();
    }

    private void LoadBubbleTextures()
    {
        // Add "bomb" to the list of textures to load
        var bubbleNames = new[] { "dps", "healer", "tank", "chocobo", "bomb" };
        foreach (var name in bubbleNames)
        {
            var texture = LoadTextureFromResource($"AetherBreaker.Images.{name}.png");
            if (texture != null)
            {
                this.bubbleTextures[name] = texture;
            }
        }
    }

    private void LoadBackgroundTextures()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourcePathPrefix = "AetherBreaker.Images.";
        var backgroundResourceNames = assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(resourcePathPrefix + "background") && r.EndsWith(".png"))
            .OrderBy(r => r)
            .ToList();

        foreach (var resourcePath in backgroundResourceNames)
        {
            var texture = LoadTextureFromResource(resourcePath);
            if (texture != null)
            {
                this.backgroundTextures.Add(texture);
            }
        }
    }

    private static IDalamudTextureWrap? LoadTextureFromResource(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        try
        {
            using var stream = assembly.GetManifestResourceStream(path);
            if (stream == null)
            {
                Plugin.Log.Warning($"Texture resource not found at path: {path}");
                return null;
            }

            using var image = Image.Load<Rgba32>(stream);
            var rgbaBytes = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgbaBytes);
            return Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Rgba32(image.Width, image.Height), rgbaBytes);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Failed to load texture: {path}");
            return null;
        }
    }

    public IDalamudTextureWrap? GetBubbleTexture(int bubbleType)
    {
        return bubbleType switch
        {
            0 => this.bubbleTextures.GetValueOrDefault("dps"),    // Red
            1 => this.bubbleTextures.GetValueOrDefault("healer"), // Green
            2 => this.bubbleTextures.GetValueOrDefault("tank"),   // Blue
            3 => this.bubbleTextures.GetValueOrDefault("chocobo"),// Yellow
            -3 => this.bubbleTextures.GetValueOrDefault("bomb"),  // New Bomb Type
            _ => null
        };
    }

    public IDalamudTextureWrap? GetBackground(int index)
    {
        if (this.backgroundTextures.Count == 0) return null;
        return this.backgroundTextures[index % this.backgroundTextures.Count];
    }

    public int GetBackgroundCount() => this.backgroundTextures.Count;

    public void Dispose()
    {
        foreach (var texture in this.bubbleTextures.Values) texture.Dispose();
        this.bubbleTextures.Clear();
        foreach (var texture in this.backgroundTextures) texture.Dispose();
        this.backgroundTextures.Clear();
    }
}
