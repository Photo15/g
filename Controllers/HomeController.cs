using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
        private readonly IConfiguration _config;

        public HomeController(FirebaseService fb, IConfiguration config)
        {
            _fb = fb;
            _config = config;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Expose Firebase Web API key to every view via ViewBag
            ViewBag.FirebaseWebApiKey = _config["Firebase:WebApiKey"] ?? "";
            base.OnActionExecuting(context);
        }

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
            ViewBag.FirebaseError   = _fb.ErrorMessage;
            ViewBag.WaitingBookings = (await _fb.GetBookingsAsync())
                .Where(b => b.Status == "WAITING")
                .ToList();
            return View(await _fb.GetDriversAsync());
        }

        [HttpPost]
        [Route("Drivers/SetStatus")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDriverStatus(string driverId, string newStatus)
        {
            if (!string.IsNullOrWhiteSpace(driverId) && _fb.IsEnabled && _fb.Firestore != null)
            {
                await _fb.Firestore.Collection("drivers").Document(driverId)
                    .UpdateAsync(new Dictionary<string, object> { ["status"] = newStatus });
                TempData["Success"] = $"Driver status updated to {newStatus}.";
            }
            return RedirectToAction("Drivers");
        }

        [HttpPost]
        [Route("Drivers/Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDriver(string driverId)
        {
            if (!string.IsNullOrWhiteSpace(driverId) && _fb.IsEnabled && _fb.Firestore != null)
            {
                await _fb.Firestore.Collection("drivers").Document(driverId).DeleteAsync();
                TempData["Success"] = "Driver removed.";
            }
            return RedirectToAction("Drivers");
        }

        [HttpPost]
        [Route("Drivers/Save")]
        [Route("Home/Drivers/Save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDriver(string driverId, string status)
        {
            if (string.IsNullOrWhiteSpace(driverId))
            {
                TempData["Error"] = "Driver ID missing.";
                return RedirectToAction("Drivers");
            }
            if (!_fb.IsEnabled || _fb.Firestore == null)
            {
                TempData["Error"] = "Firebase is not configured.";
                return RedirectToAction("Drivers");
            }
            var newStatus = (status ?? "AVAILABLE").ToUpperInvariant();
            await _fb.Firestore.Collection("drivers").Document(driverId)
                .UpdateAsync(new Dictionary<string, object> { ["status"] = newStatus });
            TempData["Success"] = $"Driver status updated to {newStatus}.";
            return RedirectToAction("Drivers");
        }

        // ── Rank Performance (Trips) ──────────────────────────────────────

        [Route("Home/Rank")]
        [Route("Rank")]
        [Route("RankOverview")]
        [Route("Home/RankOverview")]
        public async Task<IActionResult> RankOverview()
        {
            var drivers = await _fb.GetDriversAsync();
            var trips   = await _fb.GetTripsAsync(1);

            var vm = new RankViewModel
            {
                AvailableDrivers  = drivers.Where(d => d.IsAvailable).ToList(),
                LoadingDrivers    = drivers.Where(d => d.IsLoading).ToList(),
                DepartedDrivers   = drivers.Where(d => d.IsDeparted).ToList(),
                ActiveTrips       = trips.Where(t => t.Status == "DEPARTED").ToList(),
                CompletedTrips    = trips.Where(t => t.Status == "ARRIVED").ToList(),
                TotalPassengersToday = trips.Where(t => t.Status == "ARRIVED"
                    && t.DepartureTime.Date == DateTime.UtcNow.Date).Sum(t => t.Passengers),
                FirebaseError     = _fb.ErrorMessage
            };

            return View(vm);
        }

        [HttpPost]
        [Route("Trips/Depart")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartTaxi(DepartTaxiViewModel model)
        {
            if (!_fb.IsEnabled)
            {
                TempData["Error"] = "Firebase is not configured.";
                return RedirectToAction("RankOverview");
            }
            try
            {
                // Set marshal name from logged-in user
                model.MarshalName = User.Identity?.Name ?? "Marshal";
                await _fb.DepartTaxiAsync(model);
                TempData["Success"] = $"🚌 {model.DriverName} departed — {model.Passengers} passengers · {model.Route}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("RankOverview");
        }

        [HttpPost]
        [Route("Trips/Arrive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkArrived(string tripId)
        {
            await _fb.MarkTripArrivedAsync(tripId);
            TempData["Success"] = "Trip marked as arrived. Driver is now available.";
            return RedirectToAction("RankOverview");
        }

        [HttpPost]
        [Route("Drivers/SetLoading")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLoading(string driverId)
        {
            if (!string.IsNullOrWhiteSpace(driverId) && _fb.IsEnabled && _fb.Firestore != null)
            {
                await _fb.Firestore.Collection("drivers").Document(driverId)
                    .UpdateAsync(new Dictionary<string, object> { ["status"] = "LOADING" });
                TempData["Success"] = "Driver set to Loading — passengers boarding.";
            }
            return RedirectToAction("RankOverview");
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
