# ExpressRecipe MAUI App

## Overview

ExpressRecipe MAUI is a full-featured Android/iOS/Windows cross-platform mobile application for dietary management with advanced barcode scanning and AI-powered product recognition.

## Key Features

### ✨ Core Features
- **Barcode Scanner** - Real-time barcode/QR code scanning using device camera
- **AI Product Recognition** - Recognize products from photos using:
  - **Cloud AI** (Azure Computer Vision or backend service)
  - **Local AI** (Ollama with LLaVA vision model) - Privacy-first, works offline
- **Allergen Detection** - Instant allergen warnings with vibration alerts
- **Offline-First** - Full SQLite local storage with background sync
- **Real-Time Updates** - SignalR integration for live notifications and sync status
- **Responsive Design** - Optimized for both phones and tablets

### 📱 Pages

1. **Scanner Page** ⭐
   - Live barcode scanning with camera
   - AI product recognition from photos (take or pick from gallery)
   - Toggle between Cloud AI and Local AI (Ollama)
   - Allergen warning alerts
   - Add to inventory/shopping list
   - Manual search fallback

2. **Home/Dashboard**
   - Quick access to all features
   - Recent scans
   - Allergen summary

3. **Inventory Management**
   - Track food items
   - Expiration date alerts
   - Quantity management

4. **Shopping List**
   - Create and manage lists
   - Check off items
   - Share lists

5. **Recipes**
   - Browse safe recipes
   - Filter by dietary restrictions
   - Allergen-free options

6. **Meal Planning**
   - Weekly meal calendar
   - Nutritional tracking
   - Grocery list generation

7. **Recall Alerts**
   - FDA/USDA recall notifications
   - Product matching
   - Safety alerts

8. **User Profile**
   - Dietary restrictions
   - Allergen management (major groups + individual ingredients)
   - Family members

9. **Settings**
   - App preferences
   - Sync settings
   - AI configuration

## Technologies

### Framework
- **.NET 9 MAUI** - Cross-platform UI framework
- **C# 13** - Modern C# with nullable reference types

### UI & Navigation
- **MAUI Shell** - Navigation pattern
- **MVVM Pattern** - CommunityToolkit.Mvvm
- **CommunityToolkit.Maui** - UI components and behaviors

### Barcode Scanning
- **ZXing.Net.Maui** - Barcode/QR code scanning
- Supports: EAN-13, EAN-8, UPC-A, UPC-E, Code 128, Code 39, QR Code

### AI & Computer Vision
- **Azure Computer Vision** - Cloud-based OCR and image analysis
- **Ollama** - Local AI with LLaVA vision model
  - Privacy-first: No data sent to cloud
  - Works offline
  - Customizable models

### Data & Storage
- **SQLite** (sqlite-net-pcl) - Local database
- **SecureStorage** - Secure token storage
- **Preferences** - App settings

### Networking
- **System.Net.Http** - REST API calls
- **SignalR Client** - Real-time WebSocket connections

### Images
- **FFImageLoading.Maui** - Efficient image caching and loading

## Architecture

```
ExpressRecipe.MAUI/
├── Platforms/              # Platform-specific code
│   └── Android/
│       ├── MainActivity.cs
│       └── AndroidManifest.xml
├── Services/               # Business logic
│   ├── AI/
│   │   ├── ProductRecognitionService.cs   # Orchestrates cloud + local AI
│   │   ├── OllamaService.cs               # Local Ollama integration
│   │   └── CloudAIService.cs              # Azure Computer Vision
│   ├── Camera/
│   │   └── CameraService.cs               # Photo capture
│   ├── BarcodeService.cs                  # Barcode scanning
│   ├── SQLiteDatabase.cs                  # Offline storage
│   ├── OfflineSyncService.cs              # Sync queue management
│   ├── NotificationHubService.cs          # Real-time notifications
│   └── SyncHubService.cs                  # Sync progress updates
├── ViewModels/             # MVVM view models
│   └── ScannerViewModel.cs                # Scanner page logic
├── Views/                  # XAML UI pages
│   ├── ScannerPage.xaml                   # ⭐ Main scanner UI
│   ├── MainPage.xaml                      # Home dashboard
│   └── ...
├── Converters/             # Value converters
│   ├── IsNotNullConverter.cs
│   └── ByteArrayToImageConverter.cs
├── App.xaml                # App resources
├── AppShell.xaml           # Navigation shell
└── MauiProgram.cs          # DI configuration
```

