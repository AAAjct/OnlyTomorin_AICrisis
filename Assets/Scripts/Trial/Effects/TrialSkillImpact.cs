using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        sealed class TideBurst
        {public Transform root,crown,pearl;public MeshRenderer crownRenderer,pearlRenderer;public float start=-10;public Color tint;}
        readonly TideBurst[] tides=new TideBurst[12];
        Material tideMaterial;
        MaterialPropertyBlock tideProperties;
        int nextTide;
        void InitializeTides()
        {
            tideMaterial=new Material(Resources.Load<Material>("Trial/VFX/TideShell"));
            tideProperties=new MaterialPropertyBlock();
            var crownMesh=Resources.Load<Mesh>("Trial/VFX/TideCrown");var pearlMesh=Resources.Load<Mesh>("Trial/VFX/TidePearl");
            for(int i=0;i<tides.Length;i++)
            {
                var root=new GameObject("Pooled tidal detonation "+i).transform;root.SetParent(transform,false);
                var crown=new GameObject("Breaking water crown",typeof(MeshFilter),typeof(MeshRenderer)).transform;crown.SetParent(root,false);
                crown.GetComponent<MeshFilter>().sharedMesh=crownMesh;var cr=crown.GetComponent<MeshRenderer>();cr.sharedMaterial=tideMaterial;cr.shadowCastingMode=ShadowCastingMode.Off;
                var pearl=new GameObject("Pressure dome",typeof(MeshFilter),typeof(MeshRenderer)).transform;pearl.SetParent(root,false);
                pearl.GetComponent<MeshFilter>().sharedMesh=pearlMesh;var pr=pearl.GetComponent<MeshRenderer>();pr.sharedMaterial=tideMaterial;pr.shadowCastingMode=ShadowCastingMode.Off;
                tides[i]=new TideBurst{root=root,crown=crown,pearl=pearl,crownRenderer=cr,pearlRenderer=pr};root.gameObject.SetActive(false);
            }
        }
        public void SkillImpact(Vector3 point,Color tint)
        {
            if(Mirror.NetworkServer.active){TrialNetworkManager.Current.BroadcastSkillImpact(point,tint);return;}
            ShowSkillImpact(point,tint);
        }
        void ShowSkillImpact(Vector3 point,Color tint)
        {
            var burst=tides[nextTide++%tides.Length];burst.start=Time.unscaledTime;burst.tint=tint;
            burst.root.position=point+Vector3.up*.08f;burst.root.gameObject.SetActive(true);
            Burst(point+Vector3.up*.3f,tint,50);Sound("tideImpact",point,Random.Range(.9f,1.06f));
        }
        void TickTides()
        {
            foreach(var burst in tides)
            {
                if(burst==null || !burst.root.gameObject.activeSelf)continue;
                float p=(Time.unscaledTime-burst.start)/.8f;
                if(p>=1){burst.root.gameObject.SetActive(false);continue;}
                float radius=Mathf.Lerp(.3f,3.4f,1-Mathf.Pow(1-p,3));
                burst.crown.localScale=new Vector3(radius,(1-p)*1.6f,radius);
                burst.crown.localRotation=Quaternion.Euler(0,p*35,0);
                burst.pearl.localScale=new Vector3(radius*1.8f,(1-p)*2,radius*1.8f);
                var c=burst.tint;c.a=(1-p)*(1-p)*.75f;tideProperties.SetColor("_BaseColor",c);
                burst.crownRenderer.SetPropertyBlock(tideProperties);burst.pearlRenderer.SetPropertyBlock(tideProperties);
            }
        }
    }
}
