"""Reexport edited source, without regenerating geometry. Run with an open .blend:
Blender --background <source.blend> --python-exit-code 1 --python ArtSource/Blender/ART1/export_art1.py
"""
import bpy
from pathlib import Path
root=Path(__file__).resolve().parents[3]
name=Path(bpy.data.filepath).stem
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.context.scene.objects:
    if o.type=='MESH':
        assert all(abs(s-1)<1e-6 for s in o.scale), 'Apply scale before export'
        assert all(abs(a)<1e-6 for a in o.rotation_euler), 'Apply rotation before export'
        o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/_Game/Art/Models/ART1'/(name+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=False)
