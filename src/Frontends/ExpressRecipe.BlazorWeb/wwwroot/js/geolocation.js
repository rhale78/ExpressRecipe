// Geolocation helper functions for AddressSelector component

window.isGeolocationSupported = function () {
    return "geolocation" in navigator;
};

window.getGeolocation = function () {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(new Error("Geolocation is not supported by this browser"));
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve({
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude
                });
            },
            (error) => {
                let message = "Unknown error occurred";
                switch (error.code) {
                    case error.PERMISSION_DENIED:
                        message = "Location permission denied";
                        break;
                    case error.POSITION_UNAVAILABLE:
                        message = "Location information unavailable";
                        break;
                    case error.TIMEOUT:
                        message = "Location request timed out";
                        break;
                }
                reject(new Error(message));
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 0
            }
        );
    });
};
