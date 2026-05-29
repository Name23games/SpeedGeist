using AurieSharpInterop;
using LibreGeist;
using LibreGeist.Core;
using System;
using System.Collections.Generic;
using System.IO;
using YYTKInterop;

namespace TG_Tools
{
    public class SplitsTimerMod : GeistMod
    {
        private enum RunState
        {
            Idle,
            Running,
            Finished
        }

        private class Split
        {
            public string Room = "";
            public string Name = "";
            public int Gold = -1;
            public int PB = -1;
            public int Current = 0;
        }

        private readonly List<Split> _splits = new();

        private RunState _runState = RunState.Idle;

        private int _frames;
        private int _currentSplit;
        private int _lastSplitFrame;

        private int _pbTotal = -1;
        private int _deathCount;
        private int _runResets;

        private string _lastRoom = "";
        private bool _justReset;

        private string _routesFolder = "mods/TG_Tools/routes/";
        private string _routeFile = "";
        private string _routeName = "Any%";

        private string _autoStartRoom = "c1p1r1";
        private string _resetRoom = "c1Comic";
        private string _endRoom = "c0EndDemoLookout";

        public SplitsTimerMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "Splits Timer";
        public override string Author => "Totoru";
        public override string ModVersion => "0.0.1";

        public override void Initialize()
        {
            Framework.Print("[Splits Timer] Loaded");

            Directory.CreateDirectory(_routesFolder);

            _routeFile = Path.Combine(_routesFolder, _routeName + ".ini");

            if (!File.Exists(_routeFile))
                CreateDefaultRoute();

            LoadRoute();
            ResetRun(false);
        }

        public override void Update(double deltaTime)
        {
            string roomName = Geist.CurrentRoom;

            if (string.IsNullOrWhiteSpace(roomName))
                return;

            if (Keyboard.CheckPressed(0x52)) // R
                ResetRun(true);

            if (Keyboard.CheckPressed(0x47)) // G
                GoToResetRoom();

            if (Keyboard.CheckPressed(0x50)) // P
                PrintStatus();

            bool showTimer = ShouldShowTimer(roomName);

            if (roomName == _autoStartRoom && _runState == RunState.Idle && !_justReset)
                StartRun(roomName);

            if (roomName != _autoStartRoom)
                _justReset = false;

            if (showTimer && _runState == RunState.Running)
                _frames++;

            if (_runState == RunState.Running && roomName != _lastRoom)
                CheckSplit(roomName);

            if (_runState == RunState.Finished && roomName == _endRoom)
                _runState = RunState.Idle;
        }

        private void StartRun(string roomName)
        {
            _runState = RunState.Running;
            _frames = 0;
            _currentSplit = 0;
            _lastSplitFrame = 0;
            _lastRoom = roomName;

            foreach (Split split in _splits)
                split.Current = 0;

            Framework.Print("[Splits Timer] Run started");
        }

        private void CheckSplit(string roomName)
        {
            if (_currentSplit >= _splits.Count)
            {
                _lastRoom = roomName;
                return;
            }

            Split split = _splits[_currentSplit];

            if (roomName == split.Room)
            {
                int segmentLength = _frames - _lastSplitFrame;
                split.Current = segmentLength;

                Framework.Print(
                    $"[Splits Timer] Split {_currentSplit + 1}/{_splits.Count} {split.Name}: {FramesToString(segmentLength)}"
                );

                if (split.Gold == -1 || segmentLength < split.Gold)
                {
                    split.Gold = segmentLength;
                    SaveGolds();
                    Framework.Print($"[Splits Timer] New gold for {split.Name}");
                }

                _currentSplit++;
                _lastSplitFrame = _frames;

                if (_currentSplit >= _splits.Count)
                    FinishRun();
            }

            _lastRoom = roomName;
        }

