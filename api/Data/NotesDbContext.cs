using Microsoft.EntityFrameworkCore;

namespace NotesApi.Data;

public class NotesDbContext(DbContextOptions options) : DbContext(options)
{
    public const int TitleMaxLength = 200;

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var note = modelBuilder.Entity<Note>();
        note.Property(n => n.Title).HasMaxLength(TitleMaxLength).IsRequired();
        note.Property(n => n.PhotoBlobName).HasMaxLength(300);
        note.HasIndex(n => n.CreatedAt);

        // Baza przechowuje czas w UTC; przy odczycie oznaczamy go jako UTC, żeby JSON miał sufiks "Z".
        note.Property(n => n.CreatedAt)
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}

// Osobne klasy na provider, bo każdy provider ma własny zestaw migracji
// (SQLite i SQL Server generują inny SQL). Aplikacja używa zawsze typu NotesDbContext.
public class SqliteNotesDbContext(DbContextOptions<SqliteNotesDbContext> options) : NotesDbContext(options);

public class SqlServerNotesDbContext(DbContextOptions<SqlServerNotesDbContext> options) : NotesDbContext(options);
