extends CharacterBody2D

@export var speed= 40.0
@export var player_node: Player
@export var max_health: float = 50.0  # 最大生命值
var current_health: float = 50.0  # 当前生命值

@onready var navigation_agent_2d = $Navigation/NavigationAgent2D

func _ready() -> void:
	# 连接 Timer 信号并启动
	$Navigation/Timer.timeout.connect(_on_timer_timeout)
	$Navigation/Timer.start()

	# 等待一帧让导航服务器初始化
	await get_tree().process_frame

	# 如果没有手动设置 player_node，自动查找
	if not player_node:
		player_node = get_tree().get_first_node_in_group("player") as Player

	# 设置初始目标位置
	if player_node:
		navigation_agent_2d.target_position = player_node.global_position
		print("导航目标已设置: ", player_node.global_position)

	# 添加到敌人组
	add_to_group("enemies")
	current_health = max_health

func _physics_process(_delta: float) -> void:
	# 检查目标位置是否有效
	if navigation_agent_2d.target_position == Vector2.INF:
		return
	if navigation_agent_2d.is_navigation_finished():
		return

	var next_pos = navigation_agent_2d.get_next_path_position()
	if next_pos == Vector2.ZERO:
		return

	var direction = to_local(next_pos).normalized()
	velocity = direction * speed
	move_and_slide()

func _on_timer_timeout() -> void:
	if player_node and navigation_agent_2d:
		navigation_agent_2d.target_position = player_node.global_position

# ========== 伤害系统 ==========
func take_damage(damage: float, is_crit: bool) -> void:
	"""接收伤害"""
	current_health -= damage

	# 暴击效果
	if is_crit:
		print("暴击! 造成 ", damage, " 点伤害")
	else:
		print("造成 ", damage, " 点伤害")

	# 死亡处理
	if current_health <= 0:
		die()

func die() -> void:
	"""死亡处理"""
	print("敌人死亡")
	queue_free()
