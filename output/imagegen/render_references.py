import bpy, math, os
from mathutils import Vector
root=r'D:\unity\unity_file\project\OnlyTomorin_AICrisis'
out=os.path.join(root,'output','imagegen','references')
os.makedirs(out,exist_ok=True)
for name,rel in [('takosaki','Takosaki/Takosaki.fbx'),('doro','DoroXueLian/DoroXueLian.fbx'),('tomorin','Tomorin/Tomorin.fbx')]:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=os.path.join(root,'Assets','Art','Models',rel))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 print('MODEL',name,[(o.name,[(m.name,tuple(m.diffuse_color)) for m in o.data.materials if m]) for o in meshes],flush=True)
 pts=[o.matrix_world@Vector(c) for o in meshes for c in o.bound_box]
 lo=Vector(tuple(min(p[i] for p in pts) for i in range(3))); hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)))
 center=(lo+hi)/2; size=max(hi-lo)
 scene=bpy.context.scene
 scene.render.engine='BLENDER_WORKBENCH'
 scene.display.shading.light='STUDIO';scene.display.shading.color_type='TEXTURE'
 scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
 scene.display.shading.background_type='WORLD';scene.world=bpy.data.worlds.new('World');scene.world.color=(.22,.22,.22)
 scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
 for side,sgn in [('front',-1),('back',1)]:
  bpy.ops.object.camera_add(location=center+Vector((0,sgn*size*2,size*.08)))
  cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=size*1.16;scene.camera=cam
  scene.render.filepath=os.path.join(out,name+'-'+side+'.png');bpy.ops.render.render(write_still=True)
