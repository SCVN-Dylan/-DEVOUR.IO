Shader "Devour/VFX/Suction Y Scroll Fade"
{
    Properties
    {
        [MainTexture] _MainTex ("Wind Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (0.35, 0.9, 1.0, 1.0)
        _Brightness ("Brightness", Range(0.0, 8.0)) = 1.0

        _ScrollSpeed ("Y Scroll Speed", Float) = 1.0
        [Toggle] _Reverse ("Reverse Y Direction", Float) = 0.0
        [Toggle] _EnableAnimation ("Enable Animation", Float) = 0.0
        _AnimationY ("Animation Y Offset (Keyframe)", Float) = 0.0

        _FadeBottom ("Bottom Y Fade", Range(0.001, 0.5)) = 0.15
        _FadeTop ("Top Y Fade", Range(0.001, 0.5)) = 0.15
        _FadePower ("Fade Power", Range(0.1, 8.0)) = 1.0
    }

    // Universal Render Pipeline.
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Brightness;
                float _ScrollSpeed;
                half _Reverse;
                half _EnableAnimation;
                float _AnimationY;
                half _FadeBottom;
                half _FadeTop;
                half _FadePower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 sampleUV : TEXCOORD0;
                float edgeY : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                float2 sampleUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float direction = lerp(1.0, -1.0, saturate(_Reverse));
                float scrollOffset = ((_Time.y * _ScrollSpeed) + _AnimationY) * saturate(_EnableAnimation);

                // Subtracting the offset makes a positive speed move visually toward +Y.
                sampleUV.y = frac(sampleUV.y - (scrollOffset * direction));

                output.sampleUV = sampleUV;
                output.edgeY = saturate(input.uv.y);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.sampleUV);

                half bottomFade = smoothstep(0.0h, max(_FadeBottom, 0.0001h), input.edgeY);
                half topFade = smoothstep(0.0h, max(_FadeTop, 0.0001h), 1.0h - input.edgeY);
                half edgeFade = pow(saturate(bottomFade * topFade), _FadePower);

                half4 tint = _Color * input.color;
                half alpha = textureSample.a * tint.a * edgeFade;
                half3 rgb = textureSample.rgb * tint.rgb * _Brightness;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    // Built-in Render Pipeline fallback.
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Brightness;
            float _ScrollSpeed;
            half _Reverse;
            half _EnableAnimation;
            float _AnimationY;
            half _FadeBottom;
            half _FadeTop;
            half _FadePower;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 sampleUV : TEXCOORD0;
                float edgeY : TEXCOORD1;
                fixed4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);

                float2 sampleUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                float direction = lerp(1.0, -1.0, saturate(_Reverse));
                float scrollOffset = ((_Time.y * _ScrollSpeed) + _AnimationY) * saturate(_EnableAnimation);
                sampleUV.y = frac(sampleUV.y - (scrollOffset * direction));

                output.sampleUV = sampleUV;
                output.edgeY = saturate(input.uv.y);
                output.color = input.color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 textureSample = tex2D(_MainTex, input.sampleUV);

                half bottomFade = smoothstep(0.0h, max(_FadeBottom, 0.0001h), input.edgeY);
                half topFade = smoothstep(0.0h, max(_FadeTop, 0.0001h), 1.0h - input.edgeY);
                half edgeFade = pow(saturate(bottomFade * topFade), _FadePower);

                fixed4 tint = _Color * input.color;
                half alpha = textureSample.a * tint.a * edgeFade;
                half3 rgb = textureSample.rgb * tint.rgb * _Brightness;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
