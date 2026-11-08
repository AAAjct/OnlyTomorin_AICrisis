using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialNaiWaRig
    {
        SkinnedMeshRenderer skin;
        Mesh wholeMesh;
        TrialNaiWaHeadMesh cutMesh;
        Transform neck,headRoot;
        MeshRenderer headRenderer;
        Matrix4x4 neckBindPose;
        Vector3 headStart,headTravel,headSpin;
        float headFloor;

        public bool HeadSevered { get; private set; }

        void PrepareSeveredHead()
        {
            skin=visual.GetComponentInChildren<SkinnedMeshRenderer>();neck=Find("spine.004");
            if(!skin || !neck)return;
            int neckIndex=System.Array.IndexOf(skin.bones,neck);
            if(neckIndex<0)return;
            wholeMesh=skin.sharedMesh;neckBindPose=wholeMesh.bindposes[neckIndex];
            cutMesh=TrialNaiWaHeadMesh.Get(skin,visual,neckIndex);
        }

        internal void SeverHead()
        {
            if(HeadSevered || cutMesh==null || hidden)return;
            if(!headRoot)
            {
                headRoot=new GameObject("NaiWa · Falling head").transform;
                headRoot.SetParent(transform,false);
                var head=new GameObject("Head mesh");head.transform.SetParent(headRoot,false);
                head.AddComponent<MeshFilter>().sharedMesh=cutMesh.Head;
                headRenderer=head.AddComponent<MeshRenderer>();
            }
            var pose=neck.localToWorldMatrix*neckBindPose;
            headStart=pose.MultiplyPoint3x4(cutMesh.Pivot);
            headRoot.SetPositionAndRotation(headStart,Quaternion.identity);
            headRenderer.transform.SetPositionAndRotation(pose.GetPosition(),pose.rotation);
            headRenderer.transform.localScale=pose.lossyScale;
            headRenderer.sharedMaterials=skin.sharedMaterials;
            headRenderer.shadowCastingMode=skin.shadowCastingMode;
            headRoot.gameObject.SetActive(true);
            skin.sharedMesh=cutMesh.Body;HeadSevered=true;

            Vector3 away=Vector3.ProjectOnPlane(owner.Recoil,Vector3.up).normalized;
            // Roll across the cut so the head stays clear of the falling torso.
            Vector3 side=away.sqrMagnitude>.01f?Vector3.Cross(Vector3.up,away):transform.right;
            Vector3 direction=(side+away*.35f).normalized;
            float distance=1.45f;
            if(Physics.SphereCast(headStart,.12f,direction,out var wall,distance,1,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0,wall.distance-.06f);
            headTravel=direction*distance;headSpin=Vector3.Cross(Vector3.up,direction);
            headFloor=Physics.Raycast(headStart+headTravel+Vector3.up*.25f,Vector3.down,out var floor,6,1,QueryTriggerInteraction.Ignore)
                ?floor.point.y+.15f:transform.position.y+.15f;
        }

        void PoseFallingHead(float age)
        {
            float travel=1-Mathf.Pow(1-Mathf.Clamp01(age/.94f),2);
            Vector3 point=headStart+headTravel*travel;
            point.y=Mathf.Max(headFloor,headStart.y+age*(1.45f-7.8f*age));
            headRoot.SetPositionAndRotation(point,Quaternion.AngleAxis(285*travel,headSpin));
        }

        void ResetSeveredHead()
        {
            if(!HeadSevered)return;
            skin.sharedMesh=wholeMesh;
            headRenderer.sharedMaterials=System.Array.Empty<Material>();
            headRoot.gameObject.SetActive(false);HeadSevered=false;
        }
    }
}
