using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    // Pure presentation. All damage, defenses and growth remain in the host simulation.
    public sealed class TrialRemnantAura : MonoBehaviour
    {
        readonly Vector3[] points=new Vector3[25];
        LineRenderer[] arcs;
        TrialChampion brain;
        TrialEnemy owner;
        Material material;
        float clock;
        void Start()
        {
            brain=GetComponentInParent<TrialChampion>();owner=GetComponentInParent<TrialEnemy>();
            material=new Material(Resources.Load<Material>("Trial/VFX/TideRibbon"));
            arcs=new LineRenderer[3];
            for(int i=0;i<arcs.Length;i++)
            {
                var go=new GameObject("Void soul ribbon "+i);go.layer=2;go.transform.SetParent(transform,false);
                var line=go.AddComponent<LineRenderer>();arcs[i]=line;line.sharedMaterial=material;line.positionCount=points.Length;
                line.useWorldSpace=false;line.widthMultiplier=.025f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            }
        }
        void LateUpdate()
        {
            if(arcs==null)return;clock+=Time.deltaTime;
            for(int i=0;i<arcs.Length;i++)
            {
                var line=arcs[i];line.enabled=owner && owner.Alive;if(!line.enabled)continue;
                float radius=.72f+Mathf.Min(8,brain.KillStacks)*.025f;
                for(int j=0;j<points.Length;j++)
                {float a=clock*(i%2==0?1:-1)+i*2.1f+j/24f*1.25f;points[j]=new Vector3(Mathf.Sin(a)*radius,1.05f+Mathf.Cos(a*1.5f+i)*.23f,Mathf.Cos(a)*radius);}
                line.SetPositions(points);Color color=owner.Guarding?new Color(1,.7f,.2f,.8f):new Color(.65f,.08f,1,.48f);line.startColor=color;line.endColor=new Color(color.r,color.g,color.b,0);
            }
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
