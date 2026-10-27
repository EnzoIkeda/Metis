using System;
using System.Collections.Generic;
using UnityEngine;

// Todas as noticias que o jornal pode publicar.
[CreateAssetMenu(fileName = "NewsLibrary", menuName = "Metis/News Library")]
public class NewsLibraryData : ScriptableObject
{
    [SerializeField] private NewsStoryData[] _stories = Array.Empty<NewsStoryData>();

    public IReadOnlyList<NewsStoryData> Stories => _stories;

    public NewsStoryData Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var story in _stories)
        {
            if (story != null && story.Id == id)
                return story;
        }
        return null;
    }
}
