using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainTexturePainter : MonoBehaviour
{
    [Header("Target")]
    public Terrain targetTerrain;

    [Header("Terrain Layers (0=ground, 1=cliff, 2=sand)")]
    public TerrainLayer groundLayer;
    public TerrainLayer cliffLayer;
    public TerrainLayer sandLayer;

    [Header("Beach")]
    [Tooltip("Distance inland from the south map edge covered by sand, in terrain units.")]
    public float beachLength = 20f;
    [Tooltip("Blend distance at the beach's inland edge. Set to 0 for a hard edge.")]
    public float beachBlend = 3f;

    [Header("Cliff")]
    [Tooltip("Slope in degrees above which terrain is considered cliff.")]
    public float cliffSlope = 35f;
    [Tooltip("Slope falloff in degrees used when blending cliff edges.")]
    public float cliffFalloff = 8f;

    [Header("Noise / Tuning")]
    [Range(0.001f, 0.5f)] public float noiseScale = 0.06f;
    [Range(0f, 1f)] public float noiseStrength = 0.35f;
    [Tooltip("Random seed for Perlin variations (0 = random).")]
    public float seed = 0f;

    [Header("Performance")]
    [Tooltip("Use terrain alpha map resolution for painting. Lower this for faster but blockier results.")]
    public int overrideAlphamapResolution = 0;

    void Reset()
    {
        targetTerrain = GetComponent<Terrain>();
    }

    [ContextMenu("Paint Terrain Textures")]
    public void PaintTextures()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
        if (targetTerrain == null)
        {
            Debug.LogWarning("TerrainTexturePainter: no terrain assigned.");
            return;
        }

        TerrainData td = targetTerrain.terrainData;
        if (td == null)
        {
            Debug.LogWarning("TerrainTexturePainter: terrainData is null on the assigned Terrain.");
            return;
        }

        if (groundLayer == null || cliffLayer == null)
        {
            Debug.LogWarning("TerrainTexturePainter: assign the ground and cliff TerrainLayer assets. Aborting paint.");
            return;
        }

        bool paintBeach = sandLayer != null && beachLength > 0f;

        td.terrainLayers = paintBeach
            ? new TerrainLayer[] { groundLayer, cliffLayer, sandLayer }
            : new TerrainLayer[] { groundLayer, cliffLayer };

        int alphRes = overrideAlphamapResolution > 0
            ? overrideAlphamapResolution
            : td.alphamapResolution;
        int numLayers = paintBeach ? 3 : 2;

        float[,,] alphaMaps = new float[alphRes, alphRes, numLayers];

        float mapSeed = seed;
        if (mapSeed == 0f) mapSeed = Random.Range(1f, 10000f);

        for (int y = 0; y < alphRes; y++)
        {
            for (int x = 0; x < alphRes; x++)
            {
                float u = (float)x / (alphRes - 1f);
                float v = (float)y / (alphRes - 1f);

                Vector3 normal = td.GetInterpolatedNormal(u, v);
                float slopeDeg = Vector3.Angle(normal, Vector3.up);

                float noise = (Mathf.PerlinNoise(
                    u / noiseScale + mapSeed * 0.001f,
                    v / noiseScale + mapSeed * 0.002f) - 0.5f) * 2f;
                noise *= noiseStrength;

                float cliffRaw = Mathf.Clamp01(
                    (slopeDeg - cliffSlope) / Mathf.Max(0.0001f, cliffFalloff));
                cliffRaw = Mathf.Clamp01(cliffRaw + noise * 0.35f);

                float groundRaw = 1f - cliffRaw;

                cliffRaw = Mathf.SmoothStep(0f, 1f, cliffRaw);
                groundRaw = Mathf.SmoothStep(0f, 1f, groundRaw);

                float baseSum = groundRaw + cliffRaw;
                if (baseSum <= 0f) baseSum = 1f;

                float groundWeight = groundRaw / baseSum;
                float cliffWeight = cliffRaw / baseSum;

                float beachWeight = 0f;
                if (paintBeach)
                {
                    float distanceFromSouth = v * td.size.z;
                    float blendDistance = Mathf.Clamp(beachBlend, 0f, beachLength);
                    float blendStart = beachLength - blendDistance;

                    if (distanceFromSouth <= blendStart)
                    {
                        beachWeight = 1f;
                    }
                    else if (distanceFromSouth < beachLength && blendDistance > 0f)
                    {
                        beachWeight = 1f - Mathf.SmoothStep(
                            blendStart,
                            beachLength,
                            distanceFromSouth);
                    }
                }

                float otherWeight = 1f - beachWeight;
                alphaMaps[y, x, 0] = groundWeight * otherWeight;
                alphaMaps[y, x, 1] = cliffWeight * otherWeight;

                if (paintBeach)
                {
                    alphaMaps[y, x, 2] = beachWeight;
                }
            }
        }

        td.SetAlphamaps(0, 0, alphaMaps);
        Debug.Log($"TerrainTexturePainter: Painted {alphRes}x{alphRes} splatmap.");
    }
}