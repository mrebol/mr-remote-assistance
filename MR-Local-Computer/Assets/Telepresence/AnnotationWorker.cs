﻿using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Threading;
 using System.Threading.Tasks;
 //using com.rfilkov.components;
 using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Telepresence
{
    public class AnnotationWorker : MonoBehaviour 
    {
        Color32 colorAnnotation = new Color32(255,0,0,255);
        Color32 colorBG = new Color32(0,0,0,255);  // ABGR, ABRG
        private bool _clearAnnotation = true;
        private Pointer _pointer;
        private bool _drawingOn;
        private MeshRender _mesh;
        private List<List<Vector2Int>> _annotations;
        private List<Vector2Int> _currentPolygon;
        private float _lastAnnotationTime;
        private byte[] localTexture;
        public Pointer pointer;

        private void Start()
        {
            _pointer = GetComponent<Pointer>();
            _mesh = GameObject.Find("MeshRenderer").GetComponent<MeshRender>();
            _annotations = new List<List<Vector2Int>>();
            localTexture = new byte[CommonParameters.ColorWidth * CommonParameters.ColorHeight];
    }

        void Update()
        {
            if (Input.GetKeyDown("x"))
            {
                if (!_drawingOn)
                {
                    print("Drawing on.");
                    _currentPolygon = new List<Vector2Int>();
                    _drawingOn = true;
                }
                else
                {
                    print("Drawing off.");
                    _annotations.Add(_currentPolygon);
                    _drawingOn = false;
                }
            }
            

            if (_clearAnnotation)
            {
                clearAnnoations();
                _clearAnnotation = false;
            }

            if (Vector3.Distance(_mesh.pointerIntersection, _mesh.PointerPosition) > 0.05)
            {
                if (pointer.pointerOnHologram)
                {
                    _annotations.Add(_currentPolygon);
                }
                pointer.pointerOnHologram = false;
                return;
            }
            else
            {
                if (!pointer.pointerOnHologram)
                {
                    _currentPolygon = new List<Vector2Int>();
                }
   }
            UpdateAnnoations();
            

        }

        public void UpdateAnnoations()
        {
            if (!_drawingOn || !pointer.pointerOnHologram)
                return;

            if (Time.time < (_lastAnnotationTime + (1.0 / 10))) // check last annotation time
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
                AnnotateLine(_currentPolygon.Last(), currentPixel);
                _currentPolygon.Add(currentPixel);
                _lastAnnotationTime = Time.time;
            }
            else // _currentPolygon.Count >= 2
            {

                AnnotateLine(_currentPolygon.Last(), currentPixel);

                _currentPolygon.Add(currentPixel);
                _lastAnnotationTime = Time.time;
                
            }
        }

        public void clearAnnoations()
        {
            Color32[] newColors;
            newColors = new Color32[CommonParameters.ColorWidth * CommonParameters.ColorHeight];
            for (int i=0; i<newColors.Length; i++)
            {
                newColors[i] = colorBG;
            }
            MeshRender.AnnotationTexture2D.SetPixels32(newColors);
            MeshRender.AnnotationTexture2D.Apply();
        }

        public void annotatePoint(ushort x, ushort y)
        {
            MeshRender.AnnotationArray[x + y * CommonParameters.ColorWidth] = 255;
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
        
       


    }
}