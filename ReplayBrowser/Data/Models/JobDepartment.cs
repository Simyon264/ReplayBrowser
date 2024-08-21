using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReplayBrowser.Data.Models;

/// <summary>
/// Maps a given job ID to a department
/// </summary>
public class JobDepartment : IEntityTypeConfiguration<JobDepartment>
{
    public int Id { get; set; }

    public required string Job { get; set; }
    public required string Department { get; set; }

    public void Configure(EntityTypeBuilder<JobDepartment> builder)
    {
        builder.HasIndex(j => j.Job).IsUnique();
    }
}