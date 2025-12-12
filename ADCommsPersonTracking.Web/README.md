# ADComms Person Tracking - Web UI

A modern Blazor WebAssembly application for the ADComms Person Tracking System, built with MudBlazor components.

## Overview

This web application provides a user-friendly interface to interact with the ADComms Person Tracking API, allowing users to:
- Monitor system health and track statistics
- Submit images with natural language prompts for person tracking
- View and manage active person tracks
- Inspect detailed information about individual tracks

## Prerequisites

- .NET 10 SDK or later
- A running instance of the ADComms Person Tracking API

## Getting Started

### 1. Start the API Server

First, ensure the API server is running:

```bash
cd ../ADCommsPersonTracking.Api
dotnet run
```

The API should be available at `http://localhost:5000` by default.

### 2. Run the Web Application

In a separate terminal:

```bash
cd ADCommsPersonTracking.Web
dotnet run
```

The application will be available at `http://localhost:5087` (or the URL shown in the console).

### 3. Open in Browser

Navigate to the URL shown in the console output (typically `http://localhost:5087`).

## Configuration

The API base URL is configured in `Program.cs`. By default, it's set to `http://localhost:5000/`. To change this:

1. Open `Program.cs`
2. Locate the `AddHttpClient` configuration
3. Update the `BaseAddress` property:

```csharp
builder.Services.AddHttpClient<IPersonTrackingApiService, PersonTrackingApiService>(client =>
{
    client.BaseAddress = new Uri("http://your-api-url:port/");
});
```

## Features

### Dashboard
- **System Health**: Real-time monitoring of API availability
- **Active Tracks Count**: Quick overview of currently tracked persons
- **Quick Actions**: Fast navigation to main features

### Track Submission
- **Image Upload**: Upload images in common formats (JPEG, PNG, etc.)
- **Natural Language Prompts**: Describe the person you want to track (e.g., "find a person in a yellow jacket and black hat with a suitcase")
- **Results Display**: View annotated images with bounding boxes and detection details
- **Match Information**: See confidence scores and matched criteria for each detection

### Active Tracks
- **Data Grid**: Sortable and filterable table of all active tracks
- **Track Details**: Click any row to view detailed information
- **Refresh**: Manually refresh the list to get the latest data

### Track Details
- **Comprehensive Information**: View tracking ID, timestamps, and description
- **Bounding Box Visualization**: Visual representation of the last known position
- **Features**: List of detected features and characteristics
- **Navigation**: Easy breadcrumb navigation back to previous pages

## Technology Stack

- **Framework**: Blazor WebAssembly (.NET 10)
- **UI Library**: MudBlazor 8.15.0
- **HTTP Client**: Microsoft.Extensions.Http
- **Styling**: Material Design via MudBlazor

## Features

- ✨ Modern, responsive Material Design UI
- 🌙 Dark mode by default with light mode toggle
- 📱 Mobile-friendly responsive layout
- 🔄 Real-time API integration
- 📊 Interactive data grids with sorting and filtering
- 🖼️ Image upload and display
- 🎨 Toast notifications for user feedback
- ⚡ Loading indicators for all async operations

## Project Structure

```
ADCommsPersonTracking.Web/
├── Models/              # DTOs matching API models
├── Services/            # API service layer
├── Pages/              # Razor pages/components
├── Layout/             # Layout components
├── wwwroot/            # Static files
├── Program.cs          # Application configuration
└── _Imports.razor      # Global using statements
```

## Development

### Building

```bash
dotnet build
```

### Publishing for Production

```bash
dotnet publish -c Release
```

The published files will be in `bin/Release/net10.0/publish/wwwroot/`.

### Running Tests

Currently, this project focuses on UI functionality. The API tests can be run from the root solution:

```bash
cd ..
dotnet test
```

## Troubleshooting

### API Connection Issues

If the dashboard shows "Offline" status:
1. Verify the API server is running
2. Check that the API base URL in `Program.cs` is correct
3. Ensure there are no firewall issues blocking the connection

### CORS Errors

If you see CORS-related errors in the browser console:
1. Verify the API has CORS enabled (already configured in the API project)
2. Ensure the CORS policy allows requests from your web application's origin

## Browser Support

The application is tested and supported on:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)

## License

See the repository root for license information.