## Setup

### Prerequisites
- .NET 9 SDK
- Visual Studio 2024 or VS Code with MAUI workload
- Android SDK (for Android development)

### Optional: Local AI (Ollama)
For privacy-first, offline AI product recognition:

```bash
# Install Ollama (https://ollama.ai)
# Pull LLaVA vision model
ollama pull llava

# Verify it's running
ollama list
```

Configure in `appsettings.json`:
```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434"  // or your Ollama server
  }
}
```

### Optional: Cloud AI (Azure Computer Vision)
For cloud-based AI recognition:

Configure in `appsettings.json`:
```json
{
  "Azure": {
    "ComputerVision": {
      "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
      "Key": "your-api-key"
    }
  }
}
```

### Build & Run
```bash
# Android
dotnet build -t:Run -f net9.0-android

# iOS (Mac only)
dotnet build -t:Run -f net9.0-ios

# Windows
dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

## Usage

### Scanner Page Workflow

1. **Barcode Scanning** (Default)
   - Point camera at product barcode
   - Automatic detection and product lookup
   - Instant allergen warnings if match found

2. **AI Product Recognition**
   - Tap "📷 Take Photo" to capture product image
   - Tap "🖼️ Pick Photo" to select from gallery
   - Toggle "Local AI" switch to use Ollama instead of cloud
   - AI extracts product name, brand, ingredients

3. **Results**
   - View product details
   - See allergen warnings (vibration + visual alert)
   - Add to inventory or shopping list

### Local vs Cloud AI

**Cloud AI** (Default)
- ✅ High accuracy
- ✅ No setup required
- ❌ Requires internet
- ❌ Sends data to cloud

**Local AI** (Ollama)
- ✅ Complete privacy (no data sent anywhere)
- ✅ Works offline
- ✅ Free (no API costs)
- ❌ Requires Ollama setup
- ❌ Slower on mobile devices

## Responsive Design

The app adapts to different screen sizes:

- **Phone** (< 600dp): Single column layout, compact UI
- **Tablet** (≥ 600dp): Two-column layouts, more details visible
- **Landscape**: Optimized layouts for wide screens

## Offline Support

- All data stored locally in SQLite
- Automatic background sync when online
- Sync queue with retry logic
- Pending operations counter in UI

## Permissions

### Android
- `CAMERA` - Barcode scanning and photo capture
- `INTERNET` - API calls and sync
- `ACCESS_NETWORK_STATE` - Network detection
- `POST_NOTIFICATIONS` - Push notifications
- `VIBRATE` - Allergen alerts

## Performance

- **Barcode Scanning**: < 500ms detection
- **AI Recognition**: 2-5s (cloud), 10-30s (local)
- **Offline Storage**: Instant writes, background sync
- **Image Loading**: Cached with FFImageLoading

## Future Enhancements

- [ ] Voice commands for hands-free scanning
- [ ] AR overlay for allergen visualization
- [ ] Multi-language support (OCR + translations)
- [ ] Wear OS companion app
- [ ] Share scan results to social media
- [ ] Barcode generation for custom products

## Credits

- **ZXing.Net.Maui** - Barcode scanning
- **Ollama** - Local AI inference
- **Azure Computer Vision** - Cloud AI
- **CommunityToolkit** - MVVM helpers

---

**Developer**: ExpressRecipe Team
**License**: MIT
**Version**: 1.0.0
