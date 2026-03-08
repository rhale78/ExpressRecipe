namespace ExpressRecipe.SafeForkService.Contracts.Responses;

public class ConflictReport
{
    public bool HasConflicts { get; set; }
    public bool HasAnaphylacticRisk { get; set; }
    public List<ConflictItem> Conflicts { get; set; } = new();
}
