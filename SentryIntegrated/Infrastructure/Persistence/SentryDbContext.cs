using Microsoft.EntityFrameworkCore;

namespace SentryIntegrated.Infrastructure.Persistence;

public sealed class DeviceLog
{
    public long Id { get; set; }
    public string AccessNumber { get; set; } = "";
    public int DeviceId { get; set; }
    public string VerifyMode { get; set; } = "";
    public string EventName { get; set; } = "";
    public string EventAddress { get; set; } = "";
    public DateTimeOffset TimeLogStamp { get; set; }
}
public sealed class Device { public int Id { get; set; } public string Name { get; set; } = ""; }
public sealed class ProcessingWatermark { public string Worker { get; set; } = ""; public long LastId { get; set; } }
public sealed class SmsDelivery { public long EventId { get; set; } public int Attempts { get; set; } public bool Delivered { get; set; } public DateTimeOffset UpdatedAt { get; set; } }

public sealed class SentryDbContext(DbContextOptions<SentryDbContext> options) : DbContext(options)
{
    public DbSet<DeviceLog> DeviceLogs => Set<DeviceLog>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ProcessingWatermark> ProcessingWatermarks => Set<ProcessingWatermark>();
    public DbSet<SmsDelivery> SmsDeliveries => Set<SmsDelivery>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<DeviceLog>(e => { e.ToTable("DeviceLogs"); e.HasKey(x => x.Id); e.Property(x => x.AccessNumber).HasMaxLength(30); e.HasIndex(x => new { x.TimeLogStamp, x.Id }); });
        b.Entity<Device>(e => { e.ToTable("Devices"); e.HasKey(x => x.Id); });
        b.Entity<ProcessingWatermark>(e => { e.ToTable("SentryProcessingWatermarks"); e.HasKey(x => x.Worker); });
        b.Entity<SmsDelivery>(e => { e.ToTable("SentrySmsDeliveries"); e.HasKey(x => x.EventId); });
    }
}

public sealed class StaffRecord { public string AccessNumber { get; set; } = ""; public string FirstName { get; set; } = ""; public string LastName { get; set; } = ""; public string? PhotoName { get; set; } public string? PhoneNumber { get; set; } }
public sealed class StaffDbContext(DbContextOptions<StaffDbContext> options) : DbContext(options)
{
    public DbSet<StaffRecord> Staff => Set<StaffRecord>();
    protected override void OnModelCreating(ModelBuilder b) => b.Entity<StaffRecord>(e => { e.ToTable("Employees"); e.HasKey(x => x.AccessNumber); });
}
public sealed class StudentRecord { public string AccessNumber { get; set; } = ""; public string FirstName { get; set; } = ""; public string LastName { get; set; } = ""; public string? PhotoName { get; set; } public string? PhoneNumber { get; set; } }
public sealed class StudentDbContext(DbContextOptions<StudentDbContext> options) : DbContext(options)
{
    public DbSet<StudentRecord> Students => Set<StudentRecord>();
    protected override void OnModelCreating(ModelBuilder b) => b.Entity<StudentRecord>(e => { e.ToTable("Students"); e.HasKey(x => x.AccessNumber); });
}
