Shader "Custom/InvertedHullOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 0, 1) // Yellow by default
        _OutlineThickness ("Outline Thickness", Range(0.001, 0.1)) = 0.02
    }
    
    SubShader
    {
        // For URP (Universal Render Pipeline)
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "OutlinePass"
            Cull Front // This is the "inverted hull" part - render the inside of the mesh
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION; // Local space position
                float3 normalOS : NORMAL;     // Local space normal
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION; // Clip space position
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineThickness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Move vertices outwards along their normals to make the shell slightly bigger
                float3 positionOS = input.positionOS.xyz + input.normalOS * _OutlineThickness;
                output.positionCS = TransformObjectToHClip(positionOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Fill the whole shell with the solid 
                return _OutlineColor; 
            }
            ENDHLSL
        }
    }
    
    // For Built-in Standard Render Pipeline (If not using URP)
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "OutlinePass"
            Cull Front
            ZWrite On
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineThickness;

            v2f vert(appdata v)
            {
                v2f o;
                v.vertex.xyz += v.normal * _OutlineThickness;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}