        private void FinishRun()
        {
            _runState = RunState.Finished;

            Framework.Print($"[Splits Timer] Finished: {FramesToString(_frames)}");

            if (_pbTotal == -1 || _frames < _pbTotal)
            {
                _pbTotal = _frames;

                foreach (Split split in _splits)
                    split.PB = split.Current;

                SavePB();

                Framework.Print("[Splits Timer] New PB saved");
            }
        }

        private void ResetRun(bool countReset)
        {
            _runState = RunState.Idle;
            _frames = 0;
            _currentSplit = 0;
            _lastSplitFrame = 0;
            _lastRoom = Geist.CurrentRoom;
            _justReset = countReset;

            foreach (Split split in _splits)
                split.Current = 0;

            if (countReset)
            {
                _runResets++;
                SaveStats();
            }

            Framework.Print("[Splits Timer] Reset");
        }

        private void GoToResetRoom()
        {
            ResetRun(true);

            GameVariable? roomRef = GML.GetAsset(_resetRoom);

            if (roomRef is null)
            {
                Framework.Print($"[Splits Timer] Invalid reset room: {_resetRoom}");
                return;
            }

            GML.Call("room_goto", roomRef);
        }

        private bool ShouldShowTimer(string roomName)
        {
            if (roomName == "c0TitleScreen")
                return false;

            if (roomName == "c0EndDemo")
                return false;

            if (roomName == "c0Overworld")
                return false;

            if (roomName == "cvTitle")
                return false;

            if (roomName == "c0DemoTitle")
                return false;

            if (roomName == "c0PAXTitleScreen")
                return false;

            return true;
        }

        private void PrintStatus()
        {
            Framework.Print(
                $"[Splits Timer] {_runState} | Time {FramesToString(_frames)} | Split {_currentSplit}/{_splits.Count}"
            );

            for (int i = 0; i < _splits.Count; i++)
            {
                Split split = _splits[i];

                Framework.Print(
                    $"[Splits Timer] {i + 1}. {split.Name} [{split.Room}] Current:{FramesToString(split.Current)} Gold:{FramesToString(split.Gold)} PB:{FramesToString(split.PB)}"
                );
            }
        }

        private void CreateDefaultRoute()
        {
            using StreamWriter writer = new(_routeFile);

            writer.WriteLine("[Settings]");
            writer.WriteLine("ResetRoom=c1Comic");
            writer.WriteLine("AutoStartRoom=c1p1r1");
            writer.WriteLine("EndRoom=c0EndDemoLookout");
            writer.WriteLine();

            writer.WriteLine("[Splits]");
            writer.WriteLine("Count=4");
            writer.WriteLine("Room0=c1HubHome");
            writer.WriteLine("Name0=Intro");
            writer.WriteLine("Room1=c1p3r0");
            writer.WriteLine("Name1=Village");
            writer.WriteLine("Room2=c2p1r01");
            writer.WriteLine("Name2=Departure");
            writer.WriteLine("Room3=c0EndDemoLookout");
            writer.WriteLine("Name3=FungalForest");
            writer.WriteLine();

            writer.WriteLine("[Gold]");
            writer.WriteLine("Gold0=-1");
            writer.WriteLine("Gold1=-1");
            writer.WriteLine("Gold2=-1");
            writer.WriteLine("Gold3=-1");
            writer.WriteLine();

            writer.WriteLine("[PB]");
            writer.WriteLine("Total=-1");
            writer.WriteLine("PB0=-1");
            writer.WriteLine("PB1=-1");
            writer.WriteLine("PB2=-1");
            writer.WriteLine("PB3=-1");
            writer.WriteLine();

            writer.WriteLine("[Stats]");
            writer.WriteLine("Deaths=0");
            writer.WriteLine("Resets=0");

            Framework.Print($"[Splits Timer] Created route file: {_routeFile}");
        }

