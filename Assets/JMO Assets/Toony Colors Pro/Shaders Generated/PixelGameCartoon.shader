// Toony Colors Pro+Mobile 2
// (c) 2014-2025 Jean Moreno

Shader "Toony Colors Pro 2/PixelGame/Cartoon"
{
	Properties
	{
		[TCP2HeaderHelp(Base)]
		_BaseColor ("Color", Color) = (1,1,1,1)
		[TCP2ColorNoAlpha] _HColor ("Highlight Color", Color) = (0.75,0.75,0.75,1)
		[TCP2ColorNoAlpha] _SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
		[MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
		[TCP2Separator]

		[TCP2Header(Ramp Shading)]
		
		_RampThreshold ("Threshold", Range(0.01,1)) = 0.5
		_RampSmoothing ("Smoothing", Range(0.001,1)) = 0.5
		[TCP2Separator]
		
		[TCP2HeaderHelp(Specular)]
		[TCP2ColorNoAlpha] _SpecularColor ("Specular Color", Color) = (0,0,0,1)
		_SpecularRoughnessPBR ("Roughness", Range(0,1)) = 0.5
		[TCP2Separator]
		
		[TCP2HeaderHelp(Rim Lighting)]
		[TCP2ColorNoAlpha] _RimColor ("Rim Color", Color) = (0,0,0,0.5)
		_RimMin ("Rim Min", Range(0,2)) = 0.5
		_RimMax ("Rim Max", Range(0,2)) = 1
		[TCP2Separator]
		
		[TCP2HeaderHelp(MatCap)]
		[NoScaleOffset] [NoScaleOffset] _MatCapTex ("MatCap (RGB)", 2D) = "gray" {}
		[TCP2ColorNoAlpha] _MatCapColor ("MatCap Color", Color) = (0,0,0,1)
		[TCP2Separator]
		
		[TCP2Header(Stylized Plastic Bevel and Specular)]
		[Toggle] _StylizedPlasticOn ("Enable Stylized Plastic", Float) = 0
		_PillowRoundness ("Pillow Roundness", Range(0, 1)) = 0
		_PlasticAngleX ("Highlight Angle X", Range(-1, 1)) = 0.42
		[TCP2ColorNoAlpha] _PlasticHighlightColor ("Plastic Highlight Color", Color) = (1,1,1,1)
		_PlasticHighlightIntensity ("Highlight Intensity", Range(0, 5)) = 1.0
		_PlasticHighlightSize ("Highlight Size / Softness", Range(0.01, 1)) = 0.35
		_PlasticTopLight ("Top Light Boost", Range(0, 1)) = 0.25
		_PlasticBevelAO ("Bevel Edge Darkening (AO)", Range(0, 1)) = 0.4
		[TCP2Separator]
		
		[ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("Receive Shadows", Float) = 1

		// Avoid compile error if the properties are ending with a drawer
		[HideInInspector] __dummy__ ("unused", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType"="Opaque"
		}

		HLSLINCLUDE
		#define fixed half
		#define fixed2 half2
		#define fixed3 half3
		#define fixed4 half4

		#if UNITY_VERSION >= 202020
			#define URP_10_OR_NEWER
		#endif
		#if UNITY_VERSION >= 202120
			#define URP_12_OR_NEWER
		#endif
		#if UNITY_VERSION >= 202220
			#define URP_14_OR_NEWER
		#endif

		// Texture/Sampler abstraction
		#define TCP2_TEX2D_WITH_SAMPLER(tex)						TEXTURE2D(tex); SAMPLER(sampler##tex)
		#define TCP2_TEX2D_NO_SAMPLER(tex)							TEXTURE2D(tex)
		#define TCP2_TEX2D_SAMPLE(tex, samplertex, coord)			SAMPLE_TEXTURE2D(tex, sampler##samplertex, coord)
		#define TCP2_TEX2D_SAMPLE_LOD(tex, samplertex, coord, lod)	SAMPLE_TEXTURE2D_LOD(tex, sampler##samplertex, coord, lod)

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

		// Uniforms

		// Shader Properties
		TCP2_TEX2D_WITH_SAMPLER(_BaseMap);
		TCP2_TEX2D_WITH_SAMPLER(_MatCapTex);

		CBUFFER_START(UnityPerMaterial)
			
			// Shader Properties
			float4 _BaseMap_ST;
			fixed4 _BaseColor;
			fixed4 _MatCapColor;
			float _RampThreshold;
			float _RampSmoothing;
			float _RimMin;
			float _RimMax;
			fixed4 _RimColor;
			float _SpecularRoughnessPBR;
			fixed4 _SpecularColor;
			fixed4 _SColor;
			fixed4 _HColor;
			float _StylizedPlasticOn;
			float _PillowRoundness;
			float _PlasticAngleX;
			fixed4 _PlasticHighlightColor;
			float _PlasticHighlightIntensity;
			float _PlasticHighlightSize;
			float _PlasticTopLight;
			float _PlasticBevelAO;
		CBUFFER_END

		//Specular help functions (from UnityStandardBRDF.cginc)
		inline float3 SpecSafeNormalize(float3 inVec)
		{
			half dp3 = max(0.001f, dot(inVec, inVec));
			return inVec * rsqrt(dp3);
		}
		
			//GGX
			#define TCP2_PI			3.14159265359
			#define TCP2_INV_PI		0.31830988618f
			#if defined(SHADER_API_MOBILE)
				#define TCP2_EPSILON 1e-4f
			#else
				#define TCP2_EPSILON 1e-7f
			#endif
			inline half GGX(half NdotH, half roughness)
			{
				half a2 = roughness * roughness;
				half d = (NdotH * a2 - NdotH) * NdotH + 1.0f;
				return TCP2_INV_PI * a2 / (d * d + TCP2_EPSILON);
			}
		
		// Built-in renderer (CG) to SRP (HLSL) bindings
		#define UnityObjectToClipPos TransformObjectToHClip
		#define _WorldSpaceLightPos0 _MainLightPosition
		
		ENDHLSL

		Pass
		{
			Name "Main"
			Tags
			{
				"LightMode"="UniversalForward"
			}

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard SRP library
			// All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 3.0

			// -------------------------------------
			// Material keywords
			#pragma shader_feature_local _ _RECEIVE_SHADOWS_OFF

			// -------------------------------------
			// Universal Render Pipeline keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"

			// -------------------------------------

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			#pragma vertex Vertex
			#pragma fragment Fragment

			// vertex input
			struct Attributes
			{
				float4 vertex       : POSITION;
				float3 normal       : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			// vertex output / fragment input
			struct Varyings
			{
				float4 positionCS     : SV_POSITION;
				float3 normal         : NORMAL;
				float4 worldPosAndFog : TEXCOORD0;
			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord    : TEXCOORD1; // compute shadow coord per-vertex for the main light
			#endif
			#ifdef _ADDITIONAL_LIGHTS_VERTEX
				half3 vertexLights : TEXCOORD2;
			#endif
				float4 pack0 : TEXCOORD3; /* pack0.xy = texcoord0  pack0.zw = matcap */
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			#if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
				// Fake InputData struct needed for Forward+ macro
				struct InputDataForwardPlusDummy
				{
					float3  positionWS;
					float2  normalizedScreenSpaceUV;
				};
			#endif

			Varyings Vertex(Attributes input)
			{
				Varyings output = (Varyings)0;

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				// Texture Coordinates
				output.pack0.xy.xy = input.texcoord0.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;

				float3 worldPos = mul(UNITY_MATRIX_M, input.vertex).xyz;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				output.shadowCoord = GetShadowCoord(vertexInput);
			#endif

				float3 meshNormal = input.normal;
				if (_PillowRoundness > 0.001)
				{
					float3 sphereNorm = normalize(input.vertex.xyz);
					meshNormal = normalize(lerp(input.normal, sphereNorm, _PillowRoundness));
				}
				VertexNormalInputs vertexNormalInput = GetVertexNormalInputs(meshNormal);
			#ifdef _ADDITIONAL_LIGHTS_VERTEX
				// Vertex lighting
				output.vertexLights = VertexLighting(vertexInput.positionWS, vertexNormalInput.normalWS);
			#endif

				// world position
				output.worldPosAndFog = float4(vertexInput.positionWS.xyz, 0);

				// normal
				output.normal = normalize(vertexNormalInput.normalWS);

				// clip position
				output.positionCS = vertexInput.positionCS;

				//MatCap
				float3 worldNorm = normalize(UNITY_MATRIX_I_M[0].xyz * input.normal.x + UNITY_MATRIX_I_M[1].xyz * input.normal.y + UNITY_MATRIX_I_M[2].xyz * input.normal.z);
				worldNorm = mul((float3x3)UNITY_MATRIX_V, worldNorm);
				output.pack0.zw = worldNorm.xy * 0.5 + 0.5;

				return output;
			}

			half4 Fragment(Varyings input
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				float3 positionWS = input.worldPosAndFog.xyz;
				float3 normalWS = normalize(input.normal);
				half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

				// Shader Properties Sampling
				float4 __albedo = ( TCP2_TEX2D_SAMPLE(_BaseMap, _BaseMap, input.pack0.xy).rgba );
				float4 __mainColor = ( _BaseColor.rgba );
				float __alpha = ( __albedo.a * __mainColor.a );
				float __ambientIntensity = ( 1.0 );
				float3 __matcapColor = ( _MatCapColor.rgb );
				float __rampThreshold = ( _RampThreshold );
				float __rampSmoothing = ( _RampSmoothing );
				float __rimMin = ( _RimMin );
				float __rimMax = ( _RimMax );
				float3 __rimColor = ( _RimColor.rgb );
				float __rimStrength = ( 1.0 );
				float __specularRoughnessPbr = ( _SpecularRoughnessPBR );
				float3 __specularColor = ( _SpecularColor.rgb );
				float3 __shadowColor = ( _SColor.rgb );
				float3 __highlightColor = ( _HColor.rgb );

				half ndv = abs(dot(viewDirWS, normalWS));
				half ndvRaw = ndv;

				// main texture
				half3 albedo = __albedo.rgb;
				half alpha = __alpha;

				half3 emission = half3(0,0,0);
				
				albedo *= __mainColor.rgb;

				// main light: direction, color, distanceAttenuation, shadowAttenuation
			#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord = input.shadowCoord;
			#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
				float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
			#else
				float4 shadowCoord = float4(0, 0, 0, 0);
			#endif

			#if defined(URP_10_OR_NEWER)
				#if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
					half4 shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
				#elif !defined (LIGHTMAP_ON)
					half4 shadowMask = unity_ProbesOcclusion;
				#else
					half4 shadowMask = half4(1, 1, 1, 1);
				#endif

				Light mainLight = GetMainLight(shadowCoord, positionWS, shadowMask);
			#else
				Light mainLight = GetMainLight(shadowCoord);
			#endif

			#if defined(_SCREEN_SPACE_OCCLUSION) || defined(USE_FORWARD_PLUS) || defined(USE_CLUSTER_LIGHT_LOOP)
				float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
			#endif

				// ambient or lightmap
				// Samples SH fully per-pixel. SampleSHVertex and SampleSHPixel functions
				// are also defined in case you want to sample some terms per-vertex.
				half3 bakedGI = SampleSH(normalWS);
				half occlusion = 1;

				half3 indirectDiffuse = bakedGI;
				indirectDiffuse *= occlusion * albedo * __ambientIntensity;

				//MatCap
				half2 capCoord = input.pack0.zw;
				half3 matcap = ( TCP2_TEX2D_SAMPLE(_MatCapTex, _MatCapTex, capCoord).rgb ) * __matcapColor;
				emission += matcap;

				half3 lightDir = mainLight.direction;
				half3 lightColor = mainLight.color.rgb;

				half atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

				half ndl = dot(normalWS, lightDir);
				half3 ramp;
				
				half rampThreshold = __rampThreshold;
				half rampSmooth = __rampSmoothing * 0.5;
				ndl = saturate(ndl);
				ramp = smoothstep(rampThreshold - rampSmooth, rampThreshold + rampSmooth, ndl);

				// apply attenuation
				ramp *= atten;

				half3 color = half3(0,0,0);
				// Rim Lighting
				half rim = 1 - ndvRaw;
				rim = ( rim );
				half rimMin = __rimMin;
				half rimMax = __rimMax;
				rim = smoothstep(rimMin, rimMax, rim);
				half3 rimColor = __rimColor;
				half rimStrength = __rimStrength;
				emission.rgb += rim * rimColor * rimStrength;
				half3 accumulatedRamp = ramp * max(lightColor.r, max(lightColor.g, lightColor.b));
				half3 accumulatedColors = ramp * lightColor.rgb;

				half3 specView = (_StylizedPlasticOn > 0.5) ? half3(0.0, 0.0, -1.0) : viewDirWS;
				half3 specLight = (_StylizedPlasticOn > 0.5) ? normalize(half3(_PlasticAngleX, 0.68, -0.58)) : lightDir;
				half3 halfDir = SpecSafeNormalize(float3(specLight) + float3(specView));
				
				//Specular: GGX
				half roughness = __specularRoughnessPbr*__specularRoughnessPbr;
				half nh = saturate(dot(normalWS, halfDir));
				half spec = GGX(nh, saturate(roughness));
				spec *= TCP2_PI * 0.05;
				#ifdef UNITY_COLORSPACE_GAMMA
					spec = max(0, sqrt(max(1e-4h, spec)));
					half surfaceReduction = 1.0 - 0.28 * roughness * __specularRoughnessPbr;
				#else
					half surfaceReduction = 1.0 / (roughness*roughness + 1.0);
				#endif
				half specNdl = (_StylizedPlasticOn > 0.5) ? saturate(dot(normalWS, specLight)) : ndl;
				spec = max(0, spec * specNdl);
				spec *= surfaceReduction;
				if (_StylizedPlasticOn > 0.5)
				{
					spec *= _PlasticHighlightIntensity;
				}
				//Apply specular
				emission.rgb += spec * lightColor.rgb * __specularColor;

				// Additional lights loop
			#ifdef _ADDITIONAL_LIGHTS
				uint pixelLightCount = GetAdditionalLightsCount();

				#if USE_FORWARD_PLUS || USE_CLUSTER_LIGHT_LOOP
					// Additional directional lights in Forward+
					for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
					{
						CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

						Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

						#if defined(_LIGHT_LAYERS)
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
						#endif
						{
							half atten = light.shadowAttenuation * light.distanceAttenuation;

							#if defined(_LIGHT_LAYERS)
								half3 lightDir = half3(0, 1, 0);
								half3 lightColor = half3(0, 0, 0);
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								{
									lightColor = light.color.rgb;
									lightDir = light.direction;
								}
							#else
								half3 lightColor = light.color.rgb;
								half3 lightDir = light.direction;
							#endif

							half ndl = dot(normalWS, lightDir);
							half3 ramp;
							
							ndl = saturate(ndl);
							ramp = smoothstep(rampThreshold - rampSmooth, rampThreshold + rampSmooth, ndl);

							// apply attenuation (shadowmaps & point/spot lights attenuation)
							ramp *= atten;

							accumulatedRamp += ramp * max(lightColor.r, max(lightColor.g, lightColor.b));
							accumulatedColors += ramp * lightColor.rgb;

							half3 halfDir = SpecSafeNormalize(float3(lightDir) + float3(viewDirWS));
							
							//Specular: GGX
							half roughness = __specularRoughnessPbr*__specularRoughnessPbr;
							half nh = saturate(dot(normalWS, halfDir));
							half spec = GGX(nh, saturate(roughness));
							spec *= TCP2_PI * 0.05;
							#ifdef UNITY_COLORSPACE_GAMMA
								spec = max(0, sqrt(max(1e-4h, spec)));
								half surfaceReduction = 1.0 - 0.28 * roughness * __specularRoughnessPbr;
							#else
								half surfaceReduction = 1.0 / (roughness*roughness + 1.0);
							#endif
							spec = max(0, spec * ndl);
							spec *= surfaceReduction;
							spec *= atten;
							
							//Apply specular
							emission.rgb += spec * lightColor.rgb * __specularColor;
						}
					}

					// Data with dummy struct used in Forward+ macro (LIGHT_LOOP_BEGIN)
					InputDataForwardPlusDummy inputData;
					inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
					inputData.positionWS = positionWS;
				#endif

				LIGHT_LOOP_BEGIN(pixelLightCount)
				{
					#if defined(URP_10_OR_NEWER)
						Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
					#else
						Light light = GetAdditionalLight(lightIndex, positionWS);
					#endif
					half atten = light.shadowAttenuation * light.distanceAttenuation;

					#if defined(_LIGHT_LAYERS)
						half3 lightDir = half3(0, 1, 0);
						half3 lightColor = half3(0, 0, 0);
						if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
						{
							lightColor = light.color.rgb;
							lightDir = light.direction;
						}
					#else
						half3 lightColor = light.color.rgb;
						half3 lightDir = light.direction;
					#endif

					half ndl = dot(normalWS, lightDir);
					half3 ramp;
					
					ndl = saturate(ndl);
					ramp = smoothstep(rampThreshold - rampSmooth, rampThreshold + rampSmooth, ndl);

					// apply attenuation (shadowmaps & point/spot lights attenuation)
					ramp *= atten;

					accumulatedRamp += ramp * max(lightColor.r, max(lightColor.g, lightColor.b));
					accumulatedColors += ramp * lightColor.rgb;

					half3 halfDir = SpecSafeNormalize(float3(lightDir) + float3(viewDirWS));
					
					//Specular: GGX
					half roughness = __specularRoughnessPbr*__specularRoughnessPbr;
					half nh = saturate(dot(normalWS, halfDir));
					half spec = GGX(nh, saturate(roughness));
					spec *= TCP2_PI * 0.05;
					#ifdef UNITY_COLORSPACE_GAMMA
						spec = max(0, sqrt(max(1e-4h, spec)));
						half surfaceReduction = 1.0 - 0.28 * roughness * __specularRoughnessPbr;
					#else
						half surfaceReduction = 1.0 / (roughness*roughness + 1.0);
					#endif
					spec = max(0, spec * ndl);
					spec *= surfaceReduction;
					spec *= atten;
					
					//Apply specular
					emission.rgb += spec * lightColor.rgb * __specularColor;
				}
				LIGHT_LOOP_END
			#endif
			#ifdef _ADDITIONAL_LIGHTS_VERTEX
				color += input.vertexLights * albedo;
			#endif

				accumulatedRamp = saturate(accumulatedRamp);
				half3 shadowColor = (1 - accumulatedRamp.rgb) * __shadowColor;
				accumulatedRamp = accumulatedColors.rgb * __highlightColor + shadowColor;
				color += albedo * accumulatedRamp;

				// apply ambient
				color += indirectDiffuse;
				color += emission;

				if (_StylizedPlasticOn > 0.5)
				{
					float3 nWS = normalize(normalWS);
					// 1. Üst tavan aydınlık desteği
					float topBoost = smoothstep(0.4, 0.9, nWS.y) * _PlasticTopLight;
					color += albedo * topBoost;

					// 2. Alt kavis gölgesi
					float bottomDark = saturate(-nWS.y * 1.5) * 0.45;
					color *= saturate(1.0 - bottomDark * _PlasticBevelAO);
				}

				return half4(color, alpha);
			}
			ENDHLSL
		}

		// Depth & Shadow Caster Passes
		HLSLINCLUDE

		#if defined(SHADOW_CASTER_PASS) || defined(DEPTH_ONLY_PASS)

			#define fixed half
			#define fixed2 half2
			#define fixed3 half3
			#define fixed4 half4

			float3 _LightDirection;
			float3 _LightPosition;

			struct Attributes
			{
				float4 vertex   : POSITION;
				float3 normal   : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS     : SV_POSITION;
				float3 normal         : NORMAL;
			#if defined(DEPTH_NORMALS_PASS)
				float3 normalWS : TEXCOORD0;
			#endif
				float3 pack0 : TEXCOORD1; /* pack0.xyz = positionWS */
				float4 pack1 : TEXCOORD2; /* pack1.xy = texcoord0  pack1.zw = matcap */
			#if defined(DEPTH_ONLY_PASS)
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			#endif
			};

			float4 GetShadowPositionHClip(Attributes input)
			{
				float3 positionWS = TransformObjectToWorld(input.vertex.xyz);
				float3 normalWS = TransformObjectToWorldNormal(input.normal);

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif
				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#endif

				return positionCS;
			}

			Varyings ShadowDepthPassVertex(Attributes input)
			{
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				#if defined(DEPTH_ONLY_PASS)
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
				#endif

				float3 worldNormalUv = mul(UNITY_MATRIX_M, float4(input.normal, 1.0)).xyz;

				// Texture Coordinates
				output.pack1.xy.xy = input.texcoord0.xy * _BaseMap_ST.xy + _BaseMap_ST.zw;

				float3 worldPos = mul(UNITY_MATRIX_M, input.vertex).xyz;
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.vertex.xyz);
				output.normal = normalize(worldNormalUv);
				output.pack0.xyz = vertexInput.positionWS;

				#if defined(DEPTH_ONLY_PASS)
					output.positionCS = TransformObjectToHClip(input.vertex.xyz);
					#if defined(DEPTH_NORMALS_PASS)
						float3 normalWS = TransformObjectToWorldNormal(input.normal);
						output.normalWS = normalWS; // already normalized in TransformObjectToWorldNormal
					#endif
				#elif defined(SHADOW_CASTER_PASS)
					output.positionCS = GetShadowPositionHClip(input);
				#else
					output.positionCS = float4(0,0,0,0);
				#endif

				return output;
			}

			half4 ShadowDepthPassFragment(
				Varyings input
	#if defined(DEPTH_NORMALS_PASS) && defined(_WRITE_RENDERING_LAYERS)
		#if UNITY_VERSION >= 60020000
				, out uint outRenderingLayers : SV_Target1
		#else
				, out float4 outRenderingLayers : SV_Target1
		#endif
	#endif
			) : SV_TARGET
			{
				#if defined(DEPTH_ONLY_PASS)
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				#endif

				float3 positionWS = input.pack0.xyz;
				float3 normalWS = normalize(input.normal);

				// Shader Properties Sampling
				float4 __albedo = ( TCP2_TEX2D_SAMPLE(_BaseMap, _BaseMap, input.pack1.xy).rgba );
				float4 __mainColor = ( _BaseColor.rgba );
				float __alpha = ( __albedo.a * __mainColor.a );

				half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
				half ndv = abs(dot(viewDirWS, normalWS));
				half ndvRaw = ndv;

				half3 albedo = half3(1,1,1);
				half alpha = __alpha;
				half3 emission = half3(0,0,0);

				#if defined(DEPTH_NORMALS_PASS)
					#if defined(_WRITE_RENDERING_LAYERS)
						#if UNITY_VERSION >= 60020000
							outRenderingLayers = EncodeMeshRenderingLayer();
						#else
							outRenderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
						#endif
					#endif

					#if defined(URP_12_OR_NEWER)
						return float4(input.normalWS.xyz, 0.0);
					#else
						return float4(PackNormalOctRectEncode(TransformWorldToViewDir(input.normalWS, true)), 0.0, 0.0);
					#endif
				#endif

				return 0;
			}

		#endif
		ENDHLSL

		Pass
		{
			Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			// using simple #define doesn't work, we have to use this instead
			#pragma multi_compile SHADOW_CASTER_PASS

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing
			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#pragma vertex ShadowDepthPassVertex
			#pragma fragment ShadowDepthPassFragment

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags
			{
				"LightMode" = "DepthOnly"
			}

			ZWrite On
			ColorMask 0

			HLSLPROGRAM

			// Required to compile gles 2.0 with standard srp library
			#pragma prefer_hlslcc gles
			#pragma exclude_renderers d3d11_9x
			#pragma target 2.0

			//--------------------------------------
			// GPU Instancing
			#pragma multi_compile_instancing

			// using simple #define doesn't work, we have to use this instead
			#pragma multi_compile DEPTH_ONLY_PASS

			#pragma vertex ShadowDepthPassVertex
			#pragma fragment ShadowDepthPassFragment

			ENDHLSL
		}

		Pass
		{
			Name "DepthNormals"
			Tags
			{
				"LightMode" = "DepthNormals"
			}

			ZWrite On

			HLSLPROGRAM
			#pragma exclude_renderers gles gles3 glcore
			#pragma target 2.0

			#pragma multi_compile_instancing

			// using simple #define doesn't work, we have to use this instead
			#pragma multi_compile DEPTH_ONLY_PASS
			#pragma multi_compile DEPTH_NORMALS_PASS

			#pragma vertex ShadowDepthPassVertex
			#pragma fragment ShadowDepthPassFragment

			ENDHLSL
		}

		// Scene selection and picking passes
		Pass
		{
			Name "SceneSelectionPass"
			Tags
			{
				"LightMode" = "SceneSelectionPass"
			}

			HLSLPROGRAM
			#pragma exclude_renderers gles gles3 glcore
			#pragma target 2.0

			#pragma multi_compile_instancing
			#pragma multi_compile DEPTH_ONLY_PASS

			#pragma vertex ShadowDepthPassVertex
			#pragma fragment SceneSelectionFragment

			int _ObjectId;
			int _PassValue;

			half4 SceneSelectionFragment(Varyings input) : SV_Target
			{
				ShadowDepthPassFragment(input);
				return float4(_ObjectId, _PassValue, 1, 1);
			}

			ENDHLSL
		}

		Pass
		{
			Name "ScenePickingPass"
			Tags
			{
				"LightMode" = "Picking"
			}

			HLSLPROGRAM
			#pragma exclude_renderers gles gles3 glcore
			#pragma target 2.0

			#pragma multi_compile_instancing
			#pragma multi_compile DEPTH_ONLY_PASS

			#pragma vertex ShadowDepthPassVertex
			#pragma fragment ScenePickingFragment

			float4 _SelectionID;

			half4 ScenePickingFragment(Varyings input) : SV_Target
			{
				ShadowDepthPassFragment(input);
				return _SelectionID;
			}

			ENDHLSL
		}
	}

	FallBack "Hidden/InternalErrorShader"
	CustomEditor "ToonyColorsPro.ShaderGenerator.MaterialInspector_SG2"
}

/* TCP_DATA u config(ver:"2.9.21";unity:"6000.3.6f1";tmplt:"SG2_Template_URP";features:list["UNITY_5_4","UNITY_5_5","UNITY_5_6","UNITY_2017_1","UNITY_2018_1","UNITY_2018_2","UNITY_2018_3","UNITY_2019_1","UNITY_2019_2","UNITY_2019_3","UNITY_2019_4","UNITY_2020_1","UNITY_2021_1","UNITY_2021_2","UNITY_2022_2","UNITY_6000_2","UNITY_6000_1","UNITY_6000_0","ENABLE_DEPTH_NORMALS_PASS","ENABLE_FORWARD_PLUS","SPEC_PBR_GGX","SPECULAR","RIM","MATCAP_ADD","MATCAP","TEMPLATE_LWRP"];flags:list[];flags_extra:dict[];keywords:dict[RENDER_TYPE="Opaque",RampTextureDrawer="[TCP2Gradient]",RampTextureLabel="Ramp Texture",SHADER_TARGET="3.0",RIM_LABEL="Rim Lighting"];shaderProperties:list[,,,,,,,,sp(name:"Specular Color";imps:list[imp_mp_color(def:RGBA(0, 0, 0, 1);hdr:False;cc:3;chan:"RGB";prop:"_SpecularColor";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"26bb281c-d06a-4fcc-a5bc-f83b4625113b";op:Multiply;lbl:"Specular Color";gpu_inst:False;dots_inst:False;locked:False;impl_index:0)];layers:list[];unlocked:list[];layer_blend:dict[];custom_blend:dict[];clones:dict[];isClone:False),,sp(name:"Rim Color";imps:list[imp_mp_color(def:RGBA(0, 0, 0, 0.5);hdr:False;cc:3;chan:"RGB";prop:"_RimColor";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"02bd8f43-7413-456f-9622-5a088607f07e";op:Multiply;lbl:"Rim Color";gpu_inst:False;dots_inst:False;locked:False;impl_index:0)];layers:list[];unlocked:list[];layer_blend:dict[];custom_blend:dict[];clones:dict[];isClone:False),,,,,sp(name:"MatCap Color";imps:list[imp_mp_color(def:RGBA(0, 0, 0, 1);hdr:False;cc:3;chan:"RGB";prop:"_MatCapColor";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"cbd70552-8c09-4761-b210-357f6f4a729d";op:Multiply;lbl:"MatCap Color";gpu_inst:False;dots_inst:False;locked:False;impl_index:0)];layers:list[];unlocked:list[];layer_blend:dict[];custom_blend:dict[];clones:dict[];isClone:False)];customTextures:list[];codeInjection:codeInjection(injectedFiles:list[];mark:False);matLayers:list[]) */
/* TCP_HASH 8b2323e1b014dbd9d7a3d23c1db4a003 */
