using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CardSpawner : MonoBehaviour
{
    [Header("Configuracoes")]
    [SerializeField] private GameObject cardPrefab; 
    [SerializeField] private Transform cardContainer;

    [Header("Baralho de Cartas Disponiveis")]
    [SerializeField] private List<CardData> deck = new List<CardData>();

    private GameObject currentSpawnedCard;

    private void Start()
    {
        SortearECriarCarta();
    }

    private void Update()
    {
        // to-do (bea) remover depois que terminar de debugar
        // Permite sortear uma nova carta ao pressionar a tecla de espaço
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SortearECriarCarta();
        }
    }

    public void SortearECriarCarta()
    {
        if (deck.Count == 0 || cardPrefab == null)
        {
            // to-do (bea) tirar depois que terminar de debugar
            Debug.LogWarning("Verifique se o Prefab e o Deck de cartas foram atribuidos no Inspector!");
            return;
        }

        // 1. Destroi a carta anterior da tela, se houver
        if (currentSpawnedCard != null)
        {
            Destroy(currentSpawnedCard);
        }

        // 2. Sorteia um indice aleatorio do baralho
        int randomIndex = Random.Range(0, deck.Count);
        CardData cartaSorteada = deck[randomIndex];

        // 3. Instancia o Prefab da carta dentro do Container da UI
        // por enquanto centraliza
        currentSpawnedCard = Instantiate(cardPrefab, cardContainer, false);
        currentSpawnedCard.transform.localPosition = Vector3.zero;
        currentSpawnedCard.transform.localScale = Vector3.one;

        // 4. Injeta os dados da carta sorteada
        CardDisplay display = currentSpawnedCard.GetComponent<CardDisplay>();
        if (display != null)
        {
            display.SetupCard(cartaSorteada);
        }

        Debug.Log("Carta sorteada: " + cartaSorteada.cardName);
    }
}
