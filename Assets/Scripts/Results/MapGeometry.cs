using System;
using MTS.Core;

namespace MTS.Results
{
    /// <summary>Latitude and longitude in degrees (north and east positive).</summary>
    public readonly struct GeoPoint
    {
        public readonly double Lat;
        public readonly double Lon;

        public GeoPoint(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }

        public override string ToString() => ResultsFormat.LatLon(Lat, Lon);
    }

    /// <summary>Normalised map position: X from 0 (west edge) to 1 (east edge), Y from 0 (south edge) to 1 (north edge).</summary>
    public readonly struct MapPoint
    {
        public readonly double X;
        public readonly double Y;

        public MapPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Equirectangular (plate carrée) projection of the results world map. The map texture spans 360 degrees of
    /// longitude by 180 degrees of latitude, with <see cref="CentralMeridian"/> in the middle (the project's map is
    /// centred on Greenwich: the left edge is 180° W). x = (lon - lon0 + 180) / 360, y = (lat + 90) / 180.
    /// </summary>
    public static class MapProjection
    {
        public const double CentralMeridian = 0.0;

        /// <summary>Map position of a point. Longitudes are wrapped into the map unless <paramref name="wrap"/> is false
        /// (ring outlines keep continuous, unwrapped longitudes and may run off either side).</summary>
        public static MapPoint ToMap(GeoPoint p, bool wrap = true)
        {
            double lon = p.Lon - CentralMeridian;
            if (wrap)
            {
                lon = Geo.WrapDegrees(lon);
            }

            double lat = Math.Max(-90.0, Math.Min(90.0, p.Lat));
            return new MapPoint((lon + 180.0) / 360.0, (lat + 90.0) / 180.0);
        }

        /// <summary>Inverse of <see cref="ToMap"/>; x outside 0..1 wraps around, y is clamped.</summary>
        public static GeoPoint FromMap(MapPoint m)
        {
            double lat = Math.Max(-90.0, Math.Min(90.0, m.Y * 180.0 - 90.0));
            double lon = Geo.WrapDegrees(m.X * 360.0 - 180.0 + CentralMeridian);
            return new GeoPoint(lat, lon);
        }

        /// <summary>
        /// Ground distance (m) covered by one map-width unit horizontally at a latitude: the east-west scale of an
        /// equirectangular map shrinks with cos(latitude).
        /// </summary>
        public static double MetresPerMapWidth(double latitude) =>
            2.0 * Math.PI * PhysicalConstants.EarthRadius * Math.Cos(latitude * Math.PI / 180.0);

        /// <summary>Ground distance (m) covered by the full map height (pole to pole along a meridian).</summary>
        public static double MetresPerMapHeight => Math.PI * PhysicalConstants.EarthRadius;
    }

    /// <summary>Great-circle geometry on a spherical Earth (radius <see cref="PhysicalConstants.EarthRadius"/>).</summary>
    public static class Geodesy
    {
        private const double Deg = Math.PI / 180.0;

        /// <summary>
        /// Destination after travelling <paramref name="distance"/> metres from <paramref name="start"/> on the initial
        /// bearing (degrees clockwise from north): lat2 = asin(sin lat1 cos d + cos lat1 sin d cos b),
        /// lon2 = lon1 + atan2(sin b sin d cos lat1, cos d - sin lat1 sin lat2), with d the angular distance.
        /// The longitude is not wrapped, so successive points of a ring stay continuous.
        /// </summary>
        public static GeoPoint Destination(GeoPoint start, double bearingDegrees, double distance)
        {
            double d = distance / PhysicalConstants.EarthRadius;
            double lat1 = start.Lat * Deg;
            double b = bearingDegrees * Deg;
            double sinLat2 = Math.Sin(lat1) * Math.Cos(d) + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(b);
            sinLat2 = Math.Max(-1.0, Math.Min(1.0, sinLat2));
            double lat2 = Math.Asin(sinLat2);
            double dLon = Math.Atan2(Math.Sin(b) * Math.Sin(d) * Math.Cos(lat1), Math.Cos(d) - Math.Sin(lat1) * sinLat2);
            return new GeoPoint(lat2 / Deg, start.Lon + dLon / Deg);
        }

        /// <summary>Great-circle distance (m), haversine formula.</summary>
        public static double Distance(GeoPoint a, GeoPoint b)
        {
            double dLat = (b.Lat - a.Lat) * Deg;
            double dLon = (b.Lon - a.Lon) * Deg;
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(a.Lat * Deg) * Math.Cos(b.Lat * Deg) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2.0 * PhysicalConstants.EarthRadius * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
        }

        /// <summary>
        /// Points of the small circle of the given ground radius around a centre (segments + 1 points, the last equal
        /// to the first). Longitudes are made continuous (no jumps of 360 degrees between neighbours).
        /// </summary>
        public static GeoPoint[] Circle(GeoPoint centre, double radius, int segments)
        {
            segments = Math.Max(8, segments);
            var points = new GeoPoint[segments + 1];
            double previousLon = centre.Lon;
            for (int i = 0; i <= segments; i++)
            {
                GeoPoint p = Destination(centre, 360.0 * i / segments, radius);
                double lon = p.Lon;
                while (lon - previousLon > 180.0) lon -= 360.0;
                while (lon - previousLon < -180.0) lon += 360.0;
                points[i] = new GeoPoint(p.Lat, lon);
                previousLon = lon;
            }

            return points;
        }

        /// <summary>+1 if a circle of this radius around the centre contains the North Pole, -1 the South Pole, else 0.</summary>
        public static int EnclosedPole(GeoPoint centre, double radius)
        {
            double angular = radius / PhysicalConstants.EarthRadius * 180.0 / Math.PI;
            if (90.0 - centre.Lat < angular) return 1;
            if (centre.Lat + 90.0 < angular) return -1;
            return 0;
        }
    }

    /// <summary>Axis range with "nice" round tick spacing (1, 2, 2.5 or 5 times a power of ten).</summary>
    public readonly struct AxisScale
    {
        public readonly double Min;
        public readonly double Max;
        public readonly double Step;

        public AxisScale(double min, double max, double step)
        {
            Min = min;
            Max = max;
            Step = step;
        }

        public int TickCount => Step > 0 ? (int)Math.Round((Max - Min) / Step) + 1 : 1;

        public double Tick(int i) => Min + i * Step;

        /// <summary>Position of a value along the axis, 0 at <see cref="Min"/> and 1 at <see cref="Max"/>.</summary>
        public double Normalise(double value) => Max > Min ? (value - Min) / (Max - Min) : 0.5;
    }

    public static class ChartScale
    {
        /// <summary>Nice step close to range / targetTicks.</summary>
        public static double NiceStep(double range, int targetTicks)
        {
            if (!(range > 0) || double.IsInfinity(range))
            {
                return 1.0;
            }

            double raw = range / Math.Max(1, targetTicks);
            double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double f = raw / power;
            double nice = f <= 1.0 ? 1.0 : f <= 2.0 ? 2.0 : f <= 2.5 ? 2.5 : f <= 5.0 ? 5.0 : 10.0;
            return nice * power;
        }

        /// <summary>
        /// Axis covering [min, max] with bounds on whole steps. A degenerate range is widened so the data sits in the middle.
        /// With <paramref name="includeZero"/> the axis always reaches 0.
        /// </summary>
        public static AxisScale Nice(double min, double max, int targetTicks = 5, bool includeZero = false)
        {
            if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max))
            {
                return new AxisScale(0, 1, 0.2);
            }

            if (min > max)
            {
                (min, max) = (max, min);
            }

            if (includeZero)
            {
                min = Math.Min(0.0, min);
                max = Math.Max(0.0, max);
            }

            if (max - min < 1e-12 * Math.Max(1.0, Math.Abs(max)))
            {
                double pad = Math.Abs(max) > 0 ? Math.Abs(max) * 0.1 : 1.0;
                min -= pad;
                max += pad;
                if (includeZero && min < 0 && max > 0 && Math.Abs(max) > 0)
                {
                    min = Math.Min(0.0, min);
                }
            }

            double step = NiceStep(max - min, targetTicks);
            double lo = Math.Floor(min / step + 1e-9) * step;
            double hi = Math.Ceiling(max / step - 1e-9) * step;
            if (hi <= lo)
            {
                hi = lo + step;
            }

            return new AxisScale(lo, hi, step);
        }

