using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Piglings.Rules;

namespace Piglings.Simulation
{
    /// <summary>What every row of a night repeats: when, which run, where the save came from, which night, the knobs.</summary>
    public sealed class BalanceContext
    {
        public string When = "";        // the row's local time, "2026-10-07 14:03:22"
        public string Run = "";         // one id per night played: rows with the same run are one night
        public string Scenario = "";    // the save's origin: "normal flow", "scenario X", "debug edit"
        public string Profile = "";
        public int Night;               // 1-based campaign index
        public string NightId = "";
        public float ClimbMult = 1f;    // the Balance Knobs in force
        public float SpawnMult = 1f;
    }

    /// <summary>The night's end, for its summary row.</summary>
    public sealed class BalanceNightSummary
    {
        public string Result = "", Reason = "";
        public float Seconds;
        public int Score, Banked, Throws, Stolen, HoursReached, Sweep;
        public float AverageWall;
    }

    /// <summary>
    /// The balance log's CSV (M11.T3): one fixed set of columns for three kinds of row — "chain" (one per closed chain),
    /// "hour" (one per hour played) and "night" (the summary) — so a whole file pastes into a spreadsheet and filters by
    /// the "row" column. Cells a row doesn't use stay empty. Numbers are invariant ("2.5", never "2,5"). Stateless.
    /// </summary>
    public static class BalanceCsv
    {
        public static readonly string[] Columns =
        {
            "when", "run", "scenario", "profile", "night", "night_id", "climb_mult", "spawn_mult", "row",
            // chain
            "hour", "chain", "t", "score", "mult", "hour_mult", "total", "wolves", "depth", "miss", "wall", "stones_left",
            // hour
            "seconds", "chains", "miss_total", "miss_share", "breaches",
            // night
            "result", "reason", "night_score", "banked", "throws", "stolen", "hours_reached", "sweep", "avg_wall",
        };

        public static string Header => string.Join(",", Columns);

        /// <summary>A file whose first line isn't today's header was written with other columns: start a new one.</summary>
        public static bool HeaderMatches(string firstLine) => firstLine != null && firstLine.TrimEnd('\r') == Header;

        public static string ChainRow(BalanceContext c, ChainRecord r)
        {
            var row = Start(c, "chain");
            row["hour"] = Int(r.Hour); row["chain"] = Int(r.Index); row["t"] = Num(r.ThrownAt);
            row["score"] = Int(r.Score); row["mult"] = Num(r.Mult); row["hour_mult"] = Num(r.HourMultiplier); row["total"] = Int(r.Total);
            row["wolves"] = Int(r.Wolves); row["depth"] = Int(r.Depth); row["miss"] = r.Miss ? "1" : "0";
            row["wall"] = Int(r.Wall); row["stones_left"] = Int(r.StonesLeft);
            return Line(row);
        }

        public static string HourRow(BalanceContext c, HourRecord r)
        {
            var row = Start(c, "hour");
            row["hour"] = Int(r.Hour); row["seconds"] = Num(r.Seconds); row["chains"] = Int(r.Chains); row["wolves"] = Int(r.Wolves);
            row["total"] = Int(r.Total); row["miss_total"] = Int(r.MissTotal); row["miss_share"] = Num(r.MissShare); row["breaches"] = Int(r.Breaches);
            return Line(row);
        }

        public static string NightRow(BalanceContext c, BalanceNightSummary s)
        {
            var row = Start(c, "night");
            row["seconds"] = Num(s.Seconds); row["result"] = s.Result; row["reason"] = s.Reason; row["night_score"] = Int(s.Score);
            row["banked"] = Int(s.Banked); row["throws"] = Int(s.Throws); row["stolen"] = Int(s.Stolen);
            row["hours_reached"] = Int(s.HoursReached); row["sweep"] = Int(s.Sweep); row["avg_wall"] = Num(s.AverageWall);
            return Line(row);
        }

        private static Dictionary<string, string> Start(BalanceContext c, string kind) => new Dictionary<string, string>
        {
            ["when"] = c.When, ["run"] = c.Run, ["scenario"] = c.Scenario, ["profile"] = c.Profile, ["night"] = Int(c.Night),
            ["night_id"] = c.NightId, ["climb_mult"] = Num(c.ClimbMult), ["spawn_mult"] = Num(c.SpawnMult), ["row"] = kind,
        };

        private static string Line(Dictionary<string, string> row)
        {
            var line = new StringBuilder();
            for (int i = 0; i < Columns.Length; i++)
            {
                if (i > 0) line.Append(',');
                if (row.TryGetValue(Columns[i], out var value)) line.Append(Escape(value));
            }
            return line.ToString();
        }

        /// <summary>A cell as CSV: quoted when it holds a comma, a quote or a line break (quotes doubled).</summary>
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string Num(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
