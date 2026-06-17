// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("click", function (event) {
    const button = event.target.closest("button[data-action-type]");
    if (!button) {
        return;
    }

    const actionType = button.dataset.actionType;
    const bookingRef = button.dataset.bookingRef || "";
    const location = button.dataset.bookingLocation || "";
    const time = button.dataset.bookingTime || "";

    if (actionType === "assign") {
        const drivers = button.dataset.availableDrivers ? JSON.parse(button.dataset.availableDrivers) : [];
        document.getElementById("assignBookingRef").textContent = bookingRef;
        document.getElementById("assignBookingLocation").textContent = location;
        document.getElementById("assignBookingTime").textContent = time;

        const list = document.getElementById("assignDriverList");
        if (drivers.length) {
            list.innerHTML = `
                <h6>Available drivers</h6>
                <ul class="list-group">
                    ${drivers.map(driver => `<li class="list-group-item">${driver.trim()}</li>`).join("")}
                </ul>
            `;
        } else {
            list.innerHTML = "<div class='alert alert-warning mb-0'>No drivers are available at this time.<br>Please try again later or update the booking schedule.</div>";
        }
    }

    if (actionType === "view") {
        const plate = button.dataset.numberPlate || "N/A";
        const status = button.dataset.bookingStatus || "Unknown";
        const complaints = button.dataset.bookingComplaints || "No complaints for this trip.";

        document.getElementById("viewBookingRef").textContent = bookingRef;
        document.getElementById("viewBookingLocation").textContent = location;
        document.getElementById("viewNumberPlate").textContent = plate;
        document.getElementById("viewBookingStatus").textContent = status;
        document.getElementById("viewBookingComplaints").textContent = complaints;
    }

    if (actionType === "track") {
        const progressSection = document.getElementById("trackProgressSection");
        const progressData = button.dataset.trackingProgress ? JSON.parse(button.dataset.trackingProgress) : [];
        const driverAvailable = button.dataset.driverAvailable === "true";

        document.getElementById("trackBookingRef").textContent = bookingRef;

        if (progressData.length) {
            progressSection.innerHTML = `
                <h6>Trip progress</h6>
                <ol class="list-group list-group-numbered">
                    ${progressData.map(step => `<li class="list-group-item">${step}</li>`).join("")}
                </ol>
            `;
        } else if (!driverAvailable) {
            progressSection.innerHTML = "<div class='alert alert-info mb-0'>No active progress is available yet and no driver is currently assigned.</div>";
        } else {
            progressSection.innerHTML = "<div class='alert alert-info mb-0'>Progress is pending. The driver has been notified and will update the trip status shortly.</div>";
        }
    }

    if (actionType === "details") {
        const date = button.dataset.bookingDate || "Unknown";
        const plate = button.dataset.numberPlate || "N/A";
        const status = button.dataset.bookingStatus || "Unknown";
        const completion = button.dataset.bookingCompletion || "Unknown";

        document.getElementById("detailsBookingRef").textContent = bookingRef;
        document.getElementById("detailsBookingLocation").textContent = location;
        document.getElementById("detailsBookingDate").textContent = date;
        document.getElementById("detailsBookingTime").textContent = time;
        document.getElementById("detailsNumberPlate").textContent = plate;
        document.getElementById("detailsBookingStatus").textContent = status;
        document.getElementById("detailsBookingCompletion").textContent = completion;
    }
});
