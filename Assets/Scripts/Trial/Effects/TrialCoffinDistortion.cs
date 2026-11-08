using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TomorinTrial
{
    sealed class TrialCoffinDistortion : IDisposable
    {
        static readonly int ChantPhase=Shader.PropertyToID("_ChantPhase");
        readonly Camera camera;
        readonly UniversalAdditionalCameraData cameraData;
        readonly Material material;
        readonly PressurePass pass;
        bool active;

        public TrialCoffinDistortion(Camera camera,Shader shader)
        {
            this.camera=camera;
            cameraData=camera.GetUniversalAdditionalCameraData();
            material=new Material(shader){name="Black coffin chant pressure"};
            pass=new PressurePass(material);
        }

        public void Update(bool chanting,float progress,float elapsed,float titleFocus)
        {
            if(active!=chanting)
            {
                active=chanting;
                if(active)RenderPipelineManager.beginCameraRendering+=Enqueue;
                else RenderPipelineManager.beginCameraRendering-=Enqueue;
            }
            if(active)
            {
                bool reduced=InkMainMenu.ReducedMotion;
                material.SetVector(ChantPhase,new Vector4(progress,reduced?0:elapsed,reduced?.16f:1,titleFocus));
            }
        }

        void Enqueue(ScriptableRenderContext context,Camera current)
        {
            if(current==camera && cameraData && cameraData.renderType==CameraRenderType.Base && cameraData.cameraStack.Count==0)
                cameraData.scriptableRenderer?.EnqueuePass(pass);
        }

        public void Dispose()
        {
            if(active)RenderPipelineManager.beginCameraRendering-=Enqueue;
            CoreUtils.Destroy(material);
        }

        sealed class PressurePass : ScriptableRenderPass
        {
            const string PassName="Black coffin chant pressure";
            readonly Material material;

            public PressurePass(Material material)
            {
                this.material=material;
                renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture=true;
            }

            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
            {
                var resources=frameData.Get<UniversalResourceData>();
                if(resources.isActiveTargetBackBuffer)return;
                var source=resources.activeColorTexture;
                var descriptor=graph.GetTextureDesc(source);
                descriptor.name=PassName;
                descriptor.clearBuffer=false;
                var destination=graph.CreateTexture(descriptor);
                var blit=new RenderGraphUtils.BlitMaterialParameters(source,destination,material,0);
                graph.AddBlitPass(blit,passName:PassName);
                // The following camera passes can read the distorted texture directly.
                resources.cameraColor=destination;
            }
        }
    }
}
