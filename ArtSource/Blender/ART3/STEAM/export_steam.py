"""Экспорт только открытого STEAM source проверенным ART1/FLAME preset."""
import bpy
from pathlib import Path

FOLDER = Path(__file__).resolve().parent
ROOT = FOLDER.parents[3]
source = Path(bpy.data.filepath).resolve()
assert source.parent == FOLDER
name = source.stem
assert name in [f'Tower_SteamFoxL{i}' for i in (1, 2, 3)]
output = ROOT / 'Assets/_Game/Art/Models/ART3/STEAM'
output.mkdir(parents=True, exist_ok=True)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(meshes) == 1
bpy.ops.object.select_all(action='DESELECT')
obj = meshes[0]
assert obj.name == name
assert obj.location.length < 1e-6 and all(abs(v-1) < 1e-6 for v in obj.scale)
assert all(abs(v) < 1e-6 for v in obj.rotation_euler)
obj.data.calc_loop_triangles()
assert 0 < len(obj.data.loop_triangles) <= 5000
assert len(obj.data.materials) == 2
assert abs(min(v.co.z for v in obj.data.vertices)) < 1e-5
assert max(abs(v.co.x) for v in obj.data.vertices) <= .47
assert max(abs(v.co.y) for v in obj.data.vertices) <= .47
obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(output / (name + '.fbx')),
    use_selection=True, object_types={'MESH'}, global_scale=1,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
    axis_forward='-Z', axis_up='Y', use_space_transform=True,
    bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
    use_triangles=True, add_leaf_bones=False, bake_anim=False,
    path_mode='STRIP', use_custom_props=False)
