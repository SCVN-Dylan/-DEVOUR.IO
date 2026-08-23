// VIEN SANG cho ITEM AN DUOC - khong phai vien cua nhan vat.
//
// Nhan vat co vien nho pass "Outline" nam san trong shader TCP2. Item thi moi loai mot material
// rieng den tu FBX (co cai con dung thang Lit mac dinh cua URP), khong cai nao co pass do - nen
// phai co duong rieng: shader nay duoc dung lam OVERRIDE MATERIAL cho mot Renderer Feature
// (RenderObjects) chi ve nhung object nam tren layer ItemHighlight.
//
// Nho vay bat/tat vien = DOI LAYER, khong dung toi material cua item. Quan trong: OccluderFade
// cung ghi sharedMaterials cua chinh may item nay de lam mo vat che - hai he ma cung ghi material
// thi de nhau, con doi layer thi chay song song vo tu.
//
// CACH VE: vo phinh (inverted hull). Ve lai chinh mesh do voi Cull Front (chi con mat trong),
// day dinh ra ngoai theo normal -> phan lo ra khoi than item chinh la duong vien.
//
// DAY VIEN TINH BANG PIXEL, khong phai world unit. Item trong game nay chenh nhau rat xa (hamburger
// 0.3u toi coffee shop 10u) va camera lai ZOOM RA theo level - lay do day co dinh trong the gioi
// thi vien cua mon nho se day bang ca mon do, con zoom ra la mat tich. Day theo pixel thi mon nao,
// zoom nao cung ra dung mot net.
Shader "Devours/Item Outline"
{
    Properties
    {
        _OutlineColor ("Mau vien", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Do day vien (pixel)", Range(0.5, 12)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ItemOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front      // chi ve mat trong -> than item that che het phan giua, chi con vanh
            ZWrite Off      // vien khong ghi do sau: khong chan vat khac, khong pha sap xep
            ZTest LEqual    // cho gi dang o TRUOC item van che duoc vien

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Tat ca tham so nam trong UnityPerMaterial -> tuong thich SRP Batcher, moi item sang
            // cung dung chung mot material nen gom het vao mot batch.
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float4 posCS = TransformObjectToHClip(IN.positionOS.xyz);

                // Day theo normal DA CHIEU LEN MAN HINH, khong phai normal trong the gioi: co vay
                // do day moi dung bang nhau o moi huong, khong bi mat nghieng thi vien mong di.
                float3 nVS = TransformWorldToViewDir(TransformObjectToWorldNormal(IN.normalOS), true);
                float2 dir = nVS.xy;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0.0, 0.0);

                // x posCS.w de phep day song sot qua buoc chia phoi canh -> ra dung so pixel da dat.
                // Camera game la orthographic (w = 1) nhung van nhan cho dung ca khi doi sang perspective.
                posCS.xy += dir * (_OutlineWidth * 2.0 / _ScreenParams.xy) * posCS.w;

                OUT.positionCS = posCS;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
