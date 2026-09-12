using System.Collections.Generic;

// Um no do mapa entre fases. Id identifica o destino; IsAvailable decide se pode ser escolhido.
public struct PhaseMapNode
{
    public string Id;
    public bool IsAvailable;
}

// Fonte dos nos do mapa de fase, pura e sem dependencia de UnityEngine.
// No Beta so existe 1 no (avancar direto), mas o formato de lista ja suporta mais rotas no futuro.
public static class PhaseMapProvider
{
    public static IReadOnlyList<PhaseMapNode> GetNodes()
    {
        return new[] { new PhaseMapNode { Id = "next", IsAvailable = true } };
    }
}
