Shader "Hidden/MicroVerse/ObjectSilhouette"
{
    // Renders every surface as solid white with depth writes. When the scene
    // is captured via a top-down ortho CommandBuffer with a black clear
    // colour, the resulting RT is a clean binary silhouette of the visible
    // renderers — used by ClearStamp to mask trees / details / objects
    // against the footprint of scene geometry on selected layers.
    //
    // Terrain-aware fragment discard
    // ------------------------------
    // A half-buried rock used to paint its full XZ footprint into the mask
    // because the projection is top-down and the rasterizer happily writes
    // every fragment that survives the depth test — including fragments
    // whose worldY is below the terrain surface.
    //
    // We fix this in the fragment shader: sample the terrain heightmap at
    // the fragment's world XZ, reconstruct the terrain's world-space Y at
    // that point, and `discard` if the fragment is below it (minus a small
    // tolerance to keep contact points stable). The top-down + ZTest LEqual
    // pipeline then automatically resolves to "this pixel is white iff some
    // surface of the mesh at this XZ is above the terrain surface".
    //
    // The C# side (ClearMeshStamp.BuildSilhouette) binds the active
    // terrain's heightmap and bounds on the material before recording the
    // draws. _MicroVerseHM_TexelSize is auto-populated by Unity.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Terrain heightmap. R16 RenderTexture from
            // Terrain.terrainData.heightmapTexture. Sampled value is the
            // legacy [0, 0.5] normalized height — see decode below.
            sampler2D _MicroVerseHM;
            float4    _MicroVerseHM_TexelSize; // (1/w, 1/h, w, h), auto.

            // (originX, originZ, sizeX, sizeZ) of the active terrain in
            // world space. Lets us map fragment.worldXZ -> heightmap UV.
            float4 _MicroVerseHMOriginSize;

            // (originY, sizeY) of the active terrain in world space. Used
            // to decode the sampled normalized height into a world-space Y.
            float2 _MicroVerseHMHeight;

            // World-space tolerance: fragments below the terrain by more
            // than this distance are discarded. Tiny positive value keeps
            // resting-on-surface contact stable against float precision /
            // heightmap quantization. C# side defaults to ~0.02.
            float _MicroVerseHMEpsilon;

            // Master switch — set to 0 by C# if there is no terrain to
            // clip against (e.g. silhouette built outside any Terrain).
            // When 0 the shader behaves like the original: solid white,
            // no per-fragment discard.
            float _MicroVerseHMEnabled;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_MicroVerseHMEnabled > 0.5)
                {
                    // Map worldXZ -> normalized terrain UV in [0,1].
                    // Outside the [0,1] range means "off this terrain
                    // tile" — the silhouette RT is projected to match the
                    // terrain footprint so this only happens for slop at
                    // the edges; clamp UV to stay on the heightmap.
                    float2 uv = (i.worldPos.xz - _MicroVerseHMOriginSize.xy)
                              / _MicroVerseHMOriginSize.zw;
                    uv = saturate(uv);

                    // Heightmap samples sit at terrain vertices, so the
                    // texture has N texels covering N-1 quad cells. Apply
                    // the standard half-texel-inset remap so worldXZ at
                    // a heightmap vertex samples that vertex exactly.
                    float2 res = _MicroVerseHM_TexelSize.zw;          // (w, h)
                    uv = (uv * (res - 1.0) + 0.5) * _MicroVerseHM_TexelSize.xy;

                    // Decode normalized height -> world Y. Unity stores
                    // heights in [0, 0.5] (legacy) in the R channel, so
                    // worldHeight = origin.y + r * 2 * size.y.
                    float h01     = tex2Dlod(_MicroVerseHM, float4(uv, 0, 0)).r;
                    float terrainY = _MicroVerseHMHeight.x + h01 * 2.0 * _MicroVerseHMHeight.y;

                    // Discard the buried portion of the mesh — the part
                    // that pokes "into" or under the terrain. Anything
                    // resting at or above the surface (within tolerance)
                    // is kept, so a rock sitting on the ground still
                    // contributes its full visible footprint.
                    if (i.worldPos.y < terrainY - _MicroVerseHMEpsilon)
                        discard;
                }

                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
