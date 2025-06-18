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

/// <summary>
/// Manages loading and accessing all image assets for the game.
/// This includes textures for various bubble types and background images.
/// </summary>
public class TextureManager : IDisposable
{
    private readonly Dictionary<string, IDalamudTextureWrap> bubbleTextures = new();
    private readonly List<IDalamudTextureWrap> backgroundTextures = new();

    public TextureManager()
    {
        LoadBubbleTextures();
        LoadBackgroundTextures();
    }

    /// <summary>
    /// Loads all bubble-related textures from embedded resources.
    /// This method identifies all required bubble textures by name and loads them into a dictionary for quick access.
    /// </summary>
    private void LoadBubbleTextures()
    {
        // Defines the set of bubble textures to be loaded.
        // New textures like "star", "paint", "chest", and "mirror" are added to this list
        // to ensure they are loaded at startup.
        var bubbleNames = new[] { "dps", "healer", "tank", "chocobo", "bomb", "star", "paint", "chest", "mirror" };
        foreach (var name in bubbleNames)
        {
            var texture = LoadTextureFromResource($"AetherBreaker.Images.{name}.png");
            if (texture != null)
            {
                this.bubbleTextures[name] = texture;
            }
        }
    }

    /// <summary>
    /// Loads all background textures from embedded resources.
    /// It scans the assembly for resources prefixed with "background" to dynamically load all available stage backgrounds.
    /// </summary>
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

    /// <summary>
    /// A helper method to load a single texture from an embedded resource path.
    /// </summary>
    /// <param name="path">The fully qualified resource path of the image.</param>
    /// <returns>An IDalamudTextureWrap instance of the loaded texture, or null if loading fails.</returns>
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

    /// <summary>
    /// Retrieves the corresponding texture for a given bubble type identifier.
    /// This now maps the integer types defined in GameBoard to their respective texture names.
    /// </summary>
    /// <param name="bubbleType">The integer identifier for the bubble type.</param>
    /// <returns>The texture wrap for the specified bubble type, or null if not found.</returns>
    public IDalamudTextureWrap? GetBubbleTexture(int bubbleType)
    {
        return bubbleType switch
        {
            // Standard colored bubbles
            0 => this.bubbleTextures.GetValueOrDefault("dps"),
            1 => this.bubbleTextures.GetValueOrDefault("healer"),
            2 => this.bubbleTextures.GetValueOrDefault("tank"),
            3 => this.bubbleTextures.GetValueOrDefault("chocobo"),

            // Special bubbles
            -3 => this.bubbleTextures.GetValueOrDefault("bomb"),
            -4 => this.bubbleTextures.GetValueOrDefault("star"),
            -5 => this.bubbleTextures.GetValueOrDefault("paint"),
            -6 => this.bubbleTextures.GetValueOrDefault("mirror"),
            -7 => this.bubbleTextures.GetValueOrDefault("chest"),

            _ => null
        };
    }

    /// <summary>
    /// Retrieves a background texture by its index.
    /// </summary>
    /// <param name="index">The index of the background texture to retrieve.</param>
    /// <returns>The requested background texture, cycling through the available textures if the index is out of bounds.</returns>
    public IDalamudTextureWrap? GetBackground(int index)
    {
        if (this.backgroundTextures.Count == 0) return null;
        return this.backgroundTextures[index % this.backgroundTextures.Count];
    }

    /// <summary>
    /// Gets the total number of loaded background textures.
    /// </summary>
    /// <returns>The count of background textures.</returns>
    public int GetBackgroundCount() => this.backgroundTextures.Count;

    /// <summary>
    /// Disposes all loaded textures to free up resources.
    /// </summary>
    public void Dispose()
    {
        foreach (var texture in this.bubbleTextures.Values) texture.Dispose();
        this.bubbleTextures.Clear();
        foreach (var texture in this.backgroundTextures) texture.Dispose();
        this.backgroundTextures.Clear();
    }
}
