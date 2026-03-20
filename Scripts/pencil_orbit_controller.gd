extends Node2D

@export var player_path: NodePath
var player: Node2D

@export var enemy_group_name: String = "enemies" 
@export_group("Weapon_Numbers") 
@export var pencil_scene: PackedScene #铅笔实例预制体
@export var max_pencil_count: int = 4 #最大铅笔数量
@export var current_pencil_count: int = 1 #当前铅笔数量，游戏开始时为1，随着游戏进程增加，最多增加到max_pencil_count
var pencils: Array = [] #运行时管理所有铅笔实例

@export_group("Surround")
@export var orbit_radius: float = 20.0 #铅笔环绕玩家的半径
@export var orbit_rotation_speed: float = 0.8 #铅笔环绕的旋转速度，单位为每秒旋转多少度（例如0.8即每秒旋转0.8圈，即288度/秒）
@export var use_orbit_rotation: bool = false #是否启用铅笔环绕旋转，如果禁用，铅笔将固定在玩家周围的某个位置，而不是旋转
var orbit_angle_offset: float = 0.0 #铅笔环绕的角度偏移，单位为度，随着时间增加而增加，控制铅笔在玩家周围的旋转位置

@export_group("Attack_Rhythm")
@export var base_cooldown: float = 1.2 #基础攻击间隔，单位为秒，铅笔每次攻击后进入冷却状态
@export var phase_offset: float = 0.3 #攻击时间偏移（秒），每支铅笔相对于上一支铅笔的偏移时间，用于错开攻击时机
@export var phase_offset_enabled: bool = true #是否启用相位偏移，如果启用，每支铅笔的攻击时机将根据偏移量进行调整

@export_group("Targeting_Rules")
@export var target_range: float = 140.0 #铅笔锁定目标的最大范围，单位为像素，铅笔只能锁定在这个范围内的敌人
@export var prefer_mouse_direction: bool = true #是否优先锁定鼠标方向上的敌人，如果启用，铅笔将优先锁定在玩家与鼠标之间的方向上的敌人，即使其他敌人更近
@export var mouse_cone_angle_deg: float = 70.0 #鼠标方向的扇形范围角度，单位为度，如果prefer_mouse_direction启用，铅笔将优先锁定在这个扇形范围内的敌人
@export var avoid_duplicate_target: bool = true #是否避免多个铅笔锁定同一个目标，如果启用，铅笔在选择目标时会优先选择没有被其他铅笔锁定的敌人，增加攻击的覆盖面和多样性

@export_group("Attack_Properties")
@export var dash_interval: float = 1.5 #冲刺间隔（秒）
@export var dash_duration: float = 0.3 #冲刺持续时间（秒）
@export var dash_speed: float = 400.0 #突刺速度，铅笔向目标突进的速度（像素/秒）
@export var return_speed: float = 600.0 #返回速度，铅笔从突刺结束位置飞回角色身边的速度（像素/秒）
@export var auto_return: bool = true #突刺结束后是否自动返回到角色身边
@export var pierce_count: int = 0 #穿透数

@export_group("Global_growth")
@export var damage_multiplier: float = 1.0 #全局伤害倍率，影响所有铅笔的伤害输出，可以通过游戏进程中的事件或升级来增加这个倍率，提升铅笔的整体威力
@export var cooldown_multiplier: float = 1.0 #全局冷却倍率，影响所有铅笔的攻击频率，可以通过游戏进程中的事件或升级来调整这个倍率，增加或减少铅笔的攻击速度
@export var dash_speed_multiplier: float = 1.0 #全局突刺速度倍率，影响所有铅笔的突刺速度，可以通过游戏进程中的事件或升级来调整这个倍率，增加或减少铅笔的突刺速度
@export var crit_bonus: float = 0.0 #全局暴击伤害加成，单位为基础伤害的倍数，例如0.5即增加50%的暴击伤害，可以通过游戏进程中的事件或升级来增加这个加成，提升铅笔的暴击威力
@export var pierce_bonus: int = 0 #全局穿透加成，增加所有铅笔的穿透数，例如1即所有铅笔的穿透数增加1，可以通过游戏进程中的事件或升级来增加这个加成，提升铅笔的穿透能力

# ========== 攻击系统 ==========
var _attack_timers: Array = [] #每支铅笔的攻击计时器
var _target_list: Array = [] #每支铅笔当前锁定的目标

func _ready() -> void:
	# 获取玩家节点
	if player_path:
		player = get_node(player_path)
	else:
		player = get_parent()

	# 初始化铅笔实例
	_initialize_pencils()

func _initialize_pencils() -> void:
	# 根据当前铅笔数量创建铅笔实例
	for i in range(current_pencil_count):
		_spawn_pencil(i)
		# 初始计时器，让铅笔在不同的相位开始
		_attack_timers.append(phase_offset * i if phase_offset_enabled else 0.0)
		_target_list.append(null)

func _process_attacks(delta: float) -> void:
	var cooldown = base_cooldown / cooldown_multiplier

	# 更新每支铅笔的计时器
	for i in range(pencils.size()):
		if i >= _attack_timers.size():
			break

		_attack_timers[i] -= delta

		# 检查是否应该攻击
		if _attack_timers[i] <= 0.0:
			# 铅笔攻击时让它可见
			var pencil_node = pencils[i]
			if pencil_node:
				pencil_node.visible = true
			_perform_attack(i)
			# 重置计时器，使用统一的冷却时间
			_attack_timers[i] = cooldown

