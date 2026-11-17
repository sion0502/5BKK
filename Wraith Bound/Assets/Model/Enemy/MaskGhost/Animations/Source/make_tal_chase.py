"""
탈춤귀신 추격 기본 달리기(TalChase) 생성 — Blender 5.1 백그라운드 실행용.
  blender -b --factory-startup --python make_tal_chase.py -- <입력 Meshy Walking FBX> <출력 FBX>
리그: Meshy 자동 리그(24뼈, T-포즈). 캐릭터는 Blender 기준 -Y를 보고, 왼쪽 = +X, 위 = +Z.
각 뼈 회전은 '캐릭터 기준 축'(rest 상태의 아마추어 공간 축)으로 지정 → 리그마다 다른 뼈 로컬 축을 신경 쓰지 않음.
  pitch(+X 회전): 상체는 +면 앞으로 숙임 / 다리·늘어뜨린 팔은 -면 앞으로 / 무릎(Leg)은 +면 굽힘
  yaw(Z), roll(Y): 팔을 내릴 때 LeftArm은 +Y, RightArm은 -Y
제자리 루프(이동은 NavMeshAgent). 덜컬거리는 비대칭 + 상체 숙임 + 고개 들어 탈이 정면.
관절을 '뚝뚝' 꺾는 건 Unity 실시간 레이어(CreatureJointSnap)가 담당 — 여기선 기본 리듬만.
"""
import bpy, sys, math
from mathutils import Quaternion, Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
src, dst = args[0], args[1]

FPS = 60
CYCLE = 40  # 0.667초에 두 걸음

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
for o in list(bpy.context.scene.objects):
    if o.type == 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)

scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start = 0
scene.frame_end = CYCLE

arm.animation_data_create()
action = bpy.data.actions.new("TalChase")
arm.animation_data.action = action
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE')

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def rest_q(pb):
    return pb.bone.matrix_local.to_quaternion()


def set_rot(name, rotations):
    """rotations: [(axis, deg), ...] 캐릭터 기준 축 회전을 순서대로 합성 → 뼈 로컬 회전으로 변환"""
    pb = arm.pose.bones[name]
    r = Quaternion()
    for axis, deg in rotations:
        r = Quaternion(axis, math.radians(deg)) @ r
    b = rest_q(pb)
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = b.inverted() @ r @ b


def set_hips_offset(offset_world):
    pb = arm.pose.bones["Hips"]
    m = pb.bone.matrix_local.to_3x3().inverted()
    pb.location = m @ offset_world


def key_all(frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
    arm.pose.bones["Hips"].keyframe_insert("location", frame=frame)


def wave(p, phase=0.0):
    return math.sin(2 * math.pi * (p + phase))


for f in range(0, CYCLE + 1, 2):
    p = f / CYCLE  # 0~1, 0 = 왼발 앞으로 내딛음
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = Quaternion()

    # 두 걸음마다 몸이 가라앉았다 솟음 (착지 직후 가장 낮음) + 좌우로 쏠림
    bob = -0.10 + 0.07 * math.cos(4 * math.pi * p)
    sway = 0.05 * wave(p, 0.25)
    set_hips_offset(Vector((sway, 0.0, bob)))

    # 상체: 크게 숙이고(합 ~48°), 다리 반대로 비틀림, 걸음마다 숙임이 출렁
    lurch = 6 * math.cos(4 * math.pi * p)
    twist = 14 * wave(p)
    set_rot("Hips", [(X, 10 + lurch * 0.5), (Z, -twist * 0.4)])
    set_rot("Spine02", [(X, 14 + lurch * 0.3), (Z, twist * 0.5)])
    set_rot("Spine01", [(X, 14), (Z, twist * 0.4), (Y, 4 * wave(p, 0.25))])
    set_rot("Spine", [(X, 10), (Z, twist * 0.3)])
    # 고개는 들어 탈이 정면 — 상체 숙임만큼 거의 되돌리고, 착지 때 덜컥
    set_rot("neck", [(X, -22 - lurch * 0.4)])
    set_rot("Head", [(X, -20 - lurch * 0.6), (Y, 6 * wave(p, 0.1))])

    # 다리: 왼쪽 보폭이 더 길고(덜컬거림), 무릎을 높이 차올림
    for side, phase, stride in (("Left", 0.0, 1.15), ("Right", 0.5, 0.9)):
        s = wave(p, phase + 0.25)            # +1 = 다리가 앞에
        swing = -(10 + 34 * s * stride)      # 골반 숙임(약 10°) 보정 포함
        lift = max(0.0, wave(p, phase + 0.5))  # 뒤에서 앞으로 넘어오는 중
        knee = 18 + 95 * lift ** 1.4 * stride
        set_rot(side + "UpLeg", [(X, swing - 12 * lift)])
        set_rot(side + "Leg", [(X, knee)])
        set_rot(side + "Foot", [(X, -knee * 0.35 + 12 * s)])

    # 팔: 축 늘어뜨리고 휘청 — 오른팔이 더 처지고 늦게 따라옴.
    # 상체를 숙이면 '몸통 기준 아래'는 실제로 뒤쪽 아래라, 숙인 만큼(골반~어깨 합) 앞으로 되돌려야 중력 방향으로 처짐
    torso_pitch = 10 + 14 + 14 + 10 + 10 + lurch * 0.8
    for side, sign, phase, droop, swing_amt in (("Left", 1, 0.5, 78, 26), ("Right", -1, 0.0, 88, 38)):
        s = wave(p, phase + 0.25)
        set_rot(side + "Shoulder", [(Y, sign * 8), (X, 10)])
        set_rot(side + "Arm", [(Y, sign * droop), (X, -torso_pitch - swing_amt * s)])
        set_rot(side + "ForeArm", [(Z, -sign * (25 + 15 * max(0.0, s)))])
        set_rot(side + "Hand", [(X, 20 * wave(p, phase + 0.4))])

    key_all(f)

# 루프 이음새 매끄럽게
for fc in action.fcurves if hasattr(action, "fcurves") else []:
    for kp in fc.keyframe_points:
        kp.interpolation = 'BEZIER'

bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=True,
    object_types={'ARMATURE'},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
)
print("@@EXPORTED", dst)
