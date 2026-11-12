import bpy, os
from mathutils import Vector
root=r'D:\unity\unity_file\project\OnlyTomorin_AICrisis'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=root+r'\Assets\Art\Models\DoroXueLian\DoroXueLian.fbx')
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 for m in o.data.materials:
  print(o.name,m.name,[(n.type,getattr(getattr(n,'image',None),'filepath',None)) for n in m.node_tree.nodes],flush=True)
  for n in m.node_tree.nodes:
   if n.type=='TEX_IMAGE':
    n.image=bpy.data.images.load(root+r'\Assets\Art\Models\DoroXueLian\Textures\DoroXueLian_'+('Head' if m.name=='Material.001' else 'Body')+'.png')
  imgs=[n for n in m.node_tree.nodes if n.type=='TEX_IMAGE']
  if imgs:m.node_tree.nodes.active=imgs[0]
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];pts=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)));c=(lo+hi)/2;s=max(hi-lo)
scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.color_type='TEXTURE';scene.display.shading.light='STUDIO'
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=c+Vector((0,-s*2,s*.08)));cam=bpy.context.object;cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=s*1.16;scene.camera=cam
scene.render.filepath=root+r'\output\imagegen\references\doro-textured.png';bpy.ops.render.render(write_still=True)
