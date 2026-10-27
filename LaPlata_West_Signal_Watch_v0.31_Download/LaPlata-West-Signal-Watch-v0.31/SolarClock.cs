using System;

namespace LapWest {
    // A clock plausibility gate, not a brightness classifier. Uses UTC and
    // approximate La Plata coordinates, so the PC's time zone and US DST
    // transitions do not affect it. Brightness and a confirmed view must also
    // agree before the detector declares DAYLIGHT / DISARMED.
    // NOAA general solar-position equations:
    // https://gml.noaa.gov/grad/solcalc/solareqns.PDF
    public static class SolarClock {
        public const double Latitude=40.02,Longitude=-92.49;
        public const double DaylightMinimumElevation=-3;
        public static double SunElevation(DateTime utc) {
            if(utc.Kind==DateTimeKind.Local)utc=utc.ToUniversalTime();
            double radians=Math.PI/180;
            double gamma=2*Math.PI/(DateTime.IsLeapYear(utc.Year)?366:365)*
                (utc.DayOfYear-1+(utc.TimeOfDay.TotalHours-12)/24);
            double eqtime=229.18*(.000075+.001868*Math.Cos(gamma)-.032077*Math.Sin(gamma)
                -.014615*Math.Cos(2*gamma)-.040849*Math.Sin(2*gamma));
            double decl=.006918-.399912*Math.Cos(gamma)+.070257*Math.Sin(gamma)
                -.006758*Math.Cos(2*gamma)+.000907*Math.Sin(2*gamma)
                -.002697*Math.Cos(3*gamma)+.00148*Math.Sin(3*gamma);
            double solar=(utc.TimeOfDay.TotalMinutes+eqtime+4*Longitude)%1440;
            if(solar<0)solar+=1440;
            double hourAngle=(solar/4-180)*radians,lat=Latitude*radians;
            double cosine=Math.Sin(lat)*Math.Sin(decl)+Math.Cos(lat)*Math.Cos(decl)*Math.Cos(hourAngle);
            return 90-Math.Acos(Math.Max(-1,Math.Min(1,cosine)))/radians;
        }
    }
}
