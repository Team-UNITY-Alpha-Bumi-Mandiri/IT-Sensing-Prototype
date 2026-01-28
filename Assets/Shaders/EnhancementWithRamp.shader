Shader "Custom/EnhancementWithRamp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RampTex ("Ramp Texture", 2D) = "white" {}
        _UseRamp ("Use Ramp", Float) = 0
        
        _Contrast ("Contrast", Float) = 1
        _Brightness ("Brightness", Float) = 0
        _Saturation ("Saturation", Float) = 1
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
        
        Stencil
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
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _RampTex;
            float _UseRamp;
            
            float _Contrast;
            float _Brightness;
            float _Saturation;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Jika UseRamp aktif, gunakan red channel sebagai index gradient
                if (_UseRamp > 0.5)
                {
                    // Clamp UV agar tidak bleeding
                    float val = clamp(col.r, 0.01, 0.99); 
                    fixed4 rampCol = tex2D(_RampTex, float2(val, 0.5));
                    
                    // Pertahankan alpha dari texture asli (jika transparan)
                    col.rgb = rampCol.rgb;
                    col.a *= rampCol.a;
                }
                
                // Apply vertex color (tint)
                col *= i.color;

                // --- ENHANCEMENT ---
                
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
