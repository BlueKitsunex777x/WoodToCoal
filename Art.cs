using System.IO;
using BlockStoryCore;
using UnityEngine;

internal static class Art
{
    public static Texture2D Icon(string name)
    {
        using Stream stream = typeof(Art).Assembly.GetManifestResourceStream($"icons/{name}.png");
        if (stream == null) { L.Warn($"no icon {name}"); return null; }

        byte[] array = new byte[stream.Length];
        stream.Read(array, 0, array.Length);
        return BSModel.TextureFromPng(array);
    }
}