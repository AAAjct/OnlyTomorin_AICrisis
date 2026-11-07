using UnityEngine;

namespace TomorinTrial
{
    // Twelve skinning panels, no per-frame mesh baking or cloth simulation.
    public sealed class TrialSkirtRig : MonoBehaviour
    {
        [SerializeField] Transform[] panels;
        [SerializeField] Transform leftThigh,leftKnee,rightThigh,rightKnee;
        [SerializeField] Quaternion[] rest;
        public void Configure(Transform[] bones,Transform lt,Transform lk,Transform rt,Transform rk)
        {
            panels=bones;leftThigh=lt;leftKnee=lk;rightThigh=rt;rightKnee=rk;
            rest=new Quaternion[bones.Length];for(int i=0;i<bones.Length;i++)rest[i]=bones[i].localRotation;
        }
        public void Pose()
        {
            if(panels==null || !leftKnee || !rightKnee)return;
            Vector3 left=transform.InverseTransformDirection(leftKnee.position-leftThigh.position).normalized;
            Vector3 right=transform.InverseTransformDirection(rightKnee.position-rightThigh.position).normalized;
            for(int i=0;i<panels.Length;i++)
            {
                float a=i*Mathf.PI*2/panels.Length;
                var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                float l=Mathf.Max(0,Vector3.Dot(left,radial));
                float r=Mathf.Max(0,Vector3.Dot(right,radial));
                float angle=Mathf.Clamp(Mathf.Asin(Mathf.Clamp01(Mathf.Max(l,r)))*Mathf.Rad2Deg+5,5,58);
                Vector3 axis=Vector3.Cross(Vector3.down,transform.TransformDirection(radial));
                var panel=panels[i];panel.localRotation=rest[i];
                panel.rotation=Quaternion.AngleAxis(angle,axis)*panel.rotation;
            }
        }
    }
}
