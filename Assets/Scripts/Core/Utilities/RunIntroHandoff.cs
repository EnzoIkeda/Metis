// Aviso de uso unico da abertura para a cidade: a primeira fase de uma rodada nova sobrevoa a cidade.
// Fica fora do save de proposito: fechar o app no meio leva a cidade sem abertura, o que e aceitavel.
public static class RunIntroHandoff
{
    public static bool PlayFlyover { get; set; }

    // Le e desliga, para o sobrevoo nunca repetir ao recarregar a cena.
    public static bool ConsumeFlyover()
    {
        var value = PlayFlyover;
        PlayFlyover = false;
        return value;
    }
}
