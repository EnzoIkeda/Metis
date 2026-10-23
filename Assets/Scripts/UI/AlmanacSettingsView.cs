using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Opcoes do almanaque nas configuracoes: revelar todas as fichas ou zerar o progresso. As duas pedem um
// segundo toque pra confirmar, ja que nao da pra desfazer.
public class AlmanacSettingsView : MonoBehaviour
{
    [SerializeField] private KnowledgeLibraryData _library;
    [SerializeField] private Button _revealButton;
    [SerializeField] private Button _resetButton;

    [Tooltip("Quanto tempo (real) o pedido de confirmacao ou o aviso de feito fica no botao.")]
    [SerializeField, Min(0.1f)] private float _messageSeconds = 3f;

    private Button _armed;
    private Button _done;
    private float _messageUntil;

    private void Start()
    {
        if (_revealButton != null)
            _revealButton.onClick.AddListener(HandleRevealClicked);
        if (_resetButton != null)
            _resetButton.onClick.AddListener(HandleResetClicked);
        LocalizationManager.OnLanguageChanged += RefreshLabels;
        RefreshLabels();
    }

    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLabels;
    }

    // Tempo real: a pausa zera a escala de tempo e as configuracoes tambem abrem nela.
    private void Update()
    {
        if ((_armed != null || _done != null) && Time.unscaledTime >= _messageUntil)
        {
            _armed = null;
            _done = null;
            RefreshLabels();
        }
    }

    // Fechar as configuracoes no meio da confirmacao cancela o pedido.
    private void OnDisable()
    {
        _armed = null;
        _done = null;
        RefreshLabels();
    }

    private void HandleRevealClicked()
    {
        if (Arm(_revealButton))
            return;
        if (_library != null)
            AlmanacProgress.UnlockAll(_library.Entries);
        ShowDone(_revealButton);
    }

    private void HandleResetClicked()
    {
        if (Arm(_resetButton))
            return;
        AlmanacProgress.Reset();
        ShowDone(_resetButton);
    }

    // Primeiro toque so pede confirmacao; devolve falso quando o botao ja estava armado.
    private bool Arm(Button button)
    {
        if (_armed == button)
            return false;
        _armed = button;
        _done = null;
        _messageUntil = Time.unscaledTime + _messageSeconds;
        RefreshLabels();
        return true;
    }

    private void ShowDone(Button button)
    {
        _armed = null;
        _done = button;
        _messageUntil = Time.unscaledTime + _messageSeconds;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        SetLabel(_revealButton, UIStrings.AlmanacRevealSetting, UIStrings.AlmanacRevealedSetting);
        SetLabel(_resetButton, UIStrings.AlmanacResetSetting, UIStrings.AlmanacResetDoneSetting);
    }

    private void SetLabel(Button button, string idle, string done)
    {
        if (button == null)
            return;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;
        if (_armed == button)
            label.text = UIStrings.AlmanacConfirmSetting;
        else if (_done == button)
            label.text = done;
        else
            label.text = idle;
    }
}
