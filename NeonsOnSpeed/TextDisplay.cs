using System.Text;
using TMPro;
using UnityEngine;

namespace NeonsOnSpeed
{
    internal class TextDisplay : MonoBehaviour
    {
        private GameObject _textHolder;
        private TextMeshProUGUI _text;

        private float _timer = 0f;
        private string _timerText = string.Empty;
        private float _timerMax;
        private string _runID = string.Empty;
        public string SessionPB = string.Empty;
        public bool UpdateTimer = false;
        public bool Staging = false;

        public delegate void Callback();
        private Callback _onTimerExpire; 

        void Start()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _textHolder = new("textHolder");
            _textHolder.transform.parent = gameObject.transform;
            _textHolder.layer = 5;
            _textHolder.transform.localPosition = new(Screen.width / 2 - 55, Screen.height / 2 - 60);
            _text = _textHolder.AddComponent<TextMeshProUGUI>();
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.fontSize = 26f;
            _text.enableWordWrapping = true;
            _text.outlineColor = Color.black;
            _text.outlineWidth = 0.15f;
            _text.lineSpacing = -30f;
        }

        void Update()
        {

            if (Staging)
            {
                _text.SetText($"{_runID}\n{Utils.FloatToTime(_timerMax, "#00:00")}");
                return;
            }

            if (!UpdateTimer) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer >= _timerMax)
            {
                _timer = _timerMax;
                _onTimerExpire();
            }
            SetTimer();
        }

        public void SetStaging(int timerMax, Callback onTimerExpire)
        {
            Reset();
            _textHolder.SetActive(true);
            _onTimerExpire = onTimerExpire;
            _timerMax = timerMax;
            _runID = GenerateID();
            Staging = true;
        }

        public void Run()
        {
            UpdateTimer = true;
            Staging = false;
        }

        public void Reset()
        {
            _textHolder.SetActive(false);
            _onTimerExpire = null;
            _timer = 0f;
            _timerMax = 0f;
            _timerText = string.Empty;
            _runID = string.Empty;
            SessionPB = string.Empty;
        }

        public void SetTimer()
        {
            _timerText = Utils.FloatToTime(_timer, "#00:00");
            _text.SetText($"{_runID}\n{_timerText}\n{SessionPB}");
        }

        private static string GenerateID()
        {
            StringBuilder builder = new();

            for (int i = 0; i < 8; i++)
            {
                int idx = UnityEngine.Random.Range(65, 101);

                if (idx > 90)
                    idx -= 43;

                builder.Append((char)idx);
            }
            return builder.ToString();
        }
    }
}
