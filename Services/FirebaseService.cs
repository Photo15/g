using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using TaxiConnectSA.Models;

namespace TaxiConnectSA.Services
{
    public class FirebaseService
    {
        public FirestoreDb? Firestore { get; private set; }
        public bool IsEnabled { get; private set; }
        public string? ErrorMessage { get; private set; }

        public FirebaseService(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var firebaseSection = configuration.GetSection("Firebase");
            var serviceAccountFile = firebaseSection.GetValue<string>("ServiceAccountKeyPath") ?? "firebase-key.json";
            var projectId = firebaseSection.GetValue<string>("ProjectId");
            var envPath = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_KEY_PATH");
            if (!string.IsNullOrWhiteSpace(envPath)) serviceAccountFile = envPath;

            if (string.IsNullOrWhiteSpace(projectId))
            {
                ErrorMessage = "Firebase ProjectId is not configured.";
                return;
            }

            var absolutePath = Path.IsPathRooted(serviceAccountFile)
                ? serviceAccountFile
                : Path.Combine(environment.ContentRootPath, serviceAccountFile);

            if (!File.Exists(absolutePath))
            {
                ErrorMessage = $"Service account key not found at '{absolutePath}'.";
                return;
            }

            try
            {
                var credential = GoogleCredential.FromFile(absolutePath);
                if (FirebaseApp.DefaultInstance == null)
                    FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = projectId });

