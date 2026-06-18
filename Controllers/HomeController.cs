using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxiConnectSA.Models;
using TaxiConnectSA.Services;

namespace TaxiConnectSA.Controllers
{
    [Authorize]
    [Route("")]
    [Route("Home")]
    public class HomeController : Controller
    {
        private readonly FirebaseService _fb;

        public HomeController(FirebaseService fb) => _fb = fb;

        // ── Dashboard ─────────────────────────────────────────────────────

        [Route("")]
        [Route("Index")]
        [Route("Home/Index")]
        public async Task<IActionResult> Index(string? status = null, string? q = null)
        {
            var allBookings = await _fb.GetBookingsAsync();

            // Apply filters for the table only — counts always use all bookings
            var filtered = allBookings.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(status) && status != "All")
                filtered = filtered.Where(b => b.Status == status.ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(q))
            {
                var lower = q.ToLower();
                filtered = filtered.Where(b =>
                    b.ReferenceNo.ToLower().Contains(lower) ||
                    b.PickupLocation.ToLower().Contains(lower) ||
                    b.PassengerName.ToLower().Contains(lower));
            }

            var vm = new DashboardViewModel
            {
                AllBookings      = allBookings,
                Bookings         = filtered.ToList(),
                AvailableDrivers = await _fb.GetAvailableDriversAsync(),
                FirebaseError    = _fb.ErrorMessage
            };

            ViewBag.CurrentStatus = status ?? "All";
            ViewBag.CurrentSearch = q ?? "";
            return View("Dashboard", vm);
        }

        [Route("Dashboard")]
        [Route("Home/Dashboard")]
        public IActionResult Dashboard() => RedirectToAction("Index");

        // ── New Booking ───────────────────────────────────────────────────

        [HttpGet]
        [Route("Bookings/New")]
        public IActionResult NewBooking() => View(new CreateBookingViewModel());

        [HttpPost]
        [Route("Bookings/New")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NewBooking(CreateBookingViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            if (!_fb.IsEnabled)
            {
                ModelState.AddModelError("", "Firebase is not configured.");
                return View(model);
            }
            try
            {
                await _fb.CreateBookingAsync(model);
                TempData["Success"] = "Booking created.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        // ── Assign Driver ─────────────────────────────────────────────────

        [HttpPost]
        [Route("Bookings/Assign")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDriver(string bookingId, string driverId)
        {
            try
            {
                await _fb.AssignDriverAsync(bookingId, driverId);
                TempData["Success"] = "Driver assigned.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("Index");
        }

        // ── Update Status ─────────────────────────────────────────────────

        [HttpPost]
        [Route("Bookings/UpdateStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string bookingId, string newStatus)
        {
            await _fb.UpdateStatusAsync(bookingId, newStatus);
            TempData["Success"] = $"Status updated to {newStatus}.";
            return RedirectToAction("Index");
        }

        // ── Delete Booking ────────────────────────────────────────────────

        [HttpPost]
        [Route("Bookings/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBooking(string bookingId)
        {
            await _fb.DeleteBookingAsync(bookingId);
            TempData["Success"] = "Booking deleted.";
            return RedirectToAction("Index");
        }

        // ── Bookings page ─────────────────────────────────────────────────

        [Route("Home/Bookings")]
        [Route("Bookings")]
        [Route("Bookings/Index")]
        public async Task<IActionResult> Bookings()
        {
            ViewBag.FirebaseError    = _fb.ErrorMessage;
            ViewBag.AvailableDrivers = await _fb.GetAvailableDriversAsync();
            return View(await _fb.GetBookingsAsync());
        }

        // ── Drivers ───────────────────────────────────────────────────────

        [Route("Home/Drivers")]
        [Route("Drivers")]
        [Route("Drivers/Index")]
        public async Task<IActionResult> Drivers()
        {
            ViewBag.FirebaseError = _fb.ErrorMessage;
            return View(await _fb.GetDriversAsync());
        }

        // ── Other pages ───────────────────────────────────────────────────

        [Route("Home/Complaints")]
        [Route("Complaints")]
        public async Task<IActionResult> Complaints()
        {
            ViewBag.FirebaseError = _fb.ErrorMessage;
            return View(await _fb.GetComplaintsAsync());
        }

        [Route("Home/Reports")]
        [Route("Reports")]
        public async Task<IActionResult> Reports()
        {
            return View(await _fb.GetReportsAsync());
        }

        [Route("Home/Settings")]
        [Route("Settings")]
        public IActionResult Settings() => View();

        [HttpPost]
        [Route("Home/Settings")]
        [Route("Settings")]
        [ValidateAntiForgeryToken]
        public IActionResult Settings(string associationName, string officeEmail, string contactNumber)
        {
            TempData["Success"] = "Settings saved successfully.";
            return RedirectToAction("Settings");
        }
    }
}
