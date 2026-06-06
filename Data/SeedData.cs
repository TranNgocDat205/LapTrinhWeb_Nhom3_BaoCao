using Microsoft.AspNetCore.Identity;
using DACS_Nhom3.Models;

namespace DACS_Nhom3.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            Console.WriteLine("🌱 Starting seed data...");

            // Create roles
            string[] roleNames = { "Admin", "User" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    Console.WriteLine($"✅ Role '{roleName}' created.");
                }
            }

            // Create admin user
            var adminEmail = "admin@storyhub.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Administrator",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(adminUser, "Admin@123");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                    Console.WriteLine("✅ Admin user created.");
                }
            }

            // Tạo thể loại mẫu nếu chưa có
            if (!context.Genres.Any())
            {
                var genres = new[]
                {
                    new Genre { Name = "Tiên Hiệp", Description = "Tu tiên, kiếm hiệp", IsActive = true, SortOrder = 1, Icon = "fas fa-dragon", Color = "#6366f1" },
                    new Genre { Name = "Ngôn Tình", Description = "Truyện tình cảm lãng mạn", IsActive = true, SortOrder = 2, Icon = "fas fa-heart", Color = "#ec4899" },
                    new Genre { Name = "Kiếm Hiệp", Description = "Võ hiệp Kim Dung", IsActive = true, SortOrder = 3, Icon = "fas fa-sword", Color = "#f59e0b" },
                    new Genre { Name = "Kinh Dị", Description = "Truyện ma, kinh dị", IsActive = true, SortOrder = 4, Icon = "fas fa-ghost", Color = "#8b5cf6" },
                    new Genre { Name = "Huyền Huyễn", Description = "Phép thuật, kỳ ảo", IsActive = true, SortOrder = 5, Icon = "fas fa-magic", Color = "#06b6d4" },
                    new Genre { Name = "Đô Thị", Description = "Cuộc sống thành thị", IsActive = true, SortOrder = 6, Icon = "fas fa-city", Color = "#10b981" }
                };

                await context.Genres.AddRangeAsync(genres);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ {genres.Length} genres created.");
            }

            // Tạo truyện mẫu nếu chưa có
            if (!context.Stories.Any())
            {
                var adminId = adminUser.Id;
                var genreList = context.Genres.ToList();

                var stories = new[]
                {
                    new Story
                    {
                        Title = "Thiên Long Bát Bộ",
                        Author = "Kim Dung",
                        Description = "Tác phẩm võ hiệp kinh điển của Kim Dung, kể về cuộc đời của Kiều Phong, Húc Trúc và Đoàn Dự.",
                        Content = "Nội dung truyện Thiên Long Bát Bộ...",
                        GenreId = genreList.First(g => g.Name == "Kiếm Hiệp").Id,
                        UserId = adminId,
                        Status = StoryStatus.Published,
                        Views = 1500,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    },
                    new Story
                    {
                        Title = "Tây Du Ký",
                        Author = "Ngô Thừa Ân",
                        Description = "Kể về hành trình thỉnh kinh của Đường Tăng và 4 đồ đệ.",
                        Content = "Nội dung truyện Tây Du Ký...",
                        GenreId = genreList.First(g => g.Name == "Huyền Huyễn").Id,
                        UserId = adminId,
                        Status = StoryStatus.Published,
                        Views = 2500,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    },
                    new Story
                    {
                        Title = "Tam Thể",
                        Author = "Lưu Từ Hân",
                        Description = "Tác phẩm khoa học viễn tưởng nổi tiếng của Trung Quốc.",
                        Content = "Nội dung truyện Tam Thể...",
                        GenreId = genreList.First(g => g.Name == "Huyền Huyễn").Id,
                        UserId = adminId,
                        Status = StoryStatus.Published,
                        Views = 800,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    }
                };

                await context.Stories.AddRangeAsync(stories);
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ {stories.Length} stories created.");
            }

            Console.WriteLine("🎉 Seed data completed!");
        }
    }
}