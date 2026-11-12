using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialAnonSkirtRepair
    {
        const string MeshPath="Assets/Art/Champions/Anon_SkirtCorrected.asset";
        [MenuItem("Tools/Tomorin/Champions/Repair Anon skirt")]
        public static void Repair()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
            string path=TrialChampionBuilder.PrefabPath((int)ChampionKind.Anon);
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Apply(root);PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Apply(GameObject root)
        {
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
            var rig=root.GetComponent<TrialChampionRig>();
            var old=root.GetComponent<TrialSkirtRig>();if(old)Object.DestroyImmediate(old);
            foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Skirt panel ")).ToArray())Object.DestroyImmediate(t.gameObject);
            var source=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Anon/Anon.fbx").OfType<Mesh>().First(m=>m.vertexCount==skin.sharedMesh.vertexCount);
            var mesh=Object.Instantiate(source);mesh.name=System.IO.Path.GetFileNameWithoutExtension(MeshPath);
            // This procedural boss never animates facial blend shapes (all source
            // weights are zero). Keep them in the imported FBX, not the generated copy.
            mesh.ClearBlendShapes();
            int originalCount=source.bindposes.Length;
            var bones=skin.bones.Take(originalCount).ToList();
            var panels=new Transform[12];var binds=source.bindposes.ToList();
            for(int i=0;i<panels.Length;i++)
            {
                var t=new GameObject("Skirt panel "+i).transform;t.SetParent(rig.DrivenBones[0],false);
                t.position=root.transform.TransformPoint(new Vector3(0,1.16f,.04f));t.rotation=root.transform.rotation;
                panels[i]=t;bones.Add(t);binds.Add(t.worldToLocalMatrix*skin.transform.localToWorldMatrix);
            }
            var vertices=mesh.vertices;var weights=mesh.boneWeights;int count=0;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=root.transform.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]));
                // Includes the matching outline shell, excludes skin, boots and sleeves.
                if(weights[i].boneIndex0!=1 || p.y>=1.16f || p.y<.70f)continue;
                float amount=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.16f,.94f,p.y));
                float sector=Mathf.Repeat(Mathf.Atan2(p.x,p.z-.04f)/(Mathf.PI*2),1)*panels.Length;
                int a=Mathf.FloorToInt(sector);float blend=sector-a;
                weights[i]=new BoneWeight{boneIndex0=originalCount+a,boneIndex1=originalCount+(a+1)%panels.Length,boneIndex2=1,weight0=amount*(1-blend),weight1=amount*blend,weight2=1-amount};count++;
            }
            mesh.boneWeights=weights;mesh.bindposes=binds.ToArray();mesh.RecalculateBounds();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;mesh.boneWeights=weights;mesh.bindposes=binds.ToArray();EditorUtility.SetDirty(mesh);}
            else AssetDatabase.CreateAsset(mesh,MeshPath);
            skin.sharedMesh=mesh;skin.bones=bones.ToArray();
            var skirt=root.AddComponent<TrialSkirtRig>();var j=rig.DrivenBones;
            skirt.Configure(panels,j[10],j[11],j[13],j[14]);rig.SetSkirt(skirt);
            Debug.Log("Anon skirt: corrected "+count+" garment/outline vertices on 12 panels.");
        }
    }
}
