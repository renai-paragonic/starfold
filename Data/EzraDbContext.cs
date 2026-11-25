using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Data;

// Ezra shares its SQLite file with Paraclete. Every Ezra table is prefixed
// "ezra_", and EF's migration history lives in "ezra_ef_migrations" (set in
// Program.cs). EF never touches PRAGMA user_version, which Paraclete owns.
public class EzraDbContext(DbContextOptions<EzraDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteRef> NoteRefs => Set<NoteRef>();
    public DbSet<StrongsEntry> Strongs => Set<StrongsEntry>();
    public DbSet<HitCounter> HitCounters => Set<HitCounter>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Note>(e =>
        {
            e.ToTable("ezra_notes");
            e.HasIndex(n => n.LogosId).IsUnique();
        });

        b.Entity<Tag>(e =>
        {
            e.ToTable("ezra_tags");
            e.Property(t => t.Name).UseCollation("NOCASE");
            e.HasIndex(t => t.Name).IsUnique();
        });

        b.Entity<NoteRef>(e =>
        {
            e.ToTable("ezra_note_refs");
            e.HasOne(r => r.Note).WithMany(n => n.Refs).HasForeignKey(r => r.NoteId);
            e.HasIndex(r => new { r.Book, r.Chapter, r.VerseStart });
        });

        b.Entity<StrongsEntry>(e =>
        {
            e.ToTable("ezra_strongs");
            e.Property(s => s.Id).ValueGeneratedNever();
        });

        b.Entity<HitCounter>(e =>
        {
            e.ToTable("ezra_hit_counter");
            e.Property(h => h.Id).ValueGeneratedNever();
            e.HasData(new HitCounter { Id = 1, Count = 0 });
        });

        b.Entity<Note>()
            .HasMany(n => n.Tags).WithMany(t => t.Notes)
            .UsingEntity("ezra_note_tags",
                r => r.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId"),
                l => l.HasOne(typeof(Note)).WithMany().HasForeignKey("NoteId"),
                j => j.HasKey("NoteId", "TagId"));

        b.Entity<Note>()
            .HasMany(n => n.Strongs).WithMany(s => s.Notes)
            .UsingEntity("ezra_note_strongs",
                r => r.HasOne(typeof(StrongsEntry)).WithMany().HasForeignKey("StrongsId"),
                l => l.HasOne(typeof(Note)).WithMany().HasForeignKey("NoteId"),
                j => j.HasKey("NoteId", "StrongsId"));

        // snake_case column names, e.g. BodyMd -> body_md.
        foreach (var entity in b.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
                prop.SetColumnName(Regex.Replace(prop.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
