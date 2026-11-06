"""Author the Penguin champion's forged frost hammer in Blender.
blender --background --python Tools/create_frost_relic.py
Coordinates are authored Z-up, FBX exports Y-up for Unity. Original geometry only.
"""
from pathlib import Path
import math
import bpy
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Art/Models/Resources/Trial/Models'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)


def material(name,color,metallic,roughness):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');output=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface']);bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metallic;bs.inputs['Roughness'].default_value=roughness
    return m


iron=material('Relic_Iron',(.055,.08,.11),.85,.28)
steel=material('Relic_Steel',(.38,.48,.55),.9,.22)
gold=material('Relic_Brass',(.52,.29,.08),.8,.3)
leather=material('Relic_Leather',(.065,.035,.027),0,.7)
ice=material('Relic_Ice',(.08,.6,.8),.35,.18)
ember=material('Relic_Ember',(.65,.045,.015),.2,.25)


def finish(obj,name,mat,bevel=0):
    obj.name=name;obj.data.materials.append(mat)
    if bevel:
        m=obj.modifiers.new('Machined bevel','BEVEL');m.width=bevel;m.segments=3
        bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=m.name)
    for p in obj.data.polygons:p.use_smooth=True
    m=obj.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');m.keep_sharp=True;m.weight=40
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=m.name)
    return obj


def box(name,pos,size,mat,bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    obj=bpy.context.object;obj.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat,bevel)


def cylinder(name,pos,r,depth,mat,axis='Z',vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=pos)
    obj=bpy.context.object
    if axis=='X':obj.rotation_euler.y=math.pi/2
    if axis=='Y':obj.rotation_euler.x=math.pi/2
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,name,mat,min(.009,r*.14))


def ring(name,pos,r,tube,mat,axis='Z'):
    bpy.ops.mesh.primitive_torus_add(major_segments=40,minor_segments=8,location=pos,major_radius=r,minor_radius=tube)
    obj=bpy.context.object
    if axis=='X':obj.rotation_euler.y=math.pi/2
    if axis=='Y':obj.rotation_euler.x=math.pi/2
    return finish(obj,name,mat)


cylinder('Octagonal ashwood grip',(0,0,.43),.048,.94,leather,vertices=16)
for i in range(17):ring('Leather grip rib', (0,0,.035+i*.047),.047,.005,iron)
cylinder('Forged pommel',(0,0,-.06),.077,.1,gold,vertices=12)
cylinder('Pommel ice seal',(0,0,-.12),.046,.025,ice,vertices=12)
cylinder('Neck socket',(0,0,1),.09,.29,iron,vertices=12)
for z in (.87,1.01,1.12):ring('Socket brass binding',(0,0,z),.091,.013,gold)
box('Forged octagonal head',(0,0,1.27),(1.03,.54,.55),iron,.09)
box('Central spine',(0,0,1.27),(.23,.61,.66),steel,.045)
for sign in (-1,1):
    x=sign*.54
    cylinder('Steel impact collar',(x,0,1.27),.34,.12,steel,'X',vertices=12)
    cylinder('Inset frost anvil',(x+sign*.063,0,1.27),.27,.045,ice,'X',vertices=12)
    ring('Outer striking rim',(x+sign*.085,0,1.27),.285,.022,gold,'X')
    ring('Inner striking seal',(x+sign*.09,0,1.27),.19,.011,iron,'X')
    for i in range(8):
        a=i*math.pi/4
        cylinder('Riveted striking bolt',(x+sign*.09,math.sin(a)*.29,1.27+math.cos(a)*.29),.022,.019,iron,'X',vertices=6)
    for side in (-1,1):
        # Six inset frost channels remain legible at gameplay distance.
        for j in range(3):
            slot=box('Engraved coolant channel',(sign*(.22+j*.086),side*.276,1.27),(.027,.014,.34),ice,.005)
            slot.rotation_euler.y=sign*.16
for side in (-1,1):
    cylinder('Boss crest',(0,side*.333,1.27),.137,.035,gold,'Y',vertices=12)
    cylinder('Molten frost eye',(0,side*.354,1.27),.095,.022,ember,'Y',vertices=8)
    ring('Crest silver edge',(0,side*.368,1.27),.105,.012,steel,'Y')
    for z in (1.04,1.5):box('Head reinforcing crossbar',(0,side*.295,z),(.76,.027,.025),gold,.007)
for x in (-.31,0,.31):
    bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=.075,radius2=.014,depth=.19,location=(x,0,1.62))
    finish(bpy.context.object,'Frost crown tooth',ice,.008)

# Join by material: six draw groups preserve editable material roles and avoid a renderer per rivet.
for mat in (iron,steel,gold,leather,ice,ember):
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.materials and o.data.materials[0]==mat]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
    obj=bpy.context.object;obj.name=mat.name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Tools/FrostRelic.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'FrostRelic.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_anim=False,add_leaf_bones=False)
print('FROST_RELIC_COMPLETE',sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH'))

