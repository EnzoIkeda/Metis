using System;
using System.Collections.Generic;
using UnityEngine;

// Todas as noticias que o jornal pode publicar.
[CreateAssetMenu(fileName = "NewsLibrary", menuName = "Metis/News Library")]
public class NewsLibraryData : ScriptableObject
{
    [SerializeField] private NewsStoryData[] _stories = Array.Empty<NewsStoryData>();

    [SerializeField] private NewsColumnData[] _columns = Array.Empty<NewsColumnData>();

    public IReadOnlyList<NewsStoryData> Stories => _stories;

    public IReadOnlyList<NewsColumnData> Columns => _columns;

    public NewsColumnData FindColumn(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var column in _columns)
        {
            if (column != null && column.Id == id)
                return column;
        }
        return null;
    }

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
