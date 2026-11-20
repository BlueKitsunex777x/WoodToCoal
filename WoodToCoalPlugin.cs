using System;
using System.Collections.Generic;
using BepInEx;
using BlockStoryCore;
using HarmonyLib;
using UnityEngine;

// Can smelt logs into coal
[BepInPlugin("com.BlueKitsunex777x.woodtocoal", "Wood To Coal", "1.0.0")]
[BepInDependency(Core.Guid)]               // hard dependency on the toolbox; won't load without it
public class WoodToCoalPlugin : BaseUnityPlugin
{
    public static bool Enabled = PlayerPrefs.GetInt("WoodToCoal_Enabled", 1) != 0;
    public static int CoalOutput = PlayerPrefs.GetInt("WoodToCoal_CoalOutput", 4);

    public static bool _savedEnabled = Enabled;
    public static int _savedCoalOutput = CoalOutput;
    private bool _wasInWorld;
    private float _next;
    
    private void Awake()
    {
        if (!BSMKCheck.Passed(this)) return;   // the BSMK check, keep it first. see BSMKCheck.cs

        try { new Harmony("com.BlueKitsunex777x.woodtocoal").PatchAll(); L.Info("loaded"); }
        catch (Exception e) { L.Err("PatchAll", e); }
        L.Log("patches in: AnyLogPatch (RecipeMatcher.search), DoubleSmeltFix (CraftingOutputSlotSpeedUp.OnComplete)");

        BSItems.WhenReady(() => Items.CreateItem("Charcoal", "Coal Ore", Art.Icon("charcoal")));
        
        ModRegistry.Register(new ModInfo
        {
            Name = "Wood To Coal",
            Description = "Can smelt logs into coal",
            GetEnabled = () => Enabled,
            SetEnabled = on =>
            {
                _savedEnabled = on;
                PlayerPrefs.SetInt("WoodToCoal_Enabled", on ? 1 : 0);
                PlayerPrefs.Save();
                ApplySaved();
                L.Info($"enabled = {on}");
            },
            Config = new ModConfig()
                .Stepper("Coal Output", 2, 10, () => _savedCoalOutput, SetCoalOutput, 1, "Amount of coal you get from smelting wood in the furnace")
                .Button("Creative worlds", "Give Charcoal", Give, "Gives 100 Charcoal")
        });
        L.Log("added to the mods menu, now waiting for a world to load");
    }

    // Run on game update cycle, checking if player has just left a world
    private void Update()
    {
        // leaving a world is when saved changes can take over, the next one loads with them
        bool inWorld = BSWorld.InWorld;
        if (_wasInWorld && !inWorld) { ApplySaved(); }
        _wasInWorld = inWorld;

        // cant do it in Awake, the furnace recipes dont exist till you load a world and every world makes new ones.
        // so just check every 3 sec. unscaledTime so it still runs while paused
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 3f;
        ModRecipes.AddFurnaceRecipe("Wood To Coal", "Bark", "Coal Ore", WoodToCoalPlugin._savedCoalOutput);
    }

    // Saves any edit made to mods config while in-game?
    private static void ApplySaved()
    {
        // if (BSWorld.InWorld)
        // {
        //     L.Info("saved, used from the next world load");
        //     return;
        // }
        Enabled = _savedEnabled;
        CoalOutput = _savedCoalOutput;
    }

    private static void SetCoalOutput(int value)
    {
        _savedCoalOutput = value;
        PlayerPrefs.SetInt("WoodToCoal_CoalOutput", value);
        PlayerPrefs.Save();
        ModRecipes.Refresh("Wood To Coal");
        ApplySaved();
    }

    private static void Give()
    {
        if(!BSWorld.InWorld) { L.Info("Not in a creative world"); return; }

        bool flag = BSItems.Give("Charcoal", 100);
        L.Info($"Give ok? {flag}");
    }

    internal static class ModRecipes
    {
        private static readonly List<Recipe> _modRecipes = [];
        private static RecipeManager _addedTo;
        private static System.Reflection.FieldInfo _matcherFI;

        public static bool IsModRecipe(Recipe _recipe) => _recipe != null && _modRecipes.Contains(_recipe);

        private static RecipeManager FurnaceManager()
        {
            try { RecipeManager manager = AchievementManager.FindRecipeManager(RecipeManager.Category.Furnace); if (manager != null) return manager; }
            catch (Exception e) { L.Log($"Achievement lookup threw ({e.Message}), trying the scene search"); }

            foreach (RecipeManager recipeManager in UnityEngine.Object.FindObjectsByType<RecipeManager>())
            {
                if (recipeManager.recipeList == null) continue;
                foreach (Recipe recipe in recipeManager.recipeList)
                {
                    if (recipe != null && recipe.ingredients != null && recipe.minutes > 0.0)
                        foreach (InvGameItem item in recipe.ingredients)
                        {
                            if (item != null && item.name == "Energy") { L.Log($"found furnace by scene search (creative?) on '{recipeManager.name}'"); return recipeManager; }
                            break;
                        }
                    L.Log($"first recipe was not a furnace recipe");
                    break;
                }
            }
            return null;
        }

