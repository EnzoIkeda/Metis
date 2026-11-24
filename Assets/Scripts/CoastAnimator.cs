using System.Collections.Generic;
using UnityEngine;

// Movimento do litoral, so de enfeite: a textura do mar corre devagar e barcos e boias balancam.
public class CoastAnimator : MonoBehaviour
{
    [SerializeField] private Vector2 _waterSpeed = new Vector2(0.03f, 0.018f);
    [SerializeField, Min(0f)] private float _bobHeight = 0.012f;
    [SerializeField, Min(0f)] private float _bobSpeed = 1.4f;
    [SerializeField, Min(0f)] private float _rollDegrees = 3f;

    private readonly List<(Transform Item, Vector3 Base, Quaternion Rotation, float Phase)> _floating = new List<(Transform, Vector3, Quaternion, float)>();
    private Material _water;

    public int FloatingCount => _floating.Count;
    public Material WaterMaterial => _water;

    // Todas as celulas de mar usam uma copia so do material, para a textura correr junta sem mexer no asset.
    public void AddWater(GameObject tile)
    {
        var renderer = tile.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;
        if (_water == null)
            _water = new Material(renderer.sharedMaterial) { name = renderer.sharedMaterial.name + " (mar)" };
        renderer.sharedMaterial = _water;
    }

    public void AddFloating(Transform item, float phase)
    {
        _floating.Add((item, item.localPosition, item.localRotation, phase * Mathf.PI * 2f));
    }

    private void Update()
    {
        var time = Time.time;
        if (_water != null)
            _water.mainTextureOffset = _waterSpeed * time;
        foreach (var (item, basePosition, rotation, phase) in _floating)
        {
            if (item == null)
                continue;
            var wave = Mathf.Sin(time * _bobSpeed + phase);
            item.localPosition = basePosition + Vector3.up * (wave * _bobHeight);
            item.localRotation = Quaternion.Euler(wave * _rollDegrees, 0f, Mathf.Cos(time * _bobSpeed + phase) * _rollDegrees * 0.6f) * rotation;
        }
    }

    private void OnDestroy()
    {
        if (_water != null)
            Destroy(_water);
    }
}
