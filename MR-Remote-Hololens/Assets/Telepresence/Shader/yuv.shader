Shader "Tango/YUV2RGB"
{
Properties 
{
    _ColorTexY ("Y channel texture", 2D) = "white" {}
    _ColorTexU ("U channel texture", 2D) = "white" {}
    _ColorTexV ("V channel texture", 2D) = "white" {}
    _TexWidth ("texture width", Float) = 1280.0
    _TexHeight ("texture height", Float) = 720.0
    _Fx      ("Fx", Float) = 1043.130005
    _Fy      ("Fy", Float) = 1038.619995
    _Cx      ("Cx", Float) = 641.926025
    _Cy      ("Cy", Float) = 359.115997
    _K0      ("K0", Float) = 0.246231
    _K1      ("K1", Float) = -0.727204
    _K2      ("K2", Float) = 0.726065
}
SubShader 
{
    // Setting the z write off to make sure our video overlay is always rendered at back.
    ZWrite Off
    ZTest Off
    Tags { //"Queue" = "Background" 
        			"RenderType" = "Opaque"
			//"Queue" = "Transparent-1"
			"Queue" = "Geometry-1"
        }
    Pass 
    {
        CGPROGRAM
        #pragma multi_compile _ DISTORTION_ON
        
        #pragma vertex vert
        #pragma fragment frag
        
		#include "UnityCG.cginc"
		#include "FragmentLighting.cginc"

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
        };
        
        v2f vert (appdata v)
        {
            v2f o;
            // We don't apply any projection or view matrix here to make sure that
            // the geometry is rendered in the screen space.
            v.vertex = float4(v.vertex.x, v.vertex.y, v.vertex.z+1.0, v.vertex.w);
            o.vertex = v.vertex;
            o.uv = v.uv;
            return o;
        }

        // The Y, U, V texture.
        // However, at present U and V textures are interleaved into the same texture,
        // so we'll only sample from _YTex and _UTex.
			sampler2D _ColorTexY;
			sampler2D _ColorTexU;
			sampler2D _ColorTexV;
        
        // Width of the RGBA texture, this is for indexing the channel of color, not
        // for scaling.
        float _TexWidth;
        float _TexHeight;
        float _Fx;
        float _Fy;
        float _Cx;
        float _Cy;
        float _K0;
        float _K1;
        float _K2;
        
        // Compute a modulo b.
        float custom_mod(float x, float y)
        {
            return x - (y * floor(x / y));
        }
        
        half3 YUVtoRGB(half3 yuv) // added by MR
        {
            // The YUV to RBA conversion, please refer to: http://en.wikipedia.org/wiki/YUV
            // Y'UV420p (I420) to RGB888 conversion section.
            half y_value = yuv.x;
            half u_value = yuv.y;
            half v_value = yuv.z;
            half r = y_value + 1.370705 * (v_value - 0.5);
            half g = y_value - 0.698001 * (v_value - 0.5) - (0.337633 * (u_value - 0.5));
            half b = y_value + 1.732446 * (u_value - 0.5);
            return half3(r, g, b);
        }
        
        fixed4 frag (v2f i) : SV_Target
        {
            fixed3 yuv;
            yuv.r = tex2D(_ColorTexY, i.uv).r;
            yuv.g = tex2D(_ColorTexU, i.uv).r;
            yuv.b = tex2D(_ColorTexV, i.uv).r;
            fixed4 rgb = fixed4(0,0,0,0);
		    rgb = fixed4(YUVtoRGB(yuv.rgb), 1);
            rgb = fixed4(1,0,0,1);
		    rgb.a = 1;
		    return rgb;
        }
        ENDCG
    }
}
}