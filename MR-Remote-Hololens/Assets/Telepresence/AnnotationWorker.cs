﻿using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Threading;
 using System.Threading.Tasks;
 using Microsoft.MixedReality.WebRTC;
 using Microsoft.MixedReality.WebRTC.Unity;
 using Telepresence;
 //using com.rfilkov.components;
 using UnityEngine;
using UnityEngine.PlayerLoop;
 using PeerConnection = Microsoft.MixedReality.WebRTC.Unity.PeerConnection;

 //namespace Telepresence
//{
    public class AnnotationWorker : MonoBehaviour
    {
        Color32 colorAnnotation = new Color32(255,0,0,255);
        Color32 colorBG = new Color32(0,0,0,255);  // ABGR, ABRG
        private bool _clearAnnotation = true;
        private bool _drawingOn;
        private IncomingMeshRenderer _mesh;
        private List<List<Vector2Int>> _annotations;
        private List<Vector2Int> _currentPolygon;
        private float _lastAnnotationTime;
        private bool _isMeshEnabled;
        private List<Vector2Int> currentPoints;
        private byte[] blackArray;
        public PeerConnection PeerConnection;
        public bool pointerOnHologram = false;
        
        private void Start()
        {
            
            if (GameObject.Find("IncomingMeshRenderer") != null)
            {
                _mesh = GameObject.Find("IncomingMeshRenderer").GetComponent<IncomingMeshRenderer>();
                _isMeshEnabled = true;
            }
            else
                _isMeshEnabled = false;
            _annotations = new List<List<Vector2Int>>();
            currentPoints = new List<Vector2Int>();
            blackArray = new byte[CommonParameters.ColorWidth * CommonParameters.ColorHeight];

            PeerConnection.AnnotationData.InitAnnotationFrame((uint)CommonParameters.ColorWidth, (uint)CommonParameters.ColorHeight);
        }

        void Update()
        {
            if (!_isMeshEnabled)
            {
                return; 
            }
            
            if (Input.GetKeyDown("x"))
            {
                toggleDrawing();
            }
            

            if (_clearAnnotation)
            {
                
                clearAnnoations();
                _clearAnnotation = false;
            }

            _mesh.PointerPosition = HandRotation.pointer;
            
            UpdatePointer();
            UpdateAnnoations();
            

        }

        public void toggleDrawing()
        {
            if (!_drawingOn)
            {
                _currentPolygon = new List<Vector2Int>();
                _drawingOn = true;
                print("drawing on");
            }
            else
            {
                _annotations.Add(_currentPolygon);
                _drawingOn = false;
                print("drawing off");
            }
        }

        public void UpdateAnnoations()
        {
            if (!_drawingOn || !pointerOnHologram)
                return;

            if (Time.time < (_lastAnnotationTime + (1.0 / 10.0))) // check last annotation time
                return;

            if (_mesh.annotationPixel.x == -1 || _mesh.annotationPixel.y == -1)  //TODO check if pointer leaves the Hologram, end polygon then
                    return;
            
            Vector2Int currentPixel = new Vector2Int(Convert.ToUInt16(_mesh.annotationPixel.x), 
                Convert.ToUInt16(_mesh.annotationPixel.y));

            
            if (_currentPolygon.Count == 0)
            {
                _currentPolygon.Add(currentPixel);
                _lastAnnotationTime = Time.time;
                return;
            }


            if (Vector2Int.Distance(currentPixel, _currentPolygon.Last()) < CommonParameters.ColorHeight / 200.0)
                return;
            
            if (_currentPolygon.Count == 1)
            {
                AnnotateLine(_currentPolygon.Last(), new Vector2Int(currentPixel.x, currentPixel.y));
                _currentPolygon.Add(new Vector2Int(currentPixel.x, currentPixel.y));
                _lastAnnotationTime = Time.time;
            }
            else // _currentPolygon.Count >= 2
            {
                AnnotateLine(_currentPolygon.Last(), new Vector2Int(currentPixel.x, currentPixel.y));
                _currentPolygon.Add(new Vector2Int(currentPixel.x, currentPixel.y));
                _lastAnnotationTime = Time.time;
                
            }
            PeerConnection.AnnotationData.addAnnotationData(IncomingMeshRenderer.AnnotationArray,0,this.transform);
        }

        public void clearAnnoations()
        {
            IncomingMeshRenderer.AnnotationArray = new byte[CommonParameters.ColorWidth * CommonParameters.ColorHeight];
            PeerConnection.AnnotationData.addAnnotationData(IncomingMeshRenderer.AnnotationArray,0,this.transform);
        }

        public void annotatePoint(ushort x, ushort y)
        {
            IncomingMeshRenderer.AnnotationArray[x+ y*CommonParameters.ColorWidth] = 255;
        }

        
        void AnnotateLine(Vector2Int p1, Vector2Int p2)
        {
            Task.Run(() =>  // Tasks runs in main thread, but async/non-blocking
            {
                Vector2 t = p1;
                float frac = 1.0f / Mathf.Sqrt(Mathf.Pow(p2.x - p1.x, 2) + Mathf.Pow(p2.y - p1.y, 2));
                float ctr = 0;

                while ((int) t.x != p2.x || (int) t.y != p2.y)
                {
                    t = Vector2.Lerp(p1, p2, ctr);
                    ctr += frac;
                    annotatePoint((ushort) t.x, (ushort) t.y);
                }
            });
        }
        
        public void UpdatePointer()
        {

            if (_mesh.annotationPixel.x == -1 || _mesh.annotationPixel.y == -1)
            {
                _mesh.PointerArray = blackArray;
                pointerOnHologram = false;
                return;
            }
            if (Vector3.Distance(_mesh.pointerIntersection, _mesh.PointerPosition) > 0.05)
            {
                _mesh.PointerArray = blackArray;
                if (pointerOnHologram)
                {
                    _annotations.Add(_currentPolygon);
                    if (PeerConnection.myDataChannelOut != null &&
                        PeerConnection.myDataChannelOut.State == DataChannel.ChannelState.Open)
                    {
                        PeerConnection.SendMessage(-1, -1);
                    }
                }
                pointerOnHologram = false;
                return;
            }
            else
            {
                if (!pointerOnHologram)
                {
                    _currentPolygon = new List<Vector2Int>();
                }

                pointerOnHologram = true;
                drawPointer(Convert.ToUInt16(_mesh.annotationPixel.x),
                    Convert.ToUInt16(_mesh.annotationPixel.y));
                if (PeerConnection.myDataChannelOut != null &&
                    PeerConnection.myDataChannelOut.State == DataChannel.ChannelState.Open)
                {
                    PeerConnection.SendMessage(_mesh.annotationPixel.x, _mesh.annotationPixel.y);
                }
            }
            
        }

        public void drawPointer(ushort x, ushort y)
        {
            Task.Run(() =>  // Tasks runs in main thread, but async/non-blocking
            {
                byte[] b = new byte[CommonParameters.ColorHeight * CommonParameters.ColorWidth];
                int radius = 4;
                float rSquared = radius * radius;

                for (int u = x - radius; u < x + radius + 1; u++)
                {
                    for (int v = y - radius; v < y + radius + 1; v++)
                    {
                        if ((x - u) * (x - u) + (y - v) * (y - v) < rSquared)
                        {
                            b[u + v * CommonParameters.ColorWidth] = 255;
                        }
                    }
                }

                _mesh.PointerArray = b;

            });
        }
        
    }
