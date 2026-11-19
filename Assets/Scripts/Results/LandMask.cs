using System;
using MTS.Core;

namespace MTS.Results
{
    /// <summary>
    /// Coarse 1-degree land/ocean mask of the Earth, generated from the results world map (Assets/Art/Results/WorldMap.jpg)
    /// by Assets/Editor/Results/LandMask~/gen_land_mask.py: blue pixels are sea, green and brown pixels land, and
    /// cloud-covered cells take the majority of their neighbours. The ice sheets are set by hand (Antarctica south of
    /// 70 S, Greenland). Islands smaller than about two cells are lost and coastlines are only good to about a degree
    /// (~100 km), so a point near a coast may be classified either way. Used to decide whether an impact hit the ocean.
    /// </summary>
    public static class LandMask
    {
        public const int Columns = 360;
        public const int Rows = 180;

        // Bit k = row * 360 + col (row 0 = 90 S..89 S, col 0 = 180 W..179 W), set = land; bytes little-endian by bit.
        private const string Data =
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////////////////////////////////////" +
            "////////////////////////////////////////////////////////////////////////////////AAAAAAAAAAAAAAAAAADgHwAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAHwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACADwAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAADAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPgA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAHwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAPwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPwBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAP4DAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAYAAAAAP4DAAAAAAAAAADgAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAMAAAAAP4DAAAAAAAAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAP4DAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAHAAAAAAAAAAAAAAAAAAAP4DAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPAAAAAAAAAAAAAAAAAAAPwHAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAEAOABAAAAAAAAAAAAAAAAAPwHAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMAIADAAAAAAAAAAAAAAAAAPwPAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAgMEBAAADAAAAAAAAAAAAAAAAAPw/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgPABAAAIAAAAAAAAAAAAAAAAAPh/AAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAPgAAAA8AAAAAAAAAAAAAAAAAPj/AQAAAAAAAAAAAAAAAAAAAAAAAAAAMAAwAPwAAAAcAAAAAwAAAAAAAAAA" +
            "APj/BwAAAAAAAAAAAAAAAAAAAAAAAAAAMAQ4AP8BAAAIAAAAAwAAAAAAAAAAAPj/DwAAAAEAAAAAAAAAAAAAAAAAAAAAAAY8AP8DAAAAAAAAAAAA" +
            "AAAAAAAAAPD/DwAAAAMAAAAAAAAAAAAAAAAAAAAAAAY8wP8HAAAAAAAAAAAAAAAAAAAAAPD/PwAAgAcAAAAAAAEAAAAAAAAAAAAAAI4f4P8HAAAA" +
            "AAAAAAAAAAAAAAAAAPD/fwAAAAcAAAAAwB8AAAAAAAAAAAAAAP8f+P8PAAAAAAAAAAAAAAAAAAAAAPD/fwAAAAAAAAAAwP8AAAAAAAAAAAAAAP8/" +
            "/P8PAAAAAAAAAAAAAAAAAAAAAPD//wAAAAAAAAAAwP8AAAAAAAAAAAAAAP+///8fAAAAAAAAAAAAAAAAAAAAAPD//wMAAAAAAAAA4P8AAAAAAAAA" +
            "AAAAAP////8fAAAAAAAAAAAAAAAAAAAAAvD//wMAAAAAAAAA8P8RAAAAAAAAAAAAgP////8fAAAAAAAAAAAAAAAAAAAAB+D//wcAAQAAAAAA8P87" +
            "AAAAAAAAAAAAwP////8fAAAAAAAAAAAAAAAAAAAAD+D//wcAAwAAAAAA+P8fAAAAAAAAAAAAwP////8fAAAAAAAAAACAAQAAAAAABuD//wcAHgAA" +
            "AAAA+P8PAAAAAAAAAAAAwP////8fAAAAAAAAAACAAwAAAAAABuD//w8ANAAAAAAA/P8PAAAAAAAAAAAAwP////8fAAAAAAAAAAAAAAAAAAAAB8D/" +
            "/x8AYAAAAAAA/P8fAAcAAAAAAAAAwP////8fAAAAAAAAAAAAAAAAAAAABuD//38AAAAAAAAA/P9/AA8AAAAAAAAAwP////8PAAAAAAAAAAAAAAAA" +
            "AAAABu7///8BAAAAAAAA/P//AB8AAAAAAAAAwP////8HAAAAAAAAAAAAAAAAAAAAB8f///8BAAAAAAAA/P9/AB8AAAAAAAAAgP////8DAAAAAAAA" +
            "AAAAAAAAAAAAB8P///8DAAAAAAAA/v9/AA8AAAAAAAAAAP7///8BAAAAAAAAAAAAAAAAAAAAB8P///8HAAAAAAAA/v//AB8AAAAAAAAAAPD///8A" +
            "AAAAAAAAAAAAAAAAAAAAB8D///8PAAAAAAAA////AD8AAAAAAAAAAMD//38AAAAAAAAAAAAAAAAAAAAAAPD///8PAAAAAAAA////Az8AAAAAAAAA" +
            "AMD//z8AAAAAAAAAAAAAAAAAAAAAAP7///8fAAAAAAAA////Bz4AAAAAAAAAAID//z4AAAAAAAAAAAAAAAAAAAAAxv7///8fAAAAAAAA////BzgA" +
            "AAAAAAIAAAD/PzwAAAAAAAAAAAAAAAAAAAAAjP////8fAAAAAAAA////DzAAAAAAAAMAAAD+HxwAAAAAAAAAAAAAAAAAAAAAiP////8/AAAAAAAA" +
            "/v//HzAAAAAAAAAAAADAHwwAAAAAAAAAAAAAAAAAAAAAgP////8/AAAAAAAA/v//HxAAAAAAAAAAAACAHwQAAAAAAAAAAAAAAAAAAAAAwP////8/" +
            "AAAAAAAA/P//HwAAAAAAAAAAAACAAwQAAAAAAAAAAAAAAAAAAAAAwP////9/AAAAAAAA/P//DwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "wP//////AAAAAAAA/P//DwAAAAAAAAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAA4P//////AQAAAAAA/v//DwAAAAAAAAAAAAAAAMYAAAAAAAAAAAAA" +
            "AAAAAAAA4P//////AQAAAAAA/v//DwAAAAAAAAAAPgAAAP4AAAAAAAAAAAAAAAAAAAAA5P//////AQAAAAAA/v//DwAAAAAAAADAHwAAAP8AAAAA" +
            "AAAAAAAAAAAAAAAA/v//////AQAAAAAA/v//DwAAAAAAAABgAAAAgH8AAAAAAAAAAAAAAAAAAAAA+P//////AAAAAAAA////DwAAAAAAAAA4AAAA" +
            "+B8AAAAAAAAAAAAAAAAAAAAA+P////8/AAAAAACA////DwAAAAAAAAB8wAAA/g8AAAAAAAAAAAAAAAAAAAAA8P////8DAAAAAADA////HwAAAAAA" +
            "AAF++AEA/gEDAAAAAAAAAAAAAAAAAAAA8P////8AAAAAAADg////PwAAAAAAAAN+/BsAAwABAAAAAAAAAAAAAAAAAAAA+P///z8AAAAAAADg///P" +
            "fwAAAAAAgAcP/g+AAQABAAAAAAAAAAAAAAAAAAAA8P///w8AAAAAAADA////fwAAAAAAgIcP/gcAAIADAAAAAAAAAAAAAAAAAAAA8P///w8AAAAA" +
            "AADA/////wEAAAAIwMMP/gMAAMADAAAAAAAAAAAAAAAAAAAAwP///wcAAAAAAADA/////wMAAAAcgOAP8AEAAIAAAAAAAAAAAAAAAAAAAAAAwP//" +
            "/wMAAAAAAADA/////wcAAAAAAOAN8AMAAAAAAAAAAAAAAAAAAAAAAAAAgP///wEAAAAAAADw/////w8AAAAAAAAP4AcAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAgP///wCAAAAAACDg/////x8AAAAAAAAHgAcAAAAAAAAAAAAAAAAAAAAAAAAAgP//BwDAHwAA+H/w/////x8AAAAwAAADAAMBAAAAAAAAAAAA" +
            "AAAAAAAAAAAAwP//AwCADwAA/v///////z8AAAAwAAADAAADAAAAAAAAAAAAAAAAAAAAAAAA/P//AQAAAACA/////////z8AAAAwAMAAAKADAAAA" +
            "AAAAAAAAAAAAAAAAAACADf//AAAAAADA/////////38AAAAeAMBwAGABAAAAAAAAAAAAAAAAAAAAAACAAf9/AAAAAADA//////////8AAIAPAABw" +
            "AMAAAAAAAAAAAAAAAAAAAAAAAAAAADhAAAAAAADw//////////8AAIAPAAD4AQAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAADw/////////wEA" +
            "AAAPAAD8AQAAAAAAAAAAAAAAAAAAAAAAAAB4AAAAAAAAAAD4/////////wMAAAAPAID/AQAAAAAAAAAAAAAAAAAAAAAAAAB/AAAAAAAAAAD4////" +
            "/////z8AAAAfAMD/ATAAAAAAAAAAAAAAAAAAAAAAAIB/AAAAAAAAAAD4////////z/8AAIAfAPD/ATAAAAAAAAAAAAAAAAAAAAAAAPx/AAAAAAAA" +
            "AAD4////////z/8BAMA/APj/ADAAAAAAAAAAAAAAAAAAAAAAAP8/AAAAAAAAAADw////////x/8HAOB/APw/ABAAAAAAAAAAAAAAAAAAAAAA4D8P" +
            "ADgAAAAAAADw////////x/8PAOD/APw/AgAAAAAAAAAAAAAAAAAAAAAA+A8OAHgAAAAAAADw////////4/8/APD/A/x/BgAAAAAAAAAAAAAAAAAA" +
            "AAAA+AcOgAEAAAAAAADw////////8f9/APD/B/5/BgAAAAAAAAAAAAAAAAAAAAAA+AccwAAAAAAAAADw////////8f9/APj/D/9/BgAAAAAAAAAA" +
            "AAAAAAAAAAAA/AcAPAAAAAAAAAD4////////+f//APz/7///fwAAAAAAAAAAAAAAAAAAAAAA/AMAAAAAAAAAAADw////////+P9/AP7//////wEA" +
            "AAAAAAAAAAAAAAAAAAAA/wMAAAAAAAAAAADg/////////P8/AP///////wMAAAAAAAAAAAAAAAAAAAAQ/wcAAAAAAAAAAADA//////9//v8Y4P//" +
            "/////wcAAAAAAAAAAAAAAAAAAACY/wcADAAAAAAAAADA//////9//3/o/////////w8AAAAAAAAAAAAAAAAAAADM/wcADAAAAAAAAACA////////" +
            "/z///////////x8AAAAAAAAAAAAAAAAAAADu/w8ABgAAAAAAAAAA/v///////5///////////z8AAAAAAAAAAAAAAAAAAAD+/z8ABgAAAAAAAAAA" +
            "/P///////5///////////z8AAAAAAAAAAAAAAAAAAAD+///PBwAAAAAAAAAA/P///////////////////z8AAAAAAAAAAAAAAAAAAID/////BwAA" +
            "AAAAAAAA+P//P37e/////////////z8AAAAAAAAAAAAAAAAAAOD/////DwAAAAAAAAAA+P//Aw6A/////////////z/AAAAAAAAAAAAAAAAAAPD/" +
            "////HwAAAAAAAAAA+P//AACA/////////////x/AAQAAAAAAAAAAAAAAAPj/////HwAAAAAAAAAA4P9/AAAA/////////////x+ADwAAAAAAAAAA" +
            "AAAAAPj/////HwAAAAAAAAAAQPh/AAAA/////////////w88+gAAAAAAAAAAAAAAAPz/////HwAAAAAAAAAAwPB/AAD2/////////////wc84AMA" +
            "AAAAAAAAAAAAAPz/////HwAAAAAAAAAA+AcAAIT//z/+/////////wcegAMAAAAAAAAAAAAAAP7//////wcAAAAAAAAA+A8AAI7//z/8////////" +
            "/wcHAAAAAAAAAAAAAAAAAP7//////wcAAAAAAAAA+A8AEMb//x/+/////////88PAAAAAAAAAAAAAAAAAP///////wcAAAAAAAAA+A8wmM///x/+" +
            "//////////8/AAAAAAAAAAAAAADgAP///////w8AAAAAAAAA+D8gnv/x4D/8//////////9/AAAAAAAAAAAAAAAAAP///////x8AAAAAAAAA+H8A" +
            "h/8AwB//////////////AAAAAAAAAAAAAAAAAP//////vz8AAAAAAAAA8P+A4/8A8I//////////////AQ4AAAAAAAAAAAAAAP/////f/P8EAAAA" +
            "AAAAAPj/+f8B/I//////////////AwwAAAAAACAAAAAAAP////8//P8/AAAAAAAAAPj////j/x//////////////HwAAAAAAADwAAAAAAP////8j" +
            "+f8PAAAAAAAAAPz//////3/+////////////PwAAAAAAADgAAAAAAP////+D//8HMAAAAAAAAP7//////////////////////wAAAAAAAAAAAAAA" +
            "gP/////H//8HfgAAAAAAAP///////////////////////wAAAAAAAAAAAAAAwP////////8HPAAAAAAAAPz//////////////////////wwAAAAA" +
            "AgAAAAAA4P////////8zGAAAAAAAAPD//////////////////////w8AAAAABgAAAAAA4P//////////HwAAAAAAgL//////////////////////" +
            "/w8ABgAMAAAAAGAA4P//////7///HwAAAAAAAB///////////////////////w8AAwAMAAAAAOAA8P//////5///DwAAAAAAHB/+////////////" +
            "/////////wcADwAAAAAAAGAA8P//////4///DwAAAAAAuA/w8///////////////////PwAAPwAAAAAAAAAA/v//////w///BwQAAAAAgAHwA/7/" +
            "////////////////HwDAPwAAAAB8AAAA//////8fgP//AAQAAAAAgAFwBvz/////////////////PwAAfwAAAAA/AACA//////8BgP9/AAwAAAAA" +
            "gANgHvz//////////////////wAAfgAAAAC+AQDw/////z8AgP8fAAwAAAAAAAEAH/j//////////////////wMAfAAAAAD8JwD4/////z8AwP8c" +
            "AAAAAAAAAACAPwD//////////////////0c48AAAAAD+fwT+/////z8AwH8AAOAAAAAAAADAPwz3////////////////////4H8AAMD///7////9" +
            "/z8AwH8AAPgBAAAAAADgH/z3////////////////////8f8BAMD///////////8AwP8DAPwDAAAAAADwP/z///////////////////////8/AA//" +
            "//////////8AwP8PAP4HAAAAYADwP/7/////////////////////////AAL+///////////n4/8PAP8PAOAP4AAAPv///v//////////////////" +
            "////8AH+//////////////8PgP8fAPAf4AAA+P//8P//////////////////////8wP///////////////8f4P//APg/AAAA4P9//P//////////" +
            "/////////////wH///////////////8/8P//BxwcAAAAgP8vHP///////////////////////yD///////////////8/+P//fz4IAAAAAPAPAOD/" +
            "////////////////////f/D/////3/////////9//P///z8AAAAAAMAPAMD/////////////////////f/j/////h////////////v///z8AAAAA" +
            "AIAPAAD+//////////////9/+Pv/P/z/////j//////8/////////38AAAAAAAAAAAD+//////////////8/cPD/P/7/////h///////////////" +
            "//8AAAAAAAAAAAD+//////////8/wP8fMOD/f///////g////////4f///////8AAAAAAAAAAAD8//////////8/wP8fAMD/////////wf//////" +
            "PwD///////8BAAAAAAAAAAD4//////////8fwP8fAID/////////4P//////D4D///////8DAAAAAAAAAADw//////////8fgP8PAAD///////9/" +
            "8P//////D8D///////8HAAAAAAAAAADg//////////8fgP8HAAD+//////8/+P//////H/D/////////AwAAAAAAAADA//////////8fgP8DAAD+" +
            "//////8f/P//////P+D/////////BwAAAAAAAACA//////////8fgP8DAAD+//////8P/v//////f+D/////////BwAAAAAAAAAA//////////8f" +
            "gP8BAAD+//////8H//////////7/////////BwAAAAAAAAAA/v////////8fgP8BAAD+//////+D////////////////////BwAAAAAAAAAA/P//" +
            "//////8fgP8BAAD+AAAAAAAAAAAAAOD/////APz//x8AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        private static byte[] bits;

        private static byte[] Bits => bits ?? (bits = Convert.FromBase64String(Data));

        /// <summary>True if the 1-degree cell containing the point is land.</summary>
        public static bool IsLand(double latitude, double longitude)
        {
            if (double.IsNaN(latitude) || double.IsNaN(longitude)) return true;
            int row = (int)Math.Floor(Math.Max(-90.0, Math.Min(89.999, latitude)) + 90.0);
            double lon = Geo.WrapDegrees(longitude);
            int col = (int)Math.Floor(lon + 180.0);
            if (col >= Columns) col -= Columns;
            if (col < 0) col += Columns;
            int k = row * Columns + col;
            return (Bits[k >> 3] & (1 << (k & 7))) != 0;
        }

        public static bool IsOcean(double latitude, double longitude) => !IsLand(latitude, longitude);

        /// <summary>Fraction of the cells that are land (about 0.38: high latitudes are over-represented on a lat-lon grid).</summary>
        public static double LandCellFraction()
        {
            int n = 0;
            foreach (byte b in Bits)
            {
                for (int i = 0; i < 8; i++) n += (b >> i) & 1;
            }

            return n / (double)(Columns * Rows);
        }
    }
}
