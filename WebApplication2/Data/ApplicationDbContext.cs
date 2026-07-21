// ملف: Data/ApplicationDbContext.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebApplication2.Models;
using WebApplication2.Models.Audit;

using WebApplication2.Models.Request;

namespace WebApplication2.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        { }

        public DbSet<News> News { get; set; }
        public DbSet<Identify> Identifies { get; set; }
        public DbSet<WorkLocation> WorkLocations { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<SiteSettings> SiteSettings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }
        public DbSet<VoterCard> VoterCards { get; set; }
        public DbSet<UnionMembership> UnionMemberships { get; set; }
        public DbSet<FederationMembership> FederationMemberships { get; set; }
        public DbSet<AssociationMembership> AssociationMemberships { get; set; }
        public DbSet<NgoMembership> NgoMemberships { get; set; }
        public DbSet<AffiliationInfo> AffiliationInfos { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<RequestRecipient> RequestRecipients { get; set; }
        public DbSet<RequestReply> RequestReplies { get; set; }
        public DbSet<AffiliationEntity> AffiliationEntities { get; set; }
        public DbSet<Division> Divisions { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Union> Unions { get; set; }
        public DbSet<Federation> Federations { get; set; }
        public DbSet<Association> Associations { get; set; }
        public DbSet<Ngo> Ngos { get; set; }
        public DbSet<ManagementAssignment> ManagementAssignments { get; set; }
        public DbSet<ManagementAssignmentRequest> ManagementAssignmentRequests { get; set; }
        public DbSet<FederationDivision> FederationDivisions { get; set; }
        public DbSet<FederationSection> FederationSections { get; set; }
        public DbSet<FederationGroup> FederationGroups { get; set; }
        public DbSet<CommunicationEvaluation> CommunicationEvaluations { get; set; }
        public DbSet<MediaActivityEvaluation> MediaActivityEvaluations { get; set; }
        public DbSet<MovementActivityEvaluation> MovementActivityEvaluations { get; set; }
        public DbSet<PolarizationEvaluation> PolarizationEvaluations { get; set; }
        public DbSet<SocialMediaEvaluation> SocialMediaEvaluations { get; set; }
        public DbSet<SupervisorOpinionEvaluation> SupervisorOpinionEvaluations { get; set; }

        // جداول الحضور (منفصلة)
        public DbSet<PoliticalForumAttendance> PoliticalForumAttendances { get; set; }
        public DbSet<PeriodicMeetingAttendance> PeriodicMeetingAttendances { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<AuditLogEntry> AuditLogs { get; set; }

        // جدول التقييم السنوي (يبقى كما هو)



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);                 

            // بدلاً من ذلك، يمكن إضافة فهارس (Indexes) لتحسين الأداء
            modelBuilder.Entity<Request>()
                .HasIndex(r => r.SenderId)
                .HasDatabaseName("IX_Requests_SenderId");

           

            modelBuilder.Entity<Request>()
                .HasIndex(r => r.ProcessedById)
                .HasDatabaseName("IX_Requests_ProcessedById");

            // ===== ✅ فهارس للجداول الأخرى =====
            modelBuilder.Entity<Address>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("IX_Addresses_UserId");

            modelBuilder.Entity<WorkLocation>()
                .HasIndex(w => w.IdentifyId)
                .IsUnique()
                .HasDatabaseName("IX_WorkLocations_IdentifyId");

            modelBuilder.Entity<Identify>()
                .HasOne(i => i.WorkLocation)
                .WithOne(w => w.Identify)
                .HasForeignKey<WorkLocation>(w => w.IdentifyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VoterCard>()
                .HasIndex(v => v.UserId)
                .HasDatabaseName("IX_VoterCards_UserId");

            modelBuilder.Entity<AuditLogEntry>()
                .HasIndex(a => new { a.Category, a.TimestampUtc })
                .HasDatabaseName("IX_AuditLogs_Category_TimestampUtc");

            modelBuilder.Entity<AuditLogEntry>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("IX_AuditLogs_UserId");

            modelBuilder.Entity<UnionMembership>()
                .HasIndex(u => u.UserId)
                .HasDatabaseName("IX_UnionMemberships_UserId");

            modelBuilder.Entity<FederationMembership>()
                .HasIndex(f => f.UserId)
                .HasDatabaseName("IX_FederationMemberships_UserId");

            modelBuilder.Entity<AssociationMembership>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("IX_AssociationMemberships_UserId");

            modelBuilder.Entity<NgoMembership>()
                .HasIndex(n => n.UserId)
                .HasDatabaseName("IX_NgoMemberships_UserId");

            modelBuilder.Entity<AffiliationInfo>()
                .HasIndex(a => a.UserId)
                .HasDatabaseName("IX_AffiliationInfos_UserId");

            // إضافة الأدوار الافتراضية
            modelBuilder.Entity<IdentityRole>().HasData(
     new IdentityRole { Id = "1", Name = "SuperAdmin", NormalizedName = "SUPERADMIN" },
     new IdentityRole { Id = "2", Name = "Admin", NormalizedName = "ADMIN" },
     new IdentityRole { Id = "3", Name = "User", NormalizedName = "USER" },
     new IdentityRole { Id = "4", Name = "فرد", NormalizedName = "فرد" },
     new IdentityRole { Id = "5", Name = "NewsEditor", NormalizedName = "NEWSEDITOR" },
     new IdentityRole { Id = "6", Name = "MapViewer", NormalizedName = "MAPVIEWER" },
     new IdentityRole { Id = "7", Name = "DistrictAdmin", NormalizedName = "DISTRICTADMIN" },

     // ✅ الجديد
     new IdentityRole { Id = "8", Name = "Manager", NormalizedName = "MANAGER" },
     new IdentityRole { Id = "9", Name = "AssistantManager", NormalizedName = "ASSISTANTMANAGER" },
     new IdentityRole { Id = "10", Name = "ManagerViewer", NormalizedName = "MANAGERVIEWER" }
 );

            
        }
    }
}
