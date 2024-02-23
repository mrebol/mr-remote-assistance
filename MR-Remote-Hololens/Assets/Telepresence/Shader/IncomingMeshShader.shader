Shader "Kinect/IncomingMeshShader" 
{
	Properties
	{
		_ColorTex("Albedo (RGB)", 2D) = "white" {}
		_DepthTex("Depth (HSV)", 2D) = "white" {}
		_scaleTranslation("Scale Translation", Range(0.0001, 0.005)) = 0.001
		
		_xScale("X Scale", Range(0.8, 4)) = 1.42
		_yScale("Y Scale", Range(0.8, 4)) = 1.42
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue" = "Geometry-1"
		}

		Pass
		{
			ZWrite On // Mesh hands should disappear behind hologram 
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom

			#pragma target 5.0
			#include "UnityCG.cginc"
			#include "FragmentLighting.cginc"


			sampler2D _ColorTex;
			sampler2D _ColorTexY;
			sampler2D _ColorTexU;
			sampler2D _ColorTexV;
			sampler2D _AnnotationTex;
			sampler2D _PointerTex;
			
			float4 _ColorTex_ST;
			float4 _ColorTex_TexelSize;

			sampler2D _CameraDepthTexture;

			float _MaxEdgeLen;
			float _scaleTranslation;
		    float _xScale;
		    float _yScale;
			
			struct InZ
			{
				uint z;
			};
			struct InXY
			{
				uint xy;
			};
			struct Ray
            {
                float3 Origin;
                float3 Direction;
            };

			struct Vertex
            {
                float4 vpos;
                float3 color;
            };
			
			StructuredBuffer<uint> _DepthMap: register(t0);
			RWStructuredBuffer<float3> _PointerIntersectionWorld: register(u2);
			RWStructuredBuffer<uint> _PointerIntersectionPx: register(u1);


			int _CoarseFactor;
			int _IsPointCloud;
			float2 _SpaceScale;
            int _showColor;
            int _interpolateCurrentDepth;
            int _stableTriangles;
			int _moveEdges;
			int _colorAlpha;

			float2 _ColorTexRes;
			float2 _DepthTexRes;
			float3 _PointerPosition; 
            float3 _CameraPosition;
            
            float2 _colorIntrinsicsCxy;
            float2 _colorIntrinsicsFxy;
            float _ColorExtrinsicsR[9]; 
            float3 _ColorExtrinsicsT; 
            uniform float _ColorDistortionRadial[6];
            float2 _ColorDistortionTangential;
            
            float2 _depthIntrinsicsCxy;
            float2 _depthIntrinsicsFxy;
            float _DepthExtrinsicsR[9]; 
            float3 _DepthExtrinsicsT; 
            uniform float _DepthDistortionRadial[6];
            float2 _DepthDistortionTangential;

			float4x4 _LocalToWorldTransform;
			float4x4 _WorldToLocalTransform;
			float3 _PosMin;
			float3 _PosMax;
			float _DepthFactor;
            
            UNITY_VERTEX_INPUT_INSTANCE_ID //Insert for Single Pass Rendering

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv_ColorTex : TEXCOORD0;
				float4 vertexPos : TEXCOORD1;
				uint idx : TEXCOORD2;

				bool mask : TEXCOORD3;
				float3 wPos : TEXCOORD4;
            	float alpha : TEXCOORD5;
				
				UNITY_VERTEX_OUTPUT_STEREO //Insert for Single Pass Rendering
				
			};

			int _ApplyLights;
			int _ApplyShadows;

			float4 _DirectionalLights[2];
			sampler2D _DirectionalShadowMap;

			uint readCurrentDepthAtIndex(uint di)
			{
				uint depth2 = 0;
				depth2 = _DepthMap[di >> 1];  // div by 2

				uint depthToShow = di & 1 != 0 ? depth2 >> 16 : depth2 & 0xffff;  // div by 2^16

				return depthToShow;
			}

			float interpolateCurrentDepthAt(float2 di)
			{
				const uint xLow = di.x;
				uint xHigh = xLow + 1;
				uint yLow = di.y;
				uint yHigh = yLow + 1;
				uint leftTop = yLow * _DepthTexRes.x + xLow;
				uint rightTop = yLow * _DepthTexRes.x + xHigh;
				uint leftBot = yHigh * _DepthTexRes.x + xLow;
				uint rightBot = yHigh * _DepthTexRes.x + xHigh;
				uint leftTopDepth = readCurrentDepthAtIndex(leftTop);
				uint rightTopDepth = readCurrentDepthAtIndex(rightTop);
				uint leftBotDepth = readCurrentDepthAtIndex(leftBot);
				uint rightBotDepth = readCurrentDepthAtIndex(rightBot);
				if(leftTopDepth == 0 || rightTopDepth == 0 || leftBotDepth == 0 || rightBotDepth == 0)
				{
					if(leftTopDepth != 0)
						return leftTopDepth;
					if(rightBotDepth != 0)
						return rightBotDepth;
					if(leftBotDepth != 0)
						return leftBotDepth;
					if(rightTopDepth != 0)
						return rightTopDepth;
					return 0;
				}
				//https://en.wikipedia.org/wiki/Bilinear_interpolation
				float interDepth = (1.0/float((xHigh - xLow)*(yHigh-yLow))) *(
						(leftBotDepth  * (xHigh - di.x) * (yHigh - di.y)) +
						(rightBotDepth * (di.x - xLow)  * (yHigh - di.y))+
						(leftTopDepth  * (xHigh - di.x) * (di.y - yLow)) +
						(rightTopDepth * (di.x - xLow)  * (di.y - yLow))
						);
				return interDepth;
			}

			float4 getSpacePos(uint idx, float fDepth, float2 texCoord)
			{
				float4 spacePos = float4(texCoord.x *  _SpaceScale.x, texCoord.y *  _SpaceScale.y, fDepth, 1.0);
				return spacePos;
			}
			
		float2 distort(float3 xyz, uniform float distortionRadial[6], float2 distortionTangential, float4 distortionPrism){
                float xPrime = (xyz.x) / xyz.z ;         
                float yPrime = (xyz.y) / xyz.z ; 
                float r2 = xPrime * xPrime + yPrime * yPrime;
                float r4 = r2 * r2;
                float r6 = r4 * r2;
                
                float radialFactor = ((1.0+distortionRadial[0]*r2+distortionRadial[1]*r4+ distortionRadial[2]*r6)/
                                      (1.0+distortionRadial[3]*r2+distortionRadial[4]*r4+ distortionRadial[5]*r6));
                float xDPrime = xPrime * radialFactor + 2 * distortionTangential[0]*xPrime*yPrime + 
                    distortionTangential[1]*(r2 + 2*xPrime*xPrime) + distortionPrism[0]*r2 + distortionPrism[1]*r4;
                float yDPrime = yPrime * radialFactor + distortionTangential[0]*(r2 + 2 *yPrime*yPrime) + 
                    2 * distortionTangential[1]*xPrime*yPrime + distortionPrism[2]*r2 + distortionPrism[3]*r4;
                       
                return float2(xDPrime, yDPrime);
            }
            

            float2 applyIntrinsics(float2 xyDPrime, float2 fxy, float2 cxy)
            {
                float u = fxy.x * xyDPrime.x + cxy.x;
                float v = fxy.y * xyDPrime.y + cxy.y;
                return float2(u,v);
            }
            
            
            float3 applyIntrinsicsInv(float2 uv, float2 fxy, float2 cxy)
            {
                float x = (uv.x - cxy.x) / fxy.x;
                float y = (uv.y - cxy.y) / fxy.y;
                return float3(x, y, 1);
            }

			
            float3 applyExtrinsics(float3 XYZ, float3 rotationRow0, float3 rotationRow1, float3 rotationRow2, float3 translation)
            {
                float3 rotated = float3(mul(rotationRow0 , XYZ), mul(rotationRow1 , XYZ), mul(rotationRow2 , XYZ));
                return rotated + translation;
            }


			
			float2 getPosFromIndex(uint x, uint y)
			{
				return float2((float(x) / _DepthTexRes.x  - 0.5) * _xScale, (float(y) / _DepthTexRes.y - 0.5) * _yScale);
			}

			
			float2 getDepthIdxForVertexPos(float2 xy) 
			{
				int extrinsicsPosition = 2; // 0..Off, 1..Before Distort, 2.. After Distort!!!
				float4 distortionPrism = float4(0,0,0,0);
				// Prism distortion should be zero according to
				// https://www.mathworks.com/help/symbolic/developing-an-algorithm-for-undistorting-an-image.html
				float3 colorExtrinsicsR0 = float3(_ColorExtrinsicsR[0], _ColorExtrinsicsR[1], _ColorExtrinsicsR[2]);
				float3 colorExtrinsicsR1 = float3(_ColorExtrinsicsR[3], _ColorExtrinsicsR[4], _ColorExtrinsicsR[5]);
				float3 colorExtrinsicsR2 = float3(_ColorExtrinsicsR[6], _ColorExtrinsicsR[7], _ColorExtrinsicsR[8]);
				
				float3 XYZ = float3(xy, 1.0);
				float3 xyzDepth = float3(XYZ.x, XYZ.y, XYZ.z);
				float2 distortedDepthCoordinates = distort(xyzDepth, _DepthDistortionRadial, _DepthDistortionTangential, distortionPrism);
				float2 uvDepth = applyIntrinsics(distortedDepthCoordinates,  _depthIntrinsicsFxy, _depthIntrinsicsCxy);
				return uvDepth;
			}

			float4 getVposFromDepth(float2 XY, uint depth)
			{
				float4 vPos;
				int scaleByDepth = 1;				
                float fDepth = (float) depth * 0.001;
				if(scaleByDepth) {
                    vPos = float4(XY.x * fDepth,  XY.y * fDepth, fDepth* _DepthFactor, 1);                              
                } else {
                    vPos = float4(XY.x, XY.y, fDepth * _DepthFactor, 1);
                }                 
                return vPos;
			}

			float computeAlpha(uint index, float4 pos)
			{
				int x = index % _DepthTexRes.x;
				int y = (index - x) / _DepthTexRes.x;
				uint validNeighborsCount = 0;
				for (int yi = -1; yi <= 1; yi++)
				{
					for (int xi = -1; xi <= 1; xi++)
					{
						int xSearch = x + xi * _CoarseFactor;
						int ySearch = y + yi * _CoarseFactor;
    
						if (xSearch >= 0 && xSearch <= _DepthTexRes.x && ySearch >= 0 && ySearch <= _DepthTexRes.y &&
							(!(xi == 0 && yi == 0)))
						{
							float2 neighborPos = getPosFromIndex(xSearch, ySearch);
							float2 neighborDepthIdx = getDepthIdxForVertexPos(neighborPos);
							uint depth;

							if(_interpolateCurrentDepth)
							{
								depth = (int) interpolateCurrentDepthAt(neighborDepthIdx);
							} else
							{
								uint ndx = (uint)(neighborDepthIdx.x); 
								uint ndy = (uint)(neighborDepthIdx.y);
								uint idx = (ndx + ndy * _DepthTexRes.x);
							
								depth = readCurrentDepthAtIndex(idx);
							}
							float4 neighborVPos = getVposFromDepth(neighborPos, depth);
							float edgeLen = distance(pos.xyz, neighborVPos.xyz);
							if(_stableTriangles == 0 && (edgeLen < (_MaxEdgeLen * _CoarseFactor * 1.0))) // use max edge len although it is vertex distance
							{
								validNeighborsCount += 1;
							}
						}
					}
				}
				if(validNeighborsCount < 4)
				{
					return 0;
				}
				return float(validNeighborsCount / 8.0);
			}
			
			Vertex moveEdge(uint index, float4 pos)
            {
                int x = index % (int)_DepthTexRes.x;
                int y = (index - x) / (int)_DepthTexRes.x;
				float2 centerPos = getPosFromIndex(x, y);
                
				uint validNeighbors[8];
				float4 neighborVpos[8];
                uint validNeighborsCount = 0;
				uint neighborIndex = 0;
                for (int yi = -1; yi <= 1; yi++)
                {
                  for (int xi = -1; xi <= 1; xi++)
                  {
	                int xSearch = x + xi * _CoarseFactor;
                  	int ySearch = y + yi * _CoarseFactor;
                  	if(!(xi == 0 && yi == 0))
                  	{
						if (xSearch >= 0 && xSearch <= _DepthTexRes.x &&
							ySearch >= 0 && ySearch <= _DepthTexRes.y )
						{
							if(yi == 0 && xi == -1)
							{
								neighborIndex = 7;	
							} else if(yi == 0 && xi == 1)
							{
								neighborIndex = 3;
							}
							else if(yi == 1 && xi == -1)
							{
								neighborIndex = 6;
							}
							else if(yi == 1 && xi == 0)
							{
								neighborIndex = 5;
							}
							else if(yi == 1 && xi == 1)
							{
								neighborIndex = 4;
							} 
							
							float2 neighborPos = getPosFromIndex(xSearch, ySearch);
							float2 neighborDepthIdx = getDepthIdxForVertexPos(neighborPos);
							uint depth;

							if(_interpolateCurrentDepth)
							{
								depth = (int)interpolateCurrentDepthAt(neighborDepthIdx);
							} else
							{
								uint ndx = (uint)(neighborDepthIdx.x); 
								uint ndy = (uint)(neighborDepthIdx.y);
								uint idx = (ndx + ndy * _DepthTexRes.x);
							
								depth = readCurrentDepthAtIndex(idx);
							}
							float4 neighborVPos = getVposFromDepth(neighborPos, depth);
							neighborVpos[neighborIndex] = neighborVPos;

							float edgeLen = distance(pos.xyz, neighborVPos.xyz);
							if(_stableTriangles == 0 && (edgeLen < (_MaxEdgeLen * _CoarseFactor * 1.0))) // use max edge len although it is vertex distance
							{
								validNeighborsCount += 1;
								validNeighbors[neighborIndex] = 1;
							}
							else
							{
								validNeighbors[neighborIndex] = 0;
							}

						}
                  		neighborIndex++;
					}
                  }
                }

				if(validNeighborsCount < 8) // somehow an edge vertex. Adjust Position.
				{
					if(validNeighborsCount == 2) // disable vertex if only 2 neighbors
					{
						for(uint i=0; i<8; i++)
						{
							int j = (i+1) % 8;
							if(validNeighbors[i] && validNeighbors[j])
							{
								Vertex vert;
								if(_moveEdges)
								{
									vert.vpos = lerp(neighborVpos[i], neighborVpos[j], 0.5);
								} else
								{
									vert.vpos = pos;
								}
									vert.color = float3(1,0,0);
								return vert;
							}
							
						}						
					}
					
					else if (validNeighborsCount == 3)
					{
						for(uint i=0; i<8; i++)
						{
							int j = (i+1) % 8;
							int k = (i+2) % 8;
							if(validNeighbors[i] && validNeighbors[j] && validNeighbors[k])
							{
								Vertex vert;
								vert.color = float3(0,1,0);
								if(_moveEdges)
								{
									vert.vpos = lerp(pos, neighborVpos[j], 0.5);
								} else
								{
									vert.vpos = pos;
								}
								
								return vert;
							}
						}
						
					}
					else if (validNeighborsCount == 4)
					{
						for(uint i=0; i<8; i++)
						{
							int j = (i+1) % 8;
							int k = (i+2) % 8;
							int l = (i+3) % 8;
							if(validNeighbors[i] && validNeighbors[j] && validNeighbors[k] && validNeighbors[l])
							{
								Vertex vert;
								vert.color = float3(0,0,1);
								if(_moveEdges)
								{
									vert.vpos = lerp(pos,
										lerp(neighborVpos[j], neighborVpos[k], 0.5), 0.5);
								} else
								{
									vert.vpos = pos;
								}
								return vert;
							}
						}
						
					}
					else if (validNeighborsCount == 5)
					{
						Vertex vert;
						vert.color = float3(1,1,1);
						vert.vpos = pos;
						return vert;
					}
					else if (validNeighborsCount == 6)
					{
						for(uint i=0; i<8; i++)
						{
							int j = (i+1) % 8;
							int k = (i+2) % 8;
							int l = (i+3) % 8;
							int m = (i+4) % 8;
							int n = (i+5) % 8;
							if(validNeighbors[i] && validNeighbors[j] && validNeighbors[k] &&
								validNeighbors[l] && validNeighbors[m] && validNeighbors[n])
							{
								Vertex vert;
								vert.color = float3(1,1,1);
								if(_moveEdges)
								{
									vert.vpos = lerp(pos, lerp(neighborVpos[k], neighborVpos[l], 0.5), -0.2);
								} else
								{
									vert.vpos = pos;
								}
								return vert;
							}
						}
					}else if (validNeighborsCount == 7)
					{
						for(uint i=0; i<8; i++)
						{
							if(validNeighbors[i] == 0)
							{
								Vertex vert;
								vert.color = float3(1,1,1);
								if(_moveEdges)
								{
									vert.vpos = pos;
								} else
								{
									vert.vpos = pos;
								}
								
								return vert;
							}
						}
					}
				}
				Vertex vert;
				vert.color = float3(1,1,1);
				vert.vpos = pos;
				return vert;
             }
			
			v2f vert(appdata_base v)
			{
			    int flip = 1;
			    int extrinsicsBeforeDistort = 0;
				v2f o;
				float4 vPos;
				uint depth;
				uint dx = (uint)(v.texcoord.x * _DepthTexRes.x);
				uint dy = (uint)(v.texcoord.y * _DepthTexRes.y); 
                uint idx = (dx + dy * _DepthTexRes.x);
				
				UNITY_SETUP_INSTANCE_ID(v); //Insert for Single Pass Rendering
                UNITY_INITIALIZE_OUTPUT(v2f, o); //Insert for Single Pass Rendering
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); //Insert for Single Pass Rendering

				float4 distortionPrism = float4(0,0,0,0); // should be zero according to https://www.mathworks.com/help/symbolic/developing-an-algorithm-for-undistorting-an-image.html
                float3 colorExtrinsicsR0 = float3(_ColorExtrinsicsR[0], _ColorExtrinsicsR[1], _ColorExtrinsicsR[2]);
                float3 colorExtrinsicsR1 = float3(_ColorExtrinsicsR[3], _ColorExtrinsicsR[4], _ColorExtrinsicsR[5]);
                float3 colorExtrinsicsR2 = float3(_ColorExtrinsicsR[6], _ColorExtrinsicsR[7], _ColorExtrinsicsR[8]);
                                  

                // Vertex Positions
				float3 XYZ = float3(getPosFromIndex(dx,dy), 1);
				
                
				if(_DepthFactor> 0.01)
				{
					// Step 1: Depth
					float2 dIdx = getDepthIdxForVertexPos(XYZ.xy);
					uint depthx = (uint)dIdx.x;
					uint depthy = (uint)dIdx.y; 
					uint depthidx = (depthx + depthy * _DepthTexRes.x);
					depth = readCurrentDepthAtIndex(depthidx);
					vPos = getVposFromDepth(XYZ.xy, depth);
					if(_moveEdges)
					{
						Vertex vertex = moveEdge(idx, vPos);
						vPos = vertex.vpos;
					}
					if(_colorAlpha)
					{
						o.alpha = computeAlpha(idx, vPos);
					}
				} else
				{
					vPos = float4(XYZ.x, XYZ.y, 1, 1);    
				}
                if(flip)
                {
                    vPos = float4(vPos.x, vPos.y * -1, vPos.z, vPos.w);
                }
				
				
                // Step 2: Color 
                float3 colorxyz;
                if(extrinsicsBeforeDistort)
                {
                    colorxyz = applyExtrinsics(XYZ, colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                            _ColorExtrinsicsT * _scaleTranslation);
                }
                else
                {
                    colorxyz = XYZ;
                }
                
                float2 uvColor;
                if(1)
                {
                    if(extrinsicsBeforeDistort) {
                        float2 colorxyDPrime = distort(colorxyz, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);
                        uvColor = applyIntrinsics(colorxyDPrime ,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                    }else{
                        float2 colorxyDPrime = distort(colorxyz, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);
                        float3 corrected = applyExtrinsics(float3(colorxyDPrime.x, colorxyDPrime.y, 1.0), colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                            _ColorExtrinsicsT*_scaleTranslation);
                        float2 correctedxy = float2(corrected.x / corrected.z, corrected.y / corrected.z); 
                        uvColor = applyIntrinsics(correctedxy ,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                    }
                } else {    
                    uvColor = applyIntrinsics(float2(colorxyz.x/colorxyz.z, colorxyz.y/ colorxyz.z),  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                }
                o.uv_ColorTex = float2(uvColor.x / _ColorTexRes.x, uvColor.y / _ColorTexRes.y);
           
				float4 wPos = mul(_LocalToWorldTransform, vPos);
				bool mask = vPos.x >= _PosMin.x && vPos.x <= _PosMax.x && vPos.y >= _PosMin.y && vPos.y <= _PosMax.y && vPos.z >= _PosMin.z && vPos.z <= _PosMax.z;
				o.wPos = wPos.xyz; 
				o.pos = UnityObjectToClipPos(vPos);

				o.vertexPos = vPos;
				o.mask = mask;
				return o;
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
            
            fixed3 YUVtoRGBv2(fixed3 yuv) 
            {
                fixed3 rgb;
                rgb.r = yuv.x + 1.370705 * (yuv.z - 0.5);
                rgb.g = yuv.x + 0.698001 * (yuv.z - 0.5) - (0.337633 * (yuv.y - 0.5));
                rgb.b = yuv.x + 1.732446 * (yuv.y - 0.5); 
                return rgb;
            }
            

			fixed4 frag(v2f i) : SV_Target
			{
                fixed3 yuv;
                yuv.r = tex2D(_ColorTexY, i.uv_ColorTex).r;
                yuv.g = tex2D(_ColorTexU, i.uv_ColorTex).r;
                yuv.b = tex2D(_ColorTexV, i.uv_ColorTex).r;
                fixed4 rgb = fixed4(0,0,0,0);
				if(_showColor){
				  rgb = fixed4(YUVtoRGB(yuv.rgb), 1);
				  if(_colorAlpha)
				  {
					   rgb.a = i.alpha;
				  }
					else
				  {
						rgb.a = 1;
				  }
				}

				//Annotations
				rgb.r += tex2D(_AnnotationTex, float2(i.uv_ColorTex.x, i.uv_ColorTex.y)).r;
				if(_colorAlpha == 0 && rgb.r > 0.3){
				    rgb.a = 1;
				}

		        rgb.g += tex2D(_PointerTex, i.uv_ColorTex).r;
		        if(_colorAlpha == 0 && rgb.g > 0.3){
				    rgb.a = 1;
				}
				return rgb;
			}


			float getMaxEdgeLen(float3 v0, float3 v1, float3 v2)
			{
				float maxEdgeLen = distance(v0, v1);
				maxEdgeLen = max(maxEdgeLen, distance(v1, v2));
				maxEdgeLen = max(maxEdgeLen, distance(v2, v0));

				return maxEdgeLen;
			}
			
			
			// https://stackoverflow.com/questions/59257678/intersect-a-ray-with-a-triangle-in-glsl-c
			float PointInOrOn(float3 P1, float3 P2, float3 A, float3 B )  
            {
                float3 CP1 = cross(B - A, P1 - A);
                float3 CP2 = cross(B - A, P2 - A);
                return step(0.0, dot(CP1, CP2));
            }
            
            bool PointInTriangle(float3 px, float3 p0, float3 p1, float3 p2 )
            {
                return 
                    PointInOrOn(px, p0, p1, p2) *
                    PointInOrOn(px, p1, p2, p0) *
                    PointInOrOn(px, p2, p0, p1);
            }

            
            float3 IntersectPlane(Ray ray, float3 p0, float3 p1, float3 p2)
            {
                float3 D = ray.Direction;
                float3 N = cross(p1-p0, p2-p0);
                float3 X = ray.Origin + D * dot(p0 - ray.Origin, N) / dot(D, N);
            
                return X;
            }
            
            bool IntersectTriangle(Ray ray, float3 p0, float3 p1, float3 p2)
            {
                float3 X = IntersectPlane(ray, p0, p1, p2);
                return PointInTriangle(X, p0, p1, p2);
            }

			[maxvertexcount(6)]
			void geom(triangle v2f input[3], inout TriangleStream<v2f> outStream)
			{
				v2f p0 = input[0];
				v2f p1 = input[1];
				v2f p2 = input[2];

				if (_IsPointCloud)
				{
				}
				else
				{
					if (p0.mask && p1.mask && p2.mask && getMaxEdgeLen(p0.vertexPos, p1.vertexPos, p2.vertexPos) < _MaxEdgeLen * _CoarseFactor)
					{
                        // Intersection check:
                        Ray pointerRay;
                        pointerRay.Origin = _WorldSpaceCameraPos; //_WorldSpaceCameraPos ... Main Camera (0,0,0)
                        pointerRay.Direction = normalize(_PointerPosition - _WorldSpaceCameraPos);
                        
                        if (IntersectTriangle(pointerRay, p0.wPos, p1.wPos, p2.wPos))
                        {
                            float3 intersectionPosition = IntersectPlane(pointerRay, p0.wPos, p1.wPos, p2.wPos);
                            _PointerIntersectionWorld[0] = intersectionPosition;
                                                      
                            
                            if(distance(intersectionPosition, _PointerPosition) < 0.1){ 
                                int interpolateIntersection = 1;
                                int scaleByDepth = 1;
                                if(interpolateIntersection) {
                                    float4 distortionPrism = float4(0,0,0,0);
                                    float3 colorExtrinsicsR0 = float3(_ColorExtrinsicsR[0], _ColorExtrinsicsR[1], _ColorExtrinsicsR[2]);
                                    float3 colorExtrinsicsR1 = float3(_ColorExtrinsicsR[3], _ColorExtrinsicsR[4], _ColorExtrinsicsR[5]);
                                    float3 colorExtrinsicsR2 = float3(_ColorExtrinsicsR[6], _ColorExtrinsicsR[7], _ColorExtrinsicsR[8]);
                                    
                                    float4 vIntersectionPos = mul(_WorldToLocalTransform, float4(intersectionPosition, 1));
                                    
                                    // Step 2: Color 
                                    float3 colorxyz;
                                    if (scaleByDepth) {
                                        if(_DepthFactor> 0.01)
                                    	colorxyz = float3(vIntersectionPos.x, vIntersectionPos.y *-1, vIntersectionPos.z / _DepthFactor);
											else
                                    	colorxyz = float3(vIntersectionPos.x, vIntersectionPos.y *-1, vIntersectionPos.z);
                                    
                                    }
                                    else {
                                        colorxyz = float3(intersectionPosition.x, 1.0 - intersectionPosition.y - 1.0, 1.0);
                                    }
                                    float2 uvColor;
                                    float2 colorxyDPrime = distort(colorxyz, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);
                                    float3 corrected = applyExtrinsics(float3(colorxyDPrime.x, colorxyDPrime.y, 1.0), colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                                        _ColorExtrinsicsT*_scaleTranslation);
                                    float2 correctedxy = float2(corrected.x / corrected.z, corrected.y / corrected.z); 
                                    uvColor = applyIntrinsics(correctedxy ,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                                    _PointerIntersectionPx[0] = uvColor.x;
                                    _PointerIntersectionPx[1] = uvColor.y;
                                }
                                else {
                                   _PointerIntersectionPx[0] = p0.uv_ColorTex.x * _ColorTexRes.x;
                                   _PointerIntersectionPx[1] = p0.uv_ColorTex.y * _ColorTexRes.y;
                                }
                                
                            }
                        }
                        else{
                       }
                        


						outStream.Append(p0);
						outStream.Append(p1);
						outStream.Append(p2);
						outStream.RestartStrip();
					}
				}
			}

			ENDCG
		}
	
	}
}