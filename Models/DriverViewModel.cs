namespace TaxiConnectSA.Models
{
    /// <summary>
    /// Driver statuses for SA taxi rank workflow:
    /// AVAILABLE → LOADING → DEPARTED → ARRIVED (back to AVAILABLE)
    /// </summary>
    public static class DriverStatus
    {
        public const string Available = "AVAILABLE";
        public const string Loading   = "LOADING";
        public const string Departed  = "DEPARTED";
        public const string Arrived   = "ARRIVED";
    }

    public class DriverViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Vehicle { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string TaxiNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;    // e.g. "Boulders → Tembisa"
        public string TaxiRank { get; set; } = string.Empty; // e.g. "Boulders"
        public double Rating { get; set; } = 0;

        public Dictionary<string, object> RawFields { get; set; } = new();

        // ── Status helpers ────────────────────────────────────────────────
        public bool IsAvailable => Status is "AVAILABLE" or "IDLE" or "ACTIVE";
        public bool IsLoading   => Status == "LOADING";
        public bool IsDeparted  => Status == "DEPARTED";
        public bool IsArrived   => Status == "ARRIVED";

        public string StatusLabel => Status switch
        {
            "AVAILABLE" or "IDLE" or "ACTIVE" => "AVAILABLE",
            "LOADING"  => "LOADING",
            "DEPARTED" => "DEPARTED",
            "ARRIVED"  => "ARRIVED",
            "BUSY"     => "ON TRIP",
            _ => Status
        };

        public string StatusCss => Status switch
        {
            "AVAILABLE" or "IDLE" or "ACTIVE" => "status-picked",   // green
            "LOADING"  => "status-waiting",                          // yellow
            "DEPARTED" => "status-assigned",                         // blue
            "ARRIVED"  => "status-completed",                        // grey
            "BUSY"     => "status-assigned",
            _ => "status-waiting"
        };

        public string GetField(params string[] keys)
        {
            foreach (var k in keys)
                if (RawFields.TryGetValue(k, out var v) && v != null)
                    return v.ToString() ?? "";
            foreach (var k in keys)
            {
                var match = RawFields.Keys.FirstOrDefault(rk =>
                    string.Equals(rk, k, StringComparison.OrdinalIgnoreCase));
                if (match != null && RawFields[match] != null)
                    return RawFields[match].ToString() ?? "";
            }
            return "";
        }
    }
}
