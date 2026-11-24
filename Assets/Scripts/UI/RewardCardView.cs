using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Uma opcao da recompensa de fim de fase, desenhada como carta: tipo, nome, arte, descricao e o botao de escolher.
// Nome e descricao encolhem a letra para caber no espaco fixo da carta.
public class RewardCardView : MonoBehaviour
{
    [SerializeField] private TMP_Text _kindText;
    [SerializeField] private Image _kindBackground;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _artworkImage;
    [SerializeField] private GameObject _artworkPlaceholder;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Button _chooseButton;
    [SerializeField] private TMP_Text _chooseText;

    [Tooltip("Arte das vantagens, que nao tem ilustracao propria.")]
    [SerializeField] private Sprite _advantageArtwork;
    [SerializeField] private Color _cardKindColor = new Color(0.25f, 0.55f, 0.95f);
    [SerializeField] private Color _advantageKindColor = new Color(0.98f, 0.72f, 0.18f);

    public Button ChooseButton => _chooseButton;

    public void Bind(UnityEngine.Object option)
    {
        if (option is CardData card)
            Show(UIStrings.RewardKindCard, _cardKindColor, card.CardName, card.Artwork, card.Description);
        else if (option is PassiveAdvantageData advantage)
            Show(UIStrings.RewardKindAdvantage, _advantageKindColor, advantage.AdvantageName, _advantageArtwork, advantage.Description);
        else
            throw new ArgumentException("Opcao de recompensa sem tipo conhecido.", nameof(option));
    }

    private void Show(string kind, Color kindColor, string title, Sprite artwork, string description)
    {
        if (_kindText != null)
            _kindText.text = kind;
        if (_kindBackground != null)
            _kindBackground.color = kindColor;
        if (_nameText != null)
            _nameText.text = title;
        var hasArtwork = artwork != null;
        if (_artworkImage != null)
        {
            _artworkImage.sprite = artwork;
            _artworkImage.gameObject.SetActive(hasArtwork);
        }
        if (_artworkPlaceholder != null)
            _artworkPlaceholder.SetActive(hasArtwork == false);
        if (_descriptionText != null)
            _descriptionText.text = description;
        if (_chooseText != null)
            _chooseText.text = UIStrings.RewardChooseButton;
    }
}
