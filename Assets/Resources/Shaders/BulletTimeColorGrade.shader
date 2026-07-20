Shader "Hidden/BulletTimeColorGrade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Intensity ("Intensity", Range(0, 1)) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Tint;
            float _Saturation;
            float _Intensity;

            fixed4 frag(v2f_img input) : SV_Target
            {
                const half3 luminanceWeights = half3(0.2126h, 0.7152h, 0.0722h);
                half4 source = tex2D(_MainTex, input.uv);
                half luminance = dot(source.rgb, luminanceWeights);
                half3 saturated = lerp(luminance.xxx, source.rgb, _Saturation);

                half tintLuminance = max(dot(_Tint.rgb, luminanceWeights), 0.001h);
                half3 luminancePreservingTint = _Tint.rgb / tintLuminance;
                half3 graded = saturated * luminancePreservingTint;

                return half4(lerp(source.rgb, graded, saturate(_Intensity)), source.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
