using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DACS_Nhom3.Data;
using DACS_Nhom3.Models;
using System.Text.RegularExpressions;

namespace DACS_Nhom3.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var allStories = await _context.Stories
                .AsNoTracking()
                .Include(s => s.Genre)
                .Where(s => s.Status == StoryStatus.Published)
                .ToListAsync();

            var groupedStories = allStories
                .GroupBy(s => NormalizeStoryTitle(s.Title), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var episodes = g.ToList();
                    var firstEpisode = episodes
                        .OrderBy(s => GetEpisodeNumber(s.Title) ?? int.MaxValue)
                        .ThenBy(s => s.CreatedAt)
                        .First();

                    var latestEpisode = episodes
                        .OrderByDescending(s => s.UpdatedAt)
                        .First();

                    return new Story
                    {
                        Id = firstEpisode.Id,
                        Title = g.Key,
                        Author = firstEpisode.Author,
                        Description = firstEpisode.Description,
                        CoverImageUrl = firstEpisode.CoverImageUrl,
                        Content = firstEpisode.Content,
                        Views = episodes.Sum(x => x.Views),
                        Likes = episodes.Sum(x => x.Likes),
                        Status = firstEpisode.Status,
                        CreatedAt = firstEpisode.CreatedAt,
                        UpdatedAt = latestEpisode.UpdatedAt,
                        UserId = firstEpisode.UserId,
                        GenreId = firstEpisode.GenreId,
                        Genre = firstEpisode.Genre
                    };
                })
                .ToList();

            ViewBag.LatestStories = groupedStories
                .OrderByDescending(s => s.UpdatedAt)
                .Take(12)
                .ToList();

            ViewBag.PopularStories = groupedStories
                .OrderByDescending(s => s.Views)
                .Take(12)
                .ToList();

            ViewBag.MostLikedStories = groupedStories
                .OrderByDescending(s => s.Likes)
                .Take(12)
                .ToList();

            ViewBag.FeaturedGenres = await _context.Genres
                .AsNoTracking()
                .Include(g => g.Stories.Where(s => s.Status == StoryStatus.Published))
                .Where(g => g.IsActive && g.Stories.Any())
                .OrderBy(g => g.SortOrder)
                .Take(8)
                .ToListAsync();

            ViewBag.TotalStories = groupedStories.Count;
            ViewBag.TotalGenres = await _context.Genres.CountAsync(g => g.IsActive);
            ViewBag.TotalViews = groupedStories.Sum(s => s.Views);

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        private static string NormalizeStoryTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return string.Empty;

            var normalized = title.Trim();
            normalized = Regex.Replace(normalized, @"\s*[-–—_:]*\s*(tập|tap|episode|ep|chap|chapter|phần|phan|vol|volume)\s*\d+.*$", "", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(normalized) ? title.Trim() : normalized;
        }

        private static int? GetEpisodeNumber(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return null;

            var match = Regex.Match(title, @"(?:tập|tap|episode|ep|chap|chapter|phần|phan|vol|volume)\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var number))
            {
                return number;
            }

            return null;
        }
    }
}