func _spawn_pencil(index: int) -> void:
	if pencil_scene:
		var pencil = pencil_scene.instantiate()
		add_child(pencil)
		# 铅笔初始不可见，等轮到攻击时再显示
		pencil.visible = false
		# 重置铅笔状态
		if pencil.has_method("reset_state"):
			pencil.reset_state()
		# 设置铅笔的属性
		if pencil.has_method("set_owner_controller"):
			pencil.set_owner_controller(self)
		if pencil.has_method("set_slot_index"):
			pencil.set_slot_index(index)
		# 设置冲刺参数
		if pencil.has_method("set_dash_params"):
			pencil.set_dash_params(dash_interval, dash_duration, dash_speed, return_speed, auto_return, pierce_count)
		pencils.append(pencil)

func _process(delta: float) -> void:
	# 更新环绕角度
	if use_orbit_rotation:
		orbit_angle_offset += orbit_rotation_speed * 360.0 * delta

	# 处理攻击
	_process_attacks(delta)

# 铅笔的位置由铅笔自己控制，这里不需要额外更新
# func _update_pencils(delta: float) -> void:
# 	for i in range(pencils.size()):
# 		var pencil_node = pencils[i]
# 		if pencil_node:
# 			var angle = orbit_angle_offset + (360.0 / max_pencil_count) * i
# 			var radians = deg_to_rad(angle)
# 			var target_pos = player.global_position + Vector2(cos(radians), sin(radians)) * orbit_radius
# 			pencil_node.global_position = pencil_node.global_position.lerp(target_pos, 10.0 * delta)

func _perform_attack(pencil_index: int) -> void:
	# 查找目标
	var target = _find_target(pencil_index)
	_target_list[pencil_index] = target

	var pencil_node = pencils[pencil_index]

	if pencil_node and pencil_node.has_method("attack"):
		# 执行攻击（即使没有目标也会突击）
		pencil_node.attack(target, {
			"damage_multiplier": damage_multiplier,
			"dash_speed_multiplier": dash_speed_multiplier,
			"crit_bonus": crit_bonus,
			"pierce_bonus": pierce_bonus
		})

func _find_target(pencil_index: int) -> Node2D:
	var enemies = get_tree().get_nodes_in_group(enemy_group_name)
	if enemies.is_empty():
		return null

	var valid_targets: Array = []
	var mouse_pos = get_global_mouse_position()
	var player_pos = player.global_position

	for enemy in enemies:
		var dist = player_pos.distance_to(enemy.global_position)
		if dist > target_range:
			continue

		# 检查是否避免重复锁定
		if avoid_duplicate_target:
			var already_targeted = false
			for j in range(pencils.size()):
				if j != pencil_index and _target_list[j] == enemy:
					already_targeted = true
					break
			if already_targeted:
				continue

		# 计算方向
		var to_enemy = (enemy.global_position - player_pos).normalized()
		var to_mouse = (mouse_pos - player_pos).normalized()
		var dot_product = to_enemy.dot(to_mouse)
		var angle_diff = rad_to_deg(acos(clamp(dot_product, -1.0, 1.0)))

		valid_targets.append({
			"enemy": enemy,
			"distance": dist,
			"angle_diff": angle_diff,
			"dot_product": dot_product
		})

	if valid_targets.is_empty():
		return null

	# 排序并选择目标
	if prefer_mouse_direction and mouse_cone_angle_deg > 0:
		# 优先选择鼠标方向扇形内的敌人
		var cone_targets = valid_targets.filter(func(t): return t.angle_diff <= mouse_cone_angle_deg / 2.0)
		if not cone_targets.is_empty():
			# 在扇形内选择最近的
			cone_targets.sort_custom(func(a, b): return a.distance < b.distance)
			return cone_targets[0].enemy

	# 如果没有符合扇形条件的，选择最近的
	valid_targets.sort_custom(func(a, b): return a.distance < b.distance)
	return valid_targets[0].enemy

# ========== 外部接口 ==========
func increase_pencil_count() -> void:
	"""增加铅笔数量"""
	if current_pencil_count < max_pencil_count:
		var new_index = current_pencil_count
		current_pencil_count += 1
		_spawn_pencil(new_index)

		# 新铅笔的初始计时器设置为相位偏移
		# 这样新铅笔会在适当的时机开始攻击
		var initial_timer = phase_offset * new_index if phase_offset_enabled else 0.0
		_attack_timers.append(initial_timer)
		_target_list.append(null)

func decrease_pencil_count() -> void:
	"""减少铅笔数量"""
	if current_pencil_count > 1:
		current_pencil_count -= 1
		var removed_pencil = pencils.pop_back()
		if removed_pencil:
			removed_pencil.queue_free()
		_attack_timers.pop_back()
		_target_list.pop_back()

func get_current_pencil_count() -> int:
	"""获取当前铅笔数量"""
	return current_pencil_count

func get_orbit_radius() -> float:
	"""获取铅笔环绕半径"""
	return orbit_radius

func get_orbit_rotation_speed() -> float:
	"""获取铅笔环绕旋转速度"""
	return orbit_rotation_speed if use_orbit_rotation else 0.0

func get_player_position() -> Vector2:
	"""获取玩家位置"""
	return player.global_position if player else global_position

func has_enemies() -> bool:
	"""检查是否有敌人"""
	var enemies = get_tree().get_nodes_in_group(enemy_group_name)
	return not enemies.is_empty()

func get_pencil_nodes() -> Array:
	"""获取所有铅笔节点的数组"""
	return pencils.duplicate()
