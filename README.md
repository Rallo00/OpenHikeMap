# OpenHikeMap

## The Project
I have tried many services to plan for my hikes and most of them were always missing something or requesting a premium plan to access those features. 
I then started to use AI to build a Windows application that suited my needs, and honestly came out pretty well.

<img width="1381" height="1017" alt="image" src="https://github.com/user-attachments/assets/afcdc3eb-a07c-4a35-8a8f-f75b56c2e41e" />

<img width="1382" height="1019" alt="image" src="https://github.com/user-attachments/assets/82e3103a-1f4f-4e7a-b9b9-7fb746d5091d" />

## Functions
- Most European languages are supported (please advice in case of missing translation, fallback is English anyways);
- Supports metric and imperial measurements;
- Access to OpenStreetMap (several free maps);
- Search registered Cities, Peaks, Bivouacs, Lodges;
- Customizable pins of places with images and colors with tree structure with editability;
- Access to weather with 15 days in advance (Temperature, Wind, Condition, UV, Sunrise/Sunset);
- EUMETNET – Meteoalarm weather warnings for participating European countries, including yellow, orange and red warnings, with route intersection checks;
- Show on map registered hike paths;
- Show on map satellite view;
- Show on map countour lines to quickly figure out altitude is desired OpenStreetMap does not have topographic lines;
- Show on map ruler feature with distance and bearing;
- Show on map ruler feature with bering/azimuth;
- Find webcams near your area to check live feed (opencam.live external website);

- Route planning;
- GPS route (GPX) import and export;
- KML import and export for Google Earth;

<img width="1386" height="1014" alt="image" src="https://github.com/user-attachments/assets/cacce7df-a01a-4e11-bdb8-823d4db0d7bc" />
<img width="639" height="559" alt="image" src="https://github.com/user-attachments/assets/9ed390bb-41d5-4dbf-aec1-25ee2a0c8418" />
<img width="552" height="466" alt="image" src="https://github.com/user-attachments/assets/cb7be187-0c21-4a01-aacf-9a7b6f927f11" />
<img width="451" height="394" alt="image" src="https://github.com/user-attachments/assets/f62e0210-b376-49ff-aa5f-cad8f8bfaa88" />
<img width="495" height="369" alt="image" src="https://github.com/user-attachments/assets/a3ece847-5b1c-4897-be24-73ce7cbe5e8b" />
<img width="400" height="411" alt="image" src="https://github.com/user-attachments/assets/4c58316c-2699-462f-81c2-b6de33e6234c" />


## EUMETNET – Meteoalarm integration

Weather warnings come from the official public [Meteoalarm feeds](https://feeds.meteoalarm.org/). The integration supports the 39 participants listed there and requires no API key. All participating countries are loaded once per application session at startup. The map displays all downloaded warning areas; the planning panel selects only the regions intersecting the current route. The weather reference point is used for warning selection only when there is no route. The weather reference itself is retained independently from this text filter. Active and upcoming yellow, orange and red warnings are shown; expired, superseded, canceled, test and private messages are excluded.

Warning areas use CAP polygons/circles or the official geocodes and aliases published in the [Redistribution Hub](https://meteoalarm.org/en/live/page/redistribution-hub). The geometry catalog is downloaded on demand (about 31 MB initially), cached on disk and checked weekly. The complete warning snapshot is retained in memory for the application session. Map navigation, route changes, settings and the weather Refresh button never redownload warnings. Weather forecasts are requested when a destination completes the route, a route is imported, the weather reference point is changed manually, or the user explicitly presses Refresh. Language and unit changes reformat the downloaded forecast locally. Each successful route calculation sets and saves the weather reference at half of the route’s travelled distance, replacing any previous manual reference. The point can be moved manually afterwards. A weather reference point that has not changed reuses its forecast for the current day; the explicit Refresh button requests a fresh forecast. Original warning texts, the issuing service, issue time and validity interval are preserved in the map popups, with Meteoalarm attribution and source links.

An unavailable feed, unresolvable geometry or an area outside the participating countries is shown explicitly. Incomplete coverage is never presented as a verified absence of warnings. Older preference and backup files remain readable; the replaced overlay preference uses the new `ShowEumetnetAlerts` setting.

Run the offline regression checks with .NET 10:

```powershell
dotnet run --project tests/OpenHikeMap.AlertTests/OpenHikeMap.AlertTests.csproj
node tests/map-render.test.mjs
```

The checks cover CAP lifecycle and languages, awareness levels, geocodes and aliases, route/polygon intersection, a single full-Europe startup load, local planning selection, closed circuits, marker visibility/movement, caching, cancellation and incomplete coverage.
