using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DACS_Nhom3.Data;
using DACS_Nhom3.Models;
using System.Text.RegularExpressions;

namespace DACS_Nhom3.Controllers
{
    public class StoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public StoryController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        private bool IsCurrentUserAdmin()
        {
            var email = User?.Identity?.Name ?? string.Empty;
            return User?.IsInRole("Admin") == true
                || email.Equals("admin@dacsnhom3.com", StringComparison.OrdinalIgnoreCase);
        }

        // GET: Danh sách truyện
        public async Task<IActionResult> Index(int? genreId, string searchString, string sortBy = "newest", int page = 1)
        {
            int pageSize = 12;

            var storiesQuery = _context.Stories
                .Include(s => s.Genre)
                .Include(s => s.User)
                .Include(s => s.Comments)
                .Where(s => s.Status == StoryStatus.Published)
                .AsQueryable();

            if (genreId.HasValue && genreId.Value > 0)
            {
                storiesQuery = storiesQuery.Where(s => s.GenreId == genreId.Value);
                ViewBag.CurrentGenre = genreId.Value;
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                storiesQuery = storiesQuery.Where(s => s.Title.Contains(searchString) ||
                                                       s.Author.Contains(searchString) ||
                                                       s.Description.Contains(searchString));
                ViewBag.SearchString = searchString;
            }

            var allStories = await storiesQuery.ToListAsync();

            // Gom các tập cùng một bộ truyện lại thành 1 card ngoài thư viện.
            // Ví dụ: "Doraemon tập 1", "Doraemon tập 2" => ngoài trang Story chỉ hiện "Doraemon".
            var groupedStories = allStories
                .GroupBy(s => NormalizeStoryTitle(s.Title))
                .Select(g =>
                {
                    var list = g.ToList();
                    var firstEpisode = list
                        .OrderBy(s => GetEpisodeNumber(s.Title) ?? int.MaxValue)
                        .ThenBy(s => s.CreatedAt)
                        .First();

                    firstEpisode.Title = g.Key;
                    firstEpisode.Views = list.Sum(x => x.Views);
                    firstEpisode.Likes = list.Sum(x => x.Likes);

                    ViewData[$"EpisodeCount_{firstEpisode.Id}"] = list.Count;
                    return firstEpisode;
                });

            groupedStories = sortBy switch
            {
                "popular" => groupedStories.OrderByDescending(s => s.Views),
                "likes" => groupedStories.OrderByDescending(s => s.Likes),
                _ => groupedStories.OrderByDescending(s => s.CreatedAt)
            };

            var groupedList = groupedStories.ToList();
            var totalStories = groupedList.Count;
            var totalPages = (int)Math.Ceiling(totalStories / (double)pageSize);

            var storiesList = groupedList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SortBy = sortBy;
            ViewBag.Genres = await _context.Genres.Where(g => g.IsActive).ToListAsync();

            return View(storiesList);
        }

