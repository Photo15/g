namespace TaxiConnectSA.Models
{
    /// <summary>
    /// Represents a single completed taxi trip from the rank.
    /// Written to the Firestore "trips" collection when marshal clicks "Depart Taxi".
    /// </summary>
    public class TripModel
    {
        public string Id { get; set; } = string.Empty;
        public string TripRef { get; set; } = string.Empty;
        public string DriverId { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;          // e.g. "Boulders → Tembisa"
        public string TaxiRank { get; set; } = string.Empty;       // e.g. "Boulders"
        public int Passengers { get; set; }
        public string MarshalName { get; set; } = string.Empty;
        public string Status { get; set; } = "DEPARTED";           // DEPARTED | ARRIVED
        public DateTime DepartureTime { get; set; } = DateTime.UtcNow;
        public DateTime? ArrivalTime { get; set; }

        public string DepartureFormatted =>
            DepartureTime.ToLocalTime().ToString("HH:mm");

        public string ArrivalFormatted =>
            ArrivalTime.HasValue ? ArrivalTime.Value.ToLocalTime().ToString("HH:mm") : "—";

        public string TimeAgo()
        {
            var mins = (int)(DateTime.UtcNow - DepartureTime).TotalMinutes;
            if (mins < 1) return "just now";
            if (mins < 60) return $"{mins} min ago";
            var hrs = mins / 60;
            return hrs == 1 ? "1 hr ago" : $"{hrs} hrs ago";
        }
    }

    /// <summary>View model for the Rank Performance / Trips page.</summary>
    public class RankViewModel
    {
        public List<TripModel> ActiveTrips { get; set; } = new();   // DEPARTED
        public List<TripModel> CompletedTrips { get; set; } = new(); // ARRIVED today
        public List<DriverViewModel> AvailableDrivers { get; set; } = new();
        public List<DriverViewModel> LoadingDrivers { get; set; } = new();
        public List<DriverViewModel> DepartedDrivers { get; set; } = new();
        public int TotalPassengersToday { get; set; }
        public string? FirebaseError { get; set; }
    }

    /// <summary>DTO for when marshal clicks "Depart Taxi".</summary>
    public class DepartTaxiViewModel
    {
        public string DriverId { get; set; } = string.Empty;
        public string DriverName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string TaxiRank { get; set; } = string.Empty;
        public int Passengers { get; set; } = 15;
        public string MarshalName { get; set; } = string.Empty;
    }
}
