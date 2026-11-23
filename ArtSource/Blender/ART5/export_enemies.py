"""Export saved ART5 timeline, preserving editable source object transforms/animation."""
import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[3];name=Path(bpy.data.filepath).stem
assert name in ('Enemy_Shade','Enemy_RiftWarden')
assert bpy.context.scene.unit_settings.scale_length==1
bpy.context.scene.frame_set(1)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/_Game/Art/Models/ART5'/(name+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=False,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='STRIP',use_custom_props=False)