                Firestore = new FirestoreDbBuilder { ProjectId = projectId, Credential = credential }.Build();
                IsEnabled = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Firebase init failed: {ex.Message}";
            }
        }

        // ── Drivers ───────────────────────────────────────────────────────

        public async Task<List<DriverViewModel>> GetDriversAsync()
        {
            if (!IsEnabled || Firestore == null) return new();

            var drivers = new List<DriverViewModel>();
            var snapshot = await Firestore.Collection("drivers").GetSnapshotAsync();
            foreach (var doc in snapshot.Documents)
            {
                var d = doc.ToDictionary();

                // Dump all keys for the first doc to help diagnose field name mismatches
                // (visible in application logs)
                if (drivers.Count == 0)
                {
                    var keys = string.Join(", ", d.Keys);
                    Console.WriteLine($"[FirebaseService] Driver doc keys: {keys}");
                }

                // Name — cover every common casing variant
                var name = GetStr(d,
                    "name", "Name", "driverName", "DriverName",
                    "fullName", "FullName", "driver_name", "displayName") ?? "";

                // Status
                var status = (GetStr(d, "status", "Status", "driverStatus") ?? "UNKNOWN").ToUpperInvariant();

                // License plate — cover taxiNumber, taxi, plate, registration etc.
                var plate = GetStr(d,
                    "licensePlate", "LicensePlate", "license_plate",
                    "taxiNumber", "TaxiNumber", "taxi", "Taxi",
                    "registration", "Registration", "plateNumber", "plate") ?? "";

                // Vehicle
                var vehicle = GetStr(d,
                    "vehicle", "Vehicle", "vehicleType", "VehicleType",
                    "carType", "CarType", "car", "Car",
                    "vehicleModel", "model", "Model") ?? "";

                // Phone
                var phone = GetStr(d,
                    "phone", "Phone", "phoneNumber", "PhoneNumber",
                    "phone_number", "contact", "Contact",
                    "mobile", "Mobile", "cellphone") ?? "";

                // Rating
                var ratingStr = GetStr(d, "rating", "Rating", "score", "Score") ?? "0";
                double.TryParse(ratingStr, out var rating);

                drivers.Add(new DriverViewModel
                {
                    Id           = doc.Id,
                    Name         = string.IsNullOrEmpty(name) ? $"Driver ({doc.Id[..6]})" : name,
                    Vehicle      = vehicle,
                    LicensePlate = plate,
                    Phone        = phone,
                    TaxiNumber   = plate,
                    Status       = status,
                    Rating       = rating,
                    Route        = GetStr(d, "route", "Route", "destination") ?? "",
                    TaxiRank     = GetStr(d, "taxiRank", "TaxiRank", "rank", "Rank", "rankName") ?? "",
                    RawFields    = d
                });
            }
            return drivers;
        }

        public async Task<List<DriverViewModel>> GetAvailableDriversAsync()
        {
            var all = await GetDriversAsync();
            return all.Where(d => d.IsAvailable).ToList();
        }
        // ── Bookings ──────────────────────────────────────────────────────

        public async Task<List<BookingModel>> GetBookingsAsync()
        {
            if (!IsEnabled || Firestore == null) return new();

            var list = new List<BookingModel>();
            var snapshot = await Firestore.Collection("bookings")
                .OrderByDescending("bookingDate").GetSnapshotAsync();
            foreach (var doc in snapshot.Documents)
                list.Add(MapBooking(doc.Id, doc.ToDictionary()));
            return list;
        }

        public async Task<BookingModel?> GetBookingAsync(string id)
        {
            if (!IsEnabled || Firestore == null) return null;
            var doc = await Firestore.Collection("bookings").Document(id).GetSnapshotAsync();
            return doc.Exists ? MapBooking(doc.Id, doc.ToDictionary()) : null;
        }

        public async Task<string> CreateBookingAsync(CreateBookingViewModel vm)
        {
            if (!IsEnabled || Firestore == null)
                throw new InvalidOperationException("Firebase is not enabled.");

            var today = DateTime.UtcNow.Date;
            var todaySnap = await Firestore.Collection("bookings")
                .WhereGreaterThanOrEqualTo("bookingDate", today)
                .WhereLessThan("bookingDate", today.AddDays(1))
                .GetSnapshotAsync();

            var refNo = $"T{today:yyyyMMdd}-{(todaySnap.Count + 1):D4}";

            var data = new Dictionary<string, object>
            {
                ["referenceNo"]      = refNo,
                ["passengerName"]    = vm.PassengerName,
                ["passengerPhone"]   = vm.PassengerPhone,
                ["pickupLocation"]   = vm.PickupLocation,
                ["passengers"]       = vm.Passengers,
                ["status"]           = "WAITING",
                ["assignedDriver"]   = "",
                ["assignedDriverId"] = "",
                ["taxiNumber"]       = "",
                ["bookingDate"]      = DateTime.UtcNow,
                ["notes"]            = vm.Notes ?? ""
            };

            var docRef = await Firestore.Collection("bookings").AddAsync(data);
            return docRef.Id;
        }

        public async Task AssignDriverAsync(string bookingId, string driverId)
        {
            if (!IsEnabled || Firestore == null) return;

            var driverDoc = await Firestore.Collection("drivers").Document(driverId).GetSnapshotAsync();
            if (!driverDoc.Exists) throw new InvalidOperationException("Driver not found.");

            var dd = driverDoc.ToDictionary();
            var driverName = GetStr(dd, "name", "driverName") ?? "";
            var taxiNumber = GetStr(dd, "taxiNumber", "taxi") ?? "";

            // Release old driver if reassigning
            var existing = await GetBookingAsync(bookingId);
            if (existing != null && !string.IsNullOrEmpty(existing.AssignedDriverId)
                && existing.AssignedDriverId != driverId)
            {
                await Firestore.Collection("drivers").Document(existing.AssignedDriverId)
                    .UpdateAsync(new Dictionary<string, object> { ["status"] = "IDLE" });
            }

            await Firestore.Collection("bookings").Document(bookingId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    ["status"]           = "ASSIGNED",
                    ["assignedDriver"]   = driverName,
                    ["assignedDriverId"] = driverId,
                    ["taxiNumber"]       = taxiNumber
                });

            await Firestore.Collection("drivers").Document(driverId)
                .UpdateAsync(new Dictionary<string, object> { ["status"] = "BUSY" });
        }

        public async Task UpdateStatusAsync(string bookingId, string newStatus)
        {
            if (!IsEnabled || Firestore == null) return;

            var booking = await GetBookingAsync(bookingId);
            if (booking == null) return;

            if (newStatus == "COMPLETED" && !string.IsNullOrEmpty(booking.AssignedDriverId))
            {
                await Firestore.Collection("drivers").Document(booking.AssignedDriverId)
                    .UpdateAsync(new Dictionary<string, object> { ["status"] = "IDLE" });
            }

            await Firestore.Collection("bookings").Document(bookingId)
                .UpdateAsync(new Dictionary<string, object> { ["status"] = newStatus });
        }

        public async Task DeleteBookingAsync(string bookingId)
        {
            if (!IsEnabled || Firestore == null) return;
            await Firestore.Collection("bookings").Document(bookingId).DeleteAsync();
        }

        // ── Complaints ────────────────────────────────────────────────────

        public async Task<List<ComplaintModel>> GetComplaintsAsync()
        {
            if (!IsEnabled || Firestore == null) return new();

            var list = new List<ComplaintModel>();
            var snapshot = await Firestore.Collection("complaints")
                .OrderByDescending("createdAt").GetSnapshotAsync();

            foreach (var doc in snapshot.Documents)
            {
                var d = doc.ToDictionary();
                DateTime created = DateTime.UtcNow;
                if (d.TryGetValue("createdAt", out var ca))
                {
                    if (ca is Timestamp ts) created = ts.ToDateTime();
                    else if (ca is DateTime dt) created = dt;
                }
                list.Add(new ComplaintModel
                {
                    Id            = doc.Id,
                    ComplaintRef  = GetStr(d, "complaintRef", "id") ?? doc.Id[..6].ToUpper(),
                    PassengerName = GetStr(d, "passengerName", "passenger") ?? "Unknown",
                    BookingRef    = GetStr(d, "bookingRef", "booking") ?? "",
                    Category      = GetStr(d, "category") ?? "General",
                    Description   = GetStr(d, "description", "complaint") ?? "",
                    Status        = (GetStr(d, "status") ?? "OPEN").ToUpperInvariant(),
                    CreatedAt     = created
                });
            }
            return list;
        }

        // ── Reports ───────────────────────────────────────────────────────

        public async Task<ReportsViewModel> GetReportsAsync()
        {
            var bookings   = await GetBookingsAsync();
            var drivers    = await GetDriversAsync();
            var complaints = await GetComplaintsAsync();

            return new ReportsViewModel
            {
                TotalBookings    = bookings.Count,
                CompletedTrips   = bookings.Count(b => b.Status == "COMPLETED"),
                WaitingCount     = bookings.Count(b => b.Status == "WAITING"),
                AssignedCount    = bookings.Count(b => b.Status == "ASSIGNED"),
                PickedUpCount    = bookings.Count(b => b.Status == "PICKED UP"),
                TotalDrivers     = drivers.Count,
                TotalComplaints  = complaints.Count,
                RecentBookings   = bookings.Take(10).ToList(),
                RecentComplaints = complaints.Take(5).ToList(),
                FirebaseError    = ErrorMessage
            };
        }

        // ── Trips (SA Rank Workflow) ──────────────────────────────────────

        public async Task<string> DepartTaxiAsync(DepartTaxiViewModel vm)
        {
            if (!IsEnabled || Firestore == null)
                throw new InvalidOperationException("Firebase is not enabled.");

            var now = DateTime.UtcNow;

            // Generate trip reference
            var todayStr = now.ToString("yyyyMMdd");
            var todaySnap = await Firestore.Collection("trips")
                .WhereGreaterThanOrEqualTo("departureTime", now.Date)
                .WhereLessThan("departureTime", now.Date.AddDays(1))
                .GetSnapshotAsync();

            var tripRef = $"TR-{todayStr}-{(todaySnap.Count + 1):D4}";

            var tripData = new Dictionary<string, object>
            {
                ["tripRef"]       = tripRef,
                ["driverId"]      = vm.DriverId,
                ["driverName"]    = vm.DriverName,
                ["licensePlate"]  = vm.LicensePlate,
                ["route"]         = vm.Route,
                ["taxiRank"]      = vm.TaxiRank,
                ["passengers"]    = vm.Passengers,
                ["marshalName"]   = vm.MarshalName,
                ["status"]        = "DEPARTED",
                ["departureTime"] = now,
                ["arrivalTime"]   = (object?)null
            };

            var docRef = await Firestore.Collection("trips").AddAsync(tripData);

            // Update driver status to DEPARTED
            await Firestore.Collection("drivers").Document(vm.DriverId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    ["status"]        = "DEPARTED",
                    ["lastTripRef"]   = tripRef,
                    ["lastTripId"]    = docRef.Id
                });

            return docRef.Id;
        }

        public async Task MarkTripArrivedAsync(string tripId)
        {
            if (!IsEnabled || Firestore == null) return;

            var tripDoc = await Firestore.Collection("trips").Document(tripId).GetSnapshotAsync();
            if (!tripDoc.Exists) return;

            var d = tripDoc.ToDictionary();
            var driverId = GetStr(d, "driverId") ?? "";

            await Firestore.Collection("trips").Document(tripId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    ["status"]      = "ARRIVED",
                    ["arrivalTime"] = DateTime.UtcNow
                });

            // Return driver to AVAILABLE
            if (!string.IsNullOrEmpty(driverId))
            {
                await Firestore.Collection("drivers").Document(driverId)
                    .UpdateAsync(new Dictionary<string, object> { ["status"] = "AVAILABLE" });
            }
        }

        public async Task<List<TripModel>> GetTripsAsync(int daysBack = 1)
        {
            if (!IsEnabled || Firestore == null) return new();

            var since = DateTime.UtcNow.Date.AddDays(-daysBack);
            var snapshot = await Firestore.Collection("trips")
                .WhereGreaterThanOrEqualTo("departureTime", since)
                .OrderByDescending("departureTime")
                .GetSnapshotAsync();

            var trips = new List<TripModel>();
            foreach (var doc in snapshot.Documents)
            {
                var d = doc.ToDictionary();
                DateTime dep = DateTime.UtcNow;
                DateTime? arr = null;

                if (d.TryGetValue("departureTime", out var dt))
                {
                    if (dt is Timestamp ts) dep = ts.ToDateTime();
                    else if (dt is DateTime dtv) dep = dtv;
                }
                if (d.TryGetValue("arrivalTime", out var at) && at != null)
                {
                    if (at is Timestamp ts2) arr = ts2.ToDateTime();
                    else if (at is DateTime atv) arr = atv;
                }

                trips.Add(new TripModel
                {
                    Id            = doc.Id,
                    TripRef       = GetStr(d, "tripRef") ?? doc.Id[..8],
                    DriverId      = GetStr(d, "driverId") ?? "",
                    DriverName    = GetStr(d, "driverName") ?? "Unknown",
                    LicensePlate  = GetStr(d, "licensePlate") ?? "",
                    Route         = GetStr(d, "route") ?? "",
                    TaxiRank      = GetStr(d, "taxiRank") ?? "",
                    Passengers    = int.TryParse(GetStr(d, "passengers"), out var p) ? p : 0,
                    MarshalName   = GetStr(d, "marshalName") ?? "",
                    Status        = (GetStr(d, "status") ?? "DEPARTED").ToUpperInvariant(),
                    DepartureTime = dep,
                    ArrivalTime   = arr
                });
            }
            return trips;
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string? GetStr(Dictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && v != null) return v.ToString();
            return null;
        }

        private static BookingModel MapBooking(string id, Dictionary<string, object> d)
        {
            DateTime date = DateTime.UtcNow;
            if (d.TryGetValue("bookingDate", out var bd))
            {
                if (bd is Timestamp ts) date = ts.ToDateTime();
                else if (bd is DateTime dt) date = dt;
            }

            return new BookingModel
            {
                Id               = id,
                ReferenceNo      = GetStr(d, "referenceNo") ?? id,
                PassengerName    = GetStr(d, "passengerName") ?? "",
                PassengerPhone   = GetStr(d, "passengerPhone") ?? "",
                PickupLocation   = GetStr(d, "pickupLocation") ?? "",
                Passengers       = int.TryParse(GetStr(d, "passengers"), out var p) ? p : 1,
                Status           = (GetStr(d, "status") ?? "WAITING").ToUpperInvariant(),
                AssignedDriver   = GetStr(d, "assignedDriver") ?? "",
                AssignedDriverId = GetStr(d, "assignedDriverId") ?? "",
                TaxiNumber       = GetStr(d, "taxiNumber") ?? "",
                BookingDate      = date,
                Notes            = GetStr(d, "notes") ?? ""
            };
        }
    }
}
