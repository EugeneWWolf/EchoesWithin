/// <summary>
/// Куда ведёт зона выхода из данжа (TeleportZone).
/// </summary>
public enum DungeonReturnExitMode
{
    /// <summary>Точка returnSpawnPoint как сейчас.</summary>
    FixedReturnSpawn = 0,
    /// <summary>Случайная точка на поверхности в горизонтальном радиусе от returnSpawnPoint (луч вниз).</summary>
    RandomHorizontalAroundReturnSpawn = 1,
    /// <summary>Случайная точка на полу внутри сгенерированного данжа (игрок остаётся под землёй).</summary>
    RandomFloorInProceduralDungeon = 2
}
