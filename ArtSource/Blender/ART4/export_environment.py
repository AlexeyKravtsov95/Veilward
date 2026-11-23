"""Export edited ART4 source without regeneration; preset matches accepted ART1/3."""
import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[3];name=Path(bpy.data.filepath).stem
assert name.startswith('Environment_') and bpy.context.scene.unit_settings.scale_length==1
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  assert all(abs(v-1)<1e-6 for v in o.scale) and all(abs(v)<1e-6 for v in o.rotation_euler)
  assert all(abs(v)<1e-6 for v in o.location)
  o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/_Game/Art/Models/ART4'/(name+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=False)
