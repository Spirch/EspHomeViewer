using EspHomeLib.Database.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace EspHomeLib.Database;

public sealed class EfContext : DbContext
{
    public DbSet<Error> Error { get; set; }
    public DbSet<Event> Event { get; set; }
    public DbSet<RowEntry> RowEntry { get; set; }

    public EfContext(DbContextOptions<EfContext> options) : base(options)
    { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(x =>
        {
            x.HasKey(p => p.EventId);

            x.Property(p => p.EventId)
             .IsRequired()
             .ValueGeneratedOnAdd();

            x.Property(p => p.Data)
                .IsRequired()
                .HasColumnType("real");

            x.Property(p => p.RowEntryId)
             .IsRequired();

            x.Property(p => p.UnixTime)
             .IsRequired();

            x.Ignore(p => p.SourceId);

            x.HasOne(d => d.EspHomeId)
             .WithMany(dm => dm.Event)
             .HasForeignKey(dkey => dkey.RowEntryId);
        });

        modelBuilder.Entity<RowEntry>(x =>
        {
            x.HasKey(p => p.RowEntryId);

            x.Property(p => p.RowEntryId)
             .IsRequired()
             .ValueGeneratedOnAdd();

            x.Property(p => p.Name)
             .IsRequired();

            x.Property(p => p.FriendlyName)
             .IsRequired();

            x.HasIndex(p => new { p.Name, p.FriendlyName })
             .IsUnique();
        });

        modelBuilder.Entity<Error>(x =>
        {
            x.HasKey(p => p.ErrorId);

            x.Property(p => p.ErrorId)
             .IsRequired()
             .ValueGeneratedOnAdd();

            x.Property(p => p.Exception)
             .IsRequired();

            x.Property(p => p.Message)
             .IsRequired();

            x.Property(p => p.Date)
             .IsRequired();

            x.Property(p => p.IsHandled)
             .IsRequired();
        });
    }

    public static async Task CreateDBIfNotExistAsync(string dbname)
    {
        if (!File.Exists(dbname))
        {
            var optionsBuilder = new DbContextOptionsBuilder<EfContext>().UseSqlite($"Data Source={dbname}");

            using var test = new EfContext(optionsBuilder.Options);

            test.Database.EnsureDeleted();
            test.Database.EnsureCreated();

            await test.Database.OpenConnectionAsync();

            //WAL is needed since read and write at the same time can cause lock database exception
            await test.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL");

            await test.Database.ExecuteSqlRawAsync("CREATE VIEW MinMaxValue as  \r\nSELECT  row.Name \r\n      , row.FriendlyName \r\n      , data.MaxValue \r\n      , data.MinValue \r\n      , row.Unit \r\nFROM [RowEntry] row \r\nINNER join \r\n( \r\n    SELECT  [RowEntryId] \r\n          , max([Data]) MaxValue\r\n          , min([Data]) MinValue\r\n    FROM [Event] \r\n    GROUP BY RowEntryId \r\n) data ON data.[RowEntryId] = row.[RowEntryId] \r\nORDER BY row.Unit, row.FriendlyName");

            await test.Database.ExecuteSqlRawAsync("CREATE VIEW ShowAll as  \r\nSELECT    datetime(data.UnixTime, 'unixepoch', 'localtime') DateTime \r\n         , date(data.UnixTime, 'unixepoch', 'localtime') Date \r\n         , time(data.UnixTime, 'unixepoch', 'localtime') Time \r\n         , data.UnixTime \r\n         , row.Name \r\n         , row.FriendlyName \r\n         , data.Data \r\n         , row.Unit \r\nFROM [RowEntry] row \r\nINNER join [Event] data ON data.[RowEntryId] = row.[RowEntryId] \r\nORDER BY row.FriendlyName, row.Name, data.UnixTime");

            await test.Database.CloseConnectionAsync();
        }
    }
}
