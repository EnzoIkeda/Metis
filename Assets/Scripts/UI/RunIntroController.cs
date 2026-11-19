using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Abertura de uma rodada nova: as telas da posse do novo gestor, com musica propria, enquanto a cidade
// carrega em segundo plano. No fim, ou ao pular, entra na cidade, que faz o sobrevoo.
public class RunIntroController : MonoBehaviour
{
    public const string SceneName = "RunIntro";
    public const int SlideCount = 10;

    private const float SlideDuration = 4f;
    private const float LastSlideDuration = 5.5f;

    [SerializeField] private SlideSequenceView _view;

    // Desligado nos testes, que nao carregam a cidade.
    [SerializeField] private bool _loadCity = true;

    private AsyncOperation _cityLoad;

    public bool IsDone { get; private set; }

    public static List<SlideStep> Steps()
    {
        var steps = new List<SlideStep>();
        for (var i = 0; i < SlideCount; i++)
            steps.Add(new SlideStep(i, UIStrings.IntroCaptionKey(i), i == SlideCount - 1 ? LastSlideDuration : SlideDuration));
        return steps;
    }

    private void Start()
    {
        var player = MusicPlayer.Instance;
        if (player != null && player.Themes != null)
            player.PlaySequence(player.Themes.Opening, true);

        if (_loadCity)
        {
            _cityLoad = SceneManager.LoadSceneAsync(MetaProgressionManager.CitySceneName);
            if (_cityLoad != null)
                _cityLoad.allowSceneActivation = false;
        }

        if (_view == null)
        {
            EnterCity();
            return;
        }
        _view.Finished += EnterCity;
        var archetype = MetaProgressionManager.Archetype;
        _view.Play(Steps(), key => UIStrings.SlideCaption(key, archetype));
    }

    private void OnDestroy()
    {
        if (_view != null)
            _view.Finished -= EnterCity;
    }

    private void EnterCity()
    {
        if (IsDone)
            return;
        IsDone = true;
        RunIntroHandoff.PlayFlyover = true;
        if (_cityLoad != null)
            _cityLoad.allowSceneActivation = true;
        else if (_loadCity)
            SceneManager.LoadScene(MetaProgressionManager.CitySceneName);
    }
}
