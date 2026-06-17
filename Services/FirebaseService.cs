using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
            var envServiceAccountPath = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_KEY_PATH");

            if (!string.IsNullOrWhiteSpace(envServiceAccountPath))
            {
                serviceAccountFile = envServiceAccountPath;
            }

            if (string.IsNullOrWhiteSpace(projectId))
            {
                ErrorMessage = "Firebase ProjectId is not configured. Set Firebase:ProjectId in appsettings.json or use environment variable FIREBASE_PROJECT_ID.";
                return;
            }

            var absolutePath = Path.IsPathRooted(serviceAccountFile)
                ? serviceAccountFile
                : Path.Combine(environment.ContentRootPath, serviceAccountFile);

            if (!File.Exists(absolutePath))
            {
                ErrorMessage = $"Firebase service account key file not found at '{absolutePath}'. Put the service account JSON there or update Firebase:ServiceAccountKeyPath.";
                return;
            }

            try
            {
                var credential = GoogleCredential.FromFile(absolutePath);

                if (FirebaseApp.DefaultInstance == null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = credential,
                        ProjectId = projectId
                    });
                }

                Firestore = new FirestoreDbBuilder
                {
                    ProjectId = projectId,
                    Credential = credential
                }.Build();

                IsEnabled = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Firebase initialization failed: {ex.Message}";
                IsEnabled = false;
            }
        }

        public async Task<List<DriverViewModel>> GetDriversAsync()
        {
            if (!IsEnabled || Firestore == null)
            {
                return new List<DriverViewModel>();
            }

            var drivers = new List<DriverViewModel>();
            var collection = Firestore.Collection("drivers");
            var snapshot = await collection.GetSnapshotAsync();

            foreach (var document in snapshot.Documents)
            {
                var data = document.ToDictionary();

                drivers.Add(new DriverViewModel
                {
                    Id = document.Id,
                    Name = data.TryGetValue("name", out var nameValue)
                        ? nameValue?.ToString() ?? "Unknown"
                        : data.TryGetValue("driverName", out var altNameValue)
                            ? altNameValue?.ToString() ?? "Unknown"
                            : "Unknown",
                    TaxiNumber = data.TryGetValue("taxiNumber", out var taxiValue)
                        ? taxiValue?.ToString() ?? "N/A"
                        : data.TryGetValue("taxi", out var altTaxiValue)
                            ? altTaxiValue?.ToString() ?? "N/A"
                            : "N/A",
                    Status = data.TryGetValue("status", out var statusValue)
                        ? statusValue?.ToString() ?? "UNKNOWN"
                        : "UNKNOWN"
                });
            }

            return drivers;
        }
    }
}
