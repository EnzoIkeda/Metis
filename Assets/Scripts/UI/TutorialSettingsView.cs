using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botao das configuracoes que faz as dicas do tutorial aparecerem de novo.
public class TutorialSettingsView : MonoBehaviour
{
    [SerializeField] private Button _replayButton;

    [Tooltip("Quanto tempo (real) o aviso de feito fica no botao.")]
    [SerializeField, Min(0.1f)] private float _messageSeconds = 3f;

    private float _doneUntil = -1f;

    private void Start()
    {
        if (_replayButton != null)
            _replayButton.onClick.AddListener(HandleReplayClicked);
        LocalizationManager.OnLanguageChanged += RefreshLabel;
        RefreshLabel();
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLabel;
    }

    // Tempo real: a pausa zera a escala de tempo e as configuracoes tambem abrem nela.
    private void Update()
    {
        if (_doneUntil >= 0f && Time.unscaledTime >= _doneUntil)
        {
            _doneUntil = -1f;
            RefreshLabel();
        }
    }

    private void HandleReplayClicked()
    {
        TutorialProgress.Reset();
        _doneUntil = Time.unscaledTime + _messageSeconds;
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_replayButton == null)
            return;
        var label = _replayButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = _doneUntil >= 0f ? UIStrings.TutorialReplayDone : UIStrings.TutorialReplay;
    }
}
