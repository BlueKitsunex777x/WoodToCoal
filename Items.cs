
using System;
using BlockStoryCore;
using UnityEngine;

internal class Items()
{
    private static void CreateItem(
        string name,
        string clone,
        bool asMultiplier,
        Texture2D texture,
        int durability = 0,
        string spriteName = null,
        Func<GameObject> makeModel = null,
        float width = 1.04f,
        bool inHand = true,
        bool onGround = true,
        float scale = 1.0f
        )
    {
        InvBaseItem item = BSItems.RegisterClone(clone, name, spriteName);
        if (item == null)
            L.Warn($"{name} item Failed");
        else if (durability > 0)
        {
            InvBaseItem _item = BSItems.Find(name);
            if (_item != null)
                _ = asMultiplier ? item.durability = _item.durability * durability : item.durability = durability;
        }
        BSItems.SetIcon(name, texture, spriteName);
        BSItemLook.Register(name, makeModel, width, inHand, onGround);
        if (scale != 1.0f) BSItemLook.RemoteSize(name, scale);
    }
}