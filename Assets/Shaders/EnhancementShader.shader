Shader "UI/ContrastBrightnessSaturation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Contrast ("Contrast", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1

        // --- UI Masking support ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        //UI masking : Decoupled
        _ClipRect ("Clip Rect (World)", Vector) = (0,0,0,0)
        _UseClip ("Use Rect Mask", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        
    Stencil //SplitView masking
    {
        Ref [_Stencil]
        Comp [_StencilComp]
        Pass [_StencilOp]
        ReadMask [_StencilReadMask]
        WriteMask [_StencilWriteMask]
    }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode] //SplitView masking
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]  //SplitView masking

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPos : TEXCOORD1; //decoupled mask

            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Contrast;
            float _Brightness;
            float _Saturation;
              float4 _ClipRect; //decoupled mask; xMin, yMin, xMax, yMax (world)
              float _UseClip;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                 o.worldPos = mul(unity_ObjectToWorld, v.vertex); //decoupled mask
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // --- Rectangular clip ---
                if (_UseClip > 0.5)
                {
                    if (i.worldPos.x < _ClipRect.x ||
                        i.worldPos.y < _ClipRect.y ||
                        i.worldPos.x > _ClipRect.z ||
                        i.worldPos.y > _ClipRect.w)
                    {
                        discard;
                    }
                }

                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // Contrast
                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;

                // Brightness
                col.rgb += _Brightness;

                // Saturation
                float luminance = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
                col.rgb = lerp(float3(luminance, luminance, luminance), col.rgb, _Saturation);

                return col;
            }
            ENDCG
        }
    }
}