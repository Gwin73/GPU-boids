﻿using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(SphereCollider))]
public class BoidController : MonoBehaviour
{
    [SerializeField] int boidsCount = 2048;
    [SerializeField] float alignmentForceFactor;
    [SerializeField] float cohesionForceFactor;
    [SerializeField] float separationForceFactor;
    [SerializeField] float boundsForceFactor;
    [SerializeField] float alignmentDistance = 3.0f;
    [SerializeField] float cohesionDistance = 3.0f;
    [SerializeField] float separationDistance = 2.0f;
    [SerializeField] float spawnRadius;
    [SerializeField] float spawnVelocity;
    [SerializeField] float minVelocity;
    [SerializeField] float maxVelocity;

    [SerializeField] ComputeShader boidCalculation;
    [SerializeField] Mesh boidMesh;
    [SerializeField] Material boidMaterial;

    SphereCollider simulationBounds;
    int updateBoidkernelIndex;
    ComputeBuffer boidsBuffer;
    ComputeBuffer argsBuffer;
    ComputeBuffer forceFieldBuffer;
    
    const int BoidStride = sizeof(float) * 3 * 3; //size of float members in bytes
    const int ForceFieldStride = sizeof(float) * (3 + 1);
    const int ThreadGroupSize = 1024;
    
    struct Boid
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
    }

    struct Field
    {
        public Vector3 position;
        public float force;
    }
    
    void Start ()
    {
        simulationBounds = GetComponent<SphereCollider>();

        updateBoidkernelIndex = boidCalculation.FindKernel("UpdateBoid");
        
        InitAndBindBoidBuffer();
        
        InitAndBindForceFields();

        InitAndBindArgsBuffer();
        
        InitAndBindFloats();
    }
    
    void InitAndBindBoidBuffer()
    {
        var boids = new Boid[boidsCount];
        for (int i = 0; i < boids.Length; i++)
        {
            boids[i].position = simulationBounds.center + Random.insideUnitSphere * Mathf.Clamp(spawnRadius, 0, simulationBounds.radius);
            boids[i].velocity = Random.insideUnitSphere * spawnVelocity;
            boids[i].acceleration = Vector3.zero;
        }

        boidsBuffer = new ComputeBuffer(boidsCount, BoidStride);
        boidsBuffer.SetData(boids);
        boidCalculation.SetBuffer(updateBoidkernelIndex, "boids", boidsBuffer); //TODO: necessary every frame?
    }

    void InitAndBindForceFields()
    {
        var forceFields = GameObject.FindGameObjectsWithTag("ForceField").Select(g => new Field { position = g.transform.position, force = g.GetComponent<ForceField>().force }).ToArray();
        forceFieldBuffer = new ComputeBuffer(forceFields.Length, ForceFieldStride);
        forceFieldBuffer.SetData(forceFields);
        boidCalculation.SetBuffer(updateBoidkernelIndex, "forceFields", forceFieldBuffer);
    }

    void InitAndBindArgsBuffer()
    {
        var args = new uint[] { boidMesh.GetIndexCount(0), (uint)boidsCount, 0, 0, 0 };
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void InitAndBindFloats()
    {
        boidCalculation.SetFloat("alignmentForceFactor", alignmentForceFactor);
        boidCalculation.SetFloat("cohesionForceFactor", cohesionForceFactor);
        boidCalculation.SetFloat("separationForceFactor", separationForceFactor);
        boidCalculation.SetFloat("boundsForceFactor", boundsForceFactor);
        boidCalculation.SetFloat("alignmentDistance", alignmentDistance);
        boidCalculation.SetFloat("cohesionDistance", cohesionDistance);
        boidCalculation.SetFloat("separationDistance", separationDistance);
        boidCalculation.SetFloats("simulationCenter", simulationBounds.center.x, simulationBounds.center.y, simulationBounds.center.z);
        boidCalculation.SetFloat("simulationRadius", simulationBounds.radius);
        boidCalculation.SetFloat("maxVelocity", maxVelocity);
        boidCalculation.SetFloat("minVelocity", minVelocity);
    }
	
	void Update ()
    {
        boidCalculation.SetFloat("deltaTime", Time.deltaTime);

        int threadGroupsCount = Mathf.CeilToInt((float) boidsCount / ThreadGroupSize);
        boidCalculation.Dispatch(updateBoidkernelIndex, threadGroupsCount, 1, 1);
        
        boidMaterial.SetBuffer("boids", boidsBuffer);

        //render
        Graphics.DrawMeshInstancedIndirect(boidMesh, 0, boidMaterial, new Bounds(Vector3.zero, Vector3.one * 1.1f * simulationBounds.radius), argsBuffer);
    }

    void OnDestroy()
    {
        boidsBuffer.Release();
        forceFieldBuffer.Release();
        argsBuffer.Release();
    }
}