using BepInEx;
using BlockStoryCore;

// Can smelt logs into coal
[BepInPlugin("com.yourname.blockstory.woodtocoal", "Block Story - Woodtocoal", "1.0.0")]
[BepInDependency(Core.Guid)]               // hard dependency on the toolbox; won't load without it
public class WoodtocoalPlugin : BaseUnityPlugin
{
    private void Awake()
    {
        if (!BSMKCheck.Passed(this)) return;   // the BSMK check, keep it first. see BSMKCheck.cs
        ModRegistry.Register(new ModInfo
        {
            Name = "Woodtocoal",
            Description = "Can smelt logs into coal",
            GetEnabled = () => true,
            SetEnabled = _ => { },
            HasConfig = false,
        });
        Core.Log?.LogInfo("Woodtocoal loaded.");
    }
}