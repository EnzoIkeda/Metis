using UnityEngine;
using UnityEngine.UI;

// Toca o som de clique ao apertar o botao, sem precisar mexer na view que trata o clique.
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClick);
    }

    private static void HandleClick()
    {
        SfxPlayer.Play(SoundEffect.ButtonClick);
    }
}
