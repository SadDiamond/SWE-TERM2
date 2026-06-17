using UnityEngine;

public sealed class WeaponModelBuilder
{
    private readonly Transform root;

    public WeaponModelBuilder(Transform root)
    {
        this.root = root;
    }

    public Transform Box(string name, Vector3 position, Vector3 scale, Material material, Vector3 rotation = default)
    {
        return Primitive(name, PrimitiveType.Cube, position, scale, Quaternion.Euler(rotation), material);
    }

    public Transform Cylinder(string name, Vector3 position, float radius, float length, Material material, Vector3 rotation = default)
    {
        // Unity cylinders are two units tall along local Y.
        return Primitive(name, PrimitiveType.Cylinder, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), Quaternion.Euler(rotation), material);
    }

    public Transform Sphere(string name, Vector3 position, Vector3 scale, Material material)
    {
        return Primitive(name, PrimitiveType.Sphere, position, scale, Quaternion.identity, material);
    }

    public Transform Capsule(string name, Vector3 position, float radius, float length, Material material, Vector3 rotation = default)
    {
        return Primitive(name, PrimitiveType.Capsule, position, new Vector3(radius * 2f, length * 0.5f, radius * 2f), Quaternion.Euler(rotation), material);
    }

    public void MirroredBox(string name, Vector3 position, Vector3 scale, float spacing, Material material, Vector3 leftRotation = default)
    {
        Vector3 left = position + Vector3.left * spacing;
        Vector3 right = position + Vector3.right * spacing;
        Box(name + "L", left, scale, material, leftRotation);
        Box(name + "R", right, scale, material, new Vector3(leftRotation.x, -leftRotation.y, -leftRotation.z));
    }

    public void Coil(string name, Vector3 center, float radius, float spacing, int count, Material material, Vector3 rotation = default)
    {
        for (int i = 0; i < count; i++)
        {
            float z = (i - (count - 1) * 0.5f) * spacing;
            Cylinder(name + i, center + Vector3.forward * z, radius, 0.055f, material, rotation + new Vector3(90f, 0f, 0f));
        }
    }

    private Transform Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(root, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return part.transform;
    }
}
