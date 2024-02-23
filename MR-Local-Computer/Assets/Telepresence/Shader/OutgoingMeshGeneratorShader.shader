Shader "Kinect/OutgoingMeshGeneratorShader" 
{
	Properties
	{
		_ColorTex("Albedo (RGB)", 2D) = "white" {}
		_MaxEdgeLen("Max edge length", Range(0.01, 10)) = 5 
		_scaleDepth("Scale Depth", Range(0.01, 10)) = 1 
		
		_xScale("X Scale", Range(0.8, 4)) = 1.42
		_yScale("Y Scale", Range(0.8, 4)) = 1.42
		_scaleExtrinsicsTranslation("Scale Translation", Range(0.0001, 0.005)) = 0.001
		
		_extrinsicsTranslationCorrectionX("X corr", Range(-0.05, 0.05)) = 0
		_extrinsicsTranslationCorrectionY("Y corr", Range(-0.05, 0.05)) = 0
		_extrinsicsTranslationCorrectionZ("Z corr", Range(-0.05, 0.05)) = 0
		_useScaledVerticesForColor("_useScaledVerticesForColor ", Int) = 0
		
		_extrinsicsPosition("extrins",Int) = 2 // 0..Off, 1..Before Distort, 2.. After Distort!!!
		_meetAtXYZ("meetXYZ",Int) = 1 // 1.. avoid inaccuracies of optimization to get inverse distortion

	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent-1"
		}

		Pass
		{
			ZWrite On  // zBuffer  check if geometry shader only processes visible triangles
			Blend SrcAlpha OneMinusSrcAlpha
			Cull Off  // render front and back to look at Hologram from behind

			CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
//#pragma exclude_renderers d3d11 gles



			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom

			#pragma target 5.0
			#include "UnityCG.cginc"
			//#include "FragmentLighting.cginc"


			sampler2D _ColorTex;
			sampler2D _AnnotationTex;
			sampler2D _PointerTex;

			float4 _ColorTex_ST;
			float4 _ColorTex_TexelSize;

			sampler2D _CameraDepthTexture;

			float _MaxEdgeLen;
			float _scaleDepth;
		    float _xScale;
		    float _yScale;
		    float _scaleExtrinsicsTranslation;
			
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
	
            
			#ifdef SHADER_API_D3D11
			StructuredBuffer<uint> _DepthMap;  // Read Only buffer
			StructuredBuffer<float> _SpaceTable;
			StructuredBuffer<uint> _depthHistory;
			StructuredBuffer<uint> _previousDepthImage;
			//
			RWStructuredBuffer<float3> _PointerIntersectionWorld: register(u1);
			RWStructuredBuffer<uint> _PointerIntersectionPx: register(u2);
			RWStructuredBuffer<float3> _DebugBuffer : register(u3); // register maybe not needed
			RWStructuredBuffer<uint> _FilteredDepth : register(u4); 
			RWStructuredBuffer<uint> _MeasuredDepth : register(u5); 
			#endif

			int _CoarseFactor;
			int _IsPointCloud;
			float2 _SpaceScale;
			//bool _foundSomething;
			int _isColorRegistered;
			int _useDepthCoordinateSystem;
			int _flipScene;
			int _showHologram;
			int _historyFrames;
			int _innerBandThreshold;// default 2;  // TODO adjust
            int _outerBandThreshold;// default 5;
			int _useScaledVerticesForColor;
			int _extrinsicsPosition; // 0..Off, 1..Before Distort, 2.. After Distort!!!
			int _meetAtXYZ; // 1.. avoid inaccuracies of optimization to get inverse distortion

			int _interpolateCurrentDepth;
			int _interpolatePreviousDepth;
			int _interpolateHistoryDepth;
			int _replaceZeroHistoric;
            int _convergeFlat;
            int _convergeNoisy;
            int _stableTriangles;
            int _colorAlpha;
            int _moveEdges;
            int _colorEdges;
			int _resetHistory;
			float _vsx;
			float _vsy;
			float _DepthFactor;
			
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
			float3 _PosMin;  // -5, -5, 0.5 meters 
			float3 _PosMax;  // 5, 5, 10 meters
			float _extrinsicsTranslationCorrectionX;
			float _extrinsicsTranslationCorrectionY;
			float _extrinsicsTranslationCorrectionZ;


			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv_ColorTex : TEXCOORD0;
				float4 vertexPos : TEXCOORD1;
				uint vIdx : TEXCOORD2;

				bool mask : TEXCOORD3;
				float3 normal : NORMAL;
				float3 worldDirection : TEXCOORD4;
				float4 scrPos : TEXCOORD5;
				float3 wPos : TEXCOORD6;
				float alpha : TEXCOORD7;
				float2 dIdx : TEXCOORD8;
				float2 xy : TEXCOORD9;
				float3 color : TEXCOORD10;
				
			};

			int _ApplyLights;
			int _ApplyShadows;

			float4 _DirectionalLights[2];
			sampler2D _DirectionalShadowMap;

			
			float getMaxEdgeLen(float3 v0, float3 v1, float3 v2)
			{
				float maxEdgeLen = distance(v0, v1);
				maxEdgeLen = max(maxEdgeLen, distance(v1, v2));
				maxEdgeLen = max(maxEdgeLen, distance(v2, v0));

				return maxEdgeLen;
			}
			uint readCurrentDepthAtIndex(uint di)
			{
				uint depth2 = 0;
				//#ifdef SHADER_API_D3D11
				depth2 = _DepthMap[di >> 1];  // div by 2

				//#endif
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
			

			
			uint readPreviousDepthAtIndex(uint di)
			{
				uint depth2 = 0;
				depth2 = _previousDepthImage[di >> 1];  // div by 2
				uint depthToShow = di & 1 != 0 ? depth2 >> 16 : depth2 & 0xffff;  // div by 2^16

				return depthToShow;
			}

			uint interpolatePreviousDepthAt(float2 di)
			{
				uint xLow = (uint)di.x;
				uint xHigh = xLow + 1;
				uint yLow = (uint)di.y;
				uint yHigh = yLow + 1;
				uint leftTop = yLow * _DepthTexRes.x + xLow;
				uint rightTop = yLow * _DepthTexRes.x + xHigh;
				uint leftBot = yHigh * _DepthTexRes.x + xLow;
				uint rightBot = yHigh * _DepthTexRes.x + xHigh;
				uint leftTopDepth = readPreviousDepthAtIndex(leftTop);
				uint rightTopDepth = readPreviousDepthAtIndex(rightTop);
				uint leftBotDepth = readPreviousDepthAtIndex(leftBot);
				uint rightBotDepth = readPreviousDepthAtIndex(rightBot);
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
				return (1.0/((xHigh - xLow)*(yHigh-yLow))) *(
						(leftBotDepth*(xHigh-di.x)*(yHigh-di.y)) +
						(rightBotDepth * (di.x - xLow) * (yHigh-di.y))+
						(leftTopDepth*(xHigh-di.x)*(di.y - yLow)) +
						(rightTopDepth*(di.x - xLow)*(di.y - yLow))
						);
			}
			
			
			uint readHistoryDepthAtIndex(uint di, int timestep)
			{
				uint depth2 = 0;
				depth2 = _depthHistory[(uint(timestep * _DepthTexRes.x * _DepthTexRes.y) >> 1) + (di >> 1)];  // div by 2
				uint depthToShow = di & 1 != 0 ? depth2 >> 16 : depth2 & 0xffff;  // div by 2^16

				return depthToShow;
			}

			uint interpolateHistoryDepthAt(float2 di, int timestep)
			{
				uint xLow = (uint) di.x;
				uint xHigh = xLow + 1;
				uint yLow = (uint) di.y;
				uint yHigh = yLow + 1;
				uint leftTop = yLow * _DepthTexRes.x + xLow;
				uint rightTop = yLow * _DepthTexRes.x + xHigh;
				uint leftBot = yHigh * _DepthTexRes.x + xLow;
				uint rightBot = yHigh * _DepthTexRes.x + xHigh;
				uint leftTopDepth = readHistoryDepthAtIndex(leftTop, timestep);
				uint rightTopDepth = readHistoryDepthAtIndex(rightTop, timestep);
				uint leftBotDepth = readHistoryDepthAtIndex(leftBot, timestep);
				uint rightBotDepth = readHistoryDepthAtIndex(rightBot, timestep);
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
				return (1.0/((xHigh - xLow)*(yHigh-yLow))) *(
						(leftBotDepth*(xHigh-di.x)*(yHigh-di.y)) +
						(rightBotDepth * (di.x - xLow) * (yHigh-di.y))+
						(leftTopDepth*(xHigh-di.x)*(di.y - yLow)) +
						(rightTopDepth*(di.x - xLow)*(di.y - yLow))
						);
			}
			
			void setFilteredDepth(uint idxL, uint idxH, uint depthL, uint depthH)
			{
				uint depthToStore = (depthL & 0xffff) | (depthH << 16); // mul by 2^16

				_FilteredDepth[idxL >> 1] = depthToStore; // div by 2

			}
			
			void setMeasuredDepth(uint idxL, uint idxH, uint depthL, uint depthH)
			{
				uint depthToStore = (depthL & 0xffff) | (depthH << 16); // mul by 2^16

				_MeasuredDepth[idxL >> 1] = depthToStore; // div by 2

			}

			float4 getSpacePos(uint idx, float fDepth, float2 texCoord)
			{
				float4 spacePos = float4(texCoord.x *  _SpaceScale.x, texCoord.y * _SpaceScale.y, fDepth, 1.0);
				return spacePos; 
			}

            float2 distort(float3 xyz, uniform float distortionRadial[6], float2 distortionTangential, float4 distortionPrism){
                float xPrime = (xyz.x) / xyz.z ;//* 320;         
                float yPrime = (xyz.y) / xyz.z ;//* 320;  
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
                return rotated + translation + float3(_extrinsicsTranslationCorrectionX, _extrinsicsTranslationCorrectionY, _extrinsicsTranslationCorrectionZ);
            }
            
            float3 applyExtrinsicsInverse(float3 XYZ, float3 rotationRow0, float3 rotationRow1, float3 rotationRow2, float3 translation)
            {
                float3 rotatedTranslation = float3(mul(float3(rotationRow0.x, rotationRow1.x, rotationRow2.x)  , translation), 
                                                    mul(float3(rotationRow0.y, rotationRow1.y, rotationRow2.y)  , translation), 
                                                    mul(float3(rotationRow0.z, rotationRow1.z, rotationRow2.z)  , translation)); // The transpose is the inverse
                float3 translated = XYZ - translation;
                float3 rotated = float3(mul(float3(rotationRow0.x, rotationRow1.x, rotationRow2.x)  , translated), 
                                        mul(float3(rotationRow0.y, rotationRow1.y, rotationRow2.y)  , translated), 
                                        mul(float3(rotationRow0.z, rotationRow1.z, rotationRow2.z)  , translated)); // The transpose is the inverse
                return rotated;
            }

			
            float3 kinect2_undistort(float3 xyz, uniform float distortionRadial[6])
            {
                float2 xy = float2(xyz.x / xyz.z, xyz.y / xyz.z);
                float ps = (xy.x * xy.x) + (xy.y * xy.y);
                float qs = ((ps * distortionRadial[2] + distortionRadial[1]) * ps + distortionRadial[0]) * ps + 1.0;
                for (int i = 0; i < 9; i++) {
                    float qd = ps / (qs * qs);
                    qs = ((qd * distortionRadial[2] + distortionRadial[1]) * qd + distortionRadial[0]) * qd + 1.0;
                }
                return float3(xy.x, xy.y, qs);
            }
			
            uint tempFilterDepth(float2 depthIndex)
             {
				uint dx = (uint)(depthIndex.x); 
				uint dy = (uint)(depthIndex.y);
				uint idx = (dx + dy * _DepthTexRes.x);
				
                int foundDepth;
				if(_interpolateCurrentDepth)
				{
					foundDepth = (int)interpolateCurrentDepthAt(depthIndex);
				} else
				{
					foundDepth = readCurrentDepthAtIndex(idx);
				}

                if(foundDepth == 0) 
                {
                	if( _replaceZeroHistoric)
                	{
                		for (int timestep = _historyFrames - 1; timestep >= 0; timestep--)
                		{
                			uint historyDepthValue;
                			if(_interpolateHistoryDepth)
                			{
                				 historyDepthValue = interpolateHistoryDepthAt(depthIndex, timestep);
                			} else
                			{
                				historyDepthValue = readHistoryDepthAtIndex(idx, timestep);
                			}
                			if (historyDepthValue != 0)
                			{
                				if(_interpolatePreviousDepth)
                				{
                					return interpolatePreviousDepthAt(depthIndex);
                				}
                				else
                				{
                					return readPreviousDepthAtIndex(idx);
                				}
                			}
                		}
                	}
                } 
                else 
                {
                	if(_convergeFlat)
                    {
                        float avgDepth;
                        uint historySum = 0;
                        uint historyCounter = 0;
                        uint lastXframes = 10; 
                        uint deltaRange = 2; // ..mm
                    	
                    	for (int timestep = _historyFrames - lastXframes; timestep < _historyFrames; timestep++)
                        {
                            int historyDepthValue1;
							if(_interpolateHistoryDepth)
                			{
                				 historyDepthValue1 = interpolateHistoryDepthAt(depthIndex, timestep);
                			} else
                			{
                				historyDepthValue1 = readHistoryDepthAtIndex(idx, timestep);
                			}
                            if(abs(foundDepth - historyDepthValue1) < deltaRange * (foundDepth * 0.001))  // needed to avoid trace
                            {
                                historySum += historyDepthValue1;
                                historyCounter += 1;
                            }
                        }
                        historySum += foundDepth;
                        historyCounter += 1;
                        avgDepth = float(historySum) / float(historyCounter); 

						int prevDepth;
						if(_interpolatePreviousDepth)
           				{
                			prevDepth = interpolatePreviousDepthAt(depthIndex);
                		}
                    	else
                		{
                			prevDepth = readPreviousDepthAtIndex(idx);
                		}
					
                    	if(abs(avgDepth - float(prevDepth)) > (3 * (((float)foundDepth) * 0.001)) ) // relative convergence depending on distance (1 mm prescision in 1m)
                        {
                        	if(_convergeNoisy)
                        	{
                        		int deltaChange = 2; // mm
                        		uint largeChangeCount = 0;
                        		uint nonZeroCount = 0;
                        		for (int timestep=_historyFrames-40; timestep < _historyFrames-1; timestep++)
                        		{
                        			int historyDepthValue1;
                        			int historyDepthValue2;
                        			if(_interpolateHistoryDepth)
                        			{
                        				historyDepthValue1 = interpolateHistoryDepthAt(depthIndex, timestep);
                        				historyDepthValue2 = interpolateHistoryDepthAt(depthIndex, timestep+1);
                        			} else
                        			{
                        				historyDepthValue1 = readHistoryDepthAtIndex(idx, timestep);
                        				historyDepthValue2 = readHistoryDepthAtIndex(idx, timestep+1);
                        			}
		                            
                        			if(historyDepthValue1 != 0 && historyDepthValue2 != 0 &&
										abs(historyDepthValue1 - historyDepthValue2)  > deltaChange )  // scale by distance in meters
                        			{ // TODO: improvement. Count back and forth changes only, not changes in one direction. This should avoid creating artifacts.
                        				largeChangeCount += 1;
                        			}
                        			if(historyDepthValue1 != 0 && historyDepthValue2 != 0) 
                        			{
                        				nonZeroCount += 1;
                        			}
                        		}
                        		if(largeChangeCount > 25 || nonZeroCount < 30)
                        		{
                        			return prevDepth;
                        		}
                        	}
							return foundDepth;
                        	//return avgDepth;  
                        } 
                        else
                        {
                            return prevDepth;
                        }
                    }
                }
                return foundDepth;
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
				if(_useDepthCoordinateSystem == 0 && extrinsicsPosition == 1)
				{
					xyzDepth = applyExtrinsicsInverse(XYZ, colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
							_ColorExtrinsicsT * _scaleExtrinsicsTranslation);
				}
				float2 distortedDepthCoordinates = distort(xyzDepth, _DepthDistortionRadial, _DepthDistortionTangential, distortionPrism);
				if(_useDepthCoordinateSystem == 0 && extrinsicsPosition == 2)
				{
					xyzDepth = applyExtrinsicsInverse(float3(distortedDepthCoordinates.x, distortedDepthCoordinates.y, 1.0), 
																colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
																_ColorExtrinsicsT * _scaleExtrinsicsTranslation);
					distortedDepthCoordinates = float2(xyzDepth.x / xyzDepth.z, xyzDepth.y / xyzDepth.z); 
				}
				float2 uvDepth = applyIntrinsics(distortedDepthCoordinates,  _depthIntrinsicsFxy, _depthIntrinsicsCxy);
				return uvDepth;
			}

			float4 getVposFromDepth(float2 XY, uint depth)
			{
				float4 vPos;
				int scaleByDepth = 1;				
                float fDepth = (float) depth * 0.001 * _scaleDepth ;
                if(scaleByDepth) {
                    vPos = float4(XY.x * fDepth,  XY.y * fDepth, fDepth* _DepthFactor, 1);                              
                } else {
                    vPos = float4(XY.x, XY.y, fDepth * _DepthFactor, 1);
                }                
                return vPos;
			}

			
			float getHistoricEdgeLen(float2 dIndex1, float2 dIndex2, float2 pos1, float2 pos2){
                float historicEdgeLen = _MaxEdgeLen;
                for (int timestep = _historyFrames - 1; timestep >= 0; timestep--)
                {
                    float v1Z;
                    float v2Z;
                	if(_interpolateHistoryDepth)
                	{
                		v1Z = interpolateHistoryDepthAt(dIndex1, timestep) * 0.001;
						v2Z = interpolateHistoryDepthAt(dIndex2, timestep) * 0.001;
                	} else
                	{
                		uint dx = (uint)(dIndex1.x); 
						uint dy = (uint)(dIndex1.y);
						uint idx = (dx + dy * _DepthTexRes.x);
                		v1Z = readHistoryDepthAtIndex(idx, timestep);
                		dx = (uint)(dIndex2.x); 
						dy = (uint)(dIndex2.y);
						idx = (dx + dy * _DepthTexRes.x);
                		v2Z = readHistoryDepthAtIndex(idx, timestep);
                	}
                	
                    float3 v1 = float3(pos1.x, pos1.y, 1) * v1Z;
                    float3 v2 = float3(pos2.x, pos2.y, 1) * v2Z;


                    float edgeLen = distance(v1, v2);
                    if (edgeLen < _MaxEdgeLen)
                    {
                      historicEdgeLen = edgeLen;
                      break;
                    }
                }

                return historicEdgeLen;
             }
			
			
			float computeAlpha(uint index, float4 pos)
			{
				int x = index % _DepthTexRes.x;
				int y = (index - x) / _DepthTexRes.x;
                float2 centerPos = getPosFromIndex(x, y);
				float2 centerDepthIdx = getDepthIdxForVertexPos(centerPos);
				
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
							uint depth = tempFilterDepth(neighborDepthIdx);
							float4 neighborVPos = getVposFromDepth(neighborPos, depth);
							float edgeLen = distance(pos.xyz, neighborVPos.xyz);
							if(_stableTriangles && (edgeLen < (_MaxEdgeLen * 1.0) ||
								((edgeLen < _MaxEdgeLen * 1.5) && 
									getHistoricEdgeLen(centerDepthIdx, neighborDepthIdx, centerPos, neighborPos)
									< _MaxEdgeLen)) ||
									_stableTriangles == 0 && (edgeLen < (_MaxEdgeLen * 1.0))) // use max edge len although it is vertex distance
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
				if(validNeighborsCount > 7) // inside mesh
				{
					return 1;
				}
				else{ // edge vertex
					return 0;
				} 
			}
			
				
			Vertex moveEdge(uint index, float4 pos)
             {
                int x = index % (int)_DepthTexRes.x;
                int y = (index - x) / (int)_DepthTexRes.x;
				float2 centerPos = getPosFromIndex(x, y);
				float2 centerDepthIdx = getDepthIdxForVertexPos(centerPos);
                
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
							uint depth = tempFilterDepth(neighborDepthIdx);
							float4 neighborVPos = getVposFromDepth(neighborPos, depth);
							neighborVpos[neighborIndex] = neighborVPos;

							float edgeLen = distance(pos.xyz, neighborVPos.xyz);
							if(_stableTriangles && (edgeLen < (_MaxEdgeLen * 1.0) ||
								((edgeLen < _MaxEdgeLen * 1.5) && 
									getHistoricEdgeLen(centerDepthIdx, neighborDepthIdx, centerPos, neighborPos)
									< _MaxEdgeLen)) ||
								_stableTriangles == 0 && (edgeLen < (_MaxEdgeLen * 1.0))) // use max edge len although it is vertex distance
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
									//vert.vpos = lerp(pos, neighborVpos[j], 0.5);  //old
									vert.vpos = lerp(neighborVpos[i], neighborVpos[k], 0.5); 
									
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
									if(i==0 || i==4)
										vert.vpos = lerp(neighborVpos[j], neighborVpos[l], 0.5);
									else if(i==1 || i==5)
										vert.vpos = lerp(neighborVpos[i], neighborVpos[k], 0.5);
									else if(i==2 || i==3 || i==6 || i==7)
										vert.vpos = lerp(neighborVpos[i], neighborVpos[l], 0.5);
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
						for(uint i=0; i<8; i++)
						{
							int j = (i+1) % 8;
							int k = (i+2) % 8;
							int l = (i+3) % 8;
							int m = (i+4) % 8;
							if(validNeighbors[i] && validNeighbors[j] && validNeighbors[k] && validNeighbors[l] && validNeighbors[m])
							{
								Vertex vert;
								vert.color = float3(0,0,0);
								if(_moveEdges)
								{
									if(i==0 || i==4)
										vert.vpos = lerp(neighborVpos[j], neighborVpos[l], 0.5);
									else if (i==1 || i==2 || i==3 || i==5 || i==6 || i==7)
										vert.vpos = pos;
								} else
								{
									vert.vpos = pos;
								}
								return vert;
							}
						}
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
									if(i==0 || i==3|| i==4 || i==7)
										vert.vpos = pos;
									else if(i==1 || i==2 || i==5 || i==6)
										vert.vpos = lerp(neighborVpos[i], neighborVpos[n], 0.5);
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
							int j = (i+1) % 8;
							int k = (i+2) % 8;
							int l = (i+3) % 8;
							int m = (i+4) % 8;
							int n = (i+5) % 8;
							int o = (i+6) % 8;
							if(validNeighbors[i] && validNeighbors[j] && validNeighbors[k] &&
								validNeighbors[l] && validNeighbors[m] && validNeighbors[n]&& validNeighbors[o])
							{
								Vertex vert;
								vert.color = float3(1,1,1);
								if(_moveEdges)
								{
									if(i==0 || i==4)
										vert.vpos = lerp(neighborVpos[j], neighborVpos[o], 0.5);
										
									else if(i==1|| i==3 || i==5 || i==7)
										vert.vpos = lerp(neighborVpos[i], neighborVpos[o], 0.5);
									else if(i==2 || i==6)
										vert.vpos = lerp(neighborVpos[i], neighborVpos[n], 0.5);
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
             
             float getHistoricMaxEdgeLen(float2 dIndex1, float2 dIndex2, float2 dIndex3, float2 pos1, float2 pos2, float2 pos3){

                float historicMaxEdgeLen = _MaxEdgeLen;
                for (int timestep = _historyFrames - 1; timestep >= 0; timestep--)
                {
                    float v1Z;
                    float v2Z;
                    float v3Z;
                	if(_interpolateHistoryDepth)
                	{
                		v1Z = interpolateHistoryDepthAt(dIndex1, timestep) * 0.001;
						v2Z = interpolateHistoryDepthAt(dIndex2, timestep) * 0.001;
						v3Z = interpolateHistoryDepthAt(dIndex3, timestep) * 0.001;
                	} else
                	{
                		uint dx = (uint)(dIndex1.x); 
						uint dy = (uint)(dIndex1.y);
						uint idx = (dx + dy * _DepthTexRes.x);
                		v1Z = readHistoryDepthAtIndex(idx, timestep);
                		dx = (uint)(dIndex2.x); 
						dy = (uint)(dIndex2.y);
						idx = (dx + dy * _DepthTexRes.x);
                		v2Z = readHistoryDepthAtIndex(idx, timestep);
                		dx = (uint)(dIndex3.x); 
						dy = (uint)(dIndex3.y);
						idx = (dx + dy * _DepthTexRes.x);
                		v3Z = readHistoryDepthAtIndex(idx, timestep);
                	}
                	
                    float3 v1 = float3(pos1.x, pos1.y, 1) * v1Z;
                    float3 v2 = float3(pos2.x, pos2.y, 1) * v2Z;
                    float3 v3 = float3(pos3.x, pos3.y, 1) * v3Z;


                    float maxEdgeLen = getMaxEdgeLen(v1, v2, v3);
                    if (maxEdgeLen < _MaxEdgeLen)
                    {
                      historicMaxEdgeLen = maxEdgeLen;
                      break;
                    }
                }

                return historicMaxEdgeLen;
             }


			v2f vert(appdata_base v)
			{
				v2f o;
                uint dx = (uint)(v.texcoord.x * _DepthTexRes.x);
				uint dy = (uint)(v.texcoord.y * _DepthTexRes.y); 
                uint idx = (dx + dy * _DepthTexRes.x);
				float3 XYZ = float3(getPosFromIndex(dx,dy), 1);
                float4 vPos;
                uint depth;
                float fDepth;
                
                // Image Processing
                if(dx & 1 == 1){
                    uint idxH = idx;
                    uint idxL = (dx + dy * _DepthTexRes.x) - 1;
                    uint depthL = readCurrentDepthAtIndex(idxL);
                    uint depthH = readCurrentDepthAtIndex(idxH);
					uint filteredDepthL = tempFilterDepth(float2(dx-1,dy));
                	uint filteredDepthH = tempFilterDepth(float2(dx,dy));                    
                    setFilteredDepth(idxL, idxH, filteredDepthL, filteredDepthH); 
                    setMeasuredDepth(idxL, idxH, depthL, depthH);
                }
                
                
                
                if(_isColorRegistered)
                {				
                    dx = (uint)(v.texcoord.x * _DepthTexRes.x);
                    dy = (uint)(_DepthTexRes.y - v.texcoord.y * _DepthTexRes.y); // flip depth
                    idx = dx + dy * _DepthTexRes.x;
                    depth = readCurrentDepthAtIndex(idx);
                    fDepth = (float) depth * 0.001;
                    o.uv_ColorTex = float2(v.texcoord.x, 1 - v.texcoord.y); // flip color
                    vPos = float4(v.texcoord.x, v.texcoord.y, fDepth, 1);
                }
                else
                {
                    float4 distortionPrism = float4(0,0,0,0); // should be zero according to https://www.mathworks.com/help/symbolic/developing-an-algorithm-for-undistorting-an-image.html
                    float3 colorExtrinsicsR0 = float3(_ColorExtrinsicsR[0], _ColorExtrinsicsR[1], _ColorExtrinsicsR[2]);
                    float3 colorExtrinsicsR1 = float3(_ColorExtrinsicsR[3], _ColorExtrinsicsR[4], _ColorExtrinsicsR[5]);
                    float3 colorExtrinsicsR2 = float3(_ColorExtrinsicsR[6], _ColorExtrinsicsR[7], _ColorExtrinsicsR[8]);
                                      
                    if (_meetAtXYZ)
                    {
                        // Vertex Positions                       
                    	o.xy = XYZ.xy;

                    	if(_DepthFactor> 0.01)
                    	{
                    		// Step 1: Depth
                    		o.dIdx = getDepthIdxForVertexPos(XYZ.xy);
                    		depth = tempFilterDepth(o.dIdx);
                    		vPos = getVposFromDepth(XYZ.xy, depth);
                    		Vertex vertex = moveEdge(idx, vPos);
                    		vPos = vertex.vpos;
                    		o.color = vertex.color;
                    		if(_colorAlpha)
                    		{
                    			o.alpha = computeAlpha(idx, vPos);
                    		}
                    	} else {
							vPos = float4(XYZ.x, XYZ.y, 1, 1);                    		
                    	}
                    	if(_useScaledVerticesForColor)
                    	{
                    		XYZ = vPos.xyz;// Doesn't make any difference. it is just a scaling.
                    	}
                    	if(_flipScene)
                    	{
                    		vPos = float4(vPos.x, vPos.y * -1, vPos.z, vPos.w);
                    	}
                    	
                        // Step 2: Color 
                        float3 colorxyz;
                        if(_extrinsicsPosition == 1 && _useDepthCoordinateSystem)
                        {
							
                        	colorxyz = applyExtrinsics(XYZ, colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                                    _ColorExtrinsicsT * _scaleExtrinsicsTranslation);
                        }
                        else
                        {
                            colorxyz = XYZ;
                        }
                        
                        float2 uvColor;
                        if(_extrinsicsPosition == 2 && _useDepthCoordinateSystem){
                         float2 colorxyDPrime = distort(colorxyz, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);

                        	float3 corrected = applyExtrinsics(float3(colorxyDPrime.x, colorxyDPrime.y, 1.0), colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                                _ColorExtrinsicsT * _scaleExtrinsicsTranslation);
                            float2 correctedxy = float2(corrected.x / corrected.z, corrected.y / corrected.z); 
                            uvColor = applyIntrinsics(correctedxy ,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                        }else{
                           
                            float2 colorxyDPrime = distort(colorxyz, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);
                            uvColor = applyIntrinsics(colorxyDPrime ,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                        }
                        o.uv_ColorTex = float2(uvColor.x / _ColorTexRes.x, uvColor.y / _ColorTexRes.y);
                    }
                    else
                    {
						// 1. Depth UV
                        float3 depthUV =  float3(v.texcoord.x * _DepthTexRes.x, v.texcoord.y * _DepthTexRes.y , 1); // avoid dx, dy rounding problem
						// 2. Inverse Intrinsics Depth
                    	float3 depthxyz = applyIntrinsicsInv(depthUV.xy, _depthIntrinsicsFxy, _depthIntrinsicsCxy);    
						// 3. Undistort Depth
                    	depthxyz = kinect2_undistort(depthxyz, _DepthDistortionRadial); // TODO undistort not working.
                        float3 XYZ = depthxyz;
                        // 4. Distort Color
                        float2 colorxyDPrime = distort(XYZ, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);
						// 5. Extrinsics Color
                        float3 colorxyz = applyExtrinsics(float3(colorxyDPrime.x, colorxyDPrime.y, 1.0), colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                                _ColorExtrinsicsT * _scaleExtrinsicsTranslation);
                        float2 colorxy = float2(colorxyz.x / colorxyz.z, colorxyz.y / colorxyz.z); 
						// 6. Color Intrinsics
                        float2 uvColor = applyIntrinsics(colorxy,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);

                    	depth = readCurrentDepthAtIndex(idx); // TODO add temporal smoothing
                        vPos = getVposFromDepth(depthUV.xy, depth);
                        o.uv_ColorTex = float2(uvColor.x / _ColorTexRes.x, uvColor.y / _ColorTexRes.y); // TODO flip
                    }
                }
				
                float4 wPos = mul(_LocalToWorldTransform, vPos);
				bool mask = vPos.x >= _PosMin.x && vPos.x <= _PosMax.x && vPos.y >= _PosMin.y && vPos.y <= _PosMax.y && vPos.z >= _PosMin.z && vPos.z <= _PosMax.z;
				o.wPos = wPos.xyz;
				o.pos = UnityObjectToClipPos(vPos);
				o.vIdx = idx;
				o.mask = mask;
				return o;
			}


			fixed4 frag(v2f i) : SV_Target
			{
				   
				fixed4 rgb = fixed4(0,0,0,0);
				if(_showHologram){
				  rgb = tex2D(_ColorTex, float2(i.uv_ColorTex.x, i.uv_ColorTex.y));
					if(_colorAlpha)
					{
						rgb.a = i.alpha;
					}else
					{
						rgb.a = 1;
					}
				}
				
				rgb.r += tex2D(_AnnotationTex, float2(i.uv_ColorTex.x, i.uv_ColorTex.y)).r;
				if(rgb.r > 0.3 && _showHologram == 0) {
				    rgb.a = 1; 
				}
				rgb.g += tex2D(_PointerTex, i.uv_ColorTex).r;
				if(_colorEdges)
				{
					rgb = float4(i.color, 1);
				}
				return rgb;
			}




			v2f copyV2fSurf(v2f p0, float dx, float dy)
			{
				v2f p;

				p = p0;
				p.pos = p0.pos + float4(dx, dy, 0, 0);

				return p;
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
                return PointInOrOn(px, p0, p1, p2) *
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
					if (p0.mask)
					{
						float dxy = min(_ColorTex_TexelSize.x, _ColorTex_TexelSize.y) *_CoarseFactor;

						p1 = copyV2fSurf(p0, dxy, 0);
						p2 = copyV2fSurf(p0, 0, dxy);
						v2f p3 = copyV2fSurf(p0, dxy, dxy);

						outStream.Append(p0);
						outStream.Append(p1);
						outStream.Append(p2);
						outStream.RestartStrip();

						outStream.Append(p1);
						outStream.Append(p3);
						outStream.Append(p2);
						outStream.RestartStrip(); 
					}
				}
				else
				{
				    float historicMaxEdgeLen = _MaxEdgeLen; 
				    float maxEdgeLen = getMaxEdgeLen(p0.wPos, p1.wPos, p2.wPos);
				     if (_stableTriangles && maxEdgeLen >= _MaxEdgeLen)
				     {
				        historicMaxEdgeLen = getHistoricMaxEdgeLen(p0.dIdx, p1.dIdx, p2.dIdx,
				        	p0.xy, p1.xy, p2.xy);
				     }
					if (_stableTriangles && p0.mask && p1.mask && p2.mask &&
						(maxEdgeLen < _MaxEdgeLen || ((maxEdgeLen < _MaxEdgeLen*1.5) && historicMaxEdgeLen < _MaxEdgeLen))
						||
						_stableTriangles==0 && p0.mask && p1.mask && p2.mask &&
						(maxEdgeLen < _MaxEdgeLen)
						)
					{    
                        
                        // Intersection check:
                        Ray pointerRay;
                        pointerRay.Origin = _WorldSpaceCameraPos; //_WorldSpaceCameraPos ... Main Camera (0,0,0)
                        pointerRay.Direction = normalize(_PointerPosition - _WorldSpaceCameraPos); // TODO do this only once!!!
						float3 offset = float3(0.0, 0.0, 0.0);
                        if (IntersectTriangle(pointerRay, p0.wPos , p1.wPos , p2.wPos))
                        {
                            float3 intersectionPosition = IntersectPlane(pointerRay, p0.wPos, p1.wPos, p2.wPos);
                            _PointerIntersectionWorld[0] = intersectionPosition;
                        	
                            if(distance(_PointerIntersectionWorld[0], _PointerPosition) < 2.0) { // to save computing power, only do color interpolation when in 2 m range 
                                int interpolateIntersection = 1;
                                int scaleByDepth = 1;
                                if(interpolateIntersection){
                                    float4 distortionPrism = float4(0,0,0,0);
                                    float3 colorExtrinsicsR0 = float3(_ColorExtrinsicsR[0], _ColorExtrinsicsR[1], _ColorExtrinsicsR[2]);
                                    float3 colorExtrinsicsR1 = float3(_ColorExtrinsicsR[3], _ColorExtrinsicsR[4], _ColorExtrinsicsR[5]);
                                    float3 colorExtrinsicsR2 = float3(_ColorExtrinsicsR[6], _ColorExtrinsicsR[7], _ColorExtrinsicsR[8]);
                                    
                                    float4 vIntersectionPos = mul(_WorldToLocalTransform, float4(intersectionPosition, 1));
                                                 
                                    // Step 2: Color 
                                    float3 colorxyz;
                                    if (scaleByDepth){
										if(_DepthFactor> 0.01)
                                    	colorxyz = float3(vIntersectionPos.x, vIntersectionPos.y *-1, vIntersectionPos.z / _DepthFactor);
											else
                                    	colorxyz = float3(vIntersectionPos.x, vIntersectionPos.y *-1, vIntersectionPos.z);
                                    }
                                    else{
                                        colorxyz = float3(intersectionPosition.x, 
                                                       1.0 - intersectionPosition.y - 1.0, 1.0);
                                    }
                                    float2 uvColor;
                                    float2 colorxyDPrime = distort(colorxyz, _ColorDistortionRadial, _ColorDistortionTangential, distortionPrism);
                                    float3 corrected = applyExtrinsics(float3(colorxyDPrime.x, colorxyDPrime.y, 1.0), colorExtrinsicsR0, colorExtrinsicsR1, colorExtrinsicsR2, 
                                        _ColorExtrinsicsT*_scaleExtrinsicsTranslation);
                                    float2 correctedxy = float2(corrected.x / corrected.z, corrected.y / corrected.z); 
                                    uvColor = applyIntrinsics(correctedxy ,  _colorIntrinsicsFxy, _colorIntrinsicsCxy);
                                    _PointerIntersectionPx[0] = uvColor.x;
                                    _PointerIntersectionPx[1] = uvColor.y;
                                }
                                else
                                {
                                    _PointerIntersectionPx[0] = p0.uv_ColorTex.x * _ColorTexRes.x;
                                    _PointerIntersectionPx[1] = p0.uv_ColorTex.y * _ColorTexRes.y;
                                }
                            }                 
                        }
                        else
                        {
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