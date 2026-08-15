using UnityEngine;

public class StructureModel : MonoBehaviour
{
    public void CreateModel(GameObject model)
    {
        Instantiate(model, transform);
    }
}
