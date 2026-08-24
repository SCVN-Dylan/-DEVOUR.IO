// CHOP DO cho item BI HUT MA KHONG NUOT DUOC.
//
// Item hon nguoi choi dung mot hang thi bi hut se chi RUNG TAI CHO chu khong bay vao mom
// (PhysicsDevourable.Struggle). Nhin cai rung do rat de tuong la game lag hoac item bi ket -
// nguoi choi khong co cach nao biet "chua du cap". Mot lop do nhap nhay de len tra loi ngay.
//
// Dung chung duong voi vien item: mot Renderer Feature (RenderObjects) chi ve nhung gi nam tren
// layer ItemBlocked, bang shader nay lam override material. Bat/tat = doi layer, khong dung toi
// material cua item (xem ghi chu trong S_ItemOutline).
//
// NHAY HAN TOAN TRONG SHADER bang _Time: CPU khong phai lam gi ca, khong Update, khong tween,
// khong mot dong code nao chay moi frame. Bat chop = doi layer mot lan, het.
Shader "Devours/Item Blink"
{
    Properties
    {
        _BlinkColor ("Mau chop", Color) = (1, 0.15, 0.1, 1)
        _BlinkSpeed ("So lan chop moi giay", Range(0.5, 12)) = 4
        _AlphaMin ("Do dam luc nhat nhat", Range(0, 1)) = 0.05
        _AlphaMax ("Do dam luc dam nhat", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ItemBlink"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off        // lop phu, khong duoc ghi do sau
            ZTest LEqual      // "bang" cung qua -> ve dung tren be mat item, khong tranh chap z
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BlinkColor;
                float _BlinkSpeed;
                float _AlphaMin;
                float _AlphaMax;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // sin cho ra nhip len xuong muot; 0.5+0.5*sin dua ve [0..1] roi noi suy do dam.
                float t = 0.5 + 0.5 * sin(_Time.y * _BlinkSpeed * 6.28318);
                half4 c = _BlinkColor;
                c.a *= lerp(_AlphaMin, _AlphaMax, t);
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
