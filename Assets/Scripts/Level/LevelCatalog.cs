using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored level order and pen budgets; scene-local geometry stays in LevelSetup.</summary>
[CreateAssetMenu(menuName = "Egaku/Level Catalog")]
public sealed class LevelCatalog : ScriptableObject
{
    public const string LauncherScene = "AllanLauncher";
    public const string SelectionScene = "RoleSelection";
    public const string FinishScene = "FinishGame";
    [Serializable]
    public sealed class Definition
    {
        public int id;
        public string sceneName;
        public Sprite thumbnail;
        // -1 preserves the existing material's unlimited/disabled budget convention.
        public int wood, cloud, steel, electric;
    }
    public List<Definition> levels = new List<Definition>();
    public static LevelCatalog Load() => Resources.Load<LevelCatalog>("LevelCatalog");
    public Definition Find(int id) => levels.Find(level => level.id == id);
    public Definition FindScene(string name) => levels.Find(level => level.sceneName == name);
    public Definition Next(int id)
    {
        int index = levels.FindIndex(level => level.id == id);
        return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
    }
    public bool IsUnlocked(int id, int unlockedCount)
    {
        // Unlock counts refer to catalog order, not scene BuildIndex or the next level ID.
        int index = levels.FindIndex(level => level.id == id);
        return index >= 0 && index < unlockedCount;
    }
    public int UnlockedAfter(int completedId, int unlockedCount)
    {
        int index = levels.FindIndex(level => level.id == completedId);
        return Mathf.Clamp(Mathf.Max(unlockedCount, index + 2), 0, levels.Count);
    }
}
