using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    // Models authored in Blender; shared game materials avoid an allocation per sword/shard.
    public sealed class TrialSignatureProp : MonoBehaviour
    {
        Transform model;
        string resource;
        LineRenderer beam,beamHalo;
        public static TrialSignatureProp Create(Transform parent)
        {
            var root=new GameObject("Signature weapon / crystal");root.layer=2;root.transform.SetParent(parent,false);
            return root.AddComponent<TrialSignatureProp>();
        }
        public void Show(string asset,Vector3 point,Quaternion rotation,Vector3 scale,Material surface,Material core)
        {
            if(beam){beam.enabled=false;beamHalo.enabled=false;}
            if(resource!=asset)
            {
                if(model)Destroy(model.gameObject);
                resource=asset;var prefab=Resources.Load<GameObject>("Trial/VFX/"+asset);
                if(!prefab)return;
                model=Instantiate(prefab,transform).transform;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial=renderer.name.Contains("fuller") || renderer.name.Contains("jewel")?core:surface;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
                foreach(var c in model.GetComponentsInChildren<Collider>())Destroy(c);
            }
            gameObject.SetActive(true);transform.SetPositionAndRotation(point,rotation);transform.localScale=scale;
        }
        public void Beam(Vector3 ground,Vector3 tip,Material material,Color tint,float width)
        {
            if(!beam)
            {
                beam=BeamLine("Falling sword luminous spine",material);
                beamHalo=BeamLine("Falling sword soft column",material);
            }
            beam.enabled=beamHalo.enabled=true;beam.widthMultiplier=width*.22f;beamHalo.widthMultiplier=width;
            beam.SetPosition(0,ground);beam.SetPosition(1,tip);beamHalo.SetPosition(0,ground);beamHalo.SetPosition(1,tip);
            beam.startColor=Color.Lerp(tint,Color.white,.8f);beam.endColor=new Color(tint.r,tint.g,tint.b,.3f);
            tint.a=.20f;beamHalo.startColor=tint;tint.a=.03f;beamHalo.endColor=tint;
        }
        LineRenderer BeamLine(string label,Material material)
        {
            var go=new GameObject(label);go.layer=2;go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=2;line.useWorldSpace=true;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        public void Hide(){gameObject.SetActive(false);}
    }
}
