extends CharacterBody2D

@export var speed: float = 40.0
@export var player_node: Player

@onready var stats: Stats = $Stats
@onready var hurtbox: HurtBox = $HurtBox
@onready var navigation_agent_2d: NavigationAgent2D = $Navigation/NavigationAgent2D

func _ready() -> void:
	$Navigation/Timer.timeout.connect(_on_timer_timeout)
	$Navigation/Timer.start()

	await get_tree().process_frame

	if not player_node:
		player_node = get_tree().get_first_node_in_group("player") as Player

	if player_node:
		navigation_agent_2d.target_position = player_node.global_position
		print("导航目标已设置: ", player_node.global_position)

	add_to_group("enemies")

	hurtbox.hurt.connect(_on_hurtbox_hurt)
	stats.no_health.connect(die)

func _physics_process(_delta: float) -> void:
	if navigation_agent_2d.target_position == Vector2.INF:
		return
	if navigation_agent_2d.is_navigation_finished():
		return

	var next_pos := navigation_agent_2d.get_next_path_position()
	if next_pos == Vector2.ZERO:
		return

	var direction := global_position.direction_to(next_pos)
	velocity = direction * speed
	move_and_slide()

func _on_timer_timeout() -> void:
	if player_node and navigation_agent_2d:
		navigation_agent_2d.target_position = player_node.global_position

func _on_hurtbox_hurt(hitbox: HitBox) -> void:
	print("hurt signal received from:", hitbox.name)
	stats.health -= 10
	print("敌人受击，当前血量:", stats.health)

func die() -> void:
	print("敌人死亡")
	queue_free()
