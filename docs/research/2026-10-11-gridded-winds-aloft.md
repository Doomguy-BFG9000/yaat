# Gridded winds aloft as a live weather source

Date: 2026-10-11. Sources were read and requests run on 2026-10-11/12 UTC (the live requests below ran at about 2026-10-12 04:45Z). Today's weather pipeline: [`../weather-and-wind.md`](../weather-and-wind.md) (`LiveWeatherService` fetches `/api/data/metar` and `/api/data/windtemp`; the FD winds are vector-averaged into one `WeatherProfile` per room).

## Question

Can YAAT's desktop client, at scenario load, fetch a gridded upper-air model (U/V wind and temperature by pressure level, surface to about FL450) for one ARTCC's box (at most about 600 x 600 nm; ZOA is roughly lat 34-42, lon -126 to -117), so winds vary across the airspace and with altitude? Or must the next release fall back to per-station interpolation of the FAA FD winds it already fetches?

## Summary

- **Doable.** NOAA's NOMADS GRIB filter returns exactly the variables, levels and box asked for in one HTTP GET, no key, about 25 KB for 5 levels and 98 KB for 20 levels (1000-150 hPa) of GFS 0.25°, measured. The filter re-encodes the subset with GRIB2 **simple packing** (template 5.0) on a regular lat/lon grid (template 3.0), measured, so decoding needs no JPEG2000 or complex-packing support.
- The access limit is 120 hits/minute per IP; one request per scenario load per install is far below it.
- AWS Open Data is the fallback host (anonymous, byte-range by `.idx`), but each wanted field arrives as a whole-CONUS (RAP) or whole-globe (GFS) message in JPEG2000 or complex packing, so it costs more bytes and a full decoder.
- Open-Meteo works technically (measured: 90 points x 5 levels x 3 variables x 6 hours in one call, 138 KB JSON), and its free tier allows non-commercial apps, but it adds a third-party dependency with call-weighting and aggregate-limit questions the terms leave open.
- aviationweather.gov exposes no gridded winds through its Data API.
- **Recommended source: NOMADS `filter_gfs_0p25.pl`**, request given in the verdict.

## Findings

### 1. NOMADS GRIB filter (RAP 13 km, GFS 0.25°)

