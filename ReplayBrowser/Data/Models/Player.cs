using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReplayBrowser.Models.Ingested;

namespace ReplayBrowser.Data.Models;

public class Player : IEntityTypeConfiguration<Player>
{
    public int Id { get; set; }

    public List<string> AntagPrototypes { get; set; } = null!;
    public List<string> JobPrototypes { get; set; } = null!;
    public required string PlayerIcName { get; set; }
    public bool Antag { get; set; }

    public ReplayParticipant Participant { get; set; } = null!;
    public int ParticipantId { get; set; }

    public JobDepartment? EffectiveJob { get; set; }
    public int? EffectiveJobId { get; set; }

    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.HasIndex(p => p.PlayerIcName);
        builder.HasIndex(p => p.ParticipantId);
    }

    public static Player FromYaml(YamlPlayer player)
    {
        return new Player {
            PlayerIcName = player.PlayerIcName,

            JobPrototypes = player.JobPrototypes,
            AntagPrototypes = player.AntagPrototypes,

            Antag = player.Antag
        };
    }

    public void RedactInformation(bool wasGdpr = false)
    {
        if (wasGdpr)
        {
            PlayerIcName = "Removed by GDPR request";
        }
        else
        {
            PlayerIcName = "Redacted";
        }
    }
}