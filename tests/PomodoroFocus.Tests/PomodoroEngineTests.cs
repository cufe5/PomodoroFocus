using System;
using PomodoroFocus.Core.Engine;
using PomodoroFocus.Core.Models;
using PomodoroFocus.Core.Services;

namespace PomodoroFocus.Tests
{
    internal sealed class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; }
        public TimeZoneInfo LocalTimeZone { get; set; }

        public FakeClock(DateTime utcStart)
        {
            UtcNow = DateTime.SpecifyKind(utcStart, DateTimeKind.Utc);
            LocalTimeZone = TimeZoneInfo.CreateCustomTimeZone("TestUTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
        }

        public DateTime LocalNow
        {
            get { return TimeZoneInfo.ConvertTimeFromUtc(UtcNow, LocalTimeZone); }
        }

        public void AdvanceSeconds(double seconds)
        {
            UtcNow = UtcNow.AddSeconds(seconds);
        }
    }

    public sealed class PomodoroEngineTests
    {
        public void StartPauseResumeAndEffectiveSecondsExcludePausedTime()
        {
            var clock = new FakeClock(new DateTime(2026, 11, 19, 1, 0, 0, DateTimeKind.Utc));
            var settings = new AppSettings { FocusMinutes = 25 };
            var engine = new PomodoroEngine(clock, settings);

            AssertEx.Equal(TimerRunState.Idle, engine.Snapshot.State, "Initial state");
            AssertEx.Equal(1500, engine.Snapshot.RemainingSeconds, "Initial remaining");

            engine.Start();
            AssertEx.Equal(TimerRunState.Running, engine.Snapshot.State, "Running");

            clock.AdvanceSeconds(120);
            engine.AdvanceTo(clock.UtcNow);
            AssertEx.Equal(1380, engine.Snapshot.RemainingSeconds, "Remaining after 120s");
            AssertEx.Equal(120, engine.Snapshot.ElapsedEffectiveSeconds, "Effective after 120s");

            engine.Pause();
            AssertEx.Equal(TimerRunState.Paused, engine.Snapshot.State, "Paused");

            // Advance 500s while paused — must not count!
            clock.AdvanceSeconds(500);
            engine.AdvanceTo(clock.UtcNow);
            AssertEx.Equal(1380, engine.Snapshot.RemainingSeconds, "Remaining unchanged while paused");
            AssertEx.Equal(120, engine.Snapshot.ElapsedEffectiveSeconds, "Effective unchanged while paused");

            engine.Resume();
            clock.AdvanceSeconds(30);
            engine.AdvanceTo(clock.UtcNow);
            AssertEx.Equal(1350, engine.Snapshot.RemainingSeconds, "Remaining after resume+30s");
            AssertEx.Equal(150, engine.Snapshot.ElapsedEffectiveSeconds, "Effective after resume+30s");
        }

        public void ResetUnder60sDiscardsButOver60sRecordsInterrupted()
        {
            var clock = new FakeClock(new DateTime(2026, 11, 19, 2, 0, 0, DateTimeKind.Utc));
            var settings = new AppSettings { FocusMinutes = 25 };
            var engine = new PomodoroEngine(clock, settings);

            engine.Start();
            clock.AdvanceSeconds(45);
            FocusSession s1 = engine.Reset();
            AssertEx.IsNull(s1, "Under 60s should not record");
            AssertEx.Equal(TimerRunState.Idle, engine.Snapshot.State, "Back to Idle");
            AssertEx.Equal(1500, engine.Snapshot.RemainingSeconds, "Full duration restored");

            engine.Start();
            clock.AdvanceSeconds(90);
            FocusSession s2 = engine.Reset();
            AssertEx.NotNull(s2, "Over 60s should record Interrupted");
            AssertEx.Equal("Interrupted", s2.Outcome, "Outcome Interrupted");
            AssertEx.Equal(90, s2.EffectiveSeconds, "Effective 90s");
        }

        public void FourFocusCyclesTriggerLongBreakThenResetCycle()
        {
            var clock = new FakeClock(new DateTime(2026, 11, 19, 3, 0, 0, DateTimeKind.Utc));
            var settings = new AppSettings
            {
                FocusMinutes = 25,
                ShortBreakMinutes = 5,
                LongBreakMinutes = 15,
                LongBreakInterval = 4
            };
            var engine = new PomodoroEngine(clock, settings);
            PhaseCompletedEventArgs lastCompleted = null;
            engine.PhaseCompleted += delegate(object sender, PhaseCompletedEventArgs e) { lastCompleted = e; };

            for (int cycle = 1; cycle <= 3; cycle++)
            {
                AssertEx.Equal(TimerPhase.Focus, engine.Snapshot.Phase, "Cycle focus " + cycle);
                engine.Start();
                clock.AdvanceSeconds(25 * 60);
                engine.AdvanceTo(clock.UtcNow);
                AssertEx.NotNull(lastCompleted, "PhaseCompleted fired");
                AssertEx.Equal(TimerPhase.ShortBreak, engine.Snapshot.Phase, "After focus " + cycle + " is ShortBreak");
                AssertEx.Equal(cycle, engine.Snapshot.CompletedFocusInCycle, "Cycle count");

                // Skip short break to next focus
                engine.Skip();
                AssertEx.Equal(TimerPhase.Focus, engine.Snapshot.Phase, "Back to Focus after short break");
            }

            // 4th focus -> LongBreak
            engine.Start();
            clock.AdvanceSeconds(25 * 60);
            engine.AdvanceTo(clock.UtcNow);
            AssertEx.Equal(TimerPhase.LongBreak, engine.Snapshot.Phase, "4th focus enters LongBreak");
            AssertEx.Equal(4, engine.Snapshot.CompletedFocusInCycle, "4 in cycle before long break finishes");

            // Complete long break -> back to Focus with cycle reset to 0
            engine.Start();
            clock.AdvanceSeconds(15 * 60);
            engine.AdvanceTo(clock.UtcNow);
            AssertEx.Equal(TimerPhase.Focus, engine.Snapshot.Phase, "After LongBreak is Focus");
            AssertEx.Equal(0, engine.Snapshot.CompletedFocusInCycle, "Cycle counter reset to 0");
        }

        public void AddFiveMinutesRespectsCapOf240Minutes()
        {
            var clock = new FakeClock(new DateTime(2026, 11, 19, 4, 0, 0, DateTimeKind.Utc));
            var settings = new AppSettings { FocusMinutes = 25 };
            var engine = new PomodoroEngine(clock, settings);

            AssertEx.IsTrue(engine.AddFiveMinutes(), "Idle +5 works");
            AssertEx.Equal(1800, engine.Snapshot.RemainingSeconds, "Idle +5 -> 30m");

            engine.Start();
            clock.AdvanceSeconds(60);
            AssertEx.IsTrue(engine.AddFiveMinutes(), "Running +5 works");
            AssertEx.Equal(2040, engine.Snapshot.RemainingSeconds, "Running +5");

            // Custom 180m + twelve +5m = 240m, then next fails
            engine.Reset();
            engine.SetCustomFocusMinutes(180);
            for (int i = 0; i < 12; i++)
            {
                AssertEx.IsTrue(engine.AddFiveMinutes(), "+5 step " + i);
            }
            AssertEx.Equal(240 * 60, engine.Snapshot.PlannedSeconds, "At 240m cap");
            AssertEx.IsFalse(engine.AddFiveMinutes(), "Cannot exceed 240m");
        }

        public void StopwatchRecordsOnCompleteAndDiscardsOnReset()
        {
            var clock = new FakeClock(new DateTime(2026, 11, 19, 5, 0, 0, DateTimeKind.Utc));
            var settings = new AppSettings();
            var engine = new PomodoroEngine(clock, settings);

            engine.SelectModeAndPhase(TimerMode.Stopwatch, TimerPhase.Focus, true);
            AssertEx.IsFalse(engine.AddFiveMinutes(), "Stopwatch hides +5");

            engine.Start();
            clock.AdvanceSeconds(180);
            FocusSession discarded = engine.Reset();
            AssertEx.IsNull(discarded, "Stopwatch Reset discards");

            engine.Start();
            clock.AdvanceSeconds(300);
            FocusSession done = engine.Skip();
            AssertEx.NotNull(done, "Stopwatch Skip completes >= 60s");
            AssertEx.Equal("Stopwatch", done.Mode, "Mode Stopwatch");
            AssertEx.Equal("Completed", done.Outcome, "Outcome Completed");
            AssertEx.Equal(300, done.EffectiveSeconds, "Effective 300s");
        }

        public void DayStartHourAssignsEarlyMorningToPreviousLogicalDay()
        {
            // Local time UTC+8: 2026-11-19 02:30 -> UTC 2026-11-18 18:30
            var clock = new FakeClock(new DateTime(2026, 11, 18, 18, 30, 0, DateTimeKind.Utc));
            var settings = new AppSettings { FocusMinutes = 25, DayStartHour = 4 };
            var engine = new PomodoroEngine(clock, settings);

            FocusSession completed = null;
            engine.PhaseCompleted += delegate(object sender, PhaseCompletedEventArgs e) { completed = e.Session; };

            engine.Start();
            clock.AdvanceSeconds(25 * 60);
            engine.AdvanceTo(clock.UtcNow);

            AssertEx.NotNull(completed, "Completed session");
            AssertEx.Equal("2026-11-18", completed.LocalDate, "02:30 with DayStartHour=4 belongs to previous day");
        }

        public void CrashRecoveryCompletesOverdueSessionAtExactEndTime()
        {
            var clock = new FakeClock(new DateTime(2026, 11, 19, 6, 0, 0, DateTimeKind.Utc));
            var settings = new AppSettings { FocusMinutes = 25, AutoStartBreaks = true };
            var engine1 = new PomodoroEngine(clock, settings);

            engine1.Start();
            clock.AdvanceSeconds(300);
            TimerPersistedState persisted = engine1.ExportState();

            // Simulate app closed for 1 hour (overdue by 40 minutes)
            clock.AdvanceSeconds(3600);
            var engine2 = new PomodoroEngine(clock, settings);
            FocusSession recovered = engine2.RestoreState(persisted);

            AssertEx.NotNull(recovered, "Overdue session recovered as Completed");
            AssertEx.Equal("Completed", recovered.Outcome, "Outcome Completed");
            AssertEx.Equal("2026-11-19T06:25:00.000Z", recovered.EndedAtUtc, "Ended at exact scheduled EndUtc");
            AssertEx.Equal(TimerPhase.ShortBreak, engine2.Snapshot.Phase, "Advanced to ShortBreak");
            AssertEx.Equal(TimerRunState.Idle, engine2.Snapshot.State, "Overdue recovery must stay Idle even if AutoStartBreaks=true");
        }

        public void FormattersAndHotkeyGestureContracts()
        {
            AssertEx.Equal("25:00", Formatters.FormatTimer(1500), "Timer MM:SS");
            AssertEx.Equal("1:05:09", Formatters.FormatTimer(3909), "Timer H:MM:SS");
            AssertEx.Equal("不足 1 分钟", Formatters.FormatDurationChinese(45), "<1 min");
            AssertEx.Equal("25 分钟", Formatters.FormatDurationChinese(1500), "<60 min");
            AssertEx.Equal("1 小时 05 分", Formatters.FormatDurationChinese(3900), ">=60 min");

            HotkeyGesture g;
            AssertEx.IsTrue(HotkeyGesture.TryParse("Ctrl+Alt+Space", out g), "Valid hotkey");
            AssertEx.Equal("Ctrl+Alt+Space", g.ToDisplayString(), "Formatted hotkey");
            AssertEx.Equal(0x20, g.VirtualKey, "VK_SPACE");
            AssertEx.IsFalse(HotkeyGesture.TryParse("Space", out g), "No modifier rejected");
            AssertEx.IsFalse(HotkeyGesture.TryParse("Ctrl+Alt", out g), "No main key rejected");
        }
    }
}
