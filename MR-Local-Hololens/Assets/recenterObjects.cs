using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Utilities.Solvers;

//copying this over from Jesse's work on recenter objects in Remote Holo
public class recenterObjects : MonoBehaviour
{
    public GameObject toRecenter;
    public SolverHandler solver;

    private void Start()
    {
        Invoke(nameof(stopSolver), 1f);
    }

    private void stopSolver()
    {

        solver.UpdateSolvers = false;

    }
    public void recenterObject()
    {

        solver.UpdateSolvers = true;
        print("Recenter objects.");

        Invoke(nameof(stopSolver), 0.75f);

    }
}
