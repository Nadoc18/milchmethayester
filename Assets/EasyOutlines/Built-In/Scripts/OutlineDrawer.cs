using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[RequireComponent(typeof(Renderer))]
public class OutlineDrawer : MonoBehaviour
{
    private Renderer meshRenderer;
    public Material material;

    private Dictionary<Camera, CommandBuffer> cameras = new Dictionary<Camera, CommandBuffer>();


    private void OnDisable()
    {
        foreach (var kvp in cameras)
        {

            if (kvp.Key)
            {

                kvp.Key.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, kvp.Value);
            }
        }

        cameras.Clear();
    }

    void OnWillRenderObject()
    {
        bool active = enabled && gameObject.activeInHierarchy;

        if (!active)
        {

            OnDisable();
            return;
        }

        meshRenderer = GetComponent<Renderer>();

        Camera camera = Camera.current;

        if (!camera || !material || !meshRenderer)
        {
            return;
        }

        CommandBuffer buffer;

        if (!cameras.TryGetValue(camera, out buffer))
        {
            buffer = new CommandBuffer();
            buffer.name = "Command Buffer";

            camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, buffer);
            cameras.Add(camera, buffer);

            for(int i =0;i<meshRenderer.sharedMaterials.Length;i++)
            {
                buffer.DrawRenderer(meshRenderer, material, i, 0);
            }
        }
    }
}