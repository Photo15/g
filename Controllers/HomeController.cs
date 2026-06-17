using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaxiConnectSA.Services;

namespace TaxiConnectSA.Controllers
{
    [Authorize]
    [Route("")]
    [Route("Home")]
    public class HomeController : Controller
    {
        private readonly FirebaseService _firebaseService;

        public HomeController(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
        }

        // Default route (loads Dashboard first)
        [Route("")]
        [Route("Index")]
        [Route("Home/Index")]
        public IActionResult Index()
        {
            return View("Dashboard");
        }

        [Route("Dashboard")]
        [Route("Home/Dashboard")]
        public IActionResult Dashboard()
        {
            return RedirectToAction("Index");
        }

        [Route("Home/Bookings")]
        [Route("Bookings")]
        [Route("Bookings/Index")]
        public IActionResult Bookings()
        {
            return View();
        }

        [Route("Home/Drivers")]
        [Route("Drivers")]
        [Route("Drivers/Index")]
        public async Task<IActionResult> Drivers()
        {
            ViewBag.FirebaseError = _firebaseService.ErrorMessage;
            var drivers = await _firebaseService.GetDriversAsync();
            return View(drivers);
        }

        [Route("Home/Complaints")]
        [Route("Complaints")]
        [Route("Complaints/Index")]
        public IActionResult Complaints()
        {
            return View();
        }

        [Route("Home/Reports")]
        [Route("Reports")]
        [Route("Reports/Index")]
        public IActionResult Reports()
        {
            return View();
        }

        [Route("Home/Settings")]
        [Route("Settings")]
        [Route("Settings/Index")]
        public IActionResult Settings()
        {
            return View();
        }
    }
}