        private void LoadRoute()
        {
            IniFile ini = new(_routeFile);

            _resetRoom = ini.ReadString("Settings", "ResetRoom", "c1Comic");
            _autoStartRoom = ini.ReadString("Settings", "AutoStartRoom", "c1p1r1");
            _endRoom = ini.ReadString("Settings", "EndRoom", "c0EndDemoLookout");

            _deathCount = ini.ReadInt("Stats", "Deaths", 0);
            _runResets = ini.ReadInt("Stats", "Resets", 0);
            _pbTotal = ini.ReadInt("PB", "Total", -1);

            int count = Math.Max(0, ini.ReadInt("Splits", "Count", 0));

            _splits.Clear();

            for (int i = 0; i < count; i++)
            {
                _splits.Add(new Split
                {
                    Room = ini.ReadString("Splits", "Room" + i, ""),
                    Name = ini.ReadString("Splits", "Name" + i, "Split " + (i + 1)),
                    Gold = ini.ReadInt("Gold", "Gold" + i, -1),
                    PB = ini.ReadInt("PB", "PB" + i, -1),
                    Current = 0
                });
            }

            Framework.Print($"[Splits Timer] Loaded route {_routeName} with {_splits.Count} splits");
        }

        private void SaveGolds()
        {
            IniFile ini = new(_routeFile);

            for (int i = 0; i < _splits.Count; i++)
                ini.WriteInt("Gold", "Gold" + i, _splits[i].Gold);

            ini.Save();
        }

        private void SavePB()
        {
            IniFile ini = new(_routeFile);

            ini.WriteInt("PB", "Total", _pbTotal);

            for (int i = 0; i < _splits.Count; i++)
                ini.WriteInt("PB", "PB" + i, _splits[i].PB);

            ini.Save();
        }

        private void SaveStats()
        {
            IniFile ini = new(_routeFile);

            ini.WriteInt("Stats", "Deaths", _deathCount);
            ini.WriteInt("Stats", "Resets", _runResets);

            ini.Save();
        }

        private static string FramesToString(int frames)
        {
            if (frames < 0)
                return "--:--.---";

            int totalSeconds = frames / 60;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            int milliseconds = (frames * 1000 / 60) % 1000;

            return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
        }

        private class IniFile
        {
            private readonly string _path;
            private readonly Dictionary<string, Dictionary<string, string>> _data = new();

            public IniFile(string path)
            {
                _path = path;

                if (File.Exists(path))
                    Load();
            }

            public string ReadString(string section, string key, string fallback)
            {
                if (_data.TryGetValue(section, out Dictionary<string, string>? keys))
                {
                    if (keys.TryGetValue(key, out string? value))
                        return value;
                }

                return fallback;
            }

            public int ReadInt(string section, string key, int fallback)
            {
                string value = ReadString(section, key, fallback.ToString());

                if (int.TryParse(value, out int result))
                    return result;

                return fallback;
            }

            public void WriteInt(string section, string key, int value)
            {
                WriteString(section, key, value.ToString());
            }

            public void WriteString(string section, string key, string value)
            {
                if (!_data.ContainsKey(section))
                    _data[section] = new Dictionary<string, string>();

                _data[section][key] = value;
            }

            public void Save()
            {
                using StreamWriter writer = new(_path);

                foreach (KeyValuePair<string, Dictionary<string, string>> section in _data)
                {
                    writer.WriteLine("[" + section.Key + "]");

                    foreach (KeyValuePair<string, string> pair in section.Value)
                        writer.WriteLine(pair.Key + "=" + pair.Value);

                    writer.WriteLine();
                }
            }

            private void Load()
            {
                string currentSection = "";

                foreach (string rawLine in File.ReadAllLines(_path))
                {
                    string line = rawLine.Trim();

                    if (line == "" || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2);

                        if (!_data.ContainsKey(currentSection))
                            _data[currentSection] = new Dictionary<string, string>();

                        continue;
                    }

                    int equals = line.IndexOf('=');

                    if (equals <= 0)
                        continue;

                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();

                    if (!_data.ContainsKey(currentSection))
                        _data[currentSection] = new Dictionary<string, string>();

                    _data[currentSection][key] = value;
                }
            }
        }
    }
}