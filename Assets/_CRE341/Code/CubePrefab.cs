using UnityEngine;

public class CubePrefab : MonoBehaviour
{
    [SerializeField] private Material wallMaterial;
    
    private void Awake()
    {
        // Ensure the cube has a renderer
        if (GetComponent<Renderer>() == null)
        {
            gameObject.AddComponent<MeshRenderer>();
            gameObject.AddComponent<MeshFilter>().mesh = CreateCubeMesh();
        }
        
        // Apply material if provided
        if (wallMaterial != null)
        {
            GetComponent<Renderer>().material = wallMaterial;
        }
        
        // Ensure the cube has a collider
        if (GetComponent<BoxCollider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }
    
    // Create a simple cube mesh if needed
    private Mesh CreateCubeMesh()
    {
        return Resources.GetBuiltinResource<Mesh>("Cube.mesh");
    }
} 