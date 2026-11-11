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
        public void Pose(Transform leftFoot,Transform rightFoot)
        {
            if(panels==null || panels.Length==0 || !leftThigh || !leftKnee || !rightThigh || !rightKnee)return;
            var first=panels[0];
            Quaternion basis=first.parent.rotation*rest[0];
            Quaternion inverse=Quaternion.Inverse(basis);
            Vector3 center=first.position;
            Vector3 leftHip=inverse*(leftThigh.position-center), rightHip=inverse*(rightThigh.position-center);
            Vector3 left=inverse*(leftKnee.position-center), right=inverse*(rightKnee.position-center);
            Vector3 leftDirection=(left-leftHip).normalized,rightDirection=(right-rightHip).normalized;
            Vector3 leftShin=leftFoot?inverse*(Vector3.Lerp(leftKnee.position,leftFoot.position,.32f)-center):left;
            Vector3 rightShin=rightFoot?inverse*(Vector3.Lerp(rightKnee.position,rightFoot.position,.32f)-center):right;
            for(int i=0;i<panels.Length;i++)
            {
                float a=i*Mathf.PI*2/panels.Length;
                var radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                var tangent=new Vector3(radial.z,0,-radial.x);
                float flare=Mathf.Max(LegClearance(leftHip,left,leftShin,radial,tangent),
                    LegClearance(rightHip,right,rightShin,radial,tangent));
                float thighLean=Mathf.Clamp01(Mathf.Max(Vector3.Dot(leftDirection,radial),Vector3.Dot(rightDirection,radial)));
                flare=Mathf.Max(flare,Mathf.Asin(thighLean)*Mathf.Rad2Deg);
                Vector3 axis=basis*Vector3.Cross(Vector3.down,radial);
                var panel=panels[i];panel.localRotation=rest[i];
                panel.rotation=Quaternion.AngleAxis(Mathf.Clamp(6+flare,6,74),axis)*panel.rotation;
            }
        }

        static float LegClearance(Vector3 hip,Vector3 knee,Vector3 shin,Vector3 radial,Vector3 tangent)
        {
            return Mathf.Max(Clearance(Vector3.Lerp(hip,knee,.48f),radial,tangent),
                Mathf.Max(Clearance(knee,radial,tangent),Clearance(shin,radial,tangent)));
        }

        static float Clearance(Vector3 point,Vector3 radial,Vector3 tangent)
        {
            float depth=-point.y;
            if(depth<-.04f || depth>.55f)return 0;
            float outward=Vector3.Dot(point,radial);
            if(outward<=0)return 0;
            float influence=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.075f,.25f,Mathf.Abs(Vector3.Dot(point,tangent))));
            float down=Mathf.Max(.055f,depth);
            float clothRadius=.105f+.23f*down;
            float flare=Mathf.Atan2(outward+.115f,down)-Mathf.Atan2(clothRadius,down);
            return Mathf.Max(0,flare)*Mathf.Rad2Deg*influence;
        }
    }
}
