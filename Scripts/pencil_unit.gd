extends Area2D


enum State { IDLE, FLY, ATTACK }

var state: State = State.IDLE #当前状态
var target: Node2D = null #锁定的敌人
var owner_controller: Node = null #方向拿总控数据
var slot_index: int = 0 #拿的是第几支铅笔
var idle_slot_position: Vector2 = Vector2.ZERO #待机时要跟随的目标点
@export var idle_follow_speed: float = 220.0 #飞回/跟随速度
@export var idle_float_amplitude: float = 2.0 #轻微上下浮动幅度
@export var idle_float_speed: float = 5.0 #浮动频率
var float_time: float = 0.0 #浮动的时间

@export_group("Fly_Properties")
@export var fly_speed: float = 80.0 #飞行速度，铅笔平时环绕飞行时的速度
@export var fly_distance_from_owner: float = 50.0 #飞行时距离主人的距离

@export_group("Attack_Properties")
@export var base_damage: float = 12.0 #基础伤害
@export var crit_rate: float = 0.15 #暴击率（0.15即15%暴击率）
@export var crit_multiplier: float = 1.8 #暴击伤害倍率（1.8即暴击伤害为基础伤害的1.8倍）

# 冲刺参数（由 controller 设置）
var dash_interval: float = 1.5 #突刺间隔（秒）
var dash_duration: float = 0.3 #突刺持续时间（保留用于外部调用）
var dash_speed: float = 400.0 #突刺速度（保留用于外部调用）
var return_speed: float = 600.0 #返回速度（保留用于外部调用）
var auto_return: bool = true #突刺结束后是否自动返回（保留用于外部调用）
var pierce_count: int = 0 #穿透数
var current_pierce_left: int = 0 #当前突刺剩余穿透数

# 记录当前突刺已经击中的敌人，避免重复伤害
var already_hit_targets: Array = []

@export_group("Visual_and_Feedback_Attributes")
@export var rotate_to_velocity: bool = true #是否根据当前速度旋转铅笔，突刺和返回时会根据速度方向旋转铅笔
@export var hit_pause_time: float = 0.03 #击中敌人时的短暂停顿时间，增加打击感（秒）
@export var use_hit_particles: bool = true #是否在击中敌人时播放粒子效果
@export var use_crit_particles: bool = true #是否在暴击时播放特殊粒子效果

# ========== 内部变量 ==========
var _velocity: Vector2 = Vector2.ZERO
var orbit_angle_offset: float = 0.0 #铅笔环绕的角度偏移

# ========== 生命周期 ==========
func _ready() -> void:
	# 连接碰撞信号
	area_entered.connect(_on_area_entered)
	body_entered.connect(_on_body_entered)

	# 连接 AnimationPlayer 信号
	var anim_player = get_node_or_null("AnimationPlayer")
	if anim_player:
		anim_player.animation_finished.connect(_on_animation_finished)

	# 初始化状态
	state = State.FLY  # 直接进入飞行状态

func _process(delta: float) -> void:
	# 根据状态处理逻辑
	match state:
		State.IDLE:
			_process_idle(delta)
		State.FLY:
			_process_fly(delta)
		State.ATTACK:
			# 攻击状态由 AnimationPlayer 控制，不需要额外处理
			pass

	# 铅笔朝向逻辑：根据状态决定朝向
	match state:
		State.ATTACK:
			# 攻击时锁定方向
			pass
		_:
			# IDLE 和 FLY 状态：指向鼠标方向
			var mouse_pos = get_global_mouse_position()
			var player_pos: Vector2
			if owner_controller and owner_controller.has_method("get_player_position"):
				player_pos = owner_controller.get_player_position()
			else:
				player_pos = owner_controller.global_position if owner_controller else global_position
			var mouse_direction = (mouse_pos - player_pos).normalized()
			rotation = lerp_angle(rotation, mouse_direction.angle(), 10.0 * delta)

func _physics_process(delta: float) -> void:
	# 处理移动
	global_position += _velocity * delta

# ========== 状态处理 ==========
func _process_idle(delta: float) -> void:
	# 待机浮动效果
	float_time += delta * idle_float_speed
	var float_offset = sin(float_time) * idle_float_amplitude

	# 跟随待机位置
	if owner_controller and owner_controller.has_method("get_pencil_slot_position"):
		idle_slot_position = owner_controller.get_pencil_slot_position(slot_index)

	var target_pos = idle_slot_position + Vector2(0, float_offset)
	_velocity = (target_pos - global_position) * idle_follow_speed * delta

	# 进入飞行状态
	state = State.FLY

