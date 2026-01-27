Shader "UI/FusionShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        //masking
        _MaskRect ("Mask Rect (Screen Space)", Vector) = (0,0,0,0)
        [Toggle(UI_MASK)] _UseMask ("Enable Masking", Float) = 0

        //enhancement
        _Contrast ("Contrast", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
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
        
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma shader_feature_local _ UI_MASK //masking


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
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MaskRect;
            float _Contrast;
            float _Brightness;
            float _Saturation;
           
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

        fixed4 frag (v2f i) : SV_Target
            {
                //masking
                #ifdef UI_MASK
                    float2 screenUV = i.screenPos.xy / i.screenPos.w;
                    float2 screenPx = screenUV * _ScreenParams.xy;
                    if (screenPx.x < _MaskRect.x ||
                        screenPx.y < _MaskRect.y ||
                        screenPx.x > _MaskRect.z ||
                        screenPx.y > _MaskRect.w)
                    {
                        discard;
                    }
                #endif

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