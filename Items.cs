
using System;
using BlockStoryCore;
using UnityEngine;

public class Items()
{
    public static void CreateItem(
        string name,
        string clone,
        Texture2D texture,
        int durability = 0,
        bool durabilityAsMultiplier = false,
        string spriteName = null,
        Func<GameObject> makeModel = null,
        float width = 1.04f,
        bool inHand = false,
        bool onGround = true,
        float scale = 1.0f
        )
    {
        InvBaseItem item = BSItems.RegisterClone(clone, name, spriteName);
        L.Log($"name: {item.name}, category: {item.category}, attachment: {item.attachment}, extraOptions: {item.extraOptions}, iconName: {item.iconName}, options: {item.options}");

        if (item == null)
            L.Warn($"{name} item Failed");
        else if (durability > 0)
        {
            InvBaseItem _item = BSItems.Find(name);
            if (_item != null)
                _ = durabilityAsMultiplier ? item.durability = _item.durability * durability : item.durability = durability;
            L.Log($"item durability has changed: {_item.durability} -> {item.durability}");
        }
        BSItems.SetIcon(name, texture, spriteName);
        BSItemLook.Register(name, makeModel, width, inHand, onGround);
        if (scale != 1.0f) BSItemLook.RemoteSize(name, scale);
    }
}