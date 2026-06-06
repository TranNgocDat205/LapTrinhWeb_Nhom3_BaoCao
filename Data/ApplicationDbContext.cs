using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DACS_Nhom3.Models;

namespace DACS_Nhom3.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Story> Stories { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<ReadingHistory> ReadingHistories { get; set; }
        public DbSet<UserFavoriteStory> UserFavoriteStories { get; set; }
        public DbSet<UserFavoriteGenre> UserFavoriteGenres { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite keys
            modelBuilder.Entity<UserFavoriteStory>()
                .HasKey(ufs => new { ufs.UserId, ufs.StoryId });

            modelBuilder.Entity<UserFavoriteGenre>()
                .HasKey(ufg => new { ufg.UserId, ufg.GenreId });

            // UserFavoriteStory relationships
            modelBuilder.Entity<UserFavoriteStory>()
                .HasOne(ufs => ufs.User)
                .WithMany(u => u.FavoriteStories)
                .HasForeignKey(ufs => ufs.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserFavoriteStory>()
                .HasOne(ufs => ufs.Story)
                .WithMany(s => s.UserFavorites)
                .HasForeignKey(ufs => ufs.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // UserFavoriteGenre relationships
            modelBuilder.Entity<UserFavoriteGenre>()
                .HasOne(ufg => ufg.User)
                .WithMany(u => u.FavoriteGenres)
                .HasForeignKey(ufg => ufg.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserFavoriteGenre>()
                .HasOne(ufg => ufg.Genre)
                .WithMany(g => g.UserFavorites)
                .HasForeignKey(ufg => ufg.GenreId)
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 SỬA LỖI: Comment relationships - Không dùng Cascade Delete
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Story)
                .WithMany(s => s.Comments)
                .HasForeignKey(c => c.StoryId)
                .OnDelete(DeleteBehavior.Restrict);  // 🔥 Đổi từ Cascade sang Restrict

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);  // 🔥 Đổi từ Cascade sang Restrict

            // Story relationships
            modelBuilder.Entity<Story>()
                .HasOne(s => s.User)
                .WithMany(u => u.Stories)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Story>()
                .HasOne(s => s.Genre)
                .WithMany(g => g.Stories)
                .HasForeignKey(s => s.GenreId)
                .OnDelete(DeleteBehavior.Restrict);

            // ReadingHistory relationships
            modelBuilder.Entity<ReadingHistory>()
                .HasOne(rh => rh.User)
                .WithMany(u => u.ReadingHistories)
                .HasForeignKey(rh => rh.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReadingHistory>()
                .HasOne(rh => rh.Story)
                .WithMany(s => s.ReadingHistories)
                .HasForeignKey(rh => rh.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            modelBuilder.Entity<Story>()
                .HasIndex(s => s.Title)
                .HasDatabaseName("IX_Story_Title");

            modelBuilder.Entity<Story>()
                .HasIndex(s => s.CreatedAt)
                .HasDatabaseName("IX_Story_CreatedAt");

            modelBuilder.Entity<Genre>()
                .HasIndex(g => g.Name)
                .IsUnique()
                .HasDatabaseName("IX_Genre_Name");
        }
    }
}