        // GET: Chi tiết truyện
        public async Task<IActionResult> Details(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Genre)
                .Include(s => s.User)
                .Include(s => s.Comments)
                    .ThenInclude(c => c.User)
                .Include(s => s.Comments)
                    .ThenInclude(c => c.Replies)
                        .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null)
            {
                return NotFound();
            }

            story.Views++;
            await _context.SaveChangesAsync();

            // Lưu lịch sử đọc
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                ViewBag.IsFavorite = await _context.UserFavoriteStories
                    .AnyAsync(ufs => ufs.UserId == user.Id && ufs.StoryId == id);

                // Upsert reading history
                var history = await _context.ReadingHistories
                    .FirstOrDefaultAsync(rh => rh.UserId == user.Id && rh.StoryId == id);
                if (history == null)
                {
                    _context.ReadingHistories.Add(new ReadingHistory
                    {
                        UserId = user.Id,
                        StoryId = id,
                        ReadAt = DateTime.Now
                    });
                }
                else
                {
                    history.ReadAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();
            }

            var baseTitle = NormalizeStoryTitle(story.Title);
            ViewBag.BaseStoryTitle = baseTitle;

            ViewBag.Episodes = await _context.Stories
                .Include(s => s.Genre)
                .Where(s => s.Status == StoryStatus.Published)
                .ToListAsync();

            ViewBag.Episodes = ((List<Story>)ViewBag.Episodes)
                .Where(s => NormalizeStoryTitle(s.Title).Equals(baseTitle, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => GetEpisodeNumber(s.Title) ?? int.MaxValue)
                .ThenBy(s => s.CreatedAt)
                .ToList();

            ViewBag.CurrentEpisodeNumber = GetEpisodeNumber(story.Title);

            ViewBag.RelatedStories = await _context.Stories
                .Where(s => s.GenreId == story.GenreId && s.Id != id && s.Status == StoryStatus.Published)
                .OrderByDescending(s => s.Views)
                .Take(5)
                .ToListAsync();

            return View(story);
        }


        // GET: Đọc truyện
        [Authorize]
        public async Task<IActionResult> Read(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Genre)
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == StoryStatus.Published);

            if (story == null)
            {
                return NotFound();
            }

            // Lấy kim cương mới nhất từ database để hiển thị đúng ở khung tải truyện.
            // Tránh lỗi thanh menu hiện 11 💎 nhưng khung tải lại hiện 0 💎.
            var currentUserId = _userManager.GetUserId(User);
            var currentUser = !string.IsNullOrWhiteSpace(currentUserId)
                ? await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUserId)
                : null;

            const int downloadPrice = 5;
            ViewBag.UserDiamonds = currentUser?.Diamonds ?? 0;
            ViewBag.DownloadPrice = downloadPrice;
            ViewBag.CanDownloadPdf = (currentUser?.Diamonds ?? 0) >= downloadPrice;

            if (!string.IsNullOrWhiteSpace(story.Content) && story.Content.Trim().EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var pdfPath = story.Content.Trim();
                if (!pdfPath.StartsWith("/"))
                {
                    pdfPath = "/" + pdfPath;
                }

                var relativePdfPath = pdfPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePdfPath);

                // Hỗ trợ cả dữ liệu cũ lưu ở uploads/stories và dữ liệu mới ở uploads/story-pdfs
                if (!System.IO.File.Exists(physicalPath))
                {
                    relativePdfPath = relativePdfPath.Replace(
                        Path.Combine("uploads", "stories"),
                        Path.Combine("uploads", "story-pdfs")
                    );

                    physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePdfPath);

                    if (System.IO.File.Exists(physicalPath))
                    {
                        pdfPath = "/" + relativePdfPath.Replace(Path.DirectorySeparatorChar, '/');
                    }
                    else
                    {
                        TempData["Error"] = "Không tìm thấy file PDF của truyện. Vui lòng kiểm tra lại file đã tải lên.";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                }

                ViewBag.PdfUrl = Url.Content("~" + pdfPath);
                return View(story);
            }

            return View(story);
        }


        // GET: Tải PDF - yêu cầu đủ 5 kim cương và trừ 5 kim cương khi bắt đầu tải
        [Authorize]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var story = await _context.Stories
                .FirstOrDefaultAsync(s => s.Id == id && s.Status == StoryStatus.Published);

            if (story == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(story.Content) || !story.Content.Trim().EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Truyện này không có file PDF để tải.";
                return RedirectToAction(nameof(Read), new { id });
            }

            var pdfPath = story.Content.Trim();
            if (!pdfPath.StartsWith("/"))
            {
                pdfPath = "/" + pdfPath;
            }

            var relativePdfPath = pdfPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePdfPath);

            // Hỗ trợ cả dữ liệu cũ lưu ở uploads/stories và dữ liệu mới ở uploads/story-pdfs
            if (!System.IO.File.Exists(physicalPath))
            {
                relativePdfPath = relativePdfPath.Replace(
                    Path.Combine("uploads", "stories"),
                    Path.Combine("uploads", "story-pdfs")
                );

                physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePdfPath);

                if (!System.IO.File.Exists(physicalPath))
                {
                    TempData["Error"] = "Không tìm thấy file PDF của truyện.";
                    return RedirectToAction(nameof(Read), new { id });
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Challenge();
            }

            if (user.Diamonds < 5)
            {
                TempData["Error"] = $"Không đủ kim cương để tải truyện. Bạn đang có {user.Diamonds} kim cương, cần 5 kim cương.";
                return RedirectToAction(nameof(Read), new { id });
            }

            // Đủ điều kiện tải: trừ đúng 5 kim cương vào tài khoản người dùng trước khi trả file.
            user.Diamonds -= 5;
            await _context.SaveChangesAsync();

            var safeTitle = Regex.Replace(story.Title ?? "truyen", @"[^a-zA-Z0-9\-_\sÀ-ỹ]", "").Trim();
            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                safeTitle = "truyen";
            }

            return PhysicalFile(physicalPath, "application/pdf", safeTitle + ".pdf");
        }

        // AJAX: Toggle Favorite
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavoriteAjax([FromBody] FavoriteRequest req)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var favorite = await _context.UserFavoriteStories
                .FirstOrDefaultAsync(ufs => ufs.UserId == user.Id && ufs.StoryId == req.StoryId);

            bool isFavorite;
            if (favorite == null)
            {
                _context.UserFavoriteStories.Add(new UserFavoriteStory
                {
                    UserId = user.Id,
                    StoryId = req.StoryId,
                    AddedAt = DateTime.Now
                });
                isFavorite = true;
            }
            else
            {
                _context.UserFavoriteStories.Remove(favorite);
                isFavorite = false;
            }
            await _context.SaveChangesAsync();
            return Json(new { isFavorite });
        }

        // Redirect version (for non-JS fallback)
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> ToggleFavorite(int storyId)
        {
            var user = await _userManager.GetUserAsync(User);
            var favorite = await _context.UserFavoriteStories
                .FirstOrDefaultAsync(ufs => ufs.UserId == user.Id && ufs.StoryId == storyId);

            if (favorite == null)
            {
                _context.UserFavoriteStories.Add(new UserFavoriteStory
                {
                    UserId = user.Id,
                    StoryId = storyId,
                    AddedAt = DateTime.Now
                });
                TempData["Success"] = "Đã thêm vào danh sách yêu thích!";
            }
            else
            {
                _context.UserFavoriteStories.Remove(favorite);
                TempData["Success"] = "Đã xóa khỏi danh sách yêu thích!";
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = storyId });
        }

        // AJAX: Like story
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LikeAjax([FromBody] FavoriteRequest req)
        {
            var story = await _context.Stories.FindAsync(req.StoryId);
            if (story == null) return NotFound();
            story.Likes++;
            await _context.SaveChangesAsync();
            return Json(new { likes = story.Likes });
        }

        // AJAX: Add comment
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment([FromBody] CommentRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Content))
                return Json(new { success = false, message = "Nội dung không được để trống" });

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Chưa đăng nhập" });

            var storyExists = await _context.Stories.AnyAsync(s => s.Id == req.StoryId);
            if (!storyExists) return Json(new { success = false, message = "Truyện không tồn tại" });

            var comment = new Comment
            {
                Content = req.Content.Trim(),
                UserId = user.Id,
                StoryId = req.StoryId,
                ParentCommentId = req.ParentCommentId,
                CreatedAt = DateTime.Now,
                IsApproved = true
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Bình luận thành công" });
        }

        // User favorites list
        [Authorize]
        public async Task<IActionResult> MyFavorites()
        {
            var user = await _userManager.GetUserAsync(User);
            var favorites = await _context.UserFavoriteStories
                .Include(ufs => ufs.Story)
                    .ThenInclude(s => s.Genre)
                .Where(ufs => ufs.UserId == user.Id)
                .OrderByDescending(ufs => ufs.AddedAt)
                .Select(ufs => ufs.Story)
                .ToListAsync();

            return View(favorites);
        }

        // Reading history
        [Authorize]
        public async Task<IActionResult> ReadingHistory()
        {
            var user = await _userManager.GetUserAsync(User);
            var histories = await _context.ReadingHistories
                .Include(rh => rh.Story)
                    .ThenInclude(s => s.Genre)
                .Where(rh => rh.UserId == user.Id)
                .OrderByDescending(rh => rh.ReadAt)
                .Take(20)
                .ToListAsync();

            return View(histories);
        }

        // ========== ADMIN: CREATE ==========
        [Authorize]
        public IActionResult Create()
        {
            ViewBag.Genres = _context.Genres.Where(g => g.IsActive).OrderBy(g => g.SortOrder).ToList();
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Story story, IFormFile coverImage, IFormFile pdfFile)
        {
            ViewBag.Genres = _context.Genres.Where(g => g.IsActive).OrderBy(g => g.SortOrder).ToList();

            // Các field này do hệ thống tự gán hoặc là navigation property, không validate từ form
            ModelState.Remove("Content");
            ModelState.Remove("Content");
            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Genre");
            ModelState.Remove("Comments");
            ModelState.Remove("UserFavorites");
            ModelState.Remove("ReadingHistories");

            if (pdfFile == null || pdfFile.Length == 0)
            {
                ModelState.AddModelError("Content", "Vui lòng chọn file PDF của truyện.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (coverImage != null && coverImage.Length > 0)
                    {
                        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                        var ext = Path.GetExtension(coverImage.FileName).ToLower();
                        if (!allowed.Contains(ext))
                        {
                            TempData["Error"] = "Chỉ chấp nhận file ảnh (JPG, PNG, GIF, WEBP)!";
                            return View(story);
                        }
                        if (coverImage.Length > 5 * 1024 * 1024)
                        {
                            TempData["Error"] = "Ảnh không được vượt quá 5MB!";
                            return View(story);
                        }

                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "covers");
                        Directory.CreateDirectory(uploadsFolder);
                        string uniqueFileName = Guid.NewGuid().ToString() + ext;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await coverImage.CopyToAsync(fileStream);
                        }
                        story.CoverImageUrl = "/uploads/covers/" + uniqueFileName;
                    }
                    else
                    {
                        story.CoverImageUrl = "/images/default-cover.jpg";
                    }

                    if (pdfFile != null && pdfFile.Length > 0)
                    {
                        var pdfExt = Path.GetExtension(pdfFile.FileName).ToLower();
                        if (pdfExt != ".pdf")
                        {
                            ModelState.AddModelError("Content", "Chỉ chấp nhận file PDF!");
                            return View(story);
                        }

                        if (pdfFile.Length > 50 * 1024 * 1024)
                        {
                            ModelState.AddModelError("Content", "File PDF không được vượt quá 50MB!");
                            return View(story);
                        }

                        string pdfFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "story-pdfs");
                        Directory.CreateDirectory(pdfFolder);
                        string pdfFileName = Guid.NewGuid().ToString() + ".pdf";
                        string pdfPath = Path.Combine(pdfFolder, pdfFileName);

                        using (var fileStream = new FileStream(pdfPath, FileMode.Create))
                        {
                            await pdfFile.CopyToAsync(fileStream);
                        }

                        // Lưu đường dẫn PDF vào Content để không phải thêm cột database mới
                        story.Content = "/uploads/story-pdfs/" + pdfFileName;
                    }

                    story.UserId = _userManager.GetUserId(User);
                    story.CreatedAt = DateTime.Now;
                    story.UpdatedAt = DateTime.Now;
                    story.Views = 0;
                    story.Likes = 0;
                    // Giữ trạng thái admin chọn trên form (Published/Draft/Locked)

                    _context.Add(story);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Truyện đã được thêm thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                }
            }
            return View(story);
        }


        // ========== ADMIN: ADD EPISODE ==========
        [Authorize]
        public async Task<IActionResult> AddEpisode(int id)
        {
            if (!IsCurrentUserAdmin()) return Forbid();

            var baseStory = await _context.Stories
                .Include(s => s.Genre)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (baseStory == null) return NotFound();

            var baseTitle = NormalizeStoryTitle(baseStory.Title);
            var episodes = await _context.Stories
                .Where(s => s.Status == StoryStatus.Published)
                .ToListAsync();

            var sameEpisodes = episodes
                .Where(s => NormalizeStoryTitle(s.Title).Equals(baseTitle, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var nextEpisode = sameEpisodes
                .Select(s => GetEpisodeNumber(s.Title))
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .DefaultIfEmpty(sameEpisodes.Count)
                .Max() + 1;

            var newEpisode = new Story
            {
                Title = $"{baseTitle} Tập {nextEpisode}",
                Author = baseStory.Author,
                Description = baseStory.Description,
                CoverImageUrl = baseStory.CoverImageUrl,
                GenreId = baseStory.GenreId,
                Status = StoryStatus.Published,
                Content = "PDF"
            };

            ViewBag.BaseStoryId = baseStory.Id;
            ViewBag.BaseStoryTitle = baseTitle;
            ViewBag.NextEpisode = nextEpisode;
            ViewBag.Genres = await _context.Genres.Where(g => g.IsActive).OrderBy(g => g.SortOrder).ToListAsync();
            return View(newEpisode);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddEpisode(
            int baseStoryId,
            string title,
            string author,
            int genreId,
            string description,
            StoryStatus status,
            IFormFile? coverImage,
            IFormFile pdfFile)
        {
            if (!IsCurrentUserAdmin()) return Forbid();

            // Dùng AsNoTracking để không giữ entity truyện gốc trong DbContext.
            var baseStory = await _context.Stories
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == baseStoryId);

            if (baseStory == null) return NotFound();

            ViewBag.BaseStoryId = baseStoryId;
            ViewBag.BaseStoryTitle = NormalizeStoryTitle(baseStory.Title);
            ViewBag.Genres = await _context.Genres
                .AsNoTracking()
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();

            title = (title ?? string.Empty).Trim();
            author = (author ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title))
                ModelState.AddModelError("title", "Tên tập không được để trống.");

            if (string.IsNullOrWhiteSpace(description))
                ModelState.AddModelError("description", "Mô tả không được để trống.");

            if (genreId <= 0)
                ModelState.AddModelError("genreId", "Vui lòng chọn thể loại.");

            if (pdfFile == null || pdfFile.Length == 0)
                ModelState.AddModelError("pdfFile", "Vui lòng chọn file PDF cho tập mới.");

            if (!ModelState.IsValid)
            {
                var vm = new Story
                {
                    Id = 0,
                    Title = title,
                    Author = author,
                    Description = description,
                    GenreId = genreId,
                    Status = status,
                    CoverImageUrl = baseStory.CoverImageUrl,
                    Content = "PDF"
                };
                return View(vm);
            }

            try
            {
                string coverUrl;

                if (coverImage != null && coverImage.Length > 0)
                {
                    var allowedImageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var imageExt = Path.GetExtension(coverImage.FileName).ToLowerInvariant();

                    if (!allowedImageExts.Contains(imageExt))
                    {
                        ModelState.AddModelError("coverImage", "Chỉ chấp nhận file ảnh JPG, PNG, GIF hoặc WEBP.");
                        return View(new Story { Id = 0, Title = title, Author = author, Description = description, GenreId = genreId, Status = status, Content = "PDF" });
                    }

                    if (coverImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("coverImage", "Ảnh bìa không được vượt quá 5MB.");
                        return View(new Story { Id = 0, Title = title, Author = author, Description = description, GenreId = genreId, Status = status, Content = "PDF" });
                    }

                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "covers");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + imageExt;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverImage.CopyToAsync(fileStream);
                    }

                    coverUrl = "/uploads/covers/" + uniqueFileName;
                }
                else
                {
                    coverUrl = string.IsNullOrWhiteSpace(baseStory.CoverImageUrl)
                        ? "/images/default-cover.jpg"
                        : baseStory.CoverImageUrl;
                }

                var pdfExt = Path.GetExtension(pdfFile.FileName).ToLowerInvariant();
                if (pdfExt != ".pdf")
                {
                    ModelState.AddModelError("pdfFile", "Chỉ chấp nhận file PDF.");
                    return View(new Story { Id = 0, Title = title, Author = author, Description = description, GenreId = genreId, Status = status, CoverImageUrl = coverUrl, Content = "PDF" });
                }

                if (pdfFile.Length > 50 * 1024 * 1024)
                {
                    ModelState.AddModelError("pdfFile", "File PDF không được vượt quá 50MB.");
                    return View(new Story { Id = 0, Title = title, Author = author, Description = description, GenreId = genreId, Status = status, CoverImageUrl = coverUrl, Content = "PDF" });
                }

                string pdfFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "story-pdfs");
                Directory.CreateDirectory(pdfFolder);
                string pdfFileName = Guid.NewGuid().ToString() + ".pdf";
                string pdfPath = Path.Combine(pdfFolder, pdfFileName);

                using (var fileStream = new FileStream(pdfPath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(fileStream);
                }

                // Tuyệt đối KHÔNG dùng lại object baseStory hoặc object Story bind từ form.
                // Tạo entity mới hoàn toàn, Id để mặc định 0 để SQL tự sinh khóa mới.
                var newEpisode = new Story
                {
                    Title = title,
                    Author = author,
                    Description = description,
                    GenreId = genreId,
                    Status = status,
                    CoverImageUrl = coverUrl,
                    Content = "/uploads/story-pdfs/" + pdfFileName,
                    UserId = _userManager.GetUserId(User) ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Views = 0,
                    Likes = 0
                };

                _context.ChangeTracker.Clear();
                await _context.Stories.AddAsync(newEpisode);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã thêm tập mới thành công!";
                return RedirectToAction(nameof(Details), new { id = baseStoryId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi thêm tập: " + ex.Message;
                return View(new Story
                {
                    Id = 0,
                    Title = title,
                    Author = author,
                    Description = description,
                    GenreId = genreId,
                    Status = status,
                    CoverImageUrl = baseStory.CoverImageUrl,
                    Content = "PDF"
                });
            }
        }

        // ========== ADMIN: EDIT ==========
        [Authorize]
        public async Task<IActionResult> Edit(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Genre)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null) return NotFound();

            ViewBag.Genres = await _context.Genres
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();

            return View(story);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Story story, IFormFile? coverImage, IFormFile? pdfFile)
        {
            if (id != story.Id) return NotFound();

            ViewBag.Genres = await _context.Genres
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();

            // Các field này không nhập trực tiếp từ form edit
            ModelState.Remove("UserId");
            ModelState.Remove("User");
            ModelState.Remove("Genre");
            ModelState.Remove("Comments");
            ModelState.Remove("UserFavorites");
            ModelState.Remove("ReadingHistories");

            if (!ModelState.IsValid)
            {
                return View(story);
            }

            var existingStory = await _context.Stories.FindAsync(id);
            if (existingStory == null) return NotFound();

            try
            {
                if (coverImage != null && coverImage.Length > 0)
                {
                    var allowedImageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var imageExt = Path.GetExtension(coverImage.FileName).ToLowerInvariant();

                    if (!allowedImageExts.Contains(imageExt))
                    {
                        ModelState.AddModelError("CoverImageUrl", "Chỉ chấp nhận file ảnh JPG, PNG, GIF hoặc WEBP.");
                        return View(story);
                    }

                    if (coverImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("CoverImageUrl", "Ảnh bìa không được vượt quá 5MB.");
                        return View(story);
                    }

                    DeletePhysicalFile(existingStory.CoverImageUrl, "/images/default-cover.jpg");

                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "covers");
                    Directory.CreateDirectory(uploadsFolder);
                    string uniqueFileName = Guid.NewGuid().ToString() + imageExt;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverImage.CopyToAsync(fileStream);
                    }

                    existingStory.CoverImageUrl = "/uploads/covers/" + uniqueFileName;
                }

                if (pdfFile != null && pdfFile.Length > 0)
                {
                    var pdfExt = Path.GetExtension(pdfFile.FileName).ToLowerInvariant();
                    if (pdfExt != ".pdf")
                    {
                        ModelState.AddModelError("Content", "Chỉ chấp nhận file PDF.");
                        return View(story);
                    }

                    if (pdfFile.Length > 50 * 1024 * 1024)
                    {
                        ModelState.AddModelError("Content", "File PDF không được vượt quá 50MB.");
                        return View(story);
                    }

                    DeletePhysicalFile(existingStory.Content);

                    string pdfFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "story-pdfs");
                    Directory.CreateDirectory(pdfFolder);
                    string pdfFileName = Guid.NewGuid().ToString() + ".pdf";
                    string pdfPath = Path.Combine(pdfFolder, pdfFileName);

                    using (var fileStream = new FileStream(pdfPath, FileMode.Create))
                    {
                        await pdfFile.CopyToAsync(fileStream);
                    }

                    existingStory.Content = "/uploads/story-pdfs/" + pdfFileName;
                }

                // Cập nhật nội dung truyện/tập:
                // - Nếu admin upload PDF mới thì Content đã được đổi sang đường dẫn PDF ở phía trên.
                // - Nếu không upload PDF mới, cho phép sửa nội dung dạng chữ trực tiếp trong form.
                if (pdfFile == null || pdfFile.Length == 0)
                {
                    existingStory.Content = story.Content?.Trim() ?? string.Empty;
                }

                existingStory.Title = story.Title?.Trim() ?? string.Empty;
                existingStory.Author = story.Author?.Trim() ?? string.Empty;
                existingStory.Description = story.Description?.Trim() ?? string.Empty;
                existingStory.GenreId = story.GenreId;
                existingStory.Status = story.Status;
                existingStory.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật truyện thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StoryExists(story.Id)) return NotFound();
                throw;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi cập nhật truyện: " + ex.Message;
                return View(story);
            }
        }

        // ========== ADMIN: DELETE ==========
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var story = await _context.Stories
                .Include(s => s.Genre)
                .Include(s => s.User)
                .Include(s => s.Comments)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (story == null) return NotFound();
            return View(story);
        }

        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var story = await _context.Stories.FindAsync(id);
            if (story == null) return NotFound();

            try
            {
                // Xóa dữ liệu liên quan trước để tránh lỗi khóa ngoại
                var comments = await _context.Comments.Where(c => c.StoryId == id).ToListAsync();
                var favorites = await _context.UserFavoriteStories.Where(f => f.StoryId == id).ToListAsync();
                var histories = await _context.ReadingHistories.Where(h => h.StoryId == id).ToListAsync();

                _context.Comments.RemoveRange(comments);
                _context.UserFavoriteStories.RemoveRange(favorites);
                _context.ReadingHistories.RemoveRange(histories);

                DeletePhysicalFile(story.CoverImageUrl, "/images/default-cover.jpg");
                DeletePhysicalFile(story.Content);

                _context.Stories.Remove(story);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Xóa truyện thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi xóa truyện: " + ex.Message;
                return RedirectToAction(nameof(Delete), new { id });
            }
        }


        private static string NormalizeStoryTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "Không có tên";

            var result = title.Trim();

            // Bỏ các hậu tố tập/chapter/chap thường dùng khi admin đặt tên từng tập
            // Ví dụ: Doraemon tập 1, Doraemon - Tập 2, One Piece chap 10
            result = Regex.Replace(result, @"\s*[-_:]?\s*(tập|tap|chapter|chap|chương|chuong|ep|episode|vol|volume)\s*\d+\s*$",
                "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            result = Regex.Replace(result, @"\s*[-_:]?\s*\d+\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result = Regex.Replace(result, @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(result) ? title.Trim() : result;
        }

        private static int? GetEpisodeNumber(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            var match = Regex.Match(title, @"(?:tập|tap|chapter|chap|chương|chuong|ep|episode|vol|volume)\s*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                match = Regex.Match(title, @"(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            return match.Success && int.TryParse(match.Groups[1].Value, out var number) ? number : null;
        }

        private void DeletePhysicalFile(string? relativePath, string? ignoredPath = null)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return;
            if (!string.IsNullOrWhiteSpace(ignoredPath) && relativePath.Equals(ignoredPath, StringComparison.OrdinalIgnoreCase)) return;

            var cleanPath = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var physicalPath = Path.Combine(_webHostEnvironment.WebRootPath, cleanPath);

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }
        }

        private bool StoryExists(int id) => _context.Stories.Any(e => e.Id == id);
    }

    // Request DTOs
    public class FavoriteRequest
    {
        public int StoryId { get; set; }
    }

    public class CommentRequest
    {
        public int StoryId { get; set; }
        public string Content { get; set; }
        public int? ParentCommentId { get; set; }
    }
}