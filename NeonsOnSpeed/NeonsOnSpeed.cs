using HarmonyLib;
using MelonLoader;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonsOnSpeed
{
    [HarmonyPatch]
    internal class NeonsOnSpeed : MelonMod
    {
        private static NeonsOnSpeed _instance;
        private State _state = State.Disabled;

        private MelonPreferences_Entry<bool> _enabled;
        private MelonPreferences_Entry<int> _timerMax;
        private MelonPreferences_Entry<string> _opponentSessionID;

        private static readonly StringBuilder timerBuilder = new();

        private TextDisplay _display;
        private bool _lastState = false;
        private long _sessionPB = -1;
        private bool _savePB = false;

        private string _levelID = "";

        public override void OnApplicationLateStart()
        {
            _instance = this;

            MelonPreferences_Category categoryNoS = MelonPreferences.CreateCategory("Neons on Speed");
            _enabled = categoryNoS.CreateEntry("Enter Competition Mode", false, description: "This setting will start a timer and lock you in the first level you enter");
            _timerMax = categoryNoS.CreateEntry("Timer Length", 600, description: "The time you get to try your best on a level");
            _opponentSessionID = categoryNoS.CreateEntry("Opponent Session ID", "", description: "Just a place where you can store your opponents Pure session ID");

            _enabled.Value = false;

            HarmonyLib.Harmony harmony = new("de.MOPSKATER.NeonsOnSpeed");
            GameObject go = new("Neons on Speed");
            _display = go.AddComponent<TextDisplay>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            Singleton<Game>.Instance.OnLevelLoadComplete += () =>
            {
                string currentLevel = Singleton<Game>.Instance.GetCurrentLevel().levelID;
                if (currentLevel == "HUB_HEAVEN" || _state == State.Disabled)
                {
                    _savePB = false;
                    return;
                }

                _savePB = true;
                if (_state != State.Staging) return;

                if (_levelID == "")
                    _levelID = currentLevel;
                _state = State.Running;
                _display.Run();
            };
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "OnLevelWin")]
        private static void PreOnLevelWin()
        {
            if (!_instance._savePB) return;

            long currentScore = Singleton<Game>.Instance.GetCurrentLevelTimerMicroseconds();
            if (_instance._sessionPB == -1)
            {
                _instance._sessionPB = currentScore;
                _instance._display.SessionPB = LongToTime(currentScore);
                return;
            }

            if (_instance._sessionPB > currentScore)
            {
                _instance._sessionPB = currentScore;
                _instance._display.SessionPB = LongToTime(currentScore);
            }
            _instance._display.SetTimer();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MelonPrefManager.UI.UIManager), "SavePreferences")]
        private static void OnSavePreferences()
        {
            if (_instance._enabled.Value == _instance._lastState) return;

            _instance._lastState = _instance._enabled.Value;

            if (!_instance._enabled.Value)
            {
                _instance._state = State.Disabled;
                _instance._lastState = false;
                _instance._savePB = false;
                _instance._display.Reset();
                return;
            }

            if (!SceneManager.GetActiveScene().name.Equals("Heaven_Environment") && !SceneManager.GetActiveScene().name.Equals("Menu"))
            {
                _instance._enabled.Value = false;
                _instance._lastState = false;
                return;
            }

            _instance._sessionPB = -1;
            _instance._levelID = "";
            _instance._state = State.Staging;
            _instance._display.SetStaging(_instance._timerMax.Value, () =>
            {
                _instance._state = State.Disabled;
                _instance._enabled.Value = false;
                _instance._lastState = false;
                _instance._display.UpdateTimer = false;
                AudioController.Play("UI_RELATIONSHIP_LEVEL_UP", 2f);
            });
        }

        public static string LongToTime(long time)
        {
            time /= 1000;
            int num = (int)(time / 60000L);
            int num2 = (int)(time / 1000L) % 60;
            int num3 = (int)(time - (long)(num * 60000) - (long)(num2 * 1000));
            timerBuilder.Clear();
            if (num > 99)
            {
                timerBuilder.Append((char)((num / 100) + 48));
            }
            timerBuilder.Append((char)((num / 10) + 48));
            timerBuilder.Append((char)((num % 10) + 48));
            timerBuilder.Append(':');
            timerBuilder.Append((char)((num2 / 10) + 48));
            timerBuilder.Append((char)((num2 % 10) + 48));
            timerBuilder.Append('.');
            timerBuilder.Append((char)((num3 / 100) + 48));
            num3 %= 100;
            timerBuilder.Append((char)((num3 / 10) + 48));
            timerBuilder.Append((char)((num3 % 10) + 48));
            return timerBuilder.ToString();
        }

        private enum State
        {
            Disabled,
            Staging,
            Running
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MenuScreenLevelRush), "StartLevelRush")]
        private static bool BlockLevelRush() =>
            !_instance._enabled.Value && _instance._state == State.Disabled;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", new Type[] { typeof(string), typeof(bool), typeof(Action) })]
        private static bool LockInLevel(ref string newLevelID)
        {
            if (_instance._enabled.Value && _instance._state == State.Running)
                return newLevelID == _instance._levelID || newLevelID == "xxx";
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonNextLevel")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonJobArchiveFromLocation")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonReturnToHub")]
        [HarmonyPatch(typeof(MainMenu), "OnPressButtonQuitLevel")]
        private static bool PreventPlayNextArchiveLevel() =>
            !_instance._enabled.Value && _instance._state == State.Disabled;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MenuScreenLocation), "CreateActionButton")]
        private static bool PreCreateActionButton(ref HubAction hubAction) =>
            hubAction.ID != "PORTAL_CONTINUE_MISSION";

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MechController), "Die")]
        private static void PauseTimerForLoading(ref bool restartImmediately, ref bool playRestartSound)
        {
            if (restartImmediately && playRestartSound)
                _instance._display.UpdateTimer = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MainMenu), "SetState")]
        private static void ContinueTimer(ref MainMenu.State newState, ref bool fromRestart, ref bool fromArchive)
        {
            if (newState == MainMenu.State.Staging && MainMenu.Instance().GetCurrentState() == MainMenu.State.Staging && fromRestart && !fromArchive)
                _instance._display.UpdateTimer = true;
        }
    }
}
