using System.Collections.Generic;

namespace MazeWars.Client.Shared.NetworkModels;

public class StatusEffectUpdate
{
    public string PlayerId { get; set; } = string.Empty;
    public List<ActiveStatusEffect> StatusEffects { get; set; } = new();
}
