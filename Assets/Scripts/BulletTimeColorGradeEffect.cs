using UnityEngine;

[AddComponentMenu("Image Effects/Bullet Time Color Grade")]
[RequireComponent(typeof(Camera))]
public sealed class BulletTimeColorGradeEffect : MonoBehaviour
{
    private const string ShaderResourcePath = "Shaders/BulletTimeColorGrade";

    [SerializeField] private Color tint = Color.white;
    [SerializeField, Range(0f, 2f)] private float saturation = 1f;
    [SerializeField, Range(0f, 1f)] private float intensity;

    private Material material;
    private bool shaderWarningLogged;

    public Color Tint
    {
        get => tint;
        set => tint = value;
    }

    public float Saturation
    {
        get => saturation;
        set => saturation = Mathf.Clamp(value, 0f, 2f);
    }

    public float Intensity
    {
        get => intensity;
        set => intensity = Mathf.Clamp01(value);
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (Intensity <= 0f || !EnsureMaterial())
        {
            Graphics.Blit(source, destination);
            return;
        }

        material.SetColor("_Tint", tint);
        material.SetFloat("_Saturation", Saturation);
        material.SetFloat("_Intensity", Intensity);
        Graphics.Blit(source, destination, material);
    }

    void OnDisable()
    {
        ReleaseMaterial();
    }

    void OnDestroy()
    {
        ReleaseMaterial();
    }

    private bool EnsureMaterial()
    {
        if (material != null)
        {
            return true;
        }

        Shader shader = Resources.Load<Shader>(ShaderResourcePath);
        if (shader == null || !shader.isSupported)
        {
            if (!shaderWarningLogged)
            {
                Debug.LogWarning(
                    $"Bullet-time color grading shader could not be loaded from Resources/{ShaderResourcePath}.",
                    this);
                shaderWarningLogged = true;
            }

            return false;
        }

        material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return true;
    }

    private void ReleaseMaterial()
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }

        material = null;
    }
}