        /// <summary>Tick label with just enough decimals for the step.</summary>
        public static string Label(double value, double step)
        {
            int decimals = step >= 1 ? 0 : (int)Math.Ceiling(-Math.Log10(step) - 1e-9);
            if (Math.Abs(step * Math.Pow(10, decimals) - Math.Round(step * Math.Pow(10, decimals))) > 1e-6)
            {
                decimals++; // 2.5, 0.25 ...
            }

            if (Math.Abs(value) < step * 1e-6)
            {
                value = 0;
            }

            return value.ToString("N" + Math.Min(6, decimals), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Rough ocean names for points the reverse geocoder reports as "in the sea".</summary>
    public static class OceanNames
    {
        public static string Guess(double lat, double lon)
        {
            lon = Geo.WrapDegrees(lon);
            if (lat >= 66.0) return "Arctic Ocean";
            if (lat <= -60.0) return "Southern Ocean";
            if (lat >= 30.0 && lat <= 46.0 && lon >= -6.0 && lon <= 36.5) return "Mediterranean Sea";
            if (lat >= 40.5 && lat <= 47.0 && lon >= 27.5 && lon <= 42.0) return "Black Sea";
            if (lat >= 36.5 && lat <= 47.5 && lon >= 46.5 && lon <= 55.0) return "Caspian Sea";
            if (lat < 30.0 && lon >= 20.0 && lon < 100.0) return "Indian Ocean";
            if (lat < -10.0 && lon >= 100.0 && lon < 147.0) return "Indian Ocean";
            if (lat >= 0.0)
            {
                // North: the Americas separate the Pacific (west of about 100 W, or Asia's east coast) from the Atlantic.
                double westernBoundary = lat < 9.0 ? -80.0 : lat < 17.0 ? -90.0 : -100.0;
                if (lon >= westernBoundary && lon < 30.0) return "Atlantic Ocean";
                return "Pacific Ocean";
            }

            if (lon >= -68.0 && lon < 20.0) return "Atlantic Ocean";
            return "Pacific Ocean";
        }
    }
}
