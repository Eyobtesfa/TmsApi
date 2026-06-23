using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(150);


        builder.HasIndex(s => s.RegistrationNumber)
            .IsUnique();

        builder.Property(s => s.GPA)
            .HasPrecision(3, 2);
        builder.Property(s => s.Version)
            .IsRowVersion();
        builder.Property<DateTime>("Last Updated");
        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}