**Request shape (run).** The filter CGI takes `dir`, `file`, one `var_<NAME>=on` per variable, one `lev_<N>_mb=on` per level, and `subregion=&toplat=&leftlon=&rightlon=&bottomlat=`. GFS longitudes are 0-360 (ZOA's -126..-117 is 234..243).

GFS, 5 levels, run against cycle 00Z 2026-10-12, analysis hour:

```
https://nomads.ncep.noaa.gov/cgi-bin/filter_gfs_0p25.pl?dir=%2Fgfs.20261012%2F00%2Fatmos&file=gfs.t00z.pgrb2.0p25.f000&var_UGRD=on&var_VGRD=on&var_TMP=on&lev_850_mb=on&lev_700_mb=on&lev_500_mb=on&lev_300_mb=on&lev_250_mb=on&subregion=&toplat=42&leftlon=234&rightlon=243&bottomlat=34
```

Observed: HTTP 200, 24,973 bytes, 15 GRIB2 messages, 1,221 grid points each (37 x 33 at 0.25°), data representation template 5.0 (simple packing), grid template 3.0 (lat/lon). The same request for 20 levels (1000, 975, 950, 925, 900, 850, 800 ... 200, 150 hPa) on `f003`: HTTP 200, 98,058 bytes, 60 messages, template 5.0, 2.0 s.

RAP, same variables and levels:

```
https://nomads.ncep.noaa.gov/cgi-bin/filter_rap.pl?dir=%2Frap.20261012&file=rap.t01z.awp130pgrbf00.grib2&var_UGRD=on&var_VGRD=on&var_TMP=on&lev_850_mb=on&lev_700_mb=on&lev_500_mb=on&lev_300_mb=on&lev_250_mb=on&subregion=&toplat=42&leftlon=-126&rightlon=-117&bottomlat=34
```

Observed: HTTP 200, 101,126 bytes, 15 messages, 5,538 points each, template 5.0, but grid template **3.30 (Lambert conformal)**: the RAP subset needs projection math to locate points, and its U/V are grid-relative on that grid (a GRIB convention; not checked in this file's resolution flags, untested).

**Cadence and latency (observed from the directory listing).** `https://nomads.ncep.noaa.gov/pub/data/nccf/com/rap/prod/rap.20261012/` showed `rap.t02z.awp130pgrbf00.grib2` stamped 02:46 and `rap.t03z…` stamped 03:46: RAP runs hourly and its analysis appears about 46 minutes after the cycle. The `gfs.20261012/00/` directory was stamped 03:33: GFS runs every 6 hours and appears about 3.5 hours after the cycle. RAP hourly cadence is also stated by the AWS registry entry ("UpdateFrequency: Hourly"), GFS's as "4 times a day, every 6 hours starting at midnight UTC" (source 2).

**Usage limit.** NWS Service Change Notice 21-32 (https://www.weather.gov/media/notification/pdf2/scn21-32nomad_changes.pdf): "The usage policy will restrict individual user access to 120 hits/minute for only the websites listed above." and "This policy is applied as a sum of hits per user Internet Protocol (IP) address across all sites listed above." The NOMADS site's own fair-use page (`https://nomads.ncep.noaa.gov/info.php?page=fairuse`) rendered no policy text when fetched.

The NWS abusive-user page (https://www.weather.gov/abusive-user-block): "we may find it necessary to block IP addresses or query types. Users who find that their queries are blocked should send an email to sdm@noaa.gov." and "If a file is not available do not immediately retry the following second, pause for an appropriate interval based on your data refresh rate."

Since the limit is per IP, independent installs each have their own budget; YAAT's one request per scenario load is two orders of magnitude below it. No stated per-day cap or key requirement was found (unverified that none exists).

**Terms.** NOAA data is public; the licence sentence NOAA attaches to the same model output on NODD is quoted under source 2. No separate NOMADS licence page was found.

**.NET cost.** Simple packing (template 5.0) on a lat/lon grid is the easiest GRIB2 case: value = (R + X·2^E)/10^D over packed integers, plus the section-6 bitmap. A hand decoder is a small, self-contained parser, or NGrib (source 5) decodes it.

### 2. NOAA model data on AWS Open Data

**Buckets and access (run).** Anonymous HTTPS GET works with no credentials: `https://noaa-gfs-bdp-pds.s3.amazonaws.com/gfs.20261012/00/atmos/gfs.t00z.pgrb2.0p25.f000.idx` returned 200, and lines such as `468:337370063:d=2026101200:UGRD:850 mb:anl:` give each message's start byte (the next line's offset ends it). A `Range: bytes=337370063-337941485` GET returned 206 and 571,423 bytes: one global UGRD 850 hPa field, 1,038,240 points, packing template **5.3** (complex packing with spatial differencing).

RAP: `https://noaa-rap-pds.s3.amazonaws.com/rap.20261012/rap.t03z.awp130pgrbf00.grib2` (18,025,813 bytes) with `.idx`; U and V share one message (`42.1`/`42.2` at the same offset), and the file's fields use template **5.40 (JPEG2000)**, measured.

So 15 fields cost about 15 range requests and roughly 8.5 MB for GFS (estimate from the one measured field; untested in total), with no spatial subsetting, versus 25 KB from NOMADS.

**Terms.** AWS Open Data Registry entry for GFS (https://raw.githubusercontent.com/awslabs/open-data-registry/main/datasets/noaa-gfs-bdp-pds.yaml): "NOAA data disseminated through NODD are open to the public and can be used as desired." and "NOAA requests attribution for the use or dissemination of unaltered NOAA data. However, it is not permissible to state or imply endorsement by or affiliation with NOAA." The RAP entry (https://raw.githubusercontent.com/awslabs/open-data-registry/main/datasets/noaa-rap.yaml) carries the same first sentence and `ARN: arn:aws:s3:::noaa-rap-pds`. No rate limit is stated in either entry.

**.NET cost.** Needs a decoder for 5.3 (GFS) or 5.40 JPEG2000 (RAP), plus the Lambert projection for RAP.

### 3. Open-Meteo forecast API (pressure levels)

**Request shape (run).** Docs (https://open-meteo.com/en/docs/gfs-api): "Multiple coordinates can be comma separated. E.g. &latitude=52.52,48.85&longitude=13.41,2.35 . To return data for multiple locations the JSON output changes to a list of structures." Levels listed on that page run 1000, 975, 950 … 25 hPa steps … 250, 225, 200, 175, 150, 125, 100, 70, 50, 40, 30, 20, 15, 10 hPa. Models listed include GFS Seamless, GFS 0.11°/0.25°, HRRR Conus. The page's table: "GFS Pressure Variables 🌍 Global 0.25° (~25 km) Hourly … Every 6 hours" and "HRRR Conus CONUS 3 km Hourly 18 hours, (48 hours for 0, 6, 12, 18Z) Every hour".

Run, 90 points (1° grid, 9 x 10) over ZOA, 15 variables, 6 hours:

```
https://api.open-meteo.com/v1/gfs?latitude=34,34,…,42&longitude=-126,-125,…,-117&hourly=wind_speed_850hPa,wind_direction_850hPa,temperature_850hPa,wind_speed_700hPa,wind_direction_700hPa,temperature_700hPa,wind_speed_500hPa,wind_direction_500hPa,temperature_500hPa,wind_speed_300hPa,wind_direction_300hPa,temperature_300hPa,wind_speed_250hPa,wind_direction_250hPa,temperature_250hPa&wind_speed_unit=kn&forecast_hours=6&models=gfs_seamless
```

Observed: HTTP 200, 137,793 bytes JSON, 2.5 s. A 5-point version was 7,636 bytes. `models=gfs_hrrr` at 150/250/925 hPa returned 200 (440 bytes, one point).

**Terms (https://open-meteo.com/en/terms).** "Less than 10'000 API calls per day, 5'000 per hour and 600 per minute." "You may only use the free API services for non-commercial purposes." "You accept to the CC-BY 4.0 licence, as specified in the licence conditions." "We reserve the right to block applications and IP addresses that misuse our service without prior notice." Its examples of non-commercial use include "Using our service for private or non-profit websites or apps that do not have subscriptions or advertising."

YAAT (free, open source, no ads or subscription) fits that example; no API key is needed for the free tier. The terms do not say whether the limits apply per IP or per application across all its users (unverified).

Call weighting (https://open-meteo.com/en/pricing): "Requests for data covering more than 10 weather variables or extending over a period of more than 2 weeks for a single location are considered multiple API calls." So the 15-variable request is at least 1.5 calls; whether each of the 90 locations counts as a call is not stated on that page (unverified). The server is AGPLv3 and self-hostable (pricing page: "The server code is open-source under AGPLv3; the weather data is CC BY 4.0.").

**.NET cost.** `System.Text.Json` only; speed/direction already in knots and degrees.

### 4. aviationweather.gov gridded products

The Data API product table (https://aviationweather.gov/data/api/) lists METAR, TAF, PIREPs, SIGMETs, G-AIRMETs, AIRMETs, Area Forecast, AFD, CWA, MIS, TCF, "Legacy wind/temp tables", station/airport/navaid info. No gridded wind product is listed. Its restrictions: "All requests are rate limited to 100 requests per minute." So within aviationweather.gov, the FD tables (`/api/data/windtemp`, measured 378 bytes for `region=sfo&level=high&fcst=06`) are the only wind source.

### 5. .NET GRIB2 decoding

**NGrib** (https://github.com/nmangue/NGrib, NuGet `NGrib`): licence LGPL-3.0 (GitHub API); latest release 0.12.0 published 2024-12-22 (NuGet registration); last push 2025-02-09; not archived. Targets `netstandard2.0;net8`, `IsAotCompatible` on net8.

Data representation templates in `src/NGrib/Grib2/Templates/DataRepresentations`: 5.0 simple, 5.2 complex, 5.3 complex with spatial differencing, 5.40 JPEG2000, 5.42 CCSDS, 5.50002. Grid templates include 3.0 lat/lon and 3.30 Lambert conformal. So it covers NOMADS subsets, GFS 5.3 and RAP 5.40 on paper (not run against these files: untested).

Its dependencies (`NGrib.csproj`): `CSJ2K.NetCore` 3.0.0 (JPEG2000), `Libaec` 0.2.0 (native CCSDS), `System.Drawing.Common` 8.0.11. `System.Drawing.Common` is Windows-only from .NET 6 on (Microsoft breaking-change note; not re-read for this file, unverified here), which would matter for macOS installs if the JPEG2000 path touches it (not traced: untested). Open issues include #28 "LatLon grid of full NCEP GFS advertises the wrong left longitude" (opened 2025-06-22) and #30 "Getting BadGribFormatException("Decoding error")".

No other maintained, pure-.NET GRIB2 decoder was found; ecCodes wrappers need the native ecCodes library per platform (not surveyed further).

## Comparison for ZOA, UGRD/VGRD/TMP at 850/700/500/300/250 hPa

| Source | Payload (measured) | Cadence / latency | Auth | Limit | Terms | .NET parsing |
|---|---|---|---|---|---|---|
| NOMADS GFS 0.25° filter | 25 KB (98 KB for 20 levels) | 6-hourly, ~3.5 h | none | 120 hits/min per IP | NOAA open data, attribution requested | simple packing, lat/lon: small hand decoder or NGrib |
| NOMADS RAP 13 km filter | 101 KB | hourly, ~46 min | none | 120 hits/min per IP | same | simple packing, Lambert grid |
| AWS GFS / RAP | ~571 KB per GFS field; 15 range GETs (estimate) | same as NOAA | none | none stated | NODD open | 5.3 / 5.40 decoder (NGrib) |
| Open-Meteo | 138 KB JSON (90 points, 6 h) | GFS 6-hourly, HRRR hourly | none (free) | 600/min, 5,000/h, 10,000/day, non-commercial | CC BY 4.0 | JSON |
| aviationweather.gov | no gridded product | | | 100 req/min | | |

## Verdict

**Doable.** Recommended source: the NOMADS GFS 0.25° filter, one GET per scenario load (and per refresh no more often than the 6-hourly cycle), with the box widened from the ARTCC bounds and longitudes converted to 0-360:

```
https://nomads.ncep.noaa.gov/cgi-bin/filter_gfs_0p25.pl?dir=%2Fgfs.YYYYMMDD%2FHH%2Fatmos&file=gfs.tHHz.pgrb2.0p25.fFFF&var_UGRD=on&var_VGRD=on&var_TMP=on&lev_1000_mb=on&lev_975_mb=on&lev_950_mb=on&lev_925_mb=on&lev_900_mb=on&lev_850_mb=on&lev_800_mb=on&lev_750_mb=on&lev_700_mb=on&lev_650_mb=on&lev_600_mb=on&lev_550_mb=on&lev_500_mb=on&lev_450_mb=on&lev_400_mb=on&lev_350_mb=on&lev_300_mb=on&lev_250_mb=on&lev_200_mb=on&lev_150_mb=on&subregion=&toplat=42&leftlon=234&rightlon=243&bottomlat=34
```

`HH` is the latest cycle that has posted (try the newest, step back 6 h on 404, never retry within the second), `FFF` the forecast hour nearest the scenario time. 150 hPa is about FL445, covering the surface-to-FL450 goal.

The evidence: the request was run and returned 98 KB of simple-packed lat/lon GRIB2; the only stated limit (120 hits/min per IP) is far above per-install use; NOAA output is open data. RAP via the same filter is the higher-resolution, hourly alternative if Lambert-grid handling is accepted. Open-Meteo is the no-GRIB alternative if a JSON dependency on a third party is preferred over a decoder.

**What would make the opposite verdict hold:** NOAA publishing a per-application or per-day NOMADS cap, or a policy against direct end-user client access, that YAAT's install base would exceed; the filter ceasing to re-pack subsets with simple packing (forcing a 5.3 decoder whose .NET option is LGPL NGrib with an open GFS-grid issue and a Windows-only dependency); or NOMADS reliability at scenario load proving too poor to depend on. In that case per-station interpolation of the FD winds YAAT already fetches is the fallback.

## Sources

- NWS SCN 21-32, NOMADS/FTP user access changes: https://www.weather.gov/media/notification/pdf2/scn21-32nomad_changes.pdf
- NWS abusive user block: https://www.weather.gov/abusive-user-block
- NOMADS GFS and RAP filters and directory listings: https://nomads.ncep.noaa.gov/
- AWS Open Data Registry, GFS: https://raw.githubusercontent.com/awslabs/open-data-registry/main/datasets/noaa-gfs-bdp-pds.yaml
- AWS Open Data Registry, RAP: https://raw.githubusercontent.com/awslabs/open-data-registry/main/datasets/noaa-rap.yaml
- Open-Meteo terms: https://open-meteo.com/en/terms
- Open-Meteo pricing: https://open-meteo.com/en/pricing
- Open-Meteo GFS & HRRR API docs: https://open-meteo.com/en/docs/gfs-api
- aviationweather.gov Data API: https://aviationweather.gov/data/api/
- NGrib: https://github.com/nmangue/NGrib and https://www.nuget.org/packages/NGrib
