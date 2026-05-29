using AurieSharpInterop;
using LibreGeist;
using LibreGeist.Core;
using YYTKInterop;

namespace TG_Tools
{
    public class SpeedrunMod : GeistMod
    {
        private double _savedX;
        private double _savedY;
        private double _savedHsp;
        private double _savedVsp;

        private readonly double[] _savedAzaeStock = new double[5];

        private string _savedRoomName = "";

        private bool _hasSave;
        private bool _pendingLoad;
        private int _pendingLoadFrames;

        public SpeedrunMod(AurieManagedModule module) : base(module)
        {
        }

        public override string Name => "Speedrun Save Load";
        public override string Author => "Totoru";
        public override string ModVersion => "0.0.1";

        public override void Initialize()
        {
            Framework.Print("[Speedrun SaveLoad] Loaded");
        }

        public override void Update(double deltaTime)
        {
            if (Keyboard.CheckPressed(0x74)) // F5
                SavePlayer();

            if (Keyboard.CheckPressed(0x78)) // F9
                LoadPlayer();

            // Next Room
            if (Keyboard.CheckPressed(0x70)) // F1
                GoToNextRoom();

            // Previous Room
            if (Keyboard.CheckPressed(0x71)) // F2
                GoToPreviousRoom();

            HandlePendingLoad();
        }

        private void SavePlayer()
        {
            GameVariable? player = GML.FindInstance("oPlayer");

            if (player is null)
            {
                Framework.Print("[Speedrun SaveLoad] Could not find oPlayer");
                return;
            }

            _savedX = GetNumber(player, "x");
            _savedY = GetNumber(player, "y");
            _savedHsp = GetNumber(player, "hsp");
            _savedVsp = GetNumber(player, "vsp");

            _savedRoomName = Geist.CurrentRoom;

            SaveAzaeStock();

            _hasSave = true;

            Framework.Print(
                $"[Speedrun SaveLoad] Saved at X:{_savedX} Y:{_savedY} Room:{_savedRoomName}"
            );
        }

        private void LoadPlayer()
        {
            if (!_hasSave || string.IsNullOrWhiteSpace(_savedRoomName))
            {
                Framework.Print("[Speedrun SaveLoad] No save exists");
                return;
            }

            string currentRoomName = Geist.CurrentRoom;

            if (currentRoomName != _savedRoomName)
            {
                Framework.Print(
                    $"[Speedrun SaveLoad] Changing room from {currentRoomName} to {_savedRoomName}"
                );

                GameVariable? roomRef = GML.GetAsset(_savedRoomName);

                if (roomRef is null)
                {
                    Framework.Print(
                        $"[Speedrun SaveLoad] Could not find room asset: {_savedRoomName}"
                    );
                    return;
                }

                _pendingLoad = true;
                _pendingLoadFrames = 10;

                GML.Call("room_goto", roomRef);
                return;
            }

            ApplyLoad();
        }

        private void HandlePendingLoad()
        {
            if (!_pendingLoad)
                return;

            if (_pendingLoadFrames > 0)
            {
                _pendingLoadFrames--;
                return;
            }

            if (Geist.CurrentRoom != _savedRoomName)
                return;

            GameVariable? player = GML.FindInstance("oPlayer");

            if (player is null)
                return;

            _pendingLoad = false;

            ApplyLoad();
        }

        private void ApplyLoad()
        {
            GameVariable? player = GML.FindInstance("oPlayer");

            if (player is null)
            {
                Framework.Print("[Speedrun SaveLoad] Player instance missing");
                return;
            }

            GML.SetInstanceVariable(player, "x", _savedX);
            GML.SetInstanceVariable(player, "y", _savedY);
            GML.SetInstanceVariable(player, "hsp", _savedHsp);
            GML.SetInstanceVariable(player, "vsp", _savedVsp);

            LoadAzaeStock();

            Framework.Print(
                $"[Speedrun SaveLoad] Loaded save in room {_savedRoomName}"
            );
        }

        private void SaveAzaeStock()
        {
            GameVariable? azaeStock = GML.GetGlobalVariable("azae_stock");

            if (azaeStock is null)
            {
                Framework.Print("[Speedrun SaveLoad] global.azae_stock missing");
                return;
            }

            for (int i = 0; i < _savedAzaeStock.Length; i++)
            {
                GameVariable value = GML.Call("array_get", azaeStock, i);
                _savedAzaeStock[i] = ReadNumber(value);
            }

            Framework.Print("[Speedrun SaveLoad] Saved azae stock");
        }

        private void LoadAzaeStock()
        {
            GameVariable? azaeStock = GML.GetGlobalVariable("azae_stock");

            if (azaeStock is null)
            {
                Framework.Print("[Speedrun SaveLoad] global.azae_stock missing");
                return;
            }

            for (int i = 0; i < _savedAzaeStock.Length; i++)
            {
                GML.Call("array_set", azaeStock, i, _savedAzaeStock[i]);
            }

            Framework.Print("[Speedrun SaveLoad] Loaded azae stock");
        }


        private void GoToNextRoom()
        {
            try
            {
                string roomName = GML.GetCurrentRoomName();

                if (string.IsNullOrWhiteSpace(roomName))
                    return;

                GameVariable room = GML.GetAsset(roomName);

                if (ReadNumber(GML.Call("room_next", room)) == -1)
                {
                    Framework.Print("[Speedrun] No next room");
                    return;
                }

                GML.Call("variable_global_set", "cin_active", false);
                GML.Call("variable_global_set", "active_speech", false);
                GML.Call("variable_global_set", "pain_active", false);
                GML.Call("variable_global_set", "tutorial_active", false);

                GML.Call("room_goto_next");

                Framework.Print("[Speedrun] Next Room");
            }
            catch (Exception ex)
            {
                Framework.PrintEx(
                    AurieLogSeverity.Error,
                    $"[Speedrun] Next Room Failed: {ex}"
                );
            }
        }

        private void GoToPreviousRoom()
        {
            try
            {
                GML.Call("room_goto_previous");

                Framework.Print("[Speedrun] Previous Room");
            }
            catch (Exception ex)
            {
                Framework.PrintEx(
                    AurieLogSeverity.Error,
                    $"[Speedrun] Previous Room Failed: {ex}"
                );
            }
        }




        private static double GetNumber(GameVariable instance, string variableName)
        {
            GameVariable? value = GML.GetInstanceVariable(instance, variableName);

            if (value is null)
                return 0;

            return ReadNumber(value);
        }

        private static double ReadNumber(GameVariable value)
        {
            if (double.TryParse(value.ToString(), out double result))
                return result;

            return 0;
        }
    }
}