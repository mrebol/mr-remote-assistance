import numpy as np
import sys
from scipy.spatial.transform import Rotation as Rot
#import UnityEngine as ue

# Input: expects 3xN matrix of points
# Returns R,t
# R = 3x3 rotation matrix
# t = 3x1 column vector

#path = 'D:\\repos\\telehealth\\system-data\\'
path = 'C:\\data\\repos\\telehealth\\system-data\\'

def rigid_transform_3D(A, B):
    #print("B=", B)
    assert A.shape == B.shape

    num_rows, num_cols = A.shape
    if num_rows != 3:
        raise Exception("matrix A is not 3xN, it is {num_rows}x{num_cols}")

    num_rows, num_cols = B.shape
    if num_rows != 3:
        raise Exception("matrix B is not 3xN, it is {num_rows}x{num_cols}")

    # find mean column wise
    centroid_A = np.mean(A, axis=1)
    centroid_B = np.mean(B, axis=1)

    # ensure centroids are 3x1
    centroid_A = centroid_A.reshape(-1, 1)
    centroid_B = centroid_B.reshape(-1, 1)

    # subtract mean
    Am = A - centroid_A
    Bm = B - centroid_B

    #H = Am @ np.transpose(Bm)
    H = np.matmul(Am, np.transpose(Bm))
    #print("H:", H)
    # sanity check
    #if linalg.matrix_rank(H) < 3:
    #    raise ValueError("rank of H = {}, expecting 3".format(linalg.matrix_rank(H)))

    # find rotation
    U, S, Vt = np.linalg.svd(H)
    print("U ", U)
    print("Vt ", Vt)
    #R = Vt.T @ U.T
    R = np.matmul(Vt.T, U.T)

    # special reflection case
    if np.linalg.det(R) < 0:
        print("det(R) < R, reflection detected!, correcting for it ...")
        Vt[2,:] *= -1
        #R = Vt.T @ U.T
        R = np.matmul(Vt.T, U.T)

    #t = -R @ centroid_A + centroid_B
    t = np.matmul(-R, centroid_A) + centroid_B

    return R, t


#objects = ue.Object.FindObjectsOfType(ue.GameObject)
#for go in objects:
#    ue.Debug.Log(go.name)
#pointRed = ue.GameObject.Find("PointRed")
#ue.Debug.Log(pointRed)
#ue.Debug.Log(pointRed.transform)
#ue.Debug.Log(pointRed.transform.position)
#ue.Debug.Log(pointRed.transform.position.x)
#print(pointRed.transform.position)
#redX = pointRed.transform.position.x
#redX += 0.1
#ue.GameObject.Find("PointRed").transform.position.x = redX

for i in range(0, len(sys.argv)):
    print(sys.argv[i])

#if pointRed is not None and pointRed.transform is not None and pointRed.transform.position is not None:
#    ue.Debug.Log(pointRed.transform.position.x)
#else:
#    ue.Debug.Log("its null")

# Test with random data

# Random rotation and translation
R = np.random.rand(3,3)
t = np.random.rand(3,1)

# make R a proper rotation matrix, force orthonormal
U, S, Vt = np.linalg.svd(R)
#R = U@Vt
R = np.matmul(U,Vt)

# remove reflection
if np.linalg.det(R) < 0:
   Vt[2,:] *= -1
   #R = U@Vt
   R = np.matmul(U,Vt)



import json
# Opening JSON file
f = open(path + 'Ball-positions.json', )
# returns JSON object as
# a dictionary
data = json.load(f)

# Iterating through the json
# list
#for i in data['points']:
#    print(i)

#numberOfPoints = len(data['holoPoints'])
numberOfPoints = len(data['cameraBalls'][0])
#numberOfCameras = len(data['cameraPoints'])
numberOfCameras = len(data['cameraBalls'])
A= np.zeros((3,numberOfPoints))
B= np.zeros((numberOfCameras-1, 3, numberOfPoints))
for i in range(numberOfPoints):
    #A[0,i] = data['holoPoints'][i]["x"]
    #A[1,i] = data['holoPoints'][i]["y"]
    #A[2,i] = data['holoPoints'][i]["z"]
    A[0,i] = list(data['cameraBalls'][0].values())[i]["x"]
    A[1,i] = list(data['cameraBalls'][0].values())[i]["y"]
    A[2,i] = list(data['cameraBalls'][0].values())[i]["z"]
    for j in range(numberOfCameras-1):
        #B[j,0, i] = data['cameraPoints'][j]['points'][i]["x"]
        #B[j,1, i] = data['cameraPoints'][j]['points'][i]["y"]
        #B[j,2, i] = data['cameraPoints'][j]['points'][i]["z"]
        B[j,0, i] = list(data['cameraBalls'][1+j].values())[i]["x"]
        B[j,1, i] = list(data['cameraBalls'][1+j].values())[i]["y"]
        B[j,2, i] = list(data['cameraBalls'][1+j].values())[i]["z"]

# Closing file
f.close()

# number of points
#n = 10
#A = np.random.rand(3, n)
##B = R@A + t
#B = np.matmul(R,A) + t

data = {}
data['cameraTransformation'] = []
for j in range(numberOfCameras-1):
    #A= np.array([[0,1,2,3],[0,1,2,3],[0,1,2,3]])
    #B[j]= np.array([[0,1,2,3],[0,1,2,3],[0,1,2,3]])

    # Recover R and t
    ret_R, ret_t = rigid_transform_3D(A, B[j])

    #print(ret_R.tolist())
    #rotation = Rot.from_dcm(ret_R)
    rotation = Rot.from_matrix(ret_R)
    quaternion = rotation.as_quat()
    print(ret_t,ret_R )


    transformation = {}
    transformation['translation'] = {}
    transformation['translation']['x'] = ret_t[0][0]
    transformation['translation']['y'] = ret_t[1][0]
    transformation['translation']['z'] = ret_t[2][0]

    transformation['quaternion'] = {}
    transformation['quaternion']['x'] = quaternion[0]
    transformation['quaternion']['y'] = quaternion[1]
    transformation['quaternion']['z'] = quaternion[2]
    transformation['quaternion']['w'] = quaternion[3]

    data['cameraTransformation'].append(transformation)

if(numberOfCameras == 2):
    with open(path + 'Transformation-Camera.json', 'w') as outfile:
        json.dump(data, outfile)
elif(numberOfCameras == 3):
    with open(path+ 'Transformation-View.json', 'w') as outfile:
        json.dump(data, outfile)