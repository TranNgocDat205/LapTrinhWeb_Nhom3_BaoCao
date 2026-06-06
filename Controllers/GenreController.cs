using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DACS_Nhom3.Data;
using DACS_Nhom3.Models;
using System.Linq;
using System.Threading.Tasks;

namespace DACS_Nhom3.Controllers
{
    public class GenreController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public GenreController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Danh sách thể loại cho người dùng xem
        public async Task<IActionResult> Index()
        {
            var genres = await _context.Genres
                .Include(g => g.Stories.Where(s => s.Status == StoryStatus.Published))
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();

            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                ViewBag.FavoriteGenres = await _context.UserFavoriteGenres
                    .Where(ufg => ufg.UserId == user.Id)
                    .Select(ufg => ufg.GenreId)
                    .ToListAsync();
            }

            return View(genres);
        }


        // GET: Chi tiết thể loại + danh sách truyện thuộc thể loại
        public async Task<IActionResult> Details(int id)
        {
            var genre = await _context.Genres
                .Include(g => g.Stories.Where(s => s.Status == StoryStatus.Published))
                    .ThenInclude(s => s.Comments)
                .FirstOrDefaultAsync(g => g.Id == id && g.IsActive);

            if (genre == null)
            {
                TempData["Error"] = "Không tìm thấy thể loại này hoặc thể loại đã bị ẩn.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Stories = genre.Stories
                .Where(s => s.Status == StoryStatus.Published)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();

            return View(genre);
        }

        // GET: Random một thể loại cho hôm nay
        public async Task<IActionResult> RandomToday()
        {
            var genres = await _context.Genres
                .Where(g => g.IsActive)
                .ToListAsync();

            if (genres == null || !genres.Any())
            {
                TempData["Error"] = "Hiện chưa có thể loại nào để gợi ý.";
                return RedirectToAction("Index", "Home");
            }

            var random = new Random();
            var genre = genres[random.Next(genres.Count)];

            return RedirectToAction(nameof(Details), new { id = genre.Id });
        }

        // GET: Admin - Quản lý thể loại (Trang Manage chính)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var genres = await _context.Genres
                .Include(g => g.Stories)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();
            return View(genres);
        }

        // POST: Admin - Thêm thể loại qua AJAX
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Genre genre)
        {
            genre.Name = genre.Name?.Trim() ?? string.Empty;
            genre.Description = string.IsNullOrWhiteSpace(genre.Description) ? null : genre.Description.Trim();
            genre.Icon = string.IsNullOrWhiteSpace(genre.Icon) ? "fas fa-book" : genre.Icon.Trim();
            genre.Color = string.IsNullOrWhiteSpace(genre.Color) ? "#E67E22" : genre.Color.Trim();

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = "Dữ liệu không hợp lệ: " + errors });
            }

            // Kiểm tra trùng tên thể loại công khai
            if (await _context.Genres.AnyAsync(g => g.Name.ToLower() == genre.Name.ToLower()))
            {
                return Json(new { success = false, message = "Tên thể loại này đã tồn tại trong hệ thống!" });
            }

            _context.Genres.Add(genre);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thêm thể loại mới thành công!";
            return Json(new { success = true, message = "Thêm thành công!" });
        }

        // POST: Admin - Sửa thể loại qua AJAX
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Genre genre)
        {
            if (genre.Id != 0 && id == 0) id = genre.Id;

            if (id != genre.Id)
            {
                return Json(new { success = false, message = "Không trùng khớp mã định danh thể loại!" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Kiểm tra trùng tên với các thể loại khác (trừ chính nó)
                    if (await _context.Genres.AnyAsync(g => g.Name.ToLower() == genre.Name.ToLower() && g.Id != id))
                    {
                        return Json(new { success = false, message = "Tên thể loại sửa đổi đã tồn tại!" });
                    }

                    _context.Update(genre);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Cập nhật thông tin thể loại thành công!";
                    return Json(new { success = true, message = "Cập nhật thành công!" });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GenreExists(genre.Id))
                    {
                        return Json(new { success = false, message = "Thể loại không còn tồn tại trên hệ thống!" });
                    }
                    throw;
                }
            }

            var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return Json(new { success = false, message = "Dữ liệu chỉnh sửa không hợp lệ: " + errors });
        }

        // POST: Admin - Xóa thể loại qua AJAX
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var genre = await _context.Genres
                .Include(g => g.Stories)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (genre == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thể loại cần xóa!" });
            }

            if (genre.Stories != null && genre.Stories.Any())
            {
                return Json(new { success = false, message = "Không thể xóa! Thể loại này đang có truyện liên kết." });
            }

            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa thể loại thành công!";
            return Json(new { success = true, message = "Xóa thể loại thành công!" });
        }

        // POST: Admin - Bật/tắt trạng thái thể loại qua AJAX
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre == null)
            {
                return Json(new { success = false, message = "Không tìm thấy thể loại!" });
            }

            genre.IsActive = !genre.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = genre.IsActive });
        }

        // POST: Người dùng thêm/bỏ thể loại yêu thích
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFavoriteGenre(int genreId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var genreExists = await _context.Genres.AnyAsync(g => g.Id == genreId && g.IsActive);
            if (!genreExists) return NotFound();

            var favorite = await _context.UserFavoriteGenres
                .FirstOrDefaultAsync(ufg => ufg.UserId == user.Id && ufg.GenreId == genreId);

            if (favorite == null)
            {
                _context.UserFavoriteGenres.Add(new UserFavoriteGenre
                {
                    UserId = user.Id,
                    GenreId = genreId,
                    AddedAt = DateTime.Now
                });
                TempData["Success"] = "Đã thêm thể loại vào yêu thích!";
            }
            else
            {
                _context.UserFavoriteGenres.Remove(favorite);
                TempData["Success"] = "Đã bỏ thể loại khỏi yêu thích!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Danh sách thể loại yêu thích của người dùng
        [Authorize]
        public async Task<IActionResult> MyFavoriteGenres()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var favoriteGenres = await _context.UserFavoriteGenres
                .Include(ufg => ufg.Genre)
                    .ThenInclude(g => g.Stories.Where(s => s.Status == StoryStatus.Published))
                .Where(ufg => ufg.UserId == user.Id && ufg.Genre.IsActive)
                .OrderByDescending(ufg => ufg.AddedAt)
                .Select(ufg => ufg.Genre)
                .ToListAsync();

            return View(favoriteGenres);
        }

        private bool GenreExists(int id)
        {
            return _context.Genres.Any(e => e.Id == id);
        }
    }
}