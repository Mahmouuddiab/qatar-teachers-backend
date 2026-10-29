using Microsoft.EntityFrameworkCore;
using QatarLearningBackend.Models;

namespace QatarLearningBackend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<TeacherAvailability> TeacherAvailabilities => Set<TeacherAvailability>();
        public DbSet<Course> Courses => Set<Course>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique();

            modelBuilder.Entity<TeacherAvailability>()
                .HasIndex(a => new { a.TeacherId, a.IsRecurring, a.DayOfWeek, a.Date });

            // Optional: one profile per teacher
            modelBuilder.Entity<TeacherProfile>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            // Courses: fast lookups by teacher and by subject (student browsing)
            modelBuilder.Entity<Course>()
                .HasIndex(c => c.TeacherId);

            modelBuilder.Entity<Course>()
                .HasIndex(c => c.Subject);

            // Bookings: link back to the course they were made for
            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.CourseId);
        }
    }
}