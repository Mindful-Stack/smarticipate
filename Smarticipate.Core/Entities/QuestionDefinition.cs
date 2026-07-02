using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Smarticipate.Core.Entities;

// Reusable content: the type, the prompt, options, and per-type scalar config.
// Authored once, fired many times. Owned by a teacher, or system-seeded when OwnerUserId is null.
public class QuestionDefinition
{
    public int Id { get; set; }
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = string.Empty;

    // Set when saved to the toolbox; null while ad hoc.
    public string? Name { get; set; }
    public bool IsSaved { get; set; }

    // FK to Identity user; null means a system-seeded definition shown to every teacher.
    public string? OwnerUserId { get; set; }

    // Per-type scalar config as jsonb. Access the typed view through Config.
    public string ConfigJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<QuestionOption> Options { get; set; } = [];
    public List<QuestionActivation> Activations { get; set; } = [];

    // NotMapped typed accessor over ConfigJson, mirroring the existing Response.ResponseType pattern.
    [NotMapped]
    public QuestionConfigView Config
    {
        get => string.IsNullOrWhiteSpace(ConfigJson)
            ? new QuestionConfigView()
            : JsonSerializer.Deserialize<QuestionConfigView>(ConfigJson) ?? new QuestionConfigView();
        set => ConfigJson = JsonSerializer.Serialize(value);
    }
}
