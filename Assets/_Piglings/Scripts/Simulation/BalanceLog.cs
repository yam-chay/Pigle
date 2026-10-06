using System;
using System.IO;
using Piglings.Events;
using Piglings.Rules;
using UnityEngine;

namespace Piglings.Simulation
{
    /// <summary>
    /// The balance log (M11.T3): every night played appends its rows to a CSV — one per closed chain, one per hour, one
    /// summary (BalanceCsv) — at persistentDataPath/BalanceLogs/balance_&lt;profile&gt;.csv. Appended, never overwritten, so
    /// the history of every session stays in one file (the Unity console clears on Play). A file written with other columns
    /// is moved aside (…old-&lt;time&gt;.csv) and a new one started. Every row says where the save came from (normal flow, a
    /// scenario, a debug edit) and the Balance Knobs in force. A plain class owned by NightSession, like NightLog. Reads only.
    /// </summary>
    public sealed class BalanceLog
    {
        private readonly NightSession _session;
        private readonly BalanceTally _tally;
        private readonly string _run;
        private readonly string _profile;

        /// <summary>The folder the CSVs are in (the debug panel opens it).</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "BalanceLogs");

        public string FilePath { get; }

        public BalanceLog(NightSession session, string profileName)
        {
            _session = session;
            _tally = new BalanceTally(session.Bus, session.State, () => Time.time);
            _run = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            _profile = string.IsNullOrEmpty(profileName) ? "profile" : profileName;
            FilePath = Path.Combine(Folder, $"balance_{_profile}.csv");
            // After the tally: it marks the night's end on the same event.
            session.Bus.Subscribe<NightEnded>(OnNightEnded);
        }

        public void Dispose()
        {
            _session.Bus.Unsubscribe<NightEnded>(OnNightEnded);
            _tally.Dispose();
        }

        private void OnNightEnded(NightEnded e)
        {
            var s = _session.State;
            var context = new BalanceContext
            {
                When = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Run = $"{_run}-n{_session.NightIndex + 1}",
                Scenario = string.IsNullOrEmpty(_session.Profile.Origin) ? "normal flow" : _session.Profile.Origin,
                Profile = _profile,
                Night = _session.NightIndex + 1,
                NightId = _session.Night.Id,
                ClimbMult = _session.ClimbSpeedMultiplier,
                SpawnMult = _session.SpawnRateMultiplier,
            };
            var summary = new BalanceNightSummary
            {
                Result = e.Result.ToString(), Reason = e.Reason.ToString(), Seconds = _tally.Seconds, Score = e.Score, Banked = e.BankedScore,
                Throws = e.ThrowsUsed, Stolen = s.StonesStolen, HoursReached = e.HoursReached, Sweep = s.SweepScore, AverageWall = _tally.AverageWall,
            };

            var lines = new System.Text.StringBuilder();
            foreach (var chain in _tally.Chains) lines.AppendLine(BalanceCsv.ChainRow(context, chain));
            var hours = _tally.Hours();
            foreach (var hour in hours) lines.AppendLine(BalanceCsv.HourRow(context, hour));
            lines.AppendLine(BalanceCsv.NightRow(context, summary));

            string written = Append(lines.ToString());
            var shares = new System.Collections.Generic.List<string>();
            foreach (var hour in hours) shares.Add($"h{hour.Hour} {hour.MissShare:P0}");
            Debug.Log($"[Balance] {context.Scenario} · night {context.Night} · {summary.Result} in {summary.Seconds:0}s · " +
                      $"{_tally.Chains.Count} chains · avg wall {summary.AverageWall:0.#} · miss share {string.Join(", ", shares)} · " +
                      (written == null ? $"appended to {FilePath}" : $"NOT written: {written}"));
        }

        // Appends, writing the header first on a new file. Returns null, or what went wrong (never throws: a log must not
        // break the night).
        private string Append(string rows)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                if (File.Exists(FilePath))
                {
                    string first;
                    using (var reader = new StreamReader(FilePath)) first = reader.ReadLine();
                    if (!BalanceCsv.HeaderMatches(first))
                    {
                        string old = Path.Combine(Folder, $"{Path.GetFileNameWithoutExtension(FilePath)}.old-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
                        File.Move(FilePath, old);
                        Debug.Log($"[Balance] the log's columns changed — the old file is now {old}.");
                    }
                }
                if (!File.Exists(FilePath)) File.WriteAllText(FilePath, BalanceCsv.Header + Environment.NewLine);
                File.AppendAllText(FilePath, rows);
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }
    }
}
