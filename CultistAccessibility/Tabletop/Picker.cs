using System;
using System.Collections.Generic;
using System.Linq;
using CultistAccessibility.Core;
using CultistAccessibility.Core.Buffers;
using UnityEngine.InputSystem;

namespace CultistAccessibility.Tabletop
{
    internal sealed class PickerOption
    {
        public string Label;
        /// <summary>Section heading, as in the Cards group (null: no section, e.g. "Empty the slot").</summary>
        public string Section;
        public Func<List<string>> Details;
        public Action OnChoose;
    }

    /// <summary>
    /// A modal list: Up/Down browse, Enter chooses, Escape cancels, I reads details. Options with sections (card lists)
    /// are navigated like the Cards group: the section is spoken on entering it, Page Up/Down jump between sections,
    /// the read-screen key reads the list section by section.
    /// While open it owns the keyboard and the game's hotkeys are blocked (InputGate.ModalOpen).
    /// </summary>
    internal sealed class Picker
    {
        private readonly List<PickerOption> _options;
        private readonly Action _onCancel;
        private int _index;

        public string Title { get; }
        public bool IsOpen { get; private set; }

        public Picker(string title, List<PickerOption> options, Action onCancel = null)
        {
            Title = title;
            _options = options ?? new List<PickerOption>();
            _onCancel = onCancel;
        }

        public void Open()
        {
            IsOpen = true;
            InputGate.PickerOpen = true;
            _index = 0;
            if (_options.Count == 0)
            {
                Speech.Say(Title);
                Close();
                return;
            }
            var first = _options[0];
            Speech.Say(Title + " " + (first.Section != null ? SectionHeading(first.Section) + ". " : "") + first.Label);
            UpdateDetails();
        }

        public void Close()
        {
            IsOpen = false;
            InputGate.PickerOpen = false;
        }

        public void HandleInput()
        {
            if (!IsOpen) return;
            if (KeyInput.Pressed(Key.Escape))
            {
                Close();
                Speech.Say(Strings.PickerCancelled);
                _onCancel?.Invoke();
                return;
            }
            if (KeyInput.Repeat(Key.DownArrow)) Move(1);
            else if (KeyInput.Repeat(Key.UpArrow)) Move(-1);
            else if (KeyInput.Pressed(Key.Home)) MoveTo(0);
            else if (KeyInput.Pressed(Key.End)) MoveTo(_options.Count - 1);
            else if (KeyInput.Pressed(Key.PageUp)) JumpSection(-1);
            else if (KeyInput.Pressed(Key.PageDown)) JumpSection(1);
            else if (KeyInput.Pressed(Key.Enter) || KeyInput.Pressed(Key.NumpadEnter))
            {
                var opt = _options[_index];
                Close();
                try { opt.OnChoose?.Invoke(); }
                catch (Exception ex) { Plugin.LogError("Picker choice failed: " + ex); }
            }
            else if (KeyInput.Hotkey(ModConfig.KeyInspect.Value) || KeyInput.Hotkey(ModConfig.KeyRepeat.Value))
            {
                var d = _options[_index].Details?.Invoke();
                if (d != null && d.Count > 0) Speech.Say(TextCleaner.Sentences(d));
                else Speech.Say(_options[_index].Label);
            }
            else if (KeyInput.Hotkey(ModConfig.KeyReadScreen.Value))
            {
                Speech.Say(Title + " " + TextCleaner.Sentences(_options
                    .GroupBy(o => o.Section)
                    .SelectMany(g => (g.Key != null ? new[] { g.Key + ":" } : new string[0]).Concat(g.Select(o => o.Label)))));
            }
        }

        private void Move(int dir)
        {
            int next = _index + dir;
            if (next < 0) { Speech.SayFocus(Strings.StartOfList + " " + _options[_index].Label); return; }
            if (next >= _options.Count) { Speech.SayFocus(Strings.EndOfList + " " + _options[_index].Label); return; }
            MoveTo(next);
        }

        private void MoveTo(int i)
        {
            if (i < 0 || i >= _options.Count) return;
            string section = _options[i].Section;
            bool newSection = section != null && section != _options[_index].Section;
            _index = i;
            string label = _options[i].Label;
            if (ModConfig.Level == Verbosity.Verbose) label += ", " + Strings.PositionOf(i + 1, _options.Count);
            if (newSection) label = SectionHeading(section) + ". " + label;
            Speech.SayFocus(label);
            UpdateDetails();
        }

        private string SectionHeading(string section) => Strings.GroupAnnounce(section, _options.Count(o => o.Section == section));

        /// <summary>
        /// Page Up/Down: first option of the previous/next section; Page Up inside a section goes to its first option
        /// (TabletopNavigator.JumpSection does the same in the Cards group).
        /// </summary>
        private void JumpSection(int dir)
        {
            string current = _options[_index].Section;
            int start = _index;
            while (start > 0 && _options[start - 1].Section == current) start--;
            int target;
            if (dir < 0)
            {
                target = start;
                if (_index == start)
                {
                    target = start - 1;
                    while (target > 0 && _options[target - 1].Section == _options[start - 1].Section) target--;
                }
            }
            else
            {
                target = _index;
                while (target < _options.Count && _options[target].Section == current) target++;
                if (target >= _options.Count) target = -1;
            }
            if (target < 0)
            {
                Speech.SayFocus((dir < 0 ? Strings.StartOfList : Strings.EndOfList) + " " + _options[_index].Label);
                return;
            }
            _index = target;
            var o = _options[target];
            Speech.SayFocus((o.Section != null ? SectionHeading(o.Section) + ". " : "") + o.Label);
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            try
            {
                var d = _options[_index].Details?.Invoke();
                if (d != null) BufferManager.SetDetails(d);
            }
            catch { }
        }
    }
}
