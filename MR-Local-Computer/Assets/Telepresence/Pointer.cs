using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telepresence;
using UnityEngine;

public class Pointer : MonoBehaviour
{
    [Range(0.04f, 0.4f)]
    public float speed = 0.1f;
    public bool pointerActive;
    public MeshRender _mesh;
    public bool pointerOnHologram = false;
    public bool isDrawPointer;
    public KinectReader kinectReader;
    public Camera mainCamera;
    private bool keyZdown;
    private bool keyXdown;
    public Vector3 previousPosition;
    Collider m_Collider, m_Collider2;
    private Vector3 cameraPos;
    private LineRenderer lr;
    private byte[] blackArray;

    void Start()
    {
        blackArray = new byte[CommonParameters.ColorWidth * CommonParameters.ColorHeight];

        //Check that the first GameObject exists in the Inspector and fetch the Collider
        m_Collider = this.gameObject.GetComponent<Collider>();

        //Check that the second GameObject exists in the Inspector and fetch the Collider
        GameObject mesh = GameObject.Find("OutgoingMeshGenerator");
        if (mesh != null)
            m_Collider2 = mesh.GetComponent<Collider>();
        
        
        cameraPos = GameObject.Find("Main Camera").transform.position;

        lr = GetComponent<LineRenderer>();

        
        
    }

    // Update is called once per frame
    void Update()
    {

        if (pointerActive)
        {
            var x = 0f;
            var y = 0f;
            if(Input.GetKey("a"))
            {
                x = -speed * Time.deltaTime;
            }
            if(Input.GetKey("d"))
            {
                x = speed * Time.deltaTime;
            }
            if(Input.GetKey("e"))
            {
                y = speed * Time.deltaTime;
            }
            if(Input.GetKey("q"))
            {
                y = -speed * Time.deltaTime;
            }
            if(Input.GetKey("g"))
            {
                print("speed +");
                if (speed < 0.4)
                {
                    speed *= 1.03f;
                }
            }
            if(Input.GetKey("f"))
            {
                print("speed -");
                if (speed > 0.04f)
                {
                    speed *= 0.97f;
                }
            }
            // Keyboard navigation
            var z = 0.0f;
            if (Input.GetKeyDown("w"))
                keyZdown = true;
            if (Input.GetKeyUp("w"))
                keyZdown = false;
            if (Input.GetKeyDown("s"))
                keyXdown = true;
            if (Input.GetKeyUp("s"))
                keyXdown = false;


            if (keyXdown)
                z -= Time.deltaTime * speed;
            if (keyZdown)
                z += Time.deltaTime * speed;
            transform.Translate(x, y, z);

            if (Input.GetKeyUp("r"))
                transform.position = mainCamera.transform.position + mainCamera.transform.forward * 0.5f;

            if (Input.GetKeyUp("z") && _mesh.annotationPixel.x != -1 && _mesh.annotationPixel.y != -1)
            {
                transform.position = _mesh.pointerIntersection;
            }


            if (previousPosition != transform.position)
            {
                _mesh.PointerPosition = transform.position;
                if (CommonParameters.numberOfCameras > 0)
                {
                    switch (kinectReader.ballManager.currentBall)
                    {
                        case 0: break;
                        case 1:
                            kinectReader.alignmentBalls.cameraBalls[kinectReader.selectKinect].RedPosition =
                                transform.position;
                            break;
                        case 2:
                            kinectReader.alignmentBalls.cameraBalls[kinectReader.selectKinect].GreenPosition =
                                transform.position;
                            break;
                        case 3:
                            kinectReader.alignmentBalls.cameraBalls[kinectReader.selectKinect].BluePosition =
                                transform.position;
                            break;
                        case 4:
                            kinectReader.alignmentBalls.cameraBalls[kinectReader.selectKinect].YellowPosition =
                                transform.position;
                            break;
                    }
                }

                previousPosition = transform.position;
            }

            UpdatePointer();
            

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
        
        public void UpdatePointer()
        {
            if (_mesh.annotationPixel.x == -1 || _mesh.annotationPixel.y == -1)
            {
                _mesh.PointerArray = blackArray;
                pointerOnHologram = false;
                return;
            }
            if (Vector3.Distance(_mesh.pointerIntersection, _mesh.PointerPosition) > 0.05 && isDrawPointer)
            {
            }
            else
            {
                pointerOnHologram = true;
                drawPointer(Convert.ToUInt16(_mesh.annotationPixel.x), Convert.ToUInt16(_mesh.annotationPixel.y));
            }

        }
}
