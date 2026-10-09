using System.Collections.Generic;

// Um no do mapa entre fases. Id identifica o destino; IsAvailable decide se pode ser escolhido.
public struct PhaseMapNode
{
    public string Id;
    public bool IsAvailable;
}

// Nos do mapa entre fases; hoje so 1 (avancar), mas a lista ja comporta mais rotas.
public static class PhaseMapProvider
{
    public static IReadOnlyList<PhaseMapNode> GetNodes()
    {
        return new[] { new PhaseMapNode { Id = "next", IsAvailable = true } };
    }
}
