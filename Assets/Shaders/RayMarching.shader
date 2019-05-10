Shader "Hidden/RayMarching"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.6

            #include "UnityCG.cginc"
            #include "SDF.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 ray : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            struct ShapeData
            {
                uint primitiveType;
                float3 position;
                float4 color;
                float4 parameter;
                uint blendOperation;
                float smoothPower;
            };

            StructuredBuffer<ShapeData> _ShapeDatas;
            int _ShapeCount;

            float4x4 _CameraFrustum;
            int _Iteration;
            float _Accuracy;

            // Light
            float3 _LightDir;
            float3 _LightColor;
            float _LightIntensity;

            // Shadow
            float _ShadowIntensity;
            float _ShadowNearPlane;
            float _ShadowFarPlane;
            float _ShadowPenumbra;

            // Ambient Occlusion
            float _AOStep;
            float _AOIntensity;
            int _AOIteration;

            // Reflection
            float _EnvReflIntensity;
            float _ReflectionIntensity;
            int _ReflectionIteration;

            // Material
            float _ToonAmount;
            float _Glossiness;
            float _RimThreshold;
            float _RimAmount;

            #define EPSILON 0.001

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                int index = o.uv.x + o.uv.y * 2;
                o.ray = _CameraFrustum[index].xyz;

                return o;
            }

            float sdf(float3 p, in ShapeData shapeData)
            {
                if (shapeData.primitiveType == 0)
                {
                    return sdSphere(p - shapeData.position, shapeData.parameter.w);
                }
                else if (shapeData.primitiveType == 1)
                {
                    return sdBox(p - shapeData.position, shapeData.parameter.xyz);
                }

                return 0;
            }

            float4 combine(float4 d1, float4 d2, uint operation, float smoothPower)
            {
                if (operation == 0)
                {
                    return opSmoothUnion(d1, d2, smoothPower);
                }
                else if (operation == 1)
                {
                    return opSmoothSubtraction(d1, d2, smoothPower);
                }
                else if (operation == 2)
                {
                    return opSmoothIntersection(d1, d2, smoothPower);
                }

                return d2;
            }

            float4 map(float3 p)
            {
                float4 result = float4(0, 0, 0, 10000);

                if (_ShapeCount > 0)
                {
                    ShapeData shapeData = _ShapeDatas[0];
                    result = float4(shapeData.color.rgb, sdf(p, shapeData));
                    for (int i = 1; i < _ShapeCount; i++)
                    {
                        ShapeData shapeData = _ShapeDatas[i];
                        result = combine(result, float4(shapeData.color.rgb, sdf(p, shapeData)), shapeData.blendOperation, shapeData.smoothPower);
                    }
                }
                // result = opSmoothSubtraction(result, float4(1, 1, 1, sdSphere(p, 1)), 0.1);

                return result;
            }

            float3 calNorm(float3 p)
            {
                float3 norm = float3(
                    map(p + float3(EPSILON, 0, 0)).w - map(p - float3(EPSILON, 0, 0)).w,
                    map(p + float3(0, EPSILON, 0)).w - map(p - float3(0, EPSILON, 0)).w,
                    map(p + float3(0, 0, EPSILON)).w - map(p - float3(0, 0, EPSILON)).w
                    );
                return normalize(norm);
            }

            float hardShadow(float3 ro, float3 rd, float mint, float maxt)
            {
                for (float t = mint; t < maxt;)
                {
                    float h = map(ro + rd * t).w;
                    if (h < EPSILON)
                    {
                        return 0;
                    }

                    t += h;
                }

                return 1;
            }

            float softShadow(float3 ro, float3 rd, float mint, float maxt, float k)
            {
                float ret = 1.0;
                for (float t = mint; t < maxt;)
                {
                    float h = map(ro + rd * t).w;
                    if (h < EPSILON)
                    {
                        return 0;
                    }

                    ret = min(ret, k * h / t);
                    t += h;
                }

                return ret;
            }

            float ambientOcclusion(float3 p, float3 n)
            {
                float ao = 0;
                float d = 0;

                for (int i = 1; i <= _AOIteration; i++)
                {
                    d = _AOStep * i;
                    ao += max(0, (d - map(p + n * d).w) / d);
                }

                return (1 - ao * _AOIntensity);
            }

            float3 shading(float3 p, float3 n, float3 v, float3 col)
            {
                // diffuse
                float NdotL = dot(n, -_LightDir);
                float toonIntensity = smoothstep(_ToonAmount - 0.01, _ToonAmount + 0.01, NdotL) * 0.5 + 0.5;
                float3 diffuse = col * toonIntensity;

                // specular
                float3 h = normalize(-_LightDir + v);
                float NdotH = dot(n, h);
                float specularIntensity = pow(NdotH * toonIntensity, _Glossiness * _Glossiness);
                float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
                float3 specular = specularIntensitySmooth * col;

                // rim
                float rim = 1 - dot(n, v);
                float rimIntensity = rim * pow(NdotL, _RimThreshold);
                rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);
                float3 rimCol = rimIntensity * col;

                fixed3 result = (diffuse + specular + rimCol) * _LightColor * _LightIntensity;

                // shadow
                // float shadow = hardShadow(p, -_LightDir, _ShadowNearPlane, _ShadowFarPlane) * 0.5 + 0.5;
                float shadow = softShadow(p, -_LightDir, _ShadowNearPlane, _ShadowFarPlane, _ShadowPenumbra) * 0.5 + 0.5;
                shadow = max(0.0, pow(shadow, _ShadowIntensity));

                // ambient occlusion
                float ao = ambientOcclusion(p, n);

                return result * shadow * ao;
            }

            bool rayMarching(float3 ro, float3 rd, float depth, float maxDist, int maxIter, inout float3 p, inout float3 col)
            {
                bool hit;
                float t = 0;
                for (int i = 0; i < maxIter; i++)
                {
                    if (t > maxDist || t > depth)
                    {
                        hit = false;
                        break;
                    }

                    p = ro + rd * t;
                    float4 d = map(p);

                    if (d.w < _Accuracy)
                    {
                        hit = true;
                        col = d.rgb;
                        break;
                    }

                    t += d.w;
                }

                return hit;
            }

            fixed4 render(float3 ro, float3 rd, float sceneDepth)
            {
                fixed4 result;

                float3 p;
                float maxDistance = _ProjectionParams.z;
                uint iteration = _Iteration;
                fixed3 col;
                bool hit = rayMarching(ro, rd, sceneDepth, maxDistance, iteration, p, col);
                if (hit)
                {
                    float3 n = calNorm(p);
                    float3 v = normalize(_WorldSpaceCameraPos - p);
                    float3 s = shading(p, n, v, col);
                    result = fixed4(s, 1);

                    float4 envSample = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, n);
                    float3 envCol = DecodeHDR(envSample, unity_SpecCube0_HDR);

                    float reflectionIntensity = _ReflectionIntensity;
                    result += fixed4(envCol * _EnvReflIntensity * _ReflectionIntensity, 0);

                    for (int i = 0; i < _ReflectionIteration; i++)
                    {
                        rd = normalize(reflect(rd, n));
                        ro = p + ro * EPSILON;
                        maxDistance *= 0.5;
                        iteration /= 2;

                        hit = rayMarching(ro, rd, maxDistance, maxDistance, iteration, p, col);
                        if (hit)
                        {
                            float3 n = calNorm(p);
                            float3 v = normalize(_WorldSpaceCameraPos - p);
                            float3 s = shading(p, n, v, col);
                            result += fixed4(s * reflectionIntensity, 0);
                            reflectionIntensity *= 0.5;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    result = fixed4(0, 0, 0, 0);
                }

                return result;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 col = tex2D(_MainTex, i.uv);
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));

                float3 rayDir = normalize(i.ray.xyz);
                float3 rayOrigin = _WorldSpaceCameraPos;

                fixed4 result = render(rayOrigin, rayDir, depth);

                return fixed4(col * (1 - result.w) + result.xyz * result.w, 1);
            }
            ENDCG
        }
    }
}