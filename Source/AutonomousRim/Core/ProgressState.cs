using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AutonomousRim.Core
{
    public sealed class ProgressEntry : IExposable
    {
        public string Key, Label, Detail, Status;
        public float Value;
        public int LastSample = -1, LastProgress, EligibleTicks, Completions;
        public bool Eligible, Stalled;
        public float WorkLeft = -1;
        public int WorkStart = -1;
        public int CandidateCursor;
        public void ExposeData()
        {
            Scribe_Values.Look(ref Key, "key"); Scribe_Values.Look(ref Label, "label");
            Scribe_Values.Look(ref Detail, "detail"); Scribe_Values.Look(ref Status, "status");
            Scribe_Values.Look(ref Value, "value"); Scribe_Values.Look(ref LastSample, "lastSample", -1);
            Scribe_Values.Look(ref LastProgress, "lastProgress"); Scribe_Values.Look(ref EligibleTicks, "eligibleTicks");
            Scribe_Values.Look(ref Completions, "completions"); Scribe_Values.Look(ref Eligible, "eligible");
            Scribe_Values.Look(ref Stalled, "stalled");
            Scribe_Values.Look(ref WorkLeft, "workLeft", -1); Scribe_Values.Look(ref WorkStart, "workStart", -1);
            Scribe_Values.Look(ref CandidateCursor, "candidateCursor");
        }
    }
    public sealed class ProgressEvent : IExposable
    {
        public int Tick, MapId = -1, ObservationTick = -1;
        public string Text, Module, Objective, Context, Policy;
        public void ExposeData() {
            Scribe_Values.Look(ref Tick, "tick"); Scribe_Values.Look(ref Text, "text");
            Scribe_Values.Look(ref MapId,"mapId",-1); Scribe_Values.Look(ref ObservationTick,"observationTick",-1);
            Scribe_Values.Look(ref Module,"module"); Scribe_Values.Look(ref Objective,"objective");
            Scribe_Values.Look(ref Context,"context"); Scribe_Values.Look(ref Policy,"policy");
        }
    }
    // Diagnostic state never owns bills, priorities, filters or jobs.
    public sealed class ProgressState : IExposable
    {
        public const int StallTicks = 10000; // Four hours of eligible work, excluding sleep/recovery.
        public List<ProgressEntry> Entries = new List<ProgressEntry>();
        public List<ProgressEvent> Events = new List<ProgressEvent>();
        public int TotalCompletedProduction;
        // Bound to the latest scalar checkpoint, never to mutable pawns/items.
        public int MapId = -1, ObservationTick = -1;
        public string Context, Policy;
        public void Bind(int mapId, int observationTick, string context, string policy)
        { MapId=mapId; ObservationTick=observationTick; Context=context; Policy=policy; }
        public ProgressEntry Get(string key)
        {
            var entry = Entries.FirstOrDefault(e => e.Key == key);
            if (entry == null) { entry = new ProgressEntry { Key = key }; Entries.Add(entry); }
            return entry;
        }
        public void Record(int tick, string message, string module = "Diagnóstico", string objective = null)
        {
            Events.Add(new ProgressEvent { Tick = tick, Text = message, MapId=MapId,
                ObservationTick=ObservationTick, Context=Context, Policy=Policy, Module=module, Objective=objective });
            if (Events.Count > 64) Events.RemoveRange(0, Events.Count - 64);
        }
        public void Observe(string key, string label, float value, bool eligible, string status, string detail, int tick, bool externalStalled = false)
        {
            var entry = Get(key);
            bool advanced = entry.LastSample >= 0 && value > entry.Value + .001f;
            if (entry.LastSample < 0 || advanced || value < entry.Value)
            { entry.EligibleTicks = 0; entry.LastProgress = tick; }
            else if (entry.Eligible && eligible)
                entry.EligibleTicks += Math.Max(0, Math.Min(600, tick - entry.LastSample));
            bool stalled = externalStalled || eligible && entry.EligibleTicks >= StallTicks;
            if (entry.Status != status || entry.Stalled != stalled)
                Record(tick, label + ": " + detail + (stalled ? " • Sem avanço; revisar bloqueio." : ""),
                    key.StartsWith("bill:") ? "Produção" : key.StartsWith("research:") ? "Pesquisa" : "Construção", key);
            entry.Label = label; entry.Value = value; entry.Status = status; entry.Detail = detail;
            entry.Eligible = eligible; entry.Stalled = stalled; entry.LastSample = tick;
        }
        public void ResetSampling(int tick)
        {
            // Loading must not count time spent outside this map, nor restore stale eligibility.
            foreach (var entry in Entries) { entry.LastSample = -1; entry.Eligible = false; entry.Stalled = false; entry.EligibleTicks = 0; entry.WorkLeft = -1; entry.WorkStart = -1; }
            Record(tick, "Mapa inicializado: diagnóstico será reconciliado com pesquisas, obras e Bills atuais.");
        }
        public void ExposeData()
        {
            Scribe_Collections.Look(ref Entries, "entries", LookMode.Deep);
            Scribe_Collections.Look(ref Events, "events", LookMode.Deep);
            Scribe_Values.Look(ref TotalCompletedProduction,"totalCompletedProduction");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            { Entries = Entries ?? new List<ProgressEntry>(); Events = Events ?? new List<ProgressEvent>();
                Entries.RemoveAll(e => e == null || e.Key == null); Events.RemoveAll(e => e == null);
                if (Events.Count > 64) Events.RemoveRange(0, Events.Count - 64); }
        }
    }
}
