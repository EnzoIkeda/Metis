using System.Collections.Generic;
using UnityEngine;

// Vantagem passiva carregada de uma fase pra outra, aplicada uma unica vez no inicio da fase.
[CreateAssetMenu(fileName = "New Advantage", menuName = "Metis/Passive Advantage Data")]
public class PassiveAdvantageData : ScriptableObject
{
    [SerializeField] private string _advantageName;
    [SerializeField] private string _advantageNameEn;
    [SerializeField, TextArea(2, 6)] private string _description;
    [SerializeField, TextArea(2, 6)] private string _descriptionEn;
    [SerializeField] private StatModifier[] _statEffects;

    public string AdvantageName => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_advantageNameEn) == false
        ? _advantageNameEn
        : _advantageName;

    public string Description => LocalizationManager.Current == Language.English && string.IsNullOrEmpty(_descriptionEn) == false
        ? _descriptionEn
        : _description;

    public IReadOnlyList<StatModifier> StatEffects => _statEffects;
}
