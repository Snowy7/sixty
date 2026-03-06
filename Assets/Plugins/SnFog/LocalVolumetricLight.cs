// LocalVolumetricLight.cs (continued)
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Light))]
public class LocalVolumetricLight : MonoBehaviour
{
    [Header("Volumetric Settings")]
    [Range(0f, 10f)]
    public float volumetricIntensity = 1f;
    
    [Range(0f, 1f)]
    public float volumetricShadowDimmer = 0.5f;
    
    [Range(1f, 32f)]
    public float range = 10f;
    
    [Header("Performance")]
    [Tooltip("Sample this light in volumetric fog")]
    public bool affectVolumetricFog = true;
    
    private Light _light;

    private void OnEnable()
    {
        _light = GetComponent<Light>();
    }

    private void OnValidate()
    {
        _light = GetComponent<Light>();
        if (_light != null)
        {
            _light.range = range;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_light == null)
            _light = GetComponent<Light>();
        
        if (_light == null) return;

        Gizmos.color = Color.yellow;
        
        switch (_light.type)
        {
            case LightType.Point:
                Gizmos.DrawWireSphere(transform.position, range);
                break;
            case LightType.Spot:
                DrawSpotGizmo();
                break;
        }
    }

    private void DrawSpotGizmo()
    {
        float spotAngleRad = _light.spotAngle * Mathf.Deg2Rad * 0.5f;
        
        Vector3 coneBaseCenter = transform.position + transform.forward * range;
        float coneBaseRadius = range * Mathf.Tan(spotAngleRad);
        
        // Draw cone outline
        int circleSegments = 16;
        for (int i = 0; i < circleSegments; i++)
        {
            float angle0 = (i / (float)circleSegments) * Mathf.PI * 2f;
            float angle1 = ((i + 1) / (float)circleSegments) * Mathf.PI * 2f;
            
            Vector3 point0 = coneBaseCenter + (Mathf.Cos(angle0) * transform.right + Mathf.Sin(angle0) * transform.up) * coneBaseRadius;
            Vector3 point1 = coneBaseCenter + (Mathf.Cos(angle1) * transform.right + Mathf.Sin(angle1) * transform.up) * coneBaseRadius;
            
            Gizmos.DrawLine(transform.position, point0);
            Gizmos.DrawLine(point0, point1);
        }
    }
}