namespace Smarticipate.Core.Entities;

// One live firing of a definition inside one session. Firing order is derived from Id
// (monotonic, indexed, clock-skew-proof); there is deliberately no Sequence field.
public class QuestionActivation
{
    public int Id { get; set; }

    public int DefinitionId { get; set; }
    public QuestionDefinition Definition { get; set; } = null!;

    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }

    // Timer used; defaults from the definition config, teacher can override at fire time.
    public int? DurationSeconds { get; set; }

    public List<Response> Responses { get; set; } = [];
}
