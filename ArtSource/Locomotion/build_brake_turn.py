"""Bake a retimed, foot-braced variant of the imported RunningTurn180_Source action.
Run in Blender's Locomotion_BrakeTurn_Work scene. Original action/rig are preserved.
"""
import bpy
import math
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path('Z:/Work/Project/Project_Pictureless')
scene = bpy.data.scenes['Locomotion_BrakeTurn_Work']
bpy.context.window.scene = scene
source = bpy.data.objects['BrakeTurn_Source_Rig']
if bpy.data.objects.get('BrakeTurn_Authored_Rig'):
    raise RuntimeError('Authored rig already exists; inspect before rebuilding.')
source.animation_data.action = bpy.data.actions['RunningTurn180_Source']
scene.frame_set(10)
anchor = source.pose.bones['mixamorig:LeftFoot'].matrix.translation.copy()
samples = []
bone_names = [b.name for b in source.data.bones]
down = source.matrix_world.inverted().to_3x3() @ Vector((0, 0, -0.035))

def smooth(x):
    x = max(0.0, min(1.0, x))
    return x*x*(3-2*x)

def rotated_at(matrix, old_axis, new_axis, position):
    rotation = old_axis.rotation_difference(new_axis) @ matrix.to_quaternion()
    return Matrix.LocRotScale(position, rotation, matrix.to_scale())

for frame in range(1, 29):
    t = frame-1
    src = 1 + (t*0.5 if t <= 12 else 6+(t-12)*0.875 if t <= 20 else 13+(t-20))
    scene.frame_set(int(src), subframe=src-int(src))
    original = {n: source.pose.bones[n].matrix.copy() for n in bone_names}
    weight = smooth((src-2)/5) * (1-smooth((src-13)/6))
    shift = Matrix.Translation(down * weight)
    posed = {n: shift @ m for n, m in original.items()}
    for side in ('Left', 'Right'):
        upper, lower, foot = ['mixamorig:'+side+n for n in ('UpLeg','Leg','Foot')]
        a = posed[upper].translation.copy()
        b = original[lower].translation.copy()
        c = original[foot].translation.copy()
        if side == 'Left':
            plant = smooth((src-4)/3) * (1-smooth((src-13)/3))
            c = c.lerp(anchor, plant)
        length1 = (original[lower].translation-original[upper].translation).length
        length2 = (original[foot].translation-original[lower].translation).length
        axis = (c-a).normalized()
        distance = (c-a).length
        if not abs(length1-length2)+0.001 < distance < length1+length2-0.001:
            raise RuntimeError('Leg target unreachable at frame '+str(frame))
        along = (length1*length1-length2*length2+distance*distance)/(2*distance)
        bend = b-a-axis*(b-a).dot(axis)
        bend.normalize()
        knee = a+axis*along+bend*math.sqrt(max(0, length1*length1-along*along))
        posed[upper] = rotated_at(original[upper], original[lower].translation-original[upper].translation, knee-a, a)
        posed[lower] = rotated_at(original[lower], original[foot].translation-original[lower].translation, c-knee, knee)
        foot_shift = Matrix.Translation(c-original[foot].translation)
        posed[foot] = foot_shift @ original[foot]
        for child in source.data.bones[foot].children_recursive:
            posed[child.name] = foot_shift @ original[child.name]
    samples.append(posed)

edited = source.copy()
edited.data = source.data.copy()
edited.animation_data_clear()
edited.name = 'BrakeTurn_Authored_Rig'
scene.collection.objects.link(edited)
edited.animation_data_create()
edited.animation_data.action = bpy.data.actions.new('Run Brake Turn 180')
edited.animation_data.action.use_fake_user = True
for frame, poses in enumerate(samples, 1):
    for bone in edited.data.bones:
        pose = edited.pose.bones[bone.name]
        if bone.parent:
            basis = bone.matrix_local.inverted() @ bone.parent.matrix_local @ poses[bone.parent.name].inverted() @ poses[bone.name]
        else:
            basis = bone.matrix_local.inverted() @ poses[bone.name]
        pose.matrix_basis = basis
        pose.keyframe_insert('location', frame=frame)
        pose.keyframe_insert('rotation_quaternion', frame=frame)
        pose.keyframe_insert('scale', frame=frame)
for obj in list(source.children):
    if obj.type != 'MESH':
        continue
    copy = obj.copy()
    scene.collection.objects.link(copy)
    copy.parent = edited
    for modifier in copy.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.object = edited
    obj.hide_set(True)
    obj.hide_render = True
source.hide_set(True)
source.hide_render = True
scene.frame_start, scene.frame_end, scene.render.fps = 1, 28, 30
scene.frame_set(14)
bpy.ops.object.select_all(action='DESELECT')
edited.select_set(True)
bpy.context.view_layer.objects.active = edited
out = ROOT/'Assets/Dist/Visual/Anim/Authored/Run Brake Turn 180.fbx'
out.parent.mkdir(parents=True, exist_ok=True)
if out.exists():
    raise RuntimeError('Export already exists; inspect before replacing.')
bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, object_types={'ARMATURE'},
    add_leaf_bones=False, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
    bake_anim_simplify_factor=0.0, axis_forward='-Z', axis_up='Y')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'ArtSource/Locomotion/BrakeTurn.blend'))
result = {'export':str(out), 'duration':0.9, 'frames':28, 'pelvis_drop_m':0.035,
          'left_foot_plant_source_frames':[7,13], 'original_action_preserved':True}
