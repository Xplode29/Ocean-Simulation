Shader "Unlit/Water"
{
    Properties
    {
			[Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
	}
    SubShader
    {
        Tags { "LightMode"="ForwardBase" }

        Pass
        {
            ZWrite On

            CGPROGRAM

            #pragma vertex vp
            #pragma fragment fp

            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"
            
            struct VertexData {
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float3 normal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
			};

			struct Wave {
				float2 direction;
				float2 origin;
				float frequency;
				float amplitude;
				float phase;
			};
			
			StructuredBuffer<Wave> _Waves;

            int _WaveCount;
            float3 _COLOR;
            float3 _SunPosition;

            float CalculateWave(float3 pos, Wave wave) {
                float2 direction = wave.direction;
				float xz = pos.x * direction.x + pos.z * direction.y;
				float t = _Time.y * wave.phase;

                return wave.amplitude * sin(xz * wave.frequency + t);
            }

            float2 CalculateDerivee(float3 pos, Wave wave) {
                float2 direction = wave.direction;
				float xz = pos.x * direction.x + pos.z * direction.y;
				float t = _Time.y * wave.phase;

                float expression = wave.amplitude * wave.frequency * cos(xz * wave.frequency + t);

                return float2(direction.x * expression, direction.y * expression);
            }

            v2f vp(VertexData v) {
                v2f tex;
                tex.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                float height = 0.0f;
				
                for (int wi = 0; wi < _WaveCount; ++wi) {
					height += CalculateWave(tex.worldPos, _Waves[wi]);
				}

                float4 newPos = v.vertex + float4(0.0f, height, 0.0f, 0.0f);

				tex.worldPos = mul(unity_ObjectToWorld, newPos);
				tex.pos = UnityObjectToClipPos(newPos);

                return tex;
            }

            float4 fp(v2f tex) : SV_Target {
                float3 lightDir = normalize(tex.worldPos - _SunPosition);
                float3 normal = float3(0, -1, 0);
                
                for (int wi = 0; wi < _WaveCount; ++wi) {
                    float2 derivees = CalculateDerivee(tex.worldPos, _Waves[wi]);
					normal.x += derivees.x;
					normal.z += derivees.y;
				}

                float ndotl = DotClamped(lightDir, normal);
	            float3 output = _COLOR * ndotl;

                return float4(output, 1.0f);
            }
            ENDCG
        }
    }
}
