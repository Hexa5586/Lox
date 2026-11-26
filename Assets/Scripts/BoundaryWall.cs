using UnityEngine;

[DisallowMultipleComponent]
public class BoundaryWall : MonoBehaviour
{
    [SerializeField] private string wallId;
    [SerializeField] private string faceName;
    [SerializeField] private string sideName;

    public string Id => wallId;
    public string FaceName => faceName;
    public string SideName => sideName;

    public void Configure(string id, string face, string side)
    {
        wallId = id;
        faceName = face;
        sideName = side;
    }

    public static string BuildId(string face, string side)
    {
        return $"boundary.{face}.{side}";
    }
}
