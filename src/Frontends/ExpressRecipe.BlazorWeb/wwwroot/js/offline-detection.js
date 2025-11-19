// Offline detection module for ExpressRecipe
let dotNetHelper = null;

export function initialize(dotNetReference) {
    dotNetHelper = dotNetReference;

    // Add event listeners for online/offline events
    window.addEventListener('online', updateOnlineStatus);
    window.addEventListener('offline', updateOnlineStatus);

    // Return current status
    return navigator.onLine;
}

function updateOnlineStatus() {
    if (dotNetHelper) {
        dotNetHelper.invokeMethodAsync('UpdateOnlineStatus', navigator.onLine);
    }
}

export function isOnline() {
    return navigator.onLine;
}

export function dispose() {
    window.removeEventListener('online', updateOnlineStatus);
    window.removeEventListener('offline', updateOnlineStatus);
    dotNetHelper = null;
}
