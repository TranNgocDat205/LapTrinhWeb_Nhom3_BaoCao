using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DACS_Nhom3.Models;

namespace DACS_Nhom3.Controllers
{
    [Authorize]
    public class RewardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public RewardController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        private static DateTime VietNamNow()
        {
            TimeZoneInfo vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var today = VietNamNow().Date;
            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            ViewBag.IsAdmin = isAdmin;
            ViewBag.TodayChecked = user.LastCheckInDate?.Date == today;
            ViewBag.TodaySpun = !isAdmin && user.LastSpinDate?.Date == today;
            ViewBag.NeedDays = 7 - (user.CheckInStreak % 7);
            if ((int)ViewBag.NeedDays == 7 && user.CheckInStreak > 0) ViewBag.NeedDays = 7;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var today = VietNamNow().Date;
            var lastDate = user.LastCheckInDate?.Date;

            if (lastDate == today)
            {
                TempData["Error"] = "Hôm nay bạn đã điểm danh rồi.";
                return RedirectToAction(nameof(Index));
            }

            if (lastDate == today.AddDays(-1))
                user.CheckInStreak += 1;
            else
                user.CheckInStreak = 1;

            user.LastCheckInDate = today;

            // Mỗi ngày điểm danh thành công cộng ngay 1 kim cương vào tài khoản user.
            user.Diamonds += 1;

            if (user.CheckInStreak % 7 == 0)
            {
                user.Diamonds += 10;
                TempData["Success"] = "Điểm danh thành công! Bạn nhận 1 kim cương hôm nay và thưởng chuỗi 7 ngày thêm 10 kim cương.";
            }
            else
            {
                var remaining = 7 - (user.CheckInStreak % 7);
                TempData["Success"] = $"Điểm danh thành công! Bạn nhận 1 kim cương. Còn {remaining} ngày liên tục để nhận thêm 10 kim cương.";
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = "Có lỗi khi cập nhật kim cương. Vui lòng thử lại.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: /Reward/Spin
        // Vòng quay may mắn: mỗi user chỉ quay 1 lần/ngày theo giờ Việt Nam.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Spin()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để quay." });

            var today = VietNamNow().Date;
            bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            // Admin được quay vô hạn, user thường chỉ quay 1 lần/ngày
            if (!isAdmin && user.LastSpinDate?.Date == today)
            {
                return Json(new
                {
                    success = false,
                    alreadySpun = true,
                    message = "Hôm nay bạn đã quay vòng quay may mắn rồi.",
                    diamonds = user.Diamonds,
                    freeDownloads = user.FreeDownloads
                });
            }

            // Tỷ lệ dùng theo trọng số user yêu cầu: 60 / 30 / 30 / 20 / 10, tổng 150.
            // Nghĩa là tỷ lệ thực tế lần lượt là 40%, 20%, 20%, 13.33%, 6.67%.
            int roll = Random.Shared.Next(1, 151);

            string prizeKey;
            string message;
            int segmentIndex;
            int diamondReward = 0;
            int freeDownloadReward = 0;

            if (roll <= 60)
            {
                prizeKey = "MISS";
                message = "😢 Chúc bạn may mắn lần sau!";
                segmentIndex = 0;
            }
            else if (roll <= 90)
            {
                prizeKey = "DIAMOND_1";
                diamondReward = 1;
                user.Diamonds += 1;
                message = "💎 Bạn nhận được 1 kim cương!";
                segmentIndex = 1;
            }
            else if (roll <= 120)
            {
                prizeKey = "DIAMOND_2";
                diamondReward = 2;
                user.Diamonds += 2;
                message = "💎 Bạn nhận được 2 kim cương!";
                segmentIndex = 2;
            }
            else if (roll <= 140)
            {
                prizeKey = "DIAMOND_3";
                diamondReward = 3;
                user.Diamonds += 3;
                message = "💎 Bạn nhận được 3 kim cương!";
                segmentIndex = 3;
            }
            else
            {
                prizeKey = "FREE_DOWNLOAD";
                freeDownloadReward = 1;
                user.FreeDownloads += 1;
                message = "🎁 Bạn nhận được 1 lượt tải truyện miễn phí!";
                segmentIndex = 4;
            }

            // Chỉ lưu ngày quay với user thường. Admin không bị giới hạn lượt quay.
            if (!isAdmin)
            {
                user.LastSpinDate = today;
            }

            await _userManager.UpdateAsync(user);

            return Json(new
            {
                success = true,
                prizeKey,
                segmentIndex,
                diamondReward,
                freeDownloadReward,
                message,
                diamonds = user.Diamonds,
                freeDownloads = user.FreeDownloads
            });
        }
    }
}
