using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TeamFinder.Api.Data;

#nullable disable

namespace TeamFinder.Api.Migrations;

[DbContext(typeof(TeamFinderDbContext))]
public sealed class TeamFinderDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        InitialTeamFinderModel.ConfigureModel(modelBuilder);
    }
}