func _process_fly(delta: float) -> void:
	# 如果没有控制器，直接返回
	if not owner_controller:
		return

	# 获取玩家位置
	var player_pos = owner_controller.global_position

	# 获取环绕半径（优先使用控制器的 orbit_radius）
	var orbit_dist: float = fly_distance_from_owner
	if owner_controller.has_method("get_orbit_radius"):
		var controller_radius = owner_controller.get_orbit_radius()
		if controller_radius > 0:
			orbit_dist = controller_radius
	elif fly_distance_from_owner > 0:
		orbit_dist = fly_distance_from_owner

	# 更新旋转偏移
	var rotation_speed: float = 0.0
	if owner_controller.has_method("get_orbit_rotation_speed"):
		rotation_speed = owner_controller.get_orbit_rotation_speed()
	orbit_angle_offset += rotation_speed * 360.0 * delta

	# 获取鼠标相对于玩家的角度
	var mouse_pos = get_global_mouse_position()
	var mouse_angle = (mouse_pos - player_pos).angle()

	# 计算铅笔在圆周上的角度
	# 铅笔跟随鼠标公转
	var pencil_count: int = 4
	if owner_controller.has_method("get_current_pencil_count"):
		pencil_count = int(owner_controller.get_current_pencil_count())
	pencil_count = max(1, pencil_count)

	# 每支铅笔相对于鼠标方向的偏移（均匀分布）
	var slot_angle = (TAU / pencil_count) * slot_index

	# 铅笔位置 = 鼠标角度 + 偏移（这样铅笔会跟随鼠标公转）
	var total_angle = mouse_angle + slot_angle

	# 计算圆周上的位置
	var target_pos = player_pos + Vector2(cos(total_angle), sin(total_angle)) * orbit_dist

	# 铅笔朝向 = 鼠标角度 + 序号偏移，这样铅笔另一头始终指向角色
	rotation = total_angle

	# 更新位置
	global_position = target_pos

func _play_attack_animation() -> void:
	"""播放突刺动画"""
	state = State.ATTACK
	already_hit_targets.clear()
	current_pierce_left = pierce_count

	# 获取 AnimationPlayer 节点
	var anim_player = get_node_or_null("AnimationPlayer")
	if anim_player and anim_player.has_animation("Dash"):
		anim_player.play("Dash")
	else:
		# 如果没有 AnimationPlayer，直接结束攻击
		_on_dash_end()

func _on_dash_start() -> void:
	"""突刺动画开始回调"""
	state = State.ATTACK
	# 可以在这里添加突刺开始时的效果

func _on_dash_end() -> void:
	"""突刺动画结束回调"""
	state = State.FLY
	already_hit_targets.clear()

func _on_animation_finished(anim_name: StringName) -> void:
	"""AnimationPlayer 动画结束回调"""
	if anim_name == &"Dash":
		_on_dash_end()

# ========== 攻击逻辑 ==========
# （突刺逻辑已在 _process_fly 中实现）

# ========== 碰撞检测 ==========
func _on_area_entered(area: Area2D) -> void:
	_handle_collision(area)

func _on_body_entered(body: Node) -> void:
	_handle_collision(body)

func _handle_collision(col: Node) -> void:
	if state != State.ATTACK:
		return

	# 检查是否击中敌人
	if col.is_in_group("enemies"):
		if col in already_hit_targets:
			return

		already_hit_targets.append(col)

		# 计算伤害
		var is_crit = randf() < crit_rate
		var damage = base_damage
		if is_crit:
			damage *= crit_multiplier

		# 应用伤害
		if col.has_method("take_damage"):
			col.take_damage(damage, is_crit)

		# 视觉效果
		_play_hit_effects(is_crit)

		# 处理穿透
		current_pierce_left -= 1
		if current_pierce_left < 0:
			_end_attack_animation()

func _end_attack_animation() -> void:
	"""提前结束攻击动画"""
	var anim_player = get_node_or_null("AnimationPlayer")
	if anim_player and anim_player.is_playing():
		anim_player.stop()
	_on_dash_end()

func _play_hit_effects(is_crit: bool) -> void:
	# 击中停顿
	get_tree().paused = true
	await get_tree().create_timer(hit_pause_time).timeout
	get_tree().paused = false

	# 粒子效果（需要根据实际粒子节点路径调整）
	if use_hit_particles:
		# 示例：$HitParticles.emitting = true
		pass

	if is_crit and use_crit_particles:
		# 示例：$CritParticles.emitting = true
		pass

# ========== 外部接口 ==========
func attack(new_target: Node2D, params: Dictionary = {}) -> void:
	"""执行攻击（由控制器调用）"""
	target = new_target

	# 应用参数（只应用加成，不修改基础值）
	if params.has("damage_multiplier"):
		base_damage *= params["damage_multiplier"]
	if params.has("crit_bonus"):
		crit_rate += params["crit_bonus"]
	if params.has("pierce_bonus"):
		pierce_count += params["pierce_bonus"]

	# dash_speed 和 return_speed 使用铅笔本身的属性，不需要通过 attack 传递

	# 开始攻击流程（由控制器控制时直接触发突刺）
	_trigger_dash()

func _trigger_dash() -> void:
	"""立即触发突刺（由控制器调用）"""
	if state == State.FLY:
		_play_attack_animation()

func set_owner_controller(controller: Node) -> void:
	"""设置所有者控制器"""
	owner_controller = controller

func set_slot_index(index: int) -> void:
	"""设置铅笔槽位索引"""
	slot_index = index

func reset_state() -> void:
	"""重置铅笔状态"""
	# 重置动画
	var anim_player = get_node_or_null("AnimationPlayer")
	if anim_player:
		anim_player.stop()
		if anim_player.has_animation("RESET"):
			anim_player.play("RESET")
	state = State.FLY
	# 强制更新位置
	_process_fly(0.0)

func set_dash_params(interval: float, duration: float, dash_spd: float, ret_spd: float, auto_ret: bool, pierce: int) -> void:
	"""设置冲刺参数"""
	dash_interval = interval
	dash_duration = duration
	dash_speed = dash_spd
	return_speed = ret_spd
	auto_return = auto_ret
	pierce_count = pierce