        private static Recipe Template(RecipeManager recipeManager)
        {
            Recipe pick = recipeManager.recipeList[0];
            L.Log($"Template: {pick.name}");
            return pick;
        }

        public static void AddFurnaceRecipe(string _recipeName, string _input, string _output, int _count)
        {
            try
            {
                RecipeManager recipeManager = FurnaceManager();
                if (recipeManager == null || recipeManager.recipeList == null || recipeManager.recipeList.Count == 0) return;
                if (recipeManager == _addedTo && recipeManager.recipeList.Contains(_modRecipes.Count > 0 ? _modRecipes[0] : null)) return;

                L.Log(recipeManager == _addedTo ? "mod recipe went missing, adding it again" : $"new furnace manager {recipeManager.name} with {recipeManager.recipeList.Count} recipes");
                Recipe template = Template(recipeManager);
                if (template == null) { L.Warn("no furnace recipe to copy the layout from yet"); return; }
                if (_matcherFI == null) _matcherFI = AccessTools.Field(typeof(RecipeManager), "matcher");
                object matcher = _matcherFI?.GetValue(recipeManager);
                L.Log(matcher != null ? "got the matcher (" + matcher.GetType().Name + ")" : "matcher is null, recipe wont smelt!");

                _modRecipes.Clear();
                InvGameItem input = InvDatabase.CreateItem(_input, 0, 1);
                InvGameItem result = InvDatabase.CreateItem(_output, 0, 1);
                L.Log($"item: input = {input?.name ?? "NULL"}, coal = {result?.name ?? "NULL"}");
                
                if (input == null || input.baseItem == null || result == null) { L.Log($"item name is wrong, giving up"); return; }
                Recipe had = recipeManager.recipeList.Find(recipe => recipe != null && recipe.name == _recipeName);

                if (had != null) { _modRecipes.Add(had); L.Log($"recipe was already in the list, reusing it"); }
                else
                {
                    var grid = (InvGameItem[,])template.ingredients.Clone();
                    for (int x = 0; x < grid.GetLength(0); x++)
                        for (int y = 0; y < grid.GetLength(1); y++)
                            if (grid[x, y] != null && grid[x, y].name != "Energy") { L.Log($"slot {x}, {y}: {grid[x, y].name} -> {input.name}"); grid[x, y] = input; }
                            else if (grid[x, y] != null) L.Log($"slot {x}, {y}: fuel, left alone");
                    
                    result.count = WoodToCoalPlugin._savedCoalOutput;
                    var recipe = new Recipe(grid) { result = result, name = _recipeName, custom = true, minutes = template.minutes, };
                    recipe.result.count = _count;

                    recipeManager.recipeList.Add(recipe);
                    L.Log($"1/3 added to recipeList");
                    if (recipeManager.categorized != null)
                    {
                        var category = result.baseItem.category;
                        if (!recipeManager.categorized.TryGetValue(category, out var list) || list == null) { list = []; recipeManager.categorized[category] = list; }
                        list.Add(recipe);
                        L.Log($"2/3 added to categorized under {category}");
                    }
                    else L.Log("2/3 skipped, no categorized on this manager");
                    var add = matcher?.GetType().GetMethod("Add");
                    add?.Invoke(matcher, [recipe]);
                    L.Log(add != null ? $"3/3 added to the matcher" : $"3/3 Failed, no Add on the matcher");
                    _modRecipes.Add(recipe);
                }
                _addedTo = recipeManager;
                Refresh();
                L.Info($"added the log -> {_recipeName} furnace recipe (copied layout of '{template.name}', {template.minutes} min)");
            }
            catch (Exception e) { L.Err($"AddFurnaceRecipe", e); }
        }

        public static void Refresh(string _recipeName = null)
        {
            L.Log($"refresh: {_modRecipes.Count} recipe(s), hidden = {!WoodToCoalPlugin._savedEnabled}");
            foreach (Recipe recipe in _modRecipes)
            {
                recipe.hidden = !WoodToCoalPlugin._savedEnabled;
                if (recipe.result != null) L.Log($"refresh: input: {recipe.ingredients} output: {recipe.result} count: {recipe.result.count}");
                if (recipe.name == _recipeName) recipe.result.count = WoodToCoalPlugin._savedCoalOutput;
            }
        }
    }
}