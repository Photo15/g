using System.ComponentModel.DataAnnotations;

namespace TaxiConnectSA.Models
{
    public class BookingModel
    {
        public string Id { get; set; } = string.Empty;
        public string ReferenceNo { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public string PickupLocation { get; set; } = string.Empty;
        public int Passengers { get; set; } = 1;
        public string Status { get; set; } = "WAITING";
        public string AssignedDriver { get; set; } = string.Empty;
        public string AssignedDriverId { get; set; } = string.Empty;
        public string TaxiNumber { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; } = DateTime.UtcNow;
        public string Notes { get; set; } = string.Empty;

        public double MinutesWaiting => (DateTime.UtcNow - BookingDate).TotalMinutes;
        public bool IsUrgent => Status == "WAITING" && MinutesWaiting > 6;

        public string TimeAgo()
        {
            var mins = (int)MinutesWaiting;
            if (mins < 1) return "just now";
            if (mins == 1) return "1 min ago";
            if (mins < 60) return $"{mins} min ago";
            var hrs = mins / 60;
            return hrs == 1 ? "1 hr ago" : $"{hrs} hrs ago";
        }
    }

    public class CreateBookingViewModel
    {
        [Required(ErrorMessage = "Passenger name is required.")]
        public string PassengerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        public string PassengerPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pickup location is required.")]
        public string PickupLocation { get; set; } = string.Empty;

        [Range(1, 20, ErrorMessage = "Passengers must be between 1 and 20.")]
        public int Passengers { get; set; } = 1;

        public string Notes { get; set; } = string.Empty;
    }

    public class DashboardViewModel
    {
        // All bookings (used for status tile counts and urgent list)
        public List<BookingModel> AllBookings { get; set; } = new();
        // Filtered bookings (shown in the table)
        public List<BookingModel> Bookings { get; set; } = new();
        public List<DriverViewModel> AvailableDrivers { get; set; } = new();
        public string? FirebaseError { get; set; }

        public int WaitingCount   => AllBookings.Count(b => b.Status == "WAITING");
        public int AssignedCount  => AllBookings.Count(b => b.Status == "ASSIGNED");
        public int PickedUpCount  => AllBookings.Count(b => b.Status == "PICKED UP");
        public int CompletedCount => AllBookings.Count(b => b.Status == "COMPLETED");
        public List<BookingModel> UrgentBookings => AllBookings.Where(b => b.IsUrgent).ToList();
    }
}

namespace TaxiConnectSA.Models
{
    public class ComplaintModel
    {
        public string Id { get; set; } = string.Empty;
        public string ComplaintRef { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string BookingRef { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "OPEN";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string TimeAgo()
        {
            var mins = (int)(DateTime.UtcNow - CreatedAt).TotalMinutes;
            if (mins < 1) return "just now";
            if (mins < 60) return $"{mins} min ago";
            var hrs = mins / 60;
            if (hrs < 24) return hrs == 1 ? "1 hr ago" : $"{hrs} hrs ago";
            var days = hrs / 24;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }
    }

    public class ReportsViewModel
    {
        public int TotalBookings { get; set; }
        public int CompletedTrips { get; set; }
        public int TotalDrivers { get; set; }
        public int TotalComplaints { get; set; }
        public int WaitingCount { get; set; }
        public int AssignedCount { get; set; }
        public int PickedUpCount { get; set; }
        public List<BookingModel> RecentBookings { get; set; } = new();
        public List<ComplaintModel> RecentComplaints { get; set; } = new();
        public double CompletionRate => TotalBookings == 0 ? 0 : Math.Round((double)CompletedTrips / TotalBookings * 100, 1);
        public string? FirebaseError { get; set; }
    }
}
