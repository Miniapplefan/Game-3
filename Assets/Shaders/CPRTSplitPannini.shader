Shader "Hidden/CPRT/SplitPannini"
{
    Properties
    {
        _PeripheralTex("Peripheral", 2D) = "black" {}
        _CenterTex("Center", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    struct v2f
    {
        float4 pos : SV_POSITION;
        float4 peripheralPos : TEXCOORD0;
        float4 centerPos : TEXCOORD1;
    };

    sampler2D _PeripheralTex;
    sampler2D _CenterTex;
    float4x4 ObserverViewProj;
    float4x4 PeripheralPainterViewProj;
    float4x4 CenterPainterViewProj;
    float4 _PeripheralTexelSize;
    float4 _CenterTexelSize;
    float CenterBlendMargin;
    float PeripheralBlur;
    float CenterSharpen;
    float PeripheralDesaturation;
    float PeripheralContrast;
    float4 PeripheralTint;

    float2 FinalizeScreenCoords(float4 coords)
    {
        return (coords.xy / coords.w) * 0.5f + 0.5f;
    }

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = mul(ObserverViewProj, v.vertex);
        o.peripheralPos = mul(PeripheralPainterViewProj, v.vertex);
        o.centerPos = mul(CenterPainterViewProj, v.vertex);
        return o;
    }

    half3 SamplePeripheral(float2 uv, float centerWeight)
    {
        float blurStrength = PeripheralBlur * saturate(1.0f - centerWeight);
        float2 offset = _PeripheralTexelSize.xy * blurStrength * 2.0f;

        half3 color = tex2D(_PeripheralTex, uv).rgb * 4.0h;
        color += tex2D(_PeripheralTex, uv + float2(offset.x, 0.0f)).rgb;
        color += tex2D(_PeripheralTex, uv - float2(offset.x, 0.0f)).rgb;
        color += tex2D(_PeripheralTex, uv + float2(0.0f, offset.y)).rgb;
        color += tex2D(_PeripheralTex, uv - float2(0.0f, offset.y)).rgb;
        color *= 0.125h;

        half luminance = dot(color, half3(0.299h, 0.587h, 0.114h));
        color = lerp(color, luminance.xxx, PeripheralDesaturation);
        color = (color - 0.5h) * (1.0h + PeripheralContrast) + 0.5h;
        color *= PeripheralTint.rgb;
        return color;
    }

    half3 SampleCenter(float2 uv)
    {
        half3 center = tex2D(_CenterTex, uv).rgb;
        if (CenterSharpen <= 0.0f)
        {
            return center;
        }

        float2 offset = _CenterTexelSize.xy;
        half3 neighbors =
            tex2D(_CenterTex, uv + float2(offset.x, 0.0f)).rgb +
            tex2D(_CenterTex, uv - float2(offset.x, 0.0f)).rgb +
            tex2D(_CenterTex, uv + float2(0.0f, offset.y)).rgb +
            tex2D(_CenterTex, uv - float2(0.0f, offset.y)).rgb;

        half sharpenStrength = CenterSharpen * 0.25h;
        return max(center * (1.0h + 4.0h * sharpenStrength) - neighbors * sharpenStrength, 0.0h);
    }

    half ComputeCenterWeight(float2 uv)
    {
        float2 edgeDistance = min(uv, 1.0f - uv);
        float nearestEdge = min(edgeDistance.x, edgeDistance.y);
        return smoothstep(0.0f, CenterBlendMargin, nearestEdge);
    }

    half4 frag(v2f i) : SV_Target
    {
        float2 peripheralUv = FinalizeScreenCoords(i.peripheralPos);
        float2 centerUv = FinalizeScreenCoords(i.centerPos);
        half centerWeight = ComputeCenterWeight(centerUv);

        half3 peripheral = SamplePeripheral(peripheralUv, centerWeight);
        half3 center = SampleCenter(saturate(centerUv));
        half3 blended = lerp(peripheral, center, centerWeight);
        return half4(blended, 1.0h);
    }
    ENDCG

    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }

    Fallback Off
}
